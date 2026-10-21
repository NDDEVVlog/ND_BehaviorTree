using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.GOAP.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Tests
{
    /// <summary>The GOAP appearance pack as the graph uses it: asset → registry → view factory → view.</summary>
    public class GoapAppearanceTests
    {
        private const string ExpandedKeyPrefix = "ND_BehaviorTree.GOAP.CompactExpanded.";
        private readonly List<Object> m_Created = new List<Object>();

        [SetUp] public void SetUp() => ND_BTViewFactory.InvalidateCache();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Created) if (o != null) Object.DestroyImmediate(o);
            m_Created.Clear();
            ND_BTViewFactory.InvalidateCache();
        }

        private T Create<T>() where T : Node
        {
            var node = ScriptableObject.CreateInstance<T>();
            m_Created.Add(node);
            return node;
        }

        private static NodeAppearancePack LoadPack()
        {
            var path = AssetDatabase.FindAssets("GoapAppearancePack t:NodeAppearancePack").Select(AssetDatabase.GUIDToAssetPath).SingleOrDefault();
            Assert.IsNotNull(path, "GoapAppearancePack.asset is missing");
            return AssetDatabase.LoadAssetAtPath<NodeAppearancePack>(path);
        }

        [Test]
        public void Pack_IsOwnedByGoap_AndEveryKeyResolves()
        {
            var pack = LoadPack();
            Assert.AreEqual(typeof(GoapPlugin), pack.ownerPlugin.GetClass());

            var expected = new Dictionary<System.Type, System.Type>
            {
                { typeof(GOAPPlannerNode), typeof(GoapPlannerCardView) },
                { typeof(GOAPActionNode), typeof(GoapActionCompactView) },
            };
            foreach (var mapping in pack.mappings)
            {
                var nodeType = mapping.nodeScript.GetClass();
                Assert.IsTrue(expected.ContainsKey(nodeType), $"unexpected mapping for {nodeType}");
                Assert.AreEqual(expected[nodeType], mapping.customViewScript.GetClass());
                Assert.IsNotNull(pack.GetStyleSheet(mapping.styleSheetKey), $"stylesheet key '{mapping.styleSheetKey}'");
                Assert.IsNotNull(pack.GetUXML(mapping.uxmlKey), $"UXML key '{mapping.uxmlKey}'");
            }
            Assert.AreEqual(expected.Count, pack.mappings.Count);
        }

        [Test]
        public void Factory_BuildsCardPlanner_AndCompactAction()
        {
            var info = NodeAppearanceRegistry.Packs.SingleOrDefault(p => p.Pack == LoadPack());
            Assert.IsNotNull(info, "the project scan should find the pack");
            Assert.IsTrue(info.Active, $"GoapAppearancePack is not used (enabled={info.Enabled}, reason={info.InactiveReason}); switch it on in Node Appearance settings");

            var planner = ND_BTViewFactory.CreateNodeView(Create<GOAPPlannerNode>());
            Assert.IsInstanceOf<GoapPlannerCardView>(planner);
            Assert.IsTrue(planner.ClassListContains("goap-card"));
            Assert.IsNotNull(planner.Q("goap-card-band"), "the Card UXML was cloned");

            var action = ND_BTViewFactory.CreateNodeView(Create<GOAPActionNode>());
            Assert.IsInstanceOf<GoapActionCompactView>(action);
            Assert.IsTrue(action.ClassListContains("goap-compact"));
        }

        [Test]
        public void CardAndCompact_GetThePacksPorts_NotTheProjectTheme()
        {
            // Planner: drawn in C# by its painter.
            var planner = ND_BTViewFactory.CreateNodeView(Create<GOAPPlannerNode>());
            Assert.AreEqual(PortShape.Custom, planner.PortTheme.shape);
            Assert.IsInstanceOf<GoapPlannerPortPainter>(planner.InputPort.Painter);
            Assert.IsInstanceOf<GoapPlannerPortPainter>(planner.OutputPort.Painter);
            Assert.AreEqual(18f, planner.InputPort.style.height.value.value, "the planner's input port is drawn at the pack's size");

            // Action: built from a UXML template + USS, no painter.
            var action = ND_BTViewFactory.CreateNodeView(Create<GOAPActionNode>());
            Assert.AreEqual(PortShape.Custom, action.PortTheme.shape);
            Assert.IsNull(action.InputPort.Painter);
            Assert.IsNotNull(action.InputPort.Q("goap-port-body"), "the port template was cloned into the port");
            Assert.IsTrue(action.InputPort.ClassListContains("nd-port--input"));
            Assert.AreEqual(12f, action.InputPort.style.height.value.value);
            Assert.AreNotSame(ND_BehaviorTreeSetting.Instance.portTheme, action.PortTheme);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompactAction_ShowsDigestOrPanel_ByItsExpandedState(bool expanded)
        {
            var node = Create<GOAPActionNode>();
            SessionState.SetBool(ExpandedKeyPrefix + node.id, expanded);
            try
            {
                var view = ND_BTViewFactory.CreateNodeView(node);
                var panel = view.Q(className: "goap-panel");
                var digest = view.Q<Label>(className: "goap-digest");

                Assert.AreEqual(expanded ? DisplayStyle.Flex : DisplayStyle.None, panel.style.display.value);
                Assert.AreEqual(expanded ? DisplayStyle.None : DisplayStyle.Flex, digest.style.display.value);
                StringAssert.StartsWith($"{node.preconditions.Count} needs · {node.effects.Count} does · cost", digest.text);
                Assert.AreEqual(expanded ? "▾" : "▸", view.Q<Button>(className: "goap-compact-toggle").text);
            }
            finally
            {
                SessionState.EraseBool(ExpandedKeyPrefix + node.id);
            }
        }
    }
}
