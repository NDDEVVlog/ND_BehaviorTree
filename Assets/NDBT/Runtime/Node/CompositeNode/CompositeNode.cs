// --- START OF FILE CompositeNode.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        // This is the correct and only place for the 'children' list.
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

        // --- Child Management Overrides ---
        public override void AddChild(Node child)
        {
            if (children == null) children = new List<Node>();
            children.Add(child);
        }

        public override void RemoveChild(Node child)
        {
            if (children != null)
                children.Remove(child);
        }

        public override List<Node> GetChildren()
        {
            // Return a copy to prevent external modification of the list
            return new List<Node>(children);
        }
        
        // --- Auxiliary Node Handling ---

        /// <summary>
        /// Checks if all attached decorators are satisfied.
        /// Decorators on a composite node act as gatekeepers for the entire branch.
        /// </summary>
        protected bool AreDecoratorsSatisfied()
        {
            foreach (var decorator in decorators)
            {
                // Note: This assumes decorators on a composite are purely conditional.
                if (decorator.Process() == Status.Failure)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Ticks all attached services.
        /// </summary>
        protected void TickServices()
        {
            foreach (var service in services)
            {
                service.Process(); // Service's Process() handles its own timing.
            }
        }
    }
}
// --- END OF FILE CompositeNode.cs ---