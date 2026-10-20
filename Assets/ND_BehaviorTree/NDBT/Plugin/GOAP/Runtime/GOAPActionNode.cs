using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    [NodeDescription("A GOAP action: when it can run (preconditions), what it changes (effects) and its cost. Its child does the work.",
        "Put it under a GOAP Planner. The planner chains actions whose effects reach a goal at the lowest total cost.\n" +
        "When the child succeeds, the effects are written to the blackboard (turn off 'Apply Effects' if the child writes them itself).\n" +
        "Preconditions are checked again on the live blackboard right before the action starts; if they no longer hold, the planner replans.\n" +
        "With no child the action completes instantly (only its effects are applied).")]
    [NodeInfo("GOAP Action", "GOAP/Action", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/actionable.png")]
    public class GOAPActionNode : Node
    {
        [Tooltip("All must hold for the action to be planned and started.")]
        public List<KeyCondition> preconditions = new List<KeyCondition>();
        [Tooltip("What the action changes. Used to plan, and written to the blackboard when the action succeeds.")]
        public List<KeyEffect> effects = new List<KeyEffect>();
        [Tooltip("The planner prefers cheaper plans.")]
        public float cost = 1f;
        [Tooltip("Optional number key (int / float) read as the cost each time the planner plans, e.g. DistanceToTree.")]
        [GoapKey(numbersOnly: true)] public Key costKey;
        [Tooltip("Write the effects to the blackboard when the child succeeds. Turn off if the child writes the real values itself.")]
        public bool applyEffects = true;

        [HideInInspector] public Node child;

        protected override Status OnProcess() => child != null ? child.Process() : Status.Success;

        public override void Abort()
        {
            if (child != null && child.IsRunning()) child.Abort();
            base.Abort();
        }

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override Node Clone()
        {
            var node = (GOAPActionNode)base.Clone();
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
