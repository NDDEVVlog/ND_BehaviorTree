// FILE: GOAP/GOAPActionNode.cs

using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{   
    [NodeInfo("GOAPActionNode", "GOAP/GOAPActionNode", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/actionable.png")]
    public class GOAPActionNode : ActionNode
    {
        [Tooltip("The conditions that must be met for this action to be chosen.")]
        [SerializeReference]
        public List<IGoapPrecondition> preconditions;

        [Tooltip("The changes to the world state after this action is successfully completed.")]
        public List<GOAPState> effects ;

        [Tooltip("The cost of performing this action. The planner will try to find the lowest cost plan.")]
        public float cost = 1.0f;

        public Node child;

        protected override Status OnProcess()
        {
            if (child == null)
            {
                Debug.LogWarning($"GOAP Action Node '{name}' has no child Behavior Tree to execute.", this);
                return Status.Failure;
            }
            return child.Process();
        }

        public override void Reset()
        {
            base.Reset();
            if (child != null)
            {
                child.Reset();
            }
        }

        public override void AddChild(Node newChild) => this.child = newChild;
        public override void RemoveChild(Node childToRemove) { if (this.child == childToRemove) this.child = null; }
        public override List<Node> GetChildren() => child != null ? new List<Node> { child } : new List<Node>();

        public override Node Clone()
        {
            GOAPActionNode node = base.Clone() as GOAPActionNode;
            if (child != null)
            {
                node.child = child.Clone();
            }
            return node;
        }
    }
}