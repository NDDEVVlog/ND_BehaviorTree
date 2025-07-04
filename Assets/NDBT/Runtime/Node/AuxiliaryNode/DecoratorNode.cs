// --- START OF FILE DecoratorNode.cs ---

using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class DecoratorNode : AuxiliaryNode
    {
        public override Node Clone()
        {
            DecoratorNode node = base.Clone() as DecoratorNode;
            // Note: The child itself is cloned by the BehaviorTree's Clone method.
            node.child = null;
            return node;
        }

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override List<Node> GetChildren()
        {
            return child != null ? new List<Node> { child } : new List<Node>();
        }
    }
}
// --- END OF FILE DecoratorNode.cs ---