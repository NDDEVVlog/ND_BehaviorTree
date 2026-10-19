using System;
using System.Reflection;
using ND_BehaviorTree.Editor.Graph;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// A small dot for a <see cref="RerouteNode"/> (created by double-clicking an edge). Drag it to bend the edge;
    /// delete it to reconnect the parent directly. In live debug it shows the state of the node it leads to.
    /// </summary>
    public class ND_BTRerouteView : ND_BTNodeView
    {
        public const float Size = 18f;
        public static Vector2 HalfSize => new Vector2(Size * 0.5f, Size * 0.5f);

        public ND_BTRerouteView(Node node, ViewThemeData themeData) : base(node, themeData) { }

        protected override void ApplyThemeAndLayout()
        {
            ND_BTStyles.Apply(this, ND_BTStyles.Reroute);
            AddToClassList("nd-reroute");
            tooltip = "Reroute: bends the edge, skipped at runtime.\nDrag to move · Delete to reconnect directly.";

            Add(new VisualElement { name = "top-port", pickingMode = PickingMode.Ignore }.WithClass("nd-reroute__port nd-reroute__port--top"));
            Add(new VisualElement { name = "bottom-port", pickingMode = PickingMode.Ignore }.WithClass("nd-reroute__port nd-reroute__port--bottom"));
        }

        protected override void PopulateData()
        {
            SetPosition(NodeData.position.position);
            InitializePorts(NodeData.GetType().GetCustomAttribute<NodeInfoAttribute>());
        }

        // The node the reroute leads to shows the execution index; the dot stays clean.
        public override void SetExecutionIndex(int zeroBasedIndex) => base.SetExecutionIndex(-1);

        public override void DrawDecorators() { }
        public override void DrawServices() { }
        public override void RefreshKeyInfo() { }

        public override void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            var target = RerouteNode.Resolve(NodeData);
            var state = LiveDebugUtility.GetState(target != null ? lookup(target.id) : null, frame);
            EnableInClassList("running", state == LiveState.Running);
            EnableInClassList("success", state == LiveState.Succeeded);
            EnableInClassList("failure", state == LiveState.Failed);
        }
    }

    internal static class RerouteViewExtensions
    {
        public static VisualElement WithClass(this VisualElement element, string classes)
        {
            foreach (var c in classes.Split(' ')) element.AddToClassList(c);
            return element;
        }
    }
}
