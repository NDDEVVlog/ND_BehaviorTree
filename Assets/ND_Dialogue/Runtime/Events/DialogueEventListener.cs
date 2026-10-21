using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ND.Dialogue
{
    /// <summary>
    /// Reacts in the scene to events fired by dialogue: each binding maps an event name (picked from the database's
    /// Events) to a UnityEvent that receives the action's argument. Listens only while enabled.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("ND Dialogue/Dialogue Event Listener")]
    public class DialogueEventListener : MonoBehaviour, IDialogueDatabaseOwner
    {
        [Serializable]
        public class Binding
        {
            [DialogueEvent] public string eventName;
            public UnityEvent<string> response = new UnityEvent<string>();
        }

        [Tooltip("Only for the event-name pickers in the Inspector.")]
        [SerializeField] private DialogueDatabase m_Database;
        [SerializeField] private List<Binding> m_Bindings = new List<Binding>();

        public DialogueDatabase Database => m_Database;
        public List<Binding> Bindings => m_Bindings;

        private void OnEnable() => DialogueEvents.Raised += OnRaised;
        private void OnDisable() => DialogueEvents.Raised -= OnRaised;

        private void OnRaised(string eventName, string argument)
        {
            foreach (var binding in m_Bindings)
                if (binding != null && binding.eventName == eventName) binding.response.Invoke(argument);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || m_Database == null) return;
            foreach (var binding in m_Bindings)
                if (binding != null && !string.IsNullOrEmpty(binding.eventName) && m_Database.FindEvent(binding.eventName) == null)
                    Debug.LogWarning($"[DialogueEventListener] '{name}': event '{binding.eventName}' is not declared in '{m_Database.name}'.", this);
        }
#endif
    }
}
