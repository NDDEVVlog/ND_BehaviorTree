// --- START OF FILE RootNode.cs ---

using System.Collections.Generic; // Added for List support
using UnityEngine;

namespace ND_BehaviorTree
{
    // The RootNode is the entry point of the tree. It can only have one child.
    // The NodeInfo attribute seems to be for your editor, which is great.
    [NodeInfo("Root",null,false,true, iconPath:"Assets/NDBT/Icons/antivirus.png")]
    public class RootNode : Node
    {
         public Node child;

        protected override void OnEnter() { }
        protected override void OnExit() { }

        protected override Status OnProcess()
        {
            // If there's no child, the tree's work is done (successfully).
            return child == null ? Status.Success : child.Process();
        }

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override Node Clone()
        {
            RootNode node = base.Clone() as RootNode;
            // The RootNode is special. Its child is cloned and linked
            // by the main BehaviorTree.Clone() method.
            node.child = null; 
            return node;
        }
        
        // --- Child Management Overrides ---

        /// <summary>
        /// Sets the single child of the RootNode. Replaces any existing child.
        /// </summary>
        public override void AddChild(Node newChild)
        {
            this.child = newChild;
        }

        /// <summary>
        /// Removes the child if it matches the one passed in.
        /// </summary>
        public override void RemoveChild(Node childToRemove)
        {
            if (this.child == childToRemove)
            {
                this.child = null;
            }
        }

        /// <summary>
        /// Returns a list containing the single child, if it exists.
        /// </summary>
        public override List<Node> GetChildren()
        {
            var list = new List<Node>();
            if (child != null)
            {
                list.Add(child);
            }
            return list;
        }
    }
}
// --- END OF FILE RootNode.cs ---