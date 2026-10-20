using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeDescription("Logs a message (and optionally a key's value) to the Console.", "Returns the chosen status (Success by default). Remove it from shipping trees.")]
    [NodeInfo("Debug Log", "Action/Debug/DebugLog", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/DebugIcon.png")]
    public class DebugLogNode : ActionNode
    {
        [Tooltip("The base message to log to the console.")]
        public string message = "Log Message";

        [Tooltip("The color of the log message in the console.")]
        public Color logColor = Color.green;

        [Tooltip("(Optional) A Blackboard key to log the value of.")]
        [BlackboardKeyType(typeof(object))]
        public Key keyToLog;


        [Tooltip("Status returned after logging. 'None' is treated as Success.")]
        public Status returningStatus = Status.Success;

        protected override Status OnProcess()
        {
            // Start building the log string with the basic message.
            string logString = $"{message}";

            // If a valid key is provided, fetch its value from the blackboard and append it.
            if (keyToLog != null && !string.IsNullOrEmpty(keyToLog.keyName))
            {
                // Works for every key type (GetValue<object> only matched ObjectKey).
                string valueText;
                if (blackboard != null && blackboard.TryGetKey(keyToLog.keyName, out var key))
                {
                    object value = key.GetValueObject();
                    if (value is Object unityObject) valueText = unityObject != null ? unityObject.ToString() : "null";
                    else valueText = value?.ToString() ?? "null";
                }
                else
                {
                    valueText = "<key not found>";
                }

                logString += $" | Key '{keyToLog.keyName}': {valueText}";
            }

            // Wrap the entire constructed string in the chosen color using rich text.
            // ColorUtility.ToHtmlStringRGB is the perfect Unity helper for this.
            string finalColoredLog = $"<color=#{ColorUtility.ToHtmlStringRGB(logColor)}>{logString}</color>";

            // Log the final, colored string.
            Debug.Log(finalColoredLog);

            // This action is instantaneous. Status.None is not a valid result for a finished node.
            return returningStatus == Status.None ? Status.Success : returningStatus;
        }
    }
}
