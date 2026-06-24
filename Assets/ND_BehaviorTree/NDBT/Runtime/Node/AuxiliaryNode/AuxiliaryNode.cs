using System.Collections.Generic;

namespace ND_BehaviorTree
{
    public abstract class AuxiliaryNode : Node
    {
        public Node child;

        public void SetChild(Node newChild) => child = newChild;

        public override void AddChild(Node newChild) => child = newChild;

        public override void RemoveChild(Node childToRemove)
        {
            if (child == childToRemove) child = null;
        }

        public override List<Node> GetChildren() => child != null ? new List<Node> { child } : new List<Node>();

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }
    }
}