using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Optional one-time initialization branch (at most one per tree).
    ///
    /// Contract:
    ///   - Root node: the main tree, ticked every Update and restarted after it completes.
    ///   - Init node: runs ONCE per runtime instance, before the main tree's first tick. BehaviorTreeRunner calls
    ///     RuntimeTree.RunInitialization() in Start, right after the virtual Init(), so an instant branch finishes there.
    ///     If it returns Running, Update keeps ticking it; the main tree and events wait until it finishes.
    ///     It never runs again when the main tree restarts.
    ///   - Event nodes: run only when sent (after initialization has finished).
    ///   - Failure: logged as a warning and the main tree starts anyway, unless stopTreeOnFailure is on
    ///     (the tree then stays stopped and Update returns Failure).
    /// </summary>
    [NodeInfo("Init", "Event/Init (runs once at start)", false, true)]
    public class InitNode : Node
    {
        [Tooltip("If the init branch fails: On = keep the tree stopped (Update returns Failure). Off = log a warning and start the main tree.")]
        public bool stopTreeOnFailure;

        [HideInInspector] public Node child;

        protected override Status OnProcess() => child == null ? Status.Success : child.Process();

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override Node Clone()
        {
            var node = (InitNode)base.Clone();
            node.child = null; // rewired by BehaviorTree.Clone
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
