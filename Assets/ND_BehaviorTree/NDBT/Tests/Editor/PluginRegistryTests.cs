using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Editor.Plugins;
using ND_BehaviorTree.Editor.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>Configurable plugin for tests (not auto-installed: it has no parameterless constructor).</summary>
    public sealed class ProbePlugin : ND_BTPlugin
    {
        private readonly Action<ND_BTPluginContext> m_Register;
        public ProbePlugin(Action<ND_BTPluginContext> register) { m_Register = register; }
        public override string DisplayName => "Probe";
        public override void Register(ND_BTPluginContext context) => m_Register(context);
    }

    public class PluginRegistryTests
    {
        private GraphFixture f;
        private ProbePlugin m_Plugin;

        [SetUp] public void SetUp() => f = new GraphFixture();

        [TearDown]
        public void TearDown()
        {
            if (m_Plugin != null && ND_BTPluginRegistry.Entries.Any(e => e.Plugin == m_Plugin)) ND_BTPluginRegistry.Uninstall(m_Plugin);
            m_Plugin = null;
            f.Destroy();
        }

        private ProbePlugin Install(Action<ND_BTPluginContext> register)
        {
            m_Plugin = new ProbePlugin(register);
            Assert.IsTrue(ND_BTPluginRegistry.Install(m_Plugin));
            return m_Plugin;
        }

        private static void RaiseCreated(BehaviorTreeGraphService graph, Node node) =>
            typeof(GraphEditHooks).GetMethod("RaiseNodeCreatedByUser", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { graph, node });

        [Test]
        public void Install_ExposesContributions_UninstallRemovesThemAll()
        {
            var plugin = Install(ctx =>
            {
                ctx.AddTheme("Probe Theme", () => new ThemeData { displayName = "Probe Theme" });
                ctx.AddCanvasMenuItem("Probe/Canvas", _ => { });
                ctx.AddNodeMenuItem<SequenceNode>("Probe/Sequence Only", _ => { });
                ctx.AddToolbarItem("Probe/Toolbar", _ => { });
                ctx.AddValidationRule("probe", _ => new[] { new GraphIssue(GraphIssueSeverity.Warning, "probe.issue", "probe") });
            });

            Assert.IsTrue(ND_BTThemeCatalog.All().Any(e => e.Name == "Probe Theme"));
            Assert.IsTrue(ND_BTPluginRegistry.CanvasMenuItems.Any(i => i.Path == "Probe/Canvas"));
            Assert.IsTrue(ND_BTPluginRegistry.ToolbarItems.Any(i => i.Path == "Probe/Toolbar"));
            var seq = f.Create<SequenceNode>(0, 100);
            var sel = f.Create<SelectorNode>(0, 200);
            Assert.IsTrue(ND_BTPluginRegistry.NodeMenuItems(seq).Any(i => i.Path == "Probe/Sequence Only"));
            Assert.IsFalse(ND_BTPluginRegistry.NodeMenuItems(sel).Any(i => i.Plugin == plugin), "node items are filtered by node type");
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == "probe.issue"));

            Assert.IsTrue(ND_BTPluginRegistry.Uninstall(plugin));

            Assert.IsFalse(ND_BTThemeCatalog.All().Any(e => e.Name == "Probe Theme"));
            Assert.IsFalse(ND_BTPluginRegistry.CanvasMenuItems.Concat(ND_BTPluginRegistry.ToolbarItems).Any(i => i.Plugin == plugin));
            Assert.IsFalse(GraphValidator.Validate(f.Tree).Any(i => i.Code == "probe.issue"));
        }

        [Test]
        public void OnNodeCreated_FiresOnlyForItsType_AndStopsAfterUninstall()
        {
            var seen = new List<Node>();
            var plugin = Install(ctx => ctx.OnNodeCreated<SequenceNode>((graph, node) => seen.Add(node)));
            var seq = f.Create<SequenceNode>(0, 100);
            var sel = f.Create<SelectorNode>(0, 200);

            RaiseCreated(f.Graph, seq);
            RaiseCreated(f.Graph, sel);
            CollectionAssert.AreEqual(new Node[] { seq }, seen);

            ND_BTPluginRegistry.Uninstall(plugin);
            RaiseCreated(f.Graph, seq);
            Assert.AreEqual(1, seen.Count, "the hook was removed with the plugin");
        }

        [Test]
        public void RegisterThrows_PluginDisabled_PartialContributionsRemoved()
        {
            m_Plugin = new ProbePlugin(ctx =>
            {
                ctx.AddTheme("Half Theme", () => new ThemeData());
                throw new InvalidOperationException("boom");
            });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("failed to register"));

            Assert.IsFalse(ND_BTPluginRegistry.Install(m_Plugin));

            var entry = ND_BTPluginRegistry.Entries.Single(e => e.Plugin == m_Plugin);
            Assert.IsFalse(entry.Installed);
            StringAssert.Contains("boom", entry.Error);
            Assert.IsFalse(ND_BTThemeCatalog.All().Any(e => e.Name == "Half Theme"));
        }

        [Test]
        public void ThrowingValidationRule_BecomesAnErrorIssue()
        {
            Install(ctx => ctx.AddValidationRule("broken", _ => throw new Exception("rule bug")));

            var issue = GraphValidator.Validate(f.Tree).Single(i => i.Code == GraphValidator.PluginRuleFailed);
            Assert.AreEqual(GraphIssueSeverity.Error, issue.Severity);
            StringAssert.Contains("rule bug", issue.Message);
        }

        [Test]
        public void MenuItemThatChangesTheGraph_IsOneUndoStep()
        {
            ND_BTPluginMenuItem item = null;
            Install(ctx => ctx.AddCanvasMenuItem("Probe/Add Sequence", args => args.Graph.CreateNode(typeof(SequenceNode), args.Position)));
            item = ND_BTPluginRegistry.CanvasMenuItems.Single(i => i.Path == "Probe/Add Sequence");
            int before = f.Tree.nodes.Count;

            ND_BTPluginRegistry.Run(item, new ND_BTPluginArgs { Tree = f.Tree, Graph = f.Graph, Position = new Vector2(10, 10) });
            Assert.AreEqual(before + 1, f.Tree.nodes.Count);

            GraphFixture.UndoOnce();
            Assert.AreEqual(before, f.Tree.nodes.Count);
        }

        [Test]
        public void InstallSameTypeTwice_Rejected()
        {
            Install(_ => { });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already registered"));
            Assert.IsFalse(ND_BTPluginRegistry.Install(new ProbePlugin(_ => { })));
        }

        // ---------- scaffold ----------

        [TestCase("lowercase")]
        [TestCase("Has Space")]
        [TestCase("X")]
        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void Scaffold_RejectsBadNames(string name) =>
            Assert.IsNotNull(ND_BTPluginScaffold.Validate(name, "Assets/__NoSuchFolder__"));

        [Test]
        public void Scaffold_RejectsExistingAssembly() =>
            StringAssert.Contains("already exists", ND_BTPluginScaffold.Validate("StoryNodes", "Assets/__NoSuchFolder__"));

        [Test]
        public void Scaffold_GeneratesAssembliesEntryPointAndOptionalParts()
        {
            Assert.IsNull(ND_BTPluginScaffold.Validate("DialogueNodes", "Assets/__NoSuchFolder__"));

            var minimal = ND_BTPluginScaffold.Generate("DialogueNodes", new ND_BTPluginScaffold.Options());
            CollectionAssert.AreEquivalent(new[]
            {
                "Runtime/ND_BehaviorTree.DialogueNodes.asmdef", "Runtime/DialogueNodesExampleNode.cs",
                "Editor/ND_BehaviorTree.DialogueNodes.Editor.asmdef", "Editor/DialogueNodesPlugin.cs",
            }, minimal.Keys);
            StringAssert.Contains("class DialogueNodesPlugin : ND_BTPlugin", minimal["Editor/DialogueNodesPlugin.cs"]);
            StringAssert.Contains("[NodeInfo(", minimal["Runtime/DialogueNodesExampleNode.cs"]);
            StringAssert.Contains("\"ND_BehaviorTree.DialogueNodes\"", minimal["Editor/ND_BehaviorTree.DialogueNodes.Editor.asmdef"]);
            var editorAsmdef = JsonUtility.FromJson<AsmdefProbe>(minimal["Editor/ND_BehaviorTree.DialogueNodes.Editor.asmdef"]);
            CollectionAssert.AreEqual(new[] { "Editor" }, editorAsmdef.includePlatforms);
            var runtimeAsmdef = JsonUtility.FromJson<AsmdefProbe>(minimal["Runtime/ND_BehaviorTree.DialogueNodes.asmdef"]);
            Assert.AreEqual("ND_BehaviorTree.DialogueNodes", runtimeAsmdef.name);
            Assert.AreEqual(0, runtimeAsmdef.includePlatforms.Length, "the runtime assembly builds for every platform");

            var full = ND_BTPluginScaffold.Generate("DialogueNodes", new ND_BTPluginScaffold.Options { IncludeNodeView = true, IncludeTests = true });
            Assert.IsTrue(full.ContainsKey("Editor/DialogueNodesExampleNodeView.cs"));
            Assert.IsTrue(full.ContainsKey("Editor/Styles/DialogueNodesNodes.uss"));
            Assert.IsTrue(full.ContainsKey("Tests/Editor/ND_BehaviorTree.DialogueNodes.Tests.asmdef"), "test assembly name follows the ND_BehaviorTree*.Tests convention");
            StringAssert.Contains(".dialogue-nodes-node", full["Editor/Styles/DialogueNodesNodes.uss"]);
        }

        [Test]
        public void Scaffold_GeneratedCode_HasBalancedBrackets()
        {
            // Cheap guard for template typos (a stray "(" once broke the generated plugin); full compile is checked by hand.
            var files = ND_BTPluginScaffold.Generate("DialogueNodes", new ND_BTPluginScaffold.Options { IncludeNodeView = true, IncludeTests = true });
            foreach (var file in files.Where(f => f.Key.EndsWith(".cs")))
            {
                var stack = new Stack<char>();
                bool inString = false;
                foreach (char c in file.Value)
                {
                    if (c == '"') inString = !inString;
                    if (inString) continue;
                    if (c == '(' || c == '{' || c == '[') stack.Push(c);
                    else if (c == ')' || c == '}' || c == ']')
                    {
                        Assert.IsTrue(stack.Count > 0, $"{file.Key}: unexpected '{c}'");
                        char open = stack.Pop();
                        Assert.AreEqual(c == ')' ? '(' : c == '}' ? '{' : '[', open, $"{file.Key}: '{open}' closed by '{c}'");
                    }
                }
                Assert.AreEqual(0, stack.Count, $"{file.Key}: {stack.Count} unclosed bracket(s)");
            }
        }

        [Serializable]
        private class AsmdefProbe
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
        }
    }
}
