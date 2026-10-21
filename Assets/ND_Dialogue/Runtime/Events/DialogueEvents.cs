using System;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Scene-wide hub for gameplay events fired by dialogue (<see cref="RaiseEventAction"/>). Every built-in context
    /// forwards here after its own event, so scene objects can react without a reference to the runner or context
    /// (see <see cref="DialogueEventListener"/>).
    /// </summary>
    public static class DialogueEvents
    {
        /// <summary>(event name, argument)</summary>
        public static event Action<string, string> Raised;

        public static void Raise(string eventName, string argument) => Raised?.Invoke(eventName, argument);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Raised = null;
    }
}
