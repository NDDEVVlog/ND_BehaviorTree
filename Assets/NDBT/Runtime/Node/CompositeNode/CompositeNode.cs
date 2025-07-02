// --- CREATE NEW FILE: CompositeNode.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        public List<Node> children = new List<Node>();
        
        // Lists to hold the new auxiliary nodes
         public List<DecoratorNode> decorators = new List<DecoratorNode>();
       public List<ServiceNode> services = new List<ServiceNode>();

        public override Node Clone()
        {
            CompositeNode node = base.Clone() as CompositeNode;
            // Children, Decorators, and Services are cloned and assigned by the BehaviorTree's Clone method
            node.children = new List<Node>();
            node.decorators = new List<DecoratorNode>();
            node.services = new List<ServiceNode>();
            return node;
        }

        public override void Reset()
        {
            base.Reset();
            children.ForEach(c => c.Reset());
            decorators.ForEach(d => d.Reset());
            services.ForEach(s => s.Reset());
        }
    }
}