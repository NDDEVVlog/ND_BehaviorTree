// --- START OF FILE CompositeNode.cs ---

using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        [HideInInspector] public List<Node> children = new List<Node>();

        public override Node Clone()
        {
            CompositeNode node = base.Clone() as CompositeNode;
            // Note: The children themselves are cloned by the BehaviorTree's Clone method.
            // Here we just prepare the list for the cloned instance.
            node.children = new List<Node>(); 
            return node;
        }
        
        public override void Reset()
        {
            base.Reset();
            foreach(var child in children)
            {
                child.Reset();
            }
        }
    }
}
// --- END OF FILE CompositeNode.cs ---