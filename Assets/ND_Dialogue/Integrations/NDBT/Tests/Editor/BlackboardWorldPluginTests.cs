using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ND.Dialogue.Editor.Plugins;
using ND.Dialogue.NDBT.Editor;
using ND_BehaviorTree;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.NDBT.Tests
{
    public class BlackboardWorldPluginTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Blackboard m_World;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_World = ScriptableObject.CreateInstance<Blackboard>();
            m_Track.Add(m_Db);
            m_Track.Add(m_World);
            var key = ScriptableObject.CreateInstance<IntKey>();
            key.keyName = "quest.key";
            m_World.keys.Add(key);
            m_Track.Add(key);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (m_Db.schemaSource != null) m_Track.Add(m_Db.schemaSource);
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void Plugin_IsRegisteredByTheDomainScan_WithItsTab()
        {
            Assert.IsTrue(DialoguePluginRegistry.Entries.Any(e => e.Plugin is BlackboardWorldPlugin && e.Installed));
            Assert.IsTrue(DialoguePluginRegistry.DatabaseTabs.Any(t => t.Title == BlackboardWorldPlugin.TabTitle));
        }

        [Test]
        public void Use_MakesTheBlackboardTheWorld_AndValidationSeesItsQuests()
        {
            BlackboardWorldPlugin.Use(new DialoguePluginArgs { Database = m_Db }, m_World);

            var source = m_Db.schemaSource as BlackboardSchemaSource;
            Assert.IsNotNull(source);
            Assert.AreSame(m_World, source.blackboard);
            CollectionAssert.AreEqual(new[] { "key" }, m_Db.Schema.QuestNames.ToArray());
        }

        [Test]
        public void Variables_UseTheBlackboardKeyColours()
        {
            var gold = ScriptableObject.CreateInstance<BoolKey>();
            gold.keyName = "player.has_key";
            m_World.keys.Add(gold);
            m_Track.Add(gold);
            BlackboardWorldPlugin.Use(new DialoguePluginArgs { Database = m_Db }, m_World);

            var expected = typeof(BoolKey).GetCustomAttribute<KeyColorAttribute>().Color;
            Assert.AreEqual(expected, BlackboardWorldPlugin.KeyColor(m_Db, "player.has_key"));
            Assert.AreEqual(expected, ND.Dialogue.Editor.DialogueVariableStyle.Of(m_Db, "player.has_key"), "pickers and rows use the plugin's colour");
            Assert.IsNull(BlackboardWorldPlugin.KeyColor(m_Db, "missing"));
        }

        [Test]
        public void VariablesAndQuestsSections_EditTheBlackboardInPlace()
        {
            var flag = ScriptableObject.CreateInstance<BoolKey>();
            flag.keyName = "gate_open";
            m_World.keys.Add(flag);
            m_Track.Add(flag);
            BlackboardWorldPlugin.Use(new DialoguePluginArgs { Database = m_Db }, m_World);
            Assert.IsNotNull(DialoguePluginRegistry.SchemaEditorFor(m_Db.schemaSource), "the plugin edits blackboard worlds");

            var panel = new ND.Dialogue.Editor.DialogueDatabasePanel();
            panel.Bind(m_Db, 0);
            panel.ShowTab(ND.Dialogue.Editor.DialogueDatabasePanel.VariablesTab);
            Assert.AreEqual(1, panel.Query(className: "dlg-keyrow").ToList().Count, "one editable row per non-quest key");
            panel.ShowTab(ND.Dialogue.Editor.DialogueDatabasePanel.QuestsTab);
            Assert.AreEqual(1, panel.Query(className: "dlg-keyrow").ToList().Count, "one row per quest key");
        }

        [Test]
        public void AddVariableSearch_ListsOnlyTypesDialogueCanUse()
        {
            var entries = BlackboardKeysEditor.DialogueKeyEntries();
            Assert.IsTrue(entries.Any(e => (System.Type)e.UserData == typeof(BoolKey)));
            Assert.IsTrue(entries.Any(e => (System.Type)e.UserData == typeof(IntKey)));
            Assert.IsFalse(entries.Any(e => (System.Type)e.UserData == typeof(QuaternionKey)), "no dialogue variable type for quaternions");
            Assert.IsTrue(entries.All(e => e.Color.HasValue));
        }

        [Test]
        public void Rule_ReportsASourceWithoutBlackboard()
        {
            m_Db.schemaSource = ScriptableObject.CreateInstance<BlackboardSchemaSource>();
            Assert.That(DialoguePluginRegistry.Validate(m_Db), Has.Some.Contains("[Behavior Tree World]").And.Contains("has no blackboard"));
        }
    }
}
