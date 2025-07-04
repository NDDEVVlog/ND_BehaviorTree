// --- START OF FILE DebugLogNode.cs ---

using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Debug Log", "Action/DebugLog", true, false)]
    public class DebugLogNode : ActionNode
    {
        public string message = "Log Message";
        public Key stringKey;
        public Key anotherKey;

        protected override Status OnProcess()
        {
            string str1 = stringKey.GetValueObject() as string;
            string str2 = blackboard.GetValue<string>(anotherKey.name);

            string coloredStr1 = $"<color=#4CAF50>{str1}</color>"; // Green
            string coloredStr2 = $"<color=#2196F3>{str2}</color>"; // Blue

            Debug.Log($"[{Time.frameCount}] {message} | Key1: {coloredStr1} | Key2: {coloredStr2}");

            return Status.Success;
        }

    }
}
// --- END OF FILE DebugLogNode.cs ---