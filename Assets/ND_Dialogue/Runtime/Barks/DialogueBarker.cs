using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    public enum BarkTriggerEvent
    {
        /// <summary>Only when code (or a behavior tree's Bark node) calls <see cref="DialogueBarker.Bark()"/>.</summary>
        Manual,
        /// <summary>Once when the scene starts.</summary>
        OnStart,
        /// <summary>When a collider with the trigger tag enters this object's trigger collider (3D or 2D).</summary>
        OnTriggerEnter,
        /// <summary>Every <c>interval</c> seconds.</summary>
        Interval,
    }

    /// <summary>
    /// Makes a character bark: say one line of a bark conversation in passing (a guard muttering, Malfoy's "Hmph"),
    /// shown in its <see cref="BarkUI"/> bubble, while the game goes on. Which line comes from the conversation's START
    /// links and their conditions (see <see cref="DialogueRunner.Bark"/>), so barks can follow the story.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Barker")]
    public class DialogueBarker : MonoBehaviour, IDialogueDatabaseOwner
    {
        private const string LogTag = "[DialogueBarker]";

        [SerializeField] private DialogueManager m_Manager;
        [Tooltip("The bark conversation: START links to the lines this character may say.")]
        [DialogueConversation, SerializeField] private string m_Conversation;
        [SerializeField] private BarkOrder m_Order = BarkOrder.Random;
        [SerializeField] private BarkUI m_UI;

        [Header("When")]
        [SerializeField] private BarkTriggerEvent m_When = BarkTriggerEvent.Manual;
        [Tooltip("Interval: seconds between barks.")]
        [SerializeField] private float m_Interval = 8f;
        [Tooltip("OnTriggerEnter: only colliders with this tag (empty = any).")]
        [SerializeField] private string m_TriggerTag = "Player";
        [Tooltip("Seconds after a bark before the next one may start.")]
        [SerializeField] private float m_Cooldown = 3f;
        [Tooltip("No bark while the manager plays a conversation.")]
        [SerializeField] private bool m_QuietWhileTalking = true;

        private int m_Sequence;
        private float m_ReadyAt;
        private float m_NextInterval;

        public DialogueDatabase Database => m_Manager != null ? m_Manager.Database : null;
        public DialogueManager Manager { get => m_Manager; set => m_Manager = value; }
        public string Conversation { get => m_Conversation; set => m_Conversation = value; }
        public BarkOrder Order { get => m_Order; set => m_Order = value; }
        public BarkUI UI { get => m_UI; set => m_UI = value; }
        public float Cooldown { get => m_Cooldown; set => m_Cooldown = value; }

        /// <summary>(lines) This character barked.</summary>
        public event Action<IReadOnlyList<DialogueLine>> Barked;

        /// <summary>Barks its conversation. See <see cref="Bark(string)"/>.</summary>
        public bool Bark() => Bark(m_Conversation);

        /// <summary>
        /// Says a line of <paramref name="conversationTitle"/> now. False when cooling down, the manager is talking
        /// (with Quiet While Talking), or no line is valid; logged when the setup is incomplete.
        /// </summary>
        public bool Bark(string conversationTitle)
        {
            if (m_Manager == null || string.IsNullOrEmpty(conversationTitle))
            {
                Debug.LogError($"{LogTag} '{name}': {nameof(Bark)} rejected: manager={(m_Manager != null)}, conversation='{conversationTitle}'.", this);
                return false;
            }
            if (Time.time < m_ReadyAt) return false;                       // cooling down: normal, not an error
            if (m_QuietWhileTalking && m_Manager.IsTalking) return false;  // a conversation has the stage
            var lines = m_Manager.Bark(conversationTitle, m_Order, ref m_Sequence);
            if (lines.Count == 0) return false;                            // nothing valid to say right now
            m_ReadyAt = Time.time + m_Cooldown;
            if (m_UI != null) m_UI.Show(lines);
            Barked?.Invoke(lines);
            return true;
        }

        private void Start()
        {
            m_NextInterval = Time.time + m_Interval;
            if (m_When == BarkTriggerEvent.OnStart) Bark();
        }

        private void Update()
        {
            if (m_When != BarkTriggerEvent.Interval || Time.time < m_NextInterval) return;
            m_NextInterval = Time.time + m_Interval;
            Bark();
        }

        private void OnTriggerEnter(Collider other) => OnEntered(other.gameObject);
        private void OnTriggerEnter2D(Collider2D other) => OnEntered(other.gameObject);

        private void OnEntered(GameObject other)
        {
            if (m_When != BarkTriggerEvent.OnTriggerEnter) return;
            if (!string.IsNullOrEmpty(m_TriggerTag) && !other.CompareTag(m_TriggerTag)) return;
            Bark();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Dialogue Manager (its world decides which line is valid).", this);
            else if (m_Manager.Database != null && !string.IsNullOrEmpty(m_Conversation) && m_Manager.Database.FindConversation(m_Conversation) == null)
                Debug.LogWarning($"{LogTag} '{name}': no conversation titled '{m_Conversation}' in '{m_Manager.Database.name}'.", this);
            if (m_UI == null) Debug.LogWarning($"{LogTag} '{name}': no Bark UI: barks run their actions but show nothing.", this);
        }
#endif
    }
}
