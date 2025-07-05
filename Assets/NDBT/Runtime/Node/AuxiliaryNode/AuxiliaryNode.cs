// --- MODIFIED FILE: AuxiliaryNode.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class AuxiliaryNode : Node
    {
         public Node child;

        // Note: The original SetChild parameter was too specific. 
        // An AuxiliaryNode can decorate any kind of Node.
        public void SetChild(Node child)
        {
            this.child = child;
        }

        // --- Child Management Overrides ---
        public override void AddChild(Node newChild)
        {
            // An auxiliary node can only have one child.
            // This replaces any existing child.
            this.child = newChild;
        }

        public override void RemoveChild(Node childToRemove)
        {
            // Only remove the child if it's the one we currently have.
            if (this.child == childToRemove)
            {
                this.child = null;
            }
        }

        public override List<Node> GetChildren()
        {
            var list = new List<Node>();
            if (child != null)
            {
                list.Add(child);
            }
            return list;
        }

        public override void Reset()
        {
            base.Reset();
            if(child != null)
                child.Reset();
        }
    }
}