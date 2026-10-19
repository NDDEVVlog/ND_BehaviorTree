using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpellTech.DynamicMethodEvent
{
    public class MethodHub : MonoBehaviour
    {
        public string eventToAction;

        [SerializeField]
        private List<DynamicEvent> dynamicEvents = new List<DynamicEvent>();


        public void TriggerEvent(string eventName)
        {
            // Find the event in our list that matches the provided name (plain loop: no per-call closure).
            DynamicEvent eventToTrigger = null;
            for (int i = 0; i < dynamicEvents.Count; i++)
            {
                var e = dynamicEvents[i];
                if (e != null && e.eventName == eventName)
                {
                    eventToTrigger = e;
                    break;
                }
            }

            if (eventToTrigger != null)
            {
                // If we found a matching event, tell it to invoke its target method.
                eventToTrigger.Invoke();
            }
            else
            {
                // If no event with that name is found, log a warning for easier debugging.
                Debug.LogWarning($"AnimationEventHub: Event with name '{eventName}' not found on {gameObject.name}.", this);
            }
        }
    }

}
