using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Independent entry point. Its branch does not run with the main tree; it runs only after
    /// <see cref="BehaviorTree.SendEvent"/> (or <c>BehaviorTreeRunner.SendEvent</c>) is called with <see cref="eventName"/>.
    ///
    /// Lifecycle (see BehaviorTree.SendEvent for scheduling):
    ///   - Sent events start on the next tree Update and tick every Update until the branch returns Success/Failure.
    ///   - interruptMainTree = false: runs alongside the main tree.
    ///   - interruptMainTree = true: the main tree is reset and paused while the event runs, then restarts from its root.
    ///   - Sent again while running: ignored, unless restartIfRunning = true (branch is reset and restarted).
    /// </summary>
    [NodeDescription("Entry point that runs only when its event is sent (SendEvent).", "BehaviorTreeRunner.SendEvent(name) starts the branch on the next update. Interrupt Main Tree pauses and resets the main tree while it runs; otherwise it runs alongside.")]
    [NodeInfo("Event", "Event/Event", false, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Event.png")]
    public class EventNode : Node
    {
        [Tooltip("Name passed to SendEvent to run this branch. Must be unique in the tree.")]
        public string eventName = "NewEvent";

        [Tooltip("On: reset and pause the main tree while this event runs (it restarts from the root afterwards).\nOff: run alongside the main tree.")]
        public bool interruptMainTree;

        [Tooltip("If the event is sent again while it is running: On = restart the branch, Off = ignore the new call.")]
        public bool restartIfRunning;

        [HideInInspector] public Node child;

        protected override Status OnProcess() => child == null ? Status.Success : child.Process();

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override Node Clone()
        {
            var node = (EventNode)base.Clone();
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
