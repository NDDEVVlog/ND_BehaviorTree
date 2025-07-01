// --- START OF FILE AnimationEventHub.cs (Corrected) ---

using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

// Your MonoBehaviour MUST inherit from SerializedMonoBehaviour for Odin to take full control.
public class AnimationEventHub : SerializedMonoBehaviour
{
    public string eventToAction;

    [ShowInInspector]
    [OdinSerialize]
    // --- FIX: Replaced obsolete 'Expanded' with 'DefaultExpandedState' ---
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, CustomAddFunction = "CreateNewDynamicEvent")]
    [PropertyOrder(10)] // Puts it at the bottom
    private List<DynamicEvent> dynamicEvents = new List<DynamicEvent>();

    // A helper function for the '+' button on the list
    private void CreateNewDynamicEvent()
    {
        dynamicEvents.Add(new DynamicEvent());
    }

    [Button]
    public void TriggerEvent(string eventName)
    {
        DynamicEvent eventToTrigger = dynamicEvents.FirstOrDefault(e => e.eventName == eventName);

        if (eventToTrigger != null)
        {
            eventToTrigger.Invoke();
        }
        else
        {
            Debug.LogWarning($"AnimationEventHub: Event with name '{eventName}' not found on {gameObject.name}.", this);
        }
    }
    
    public void SetParameterValue<T>(string eventName, string parameterName, T value)
    {
        DynamicEvent eventToUpdate = dynamicEvents.FirstOrDefault(e => e.eventName == eventName);

        if (eventToUpdate != null)
        {
            eventToUpdate.SetParameterValue(parameterName, value);
        }
        else
        {
            Debug.LogWarning($"AnimationEventHub: Could not set parameter '{parameterName}' because event '{eventName}' was not found on {gameObject.name}.", this);
        }
    }


    public void OnValidate()
    {
        //TriggerEvent(eventToAction);
    }
}