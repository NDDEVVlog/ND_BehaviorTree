using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Debug Log", "Action/Debug/DebugLog", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/DebugIcon.png")]
    public class DebugLogNode : ActionNode
    {
        [Tooltip("The base message to log to the console.")]
        public string message = "Log Message";

        [Tooltip("The color of the log message in the console.")]
        public Color logColor = Color.green;

        [Tooltip("(Optional) A Blackboard key to log the value of.")]
        public Key keyToLog;

        public Status returningStatus = Status.Success;

        protected override Status OnProcess()
        {
            // Start building the log string with the basic message.
            string logString = $"{message}";

            // If a valid key is provided, fetch its value from the blackboard and append it.
            // (Assuming Key has a way to check if it's set, e.g., a non-empty name)
            if (keyToLog != null && !string.IsNullOrEmpty(keyToLog.keyName))
            {
                // The 'blackboard' property comes from the base Node class we set up.
                // We use GetValue<object> to handle any type of data.
                object value = blackboard.GetValue<object>(keyToLog.keyName);

                // Append the key's name and its value to the string.
                // The '?? "null"' part gracefully handles a null value.
                logString += $" | Key '{keyToLog.name}': {value ?? "null"}";
            }

            // Wrap the entire constructed string in the chosen color using rich text.
            // ColorUtility.ToHtmlStringRGB is the perfect Unity helper for this.
            string finalColoredLog = $"<color=#{ColorUtility.ToHtmlStringRGB(logColor)}>{logString}</color>";

            // Log the final, colored string.
            Debug.Log(finalColoredLog);

            // This action is instantaneous, so it always succeeds.
            return returningStatus;
        }
    }
}