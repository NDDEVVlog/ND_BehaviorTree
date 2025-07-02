// --- START OF FILE RootNode.cs ---

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ND_BehaviorTree
{
    // The RootNode is the entry point of the tree. It can only have one child.
    [NodeInfo("Root","Root/RootNode",false,true, iconPath:"Assets/NDBT/Icons/antivirus.png")]
    public class RootNode : Node
    {
        [HideInInspector] public Node child;

        protected override void OnEnter() { }
        protected override void OnExit() { }

        protected override Status OnProcess()
        {
            return child == null ? Status.Success : child.Process();
        }

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }
    }
}
// --- END OF FILE RootNode.cs ---