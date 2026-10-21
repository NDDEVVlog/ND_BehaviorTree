using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ND.Dialogue.Editor.Plugins;
using ND.GraphUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ND.Dialogue.Editor.Tests
{
    [DialogueManualInstall]
    internal class ProbePlugin : DialoguePlugin
    {
        public int Created;
        public override string DisplayName => "Probe";

        public override void Register(DialoguePluginContext context)
        {
            context.AddCanvasMenuItem("Probe/Canvas", _ => { });
            context.AddEntryMenuItem("Probe/Entry", _ => { });
            context.AddToolbarItem("Probe/Toolbar", _ => { });
            context.AddDatabaseTab("Probe Tab", args => new Label($"probe:{args.Database.name}"));
            context.AddInspectorSection("Probe Section", args => new Label($"entry:{args.Entry.id}"));
            context.AddValidationRule("probe-rule", db => db.actors.Count < 3 ? new[] { "needs three actors" } : Array.Empty<string>());
            context.OnEntryCreated((args, entry) =>
            {
                Created++;
                entry.title = "made by probe"; // inside the creation's undo step
            });
        }
    }

    [DialogueManualInstall]
    internal class BrokenPlugin : DialoguePlugin
    {
        public override void Register(DialoguePluginContext context)
        {
            context.AddValidationRule("throws", _ => throw new InvalidOperationException("boom"));
            context.AddDatabaseTab("Broken Tab", _ => throw new InvalidOperationException("no tab"));
        }
    }

    [DialogueManualInstall]
    internal class FailingRegisterPlugin : DialoguePlugin
    {
        public override void Register(DialoguePluginContext context) => throw new InvalidOperationException("cannot register");
    }

    public class DialoguePluginTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private readonly List<DialoguePlugin> m_Installed = new List<DialoguePlugin>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;

        private T Install<T>() where T : DialoguePlugin, new()
        {
            var plugin = new T();
            DialoguePluginRegistry.Install(plugin);
            m_Installed.Add(plugin);
            return plugin;
        }

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Db.name = "PluginDb";
            m_Track.Add(m_Db);
            m_Db.AddActor("Guard");
            m_Conv = m_Db.AddConversation("Gate");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in m_Installed) DialoguePluginRegistry.Uninstall(p);
            m_Installed.Clear();
            Undo.ClearAll();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void Install_ExposesEveryContribution_UninstallRemovesThem()
        {
            var probe = Install<ProbePlugin>();
            Assert.IsTrue(DialoguePluginRegistry.CanvasMenuItems.Any(i => i.Path == "Probe/Canvas"));
            Assert.IsTrue(DialoguePluginRegistry.EntryMenuItems.Any(i => i.Path == "Probe/Entry"));
            Assert.IsTrue(DialoguePluginRegistry.ToolbarItems.Any(i => i.Path == "Probe/Toolbar"));
            Assert.IsTrue(DialoguePluginRegistry.DatabaseTabs.Any(t => t.Title == "Probe Tab"));
            Assert.IsTrue(DialoguePluginRegistry.InspectorSections.Any(t => t.Title == "Probe Section"));

            DialoguePluginRegistry.Uninstall(probe);
            Assert.IsFalse(DialoguePluginRegistry.CanvasMenuItems.Any(i => i.Path == "Probe/Canvas"));
            Assert.IsFalse(DialoguePluginRegistry.DatabaseTabs.Any(t => t.Title == "Probe Tab"));
        }

        [Test]
        public void ManualInstallPlugins_AreNotPickedUpByTheDomainScan()
        {
            Assert.IsFalse(DialoguePluginRegistry.IsAutoInstalled(typeof(ProbePlugin)));
            Assert.IsFalse(DialoguePluginRegistry.Entries.Any(e => e.Plugin is ProbePlugin));
        }

        [Test]
        public void FailingRegister_IsReportedAndDisabled()
        {
            LogAssert.Expect(LogType.Error, new Regex("'FailingRegisterPlugin' failed to register"));
            var plugin = Install<FailingRegisterPlugin>();
            var entry = DialoguePluginRegistry.Entries.Single(e => e.Plugin == plugin);
            Assert.IsFalse(entry.Installed);
            StringAssert.Contains("cannot register", entry.Error);
        }

        [Test]
        public void Validate_MergesPluginRules_AndAThrowingRuleBecomesAnIssue()
        {
            Install<ProbePlugin>();
            Install<BrokenPlugin>();
            var issues = DialoguePluginRegistry.Validate(m_Db);
            Assert.That(issues, Has.Some.EqualTo("[Probe] needs three actors"));
            Assert.That(issues, Has.Some.Contains("rule 'throws' threw: boom"));
        }

        [Test]
        public void OnEntryCreated_RunsInsideTheSameUndoStep()
        {
            var probe = Install<ProbePlugin>();
            var canvas = new DialogueGraphCanvas();
            canvas.Load(m_Db, m_Conv, "en");
            Undo.ClearAll();

            var entry = canvas.CreateEntry(NewEntryKind.NpcLine, Vector2.zero);
            Assert.AreEqual(1, probe.Created);
            Assert.AreEqual("made by probe", entry.title);

            Undo.PerformUndo();
            Assert.AreEqual(1, m_Db.FindConversation(m_Conv.id).entries.Count, "one undo removes the entry and the plugin's change");
        }

        [Test]
        public void Sidebar_ShowsBuiltInAndPluginTabs_AndAThrowingTabShowsAnError()
        {
            Install<ProbePlugin>();
            Install<BrokenPlugin>();
            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);

            CollectionAssert.IsSubsetOf(
                new[] { "Conversations", "Characters", "Variables", "Quests", "Events", "Settings", "Probe Tab", "Broken Tab" },
                panel.TabTitles.ToList());

            foreach (var tab in new[] { "Conversations", "Characters", "Variables", "Quests", "Events", "Settings" })
                Assert.DoesNotThrow(() => panel.ShowTab(tab), tab);

            panel.ShowTab("Probe Tab");
            Assert.IsNotNull(panel.Query<Label>().Where(l => l.text == "probe:PluginDb").First());

            LogAssert.Expect(LogType.Error, new Regex("panel 'Broken Tab' threw"));
            panel.ShowTab("Broken Tab");
            Assert.IsNotNull(panel.Q<HelpBox>());
        }

        [Test]
        public void Inspector_AddsPluginSections_ForTheEntry()
        {
            Install<ProbePlugin>();
            var entry = m_Conv.AddEntry(m_Db.actors[0].id);
            var inspector = new DialogueEntryInspector();
            inspector.Bind(m_Db, m_Conv, entry, "en");
            Assert.IsNotNull(inspector.Query<Foldout>().Where(f => f.text == "Probe Section").First());
            Assert.IsNotNull(inspector.Query<Label>().Where(l => l.text == $"entry:{entry.id}").First());
        }
    }
}
