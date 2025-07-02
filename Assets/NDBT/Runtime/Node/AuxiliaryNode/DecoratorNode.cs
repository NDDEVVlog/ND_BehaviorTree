// --- START OF FILE DecoratorNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class DecoratorNode : AuxiliaryNode
    {
        [HideInInspector] public Node child;

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
    }
}
// --- END OF FILE DecoratorNode.cs ---