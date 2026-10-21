using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Plugins;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.Tests
{
    public class NodeAppearanceTests
    {
        private readonly List<Object> m_Created = new List<Object>();
        private ProbePlugin m_Plugin;
        private NodeEditorConfig m_Config;

        [SetUp]
        public void SetUp()
        {
            // No project scan: only the packs a test registers or lists.
            m_Config = Track(ScriptableObject.CreateInstance<NodeEditorConfig>());
            m_Config.autoDiscoverPacks = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Plugin != null && ND_BTPluginRegistry.Entries.Any(e => e.Plugin == m_Plugin)) ND_BTPluginRegistry.Uninstall(m_Plugin);
            m_Plugin = null;
            foreach (var o in m_Created) if (o != null) Object.DestroyImmediate(o);
            m_Created.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Created.Add(o); return o; }

        private NodeAppearancePack Pack(string name, int priority, params System.Type[] nodeTypes)
        {
            var pack = Track(ScriptableObject.CreateInstance<NodeAppearancePack>());
            pack.name = name;
            pack.priority = priority;
            foreach (var type in nodeTypes) pack.mappings.Add(new NodeEditorMapping { nodeTypeFullName = type.FullName });
            return pack;
        }

        private void InstallPacks(params NodeAppearancePack[] packs)
        {
            m_Plugin = new ProbePlugin(ctx => { foreach (var p in packs) ctx.AddAppearancePack(p); });
            Assert.IsTrue(ND_BTPluginRegistry.Install(m_Plugin));
        }

        private NodeAppearancePack Resolve(System.Type nodeType)
        {
            NodeAppearanceRegistry.TryResolve(nodeType, NodeAppearanceRegistry.Collect(m_Config), out var pack, out _);
            return pack;
        }

        [Test]
        public void PluginPack_IsUsed_UntilThePluginIsUninstalled()
        {
            var pack = Pack("Probe Pack", 0, typeof(SequenceNode));
            InstallPacks(pack);

            var info = NodeAppearanceRegistry.Collect(m_Config).Single(p => p.Pack == pack);
            Assert.AreEqual(NodeAppearanceSource.Plugin, info.Source);
            Assert.AreEqual("Probe", info.PluginName);
            Assert.AreSame(pack, Resolve(typeof(SequenceNode)));

            ND_BTPluginRegistry.Uninstall(m_Plugin);
            Assert.IsNull(Resolve(typeof(SequenceNode)), "the pack left with its plugin");
        }

        [Test]
        public void SameNodeType_HigherPriorityWins_InAnyOrder()
        {
            var low = Pack("A Low", 0, typeof(SequenceNode));
            var high = Pack("B High", 5, typeof(SequenceNode));
            InstallPacks(high, low);
            Assert.AreSame(high, Resolve(typeof(SequenceNode)));
        }

        [Test]
        public void MoreSpecificNodeType_BeatsHigherPriority()
        {
            var baseClass = Pack("Composite", 100, typeof(CompositeNode));
            var exact = Pack("Sequence", 0, typeof(SequenceNode));
            InstallPacks(baseClass, exact);

            Assert.AreSame(exact, Resolve(typeof(SequenceNode)));
            Assert.AreSame(baseClass, Resolve(typeof(SelectorNode)), "a subclass with no own mapping uses its base class mapping");
        }

        [Test]
        public void EqualPriority_FirstByNameWins_WithAWarning()
        {
            var a = Pack("Tie A", 1, typeof(SequenceNode));
            var b = Pack("Tie B", 1, typeof(SequenceNode));
            InstallPacks(b, a);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("both map SequenceNode"));
            Assert.AreSame(a, Resolve(typeof(SequenceNode)));
        }

        [Test]
        public void MappingWithPortOverride_ViewPortsUseIt_OthersKeepTheProjectTheme()
        {
            var pack = Pack("Ports", 1000, typeof(SequenceNode));
            pack.mappings[0].overridePorts = true;
            pack.mappings[0].ports = new PortTheme { shape = PortShape.Square, portSize = 21f };
            InstallPacks(pack);
            try
            {
                var sequence = CustomGraph.ND_BTViewFactory.CreateNodeView(Track(ScriptableObject.CreateInstance<SequenceNode>()));
                Assert.AreSame(pack.mappings[0].ports, sequence.PortTheme);
                Assert.AreEqual(21f, sequence.InputPort.style.height.value.value);

                var selector = CustomGraph.ND_BTViewFactory.CreateNodeView(Track(ScriptableObject.CreateInstance<SelectorNode>()));
                Assert.AreSame(ND_BehaviorTreeSetting.Instance.portTheme, selector.PortTheme, "nodes without an override keep the project's ports");
            }
            finally
            {
                ND_BTPluginRegistry.Uninstall(m_Plugin);
            }
        }

        /// <summary>A Sequence view whose ports use <paramref name="ports"/> (through a plugin pack override).</summary>
        private CustomGraph.ND_BTNodeView SequenceViewWithPorts(PortTheme ports)
        {
            var pack = Pack("Custom Ports", 1000, typeof(SequenceNode));
            pack.mappings[0].overridePorts = true;
            pack.mappings[0].ports = ports;
            InstallPacks(pack);
            return CustomGraph.ND_BTViewFactory.CreateNodeView(Track(ScriptableObject.CreateInstance<SequenceNode>()));
        }

        [Test]
        public void CustomPort_PainterScript_DrawsThePortAtItsSize()
        {
            var script = UnityEditor.AssetDatabase.FindAssets("TestPortPainter t:MonoScript")
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Single(s => s.GetClass() == typeof(TestPortPainter));

            var view = SequenceViewWithPorts(new PortTheme { shape = PortShape.Custom, portSize = 10f, customPainter = script });

            Assert.IsInstanceOf<TestPortPainter>(view.InputPort.Painter);
            Assert.AreEqual(30f, view.InputPort.style.width.value.value);
            Assert.AreEqual(20f, view.InputPort.style.height.value.value);
            Assert.AreNotSame(view.InputPort.Painter, view.OutputPort.Painter, "one painter per port");
        }

        [Test]
        public void CustomPort_Template_IsCloned_AndStateClassesFollowThePort()
        {
            var template = Track(ScriptableObject.CreateInstance<VisualTreeAsset>());
            var view = SequenceViewWithPorts(new PortTheme { shape = PortShape.Custom, customTemplate = template });
            var input = view.InputPort;

            Assert.IsNull(input.Painter);
            Assert.IsTrue(input.ClassListContains("nd-port"));
            Assert.IsTrue(input.ClassListContains("nd-port--input"));
            Assert.IsTrue(view.OutputPort.ClassListContains("nd-port--multi"), "a Sequence takes many children");
            Assert.IsFalse(input.ClassListContains("nd-port--candidate"));

            input.SetCandidate(true);
            Assert.IsTrue(input.ClassListContains("nd-port--candidate"));
            input.SetDragHover(true);
            Assert.IsTrue(input.ClassListContains("nd-port--hover"));
        }

        [Test]
        public void CustomPort_WithNothingToDraw_WarnsAndDrawsCircles()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Port shape is Custom"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Port shape is Custom"));
            var view = SequenceViewWithPorts(new PortTheme { shape = PortShape.Custom });
            Assert.IsFalse(view.InputPort.ClassListContains("nd-port"));
        }

        [Test]
        public void PortPainterCreate_RejectsTypesThatAreNotPainters()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not a concrete ND_BTPortPainter"));
            Assert.IsNull(CustomGraph.ND_BTPortPainter.Create(typeof(string), "NotAPainter"));
            Assert.IsInstanceOf<TestPortPainter>(CustomGraph.ND_BTPortPainter.Create(typeof(TestPortPainter), "TestPortPainter"));
        }

        [Test]
        public void DisabledInConfig_PackIsIgnored_AndConfigCanLoadPacks()
        {
            var pluginPack = Pack("From Plugin", 9, typeof(SequenceNode));
            InstallPacks(pluginPack);
            var loaded = Pack("Loaded", 0, typeof(SequenceNode));
            m_Config.packs.Add(new NodeEditorConfig.PackEntry { pack = loaded, enabled = true });

            Assert.AreSame(pluginPack, Resolve(typeof(SequenceNode)));

            m_Config.SetEnabled(pluginPack, false);
            Assert.AreSame(loaded, Resolve(typeof(SequenceNode)), "a pack switched off in the config is skipped");
            Assert.IsFalse(NodeAppearanceRegistry.Collect(m_Config).Single(p => p.Pack == pluginPack).Active);
        }
    }
}
