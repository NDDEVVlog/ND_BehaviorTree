// --- START OF FILE DebugLogNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Debug Log", "Action/DebugLog", true, false)]
    public class DebugLogNode : ActionNode
    {
        public string message = "Log Message";
        public Key stringKey;

        protected override Status OnProcess()
        {
            Debug.Log($"[{Time.frameCount}] {message}");
            return Status.Success;
        }
    }
}
// --- END OF FILE DebugLogNode.cs ---