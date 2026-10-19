using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        [HideInInspector] public List<Node> children = new List<Node>();

        public override void AddChild(Node child)
        {
            if (!children.Contains(child)) children.Add(child);
        }

        public override void RemoveChild(Node child) => children.Remove(child);

        public override List<Node> GetChildren() => children;

        public override Node Clone()
        {
            CompositeNode node = (CompositeNode)base.Clone();
            node.children = new List<Node>();
            return node;
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var child in children) child?.Reset();
        }

        /// <summary>No longer needed: services on every node are ticked by Node.Process().</summary>
        [Obsolete("Services are ticked automatically by Node.Process(); remove this call.")]
        protected void TickServices() { }
    }
}
