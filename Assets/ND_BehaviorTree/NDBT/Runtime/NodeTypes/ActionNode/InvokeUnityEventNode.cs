

using UnityEngine;
using UnityEngine.Events;

namespace ND_BehaviorTree
{
    [NodeInfo("Invoke Unity Event", "Action/Events/InvokeUnityEvent", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Event.png")]
    public class InvokeUnityEventNode : ActionNode
    {
        [Tooltip("The Blackboard key (of type UnityEvent) that holds the event to be invoked.")]
        [BlackboardKeyType(typeof(UnityEvent))] // Correctly using typeof() for the attribute
        public Key unityEventKey;

        protected override Status OnProcess()
        {
            // 1. --- Validate Configuration ---
            if (unityEventKey == null || string.IsNullOrEmpty(unityEventKey.keyName))
            {
                Debug.LogError($"InvokeUnityEventNode: The 'unityEventKey' has not been assigned in the editor on node '{name}'.");
                return Status.Failure;
            }

            // 2. --- Get the UnityEvent from the Blackboard ---
            // We expect a UnityEvent to have been stored on the blackboard by another component.
            UnityEvent eventToInvoke = blackboard.GetValue<UnityEvent>(unityEventKey.keyName);

            // 3. --- Check if the event is valid and invoke it ---
            if (eventToInvoke != null)
            {
                // The core of the node: trigger the event.
                // This will call all functions that have been registered to it in the Inspector.
                eventToInvoke.Invoke();
                
                // This action is instantaneous, so it succeeds immediately.
                return Status.Success;
            }
            else
            {
                // This is a common runtime issue: the key is set, but the value is missing or null.
                Debug.LogWarning($"InvokeUnityEventNode: Could not find a valid UnityEvent on the blackboard with key '{unityEventKey.keyName}'.");
                return Status.Failure;
            }
        }
    }
}