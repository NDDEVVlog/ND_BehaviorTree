using System;

namespace ND_BehaviorTree
{
    public class NodeInfoAttribute : Attribute
    {
        public string title { get; }
        public string menuItem { get; }
        public bool hasFlowInput { get; }
        public bool hasFlowOutput { get; }
        public string iconPath { get; }
        public bool isChildOnly { get; } // NEW: Flag to hide from main search

        public NodeInfoAttribute(
            string title, 
            string menuItem = "", 
            bool hasFlowInput = true, 
            bool hasFlowOutput = true, 
            string iconPath = null,
            bool isChildOnly = false) // NEW: Added to constructor
        {
            this.title = title;
            this.menuItem = menuItem;
            this.hasFlowInput = hasFlowInput;
            this.hasFlowOutput = hasFlowOutput;
            this.iconPath = iconPath;
            this.isChildOnly = isChildOnly; // NEW: Assign the flag
        }
    }
}