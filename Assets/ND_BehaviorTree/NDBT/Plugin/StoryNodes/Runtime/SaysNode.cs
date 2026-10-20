using UnityEngine;

namespace ND_BehaviorTree.Story
{
    [StorySentence("{self} says {message}")]
    [NodeDescription("Shows a speech bubble above the speaker (and logs it).",
        "The bubble needs a StorySpeechRenderer in the scene; the line is always logged to the Console when 'Log To Console' is on.\n" +
        "Succeeds immediately, or after the bubble disappears when 'Wait Until Done' is on.")]
    [NodeInfo("Says", "Story/Says", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Audio.png")]
    public class SaysNode : StoryActionNode
    {
        public string message = "Hello!";
        [BlackboardKeyType(typeof(string))] public Key messageKey;

        [Tooltip("Seconds the bubble stays on screen.")]
        public float duration = 2f;
        [Tooltip("Stay Running until the bubble disappears.")]
        public bool waitUntilDone;
        public bool logToConsole = true;

        [NodeProgressBar(nameof(m_Elapsed), nameof(duration))]
        [System.NonSerialized] private float m_Elapsed;

        protected override void OnEnter()
        {
            m_Elapsed = 0f;
            var speaker = Self;
            if (speaker == null) return;
            string text = Read(messageKey, message);
            StorySpeech.Say(speaker, text, duration);
            if (logToConsole) Debug.Log($"[{speaker.name}] {text}", speaker);
        }

        protected override Status OnProcess()
        {
            if (Self == null) return Status.Failure;
            if (!waitUntilDone) return Status.Success;
            m_Elapsed += Time.deltaTime;
            return m_Elapsed < duration ? Status.Running : Status.Success;
        }
    }
}
