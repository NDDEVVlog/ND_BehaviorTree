using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Editor-only routing point ("extended node") that bends an edge to keep the graph readable.
    /// Created by double-clicking an edge. It has no behavior: <see cref="BehaviorTree.Clone"/> skips it and connects
    /// the parent straight to the node behind it, so a runner never ticks a reroute. In a composite, the reroute's
    /// position (not the target's) decides the child's left-to-right order.
    /// </summary>
    [NodeDescription("Bends an edge to tidy the graph; skipped at runtime.", "Created by double-clicking an edge. The runner connects the parent straight to the node behind it. Delete it to reconnect directly.")]
    [NodeInfo("Reroute", "Utility/Reroute", true, true, isChildOnly: true)]
    public class RerouteNode : Node
    {
        [HideInInspector] public Node child;

        private const int MaxChain = 64;

        /// <summary>Follows reroute chains to the real node (null if the chain ends unconnected or loops).</summary>
        public static Node Resolve(Node node)
        {
            for (int i = 0; i < MaxChain && node is RerouteNode reroute; i++) node = reroute.child;
            return node is RerouteNode ? null : node;
        }

        // Only reached if a reroute is ticked without going through Clone (e.g. a hand-built tree): pass through.
        protected override Status OnProcess() => child != null ? child.Process() : Status.Failure;

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override Node Clone()
        {
            var node = (RerouteNode)base.Clone();
            node.child = null;
            return node;
        }

        public override void AddChild(Node newChild) => child = newChild;

        public override void RemoveChild(Node childToRemove)
        {
            if (child == childToRemove) child = null;
        }

        public override List<Node> GetChildren() => child != null ? new List<Node> { child } : new List<Node>();
    }
}
