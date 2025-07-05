// --- MODIFIED FILE: CompositeNode.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        // A composite node has a list of children which can include other composites, actions, or decorators.
        public List<Node> children = new List<Node>();

        // Services are auxiliary nodes that run on a timer as long as the composite node is active.
        // They are handled separately from the main execution flow of children.
        public List<ServiceNode> services = new List<ServiceNode>();

        public override Node Clone()
        {
            CompositeNode node = base.Clone() as CompositeNode;
            // Children and Services are cloned and assigned by the BehaviorTree's Clone method
            node.children = new List<Node>();
            node.services = new List<ServiceNode>();
            return node;
        }

        public override void Reset()
        {
            base.Reset();
            children.ForEach(c => c.Reset());
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
        /// Ticks all attached services. This is called each time the composite node's
        /// OnProcess method is executed.
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