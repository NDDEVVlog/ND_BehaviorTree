using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class CompositeNode : Node
    {
        [HideInInspector] public List<Node> children = new List<Node>();
        [HideInInspector] public List<ServiceNode> services = new List<ServiceNode>();

        public override void AddChild(Node child)
        {
            if (!children.Contains(child)) children.Add(child);
        }

        public override void RemoveChild(Node child) => children.Remove(child);

        public override List<Node> GetChildren() => children;

        public void AddService(ServiceNode service)
        {
            if (!services.Contains(service)) services.Add(service);
        }

        public void RemoveService(ServiceNode service) => services.Remove(service);

        public override Node Clone()
        {
            CompositeNode node = (CompositeNode)base.Clone();
            node.children = new List<Node>();
            node.services = new List<ServiceNode>();
            return node;
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var child in children) child?.Reset();
            foreach (var service in services) service?.Reset();
        }

        protected void TickServices()
        {
            foreach (var service in services)
            {
                service?.Process();
            }
        }
    }
}