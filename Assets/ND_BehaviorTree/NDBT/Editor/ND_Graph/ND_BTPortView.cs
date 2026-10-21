using ND.GraphUI;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Behavior-tree port: one input on top (the parent link), one output at the bottom (the children).
    /// Drawing and interaction come from <see cref="GraphPortView"/>; this adds the tree wording.
    /// </summary>
    public class ND_BTPortView : GraphPortView
    {
        public new ND_BTNodeView ParentNodeView => (ND_BTNodeView)base.ParentNodeView;

        public ND_BTPortView(ND_BTNodeView parentNodeView, PortDirection direction, PortCapacity capacity)
            : base(parentNodeView, direction, capacity, PortOrientation.Vertical) { }

        protected override string BuildTooltip()
        {
            bool plugged = IsConnected;
            if (Direction == PortDirection.Input)
                return plugged ? "Input — connected to a parent. Alt+click to disconnect from the parent." : "Input — not connected: drag from a parent's output to here.";
            return Capacity == PortCapacity.Multi
                ? (plugged ? $"Output — {Connections.Count} child(ren). Drag to add more; order = left to right. Alt+click to disconnect all children." : "Output — takes many children: drag to a node's input.")
                : (plugged ? "Output — connected (single child). Alt+click to disconnect." : "Output — single child: drag to a node's input.");
        }
    }
}
