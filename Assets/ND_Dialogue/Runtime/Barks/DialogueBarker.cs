using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ND.Dialogue
{
    public enum BarkTriggerEvent
    {
        /// <summary>Only when code (or a behavior tree's Bark node) calls <see cref="DialogueBarker.Bark()"/>.</summary>
        Manual,
        /// <summary>Once when the scene starts (after the start delay).</summary>
        OnStart,
        /// <summary>When a collider with the trigger tag enters this object's trigger collider (3D or 2D).</summary>
        OnTriggerEnter,
        /// <summary>When a collider with the trigger tag leaves this object's trigger collider (3D or 2D): "See you!".</summary>
        OnTriggerExit,
        /// <summary>Every few seconds (a random time between the interval's min and max).</summary>
        Interval,
        /// <summary>When a conversation of the manager ends (any, or the one named in After Conversation).</summary>
        AfterConversation,
    }

    /// <summary>
    /// Makes a character bark: say one line of a bark conversation in passing (a guard muttering, Malfoy's "Hmph"),
    /// shown in its <see cref="BarkUI"/> bubble, while the game goes on. Which line comes from the conversation's START
    /// links and their conditions (see <see cref="DialogueRunner.Bark"/>), so barks can follow the story.
    /// <para>On top of that the barker decides when: its trigger, a random interval, a start delay, a chance, a
    /// cooldown, a maximum count, its own conditions and how close the listener (the player) must be. When a bark does
    /// not happen, <see cref="CanBark"/> says why (Log Rejections prints it).</para>
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Barker")]
    public class DialogueBarker : MonoBehaviour, IDialogueDatabaseOwner
    {
        private const string LogTag = "[DialogueBarker]";

        [SerializeField] private DialogueManager m_Manager;
        [Tooltip("The bark conversation: START links to the lines this character may say.")]
        [DialogueBarkConversation, SerializeField] private string m_Conversation;
        [SerializeField] private BarkOrder m_Order = BarkOrder.Random;
        [Tooltip("Random order: never the same line twice in a row (when another one is valid).")]
        [SerializeField] private bool m_AvoidRepeat = true;
        [SerializeField] private BarkUI m_UI;

        [Header("When")]
        [SerializeField] private BarkTriggerEvent m_When = BarkTriggerEvent.Manual;
        [Tooltip("Interval: shortest wait between barks (seconds).")]
        [FormerlySerializedAs("m_Interval")]
        [SerializeField, Min(0f)] private float m_IntervalMin = 8f;
        [Tooltip("Interval: longest wait between barks (seconds). Each wait is random between min and max.")]
        [SerializeField, Min(0f)] private float m_IntervalMax = 12f;
        [Tooltip("Seconds after the scene starts before the first bark (OnStart and Interval).")]
        [SerializeField, Min(0f)] private float m_StartDelay;
        [Tooltip("OnTriggerEnter / OnTriggerExit: only colliders with this tag (empty = any).")]
        [SerializeField] private string m_TriggerTag = "Player";
        [Tooltip("AfterConversation: only after this conversation (empty = after any).")]
        [DialogueConversation, SerializeField] private string m_AfterConversation;

        [Header("Rules")]
        [Tooltip("Chance that a bark happens when it is due (1 = always).")]
        [SerializeField, Range(0f, 1f)] private float m_Chance = 1f;
        [Tooltip("Seconds after a bark before the next one may start.")]
        [SerializeField, Min(0f)] private float m_Cooldown = 3f;
        [Tooltip("Barks in total (0 = no limit).")]
        [SerializeField, Min(0)] private int m_MaxBarks;
        [Tooltip("No bark while the manager plays a conversation.")]
        [SerializeField] private bool m_QuietWhileTalking = true;
        [Tooltip("Optional: the listener (usually the player). With a range, barks only happen while it is that close.")]
        [SerializeField] private Transform m_Listener;
        [Tooltip("Listener range in metres (0 = anywhere).")]
        [SerializeField, Min(0f)] private float m_Range;
        [Tooltip("All must hold (checked against the manager's world). The lines' own conditions still pick which line.")]
        [SerializeReference] private List<DialogueCondition> m_Conditions = new List<DialogueCondition>();
        [Tooltip("Print why a due bark did not happen (cooldown, chance, range, conditions…).")]
        [SerializeField] private bool m_LogRejections;

        private int m_Sequence;
        private int m_LastEntryId = -1;
        private int m_Count;
        private float m_ReadyAt;
        private float m_NextInterval;
        private DialogueManager m_Bound;

        public DialogueDatabase Database => m_Manager != null ? m_Manager.Database : null;
        public DialogueManager Manager { get => m_Manager; set { Unbind(); m_Manager = value; if (isActiveAndEnabled) Bind(); } }
        public string Conversation { get => m_Conversation; set => m_Conversation = value; }
        public BarkOrder Order { get => m_Order; set => m_Order = value; }
        public BarkUI UI { get => m_UI; set => m_UI = value; }
        public BarkTriggerEvent When { get => m_When; set => m_When = value; }
        public float Cooldown { get => m_Cooldown; set => m_Cooldown = value; }
        public float Chance { get => m_Chance; set => m_Chance = Mathf.Clamp01(value); }
        public int MaxBarks { get => m_MaxBarks; set => m_MaxBarks = Mathf.Max(0, value); }
        public bool AvoidRepeat { get => m_AvoidRepeat; set => m_AvoidRepeat = value; }
        public Transform Listener { get => m_Listener; set => m_Listener = value; }
        public float Range { get => m_Range; set => m_Range = Mathf.Max(0f, value); }
        public List<DialogueCondition> Conditions => m_Conditions;
        /// <summary>Barks said so far.</summary>
        public int Count => m_Count;

        /// <summary>(lines) This character barked.</summary>
        public event Action<IReadOnlyList<DialogueLine>> Barked;

        /// <summary>
        /// Whether a bark may start now, and if not why: setup, talking, cooldown, count, listener range, conditions.
        /// (The chance and the lines' own conditions are decided by <see cref="Bark(string)"/>.)
        /// </summary>
        public bool CanBark(out string reason)
        {
            if (m_Manager == null) { reason = "no manager"; return false; }
            if (!m_Manager.IsReady) { reason = $"manager state={m_Manager.State}"; return false; }
            if (m_QuietWhileTalking && m_Manager.IsTalking) { reason = "a conversation is playing"; return false; }
            if (Time.time < m_ReadyAt) { reason = $"cooling down ({m_ReadyAt - Time.time:0.0} s left)"; return false; }
            if (m_MaxBarks > 0 && m_Count >= m_MaxBarks) { reason = $"said its {m_MaxBarks} bark(s)"; return false; }
            if (m_Range > 0f && m_Listener != null)
            {
                float distance = Vector3.Distance(m_Listener.position, transform.position);
                if (distance > m_Range) { reason = $"listener too far ({distance:0.0} m > {m_Range:0.0} m)"; return false; }
            }
            foreach (var condition in m_Conditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(m_Manager.Context)) { reason = $"condition failed: {condition.Describe()}"; return false; }
            }
            reason = null;
            return true;
        }

        /// <summary>Barks its conversation. See <see cref="Bark(string)"/>.</summary>
        public bool Bark() => Bark(m_Conversation);

        /// <summary>
        /// Says a line of <paramref name="conversationTitle"/> now, if <see cref="CanBark"/>, the chance and a valid line
        /// allow it. Logged when the setup is incomplete; the other refusals only with Log Rejections.
        /// </summary>
        public bool Bark(string conversationTitle)
        {
            if (m_Manager == null || string.IsNullOrEmpty(conversationTitle))
            {
                Debug.LogError($"{LogTag} '{name}': {nameof(Bark)} rejected: manager={(m_Manager != null)}, conversation='{conversationTitle}'.", this);
                return false;
            }
            if (!CanBark(out string reason)) return Refuse(reason);
            if (m_Chance < 1f && UnityEngine.Random.value > m_Chance) return Refuse($"chance ({m_Chance:P0}) said no");
            var lines = m_Manager.Bark(conversationTitle, m_Order, ref m_Sequence, m_AvoidRepeat ? m_LastEntryId : -1);
            if (lines.Count == 0) return Refuse($"no line of '{conversationTitle}' is valid now");
            m_ReadyAt = Time.time + m_Cooldown;
            m_Count++;
            m_LastEntryId = lines[0].Entry != null ? lines[0].Entry.id : -1;
            if (m_UI != null) m_UI.Show(lines);
            Barked?.Invoke(lines);
            return true;
        }

        private bool Refuse(string reason)
        {
            if (m_LogRejections) Debug.Log($"{LogTag} '{name}' did not bark: {reason}.", this);
            return false;
        }

        /// <summary>Forgets the count, cooldown and last line (e.g. a new day: the same barks may come again).</summary>
        public void ResetBarks()
        {
            m_Count = 0;
            m_ReadyAt = 0f;
            m_LastEntryId = -1;
            m_Sequence = 0;
        }

        private float NextWait() => UnityEngine.Random.Range(Mathf.Min(m_IntervalMin, m_IntervalMax), Mathf.Max(m_IntervalMin, m_IntervalMax));

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (m_Manager == null || m_Bound == m_Manager) return;
            Unbind();
            m_Bound = m_Manager;
            m_Bound.ConversationEnded += OnConversationEnded;
        }

        private void Unbind()
        {
            if (m_Bound == null) return;
            m_Bound.ConversationEnded -= OnConversationEnded;
            m_Bound = null;
        }

        private void Start()
        {
            m_NextInterval = Time.time + m_StartDelay + (m_When == BarkTriggerEvent.Interval ? NextWait() : 0f);
        }

        private bool m_StartBarked;

        private void Update()
        {
            if (Time.time < m_NextInterval) return;
            if (m_When == BarkTriggerEvent.OnStart && !m_StartBarked)
            {
                m_StartBarked = true;
                Bark();
            }
            else if (m_When == BarkTriggerEvent.Interval)
            {
                m_NextInterval = Time.time + NextWait();
                Bark();
            }
        }

        private void OnConversationEnded(Conversation conversation)
        {
            if (m_When != BarkTriggerEvent.AfterConversation) return;
            if (!string.IsNullOrEmpty(m_AfterConversation) && (conversation == null || conversation.title != m_AfterConversation)) return;
            Bark();
        }

        private void OnTriggerEnter(Collider other) => OnCollider(other.gameObject, BarkTriggerEvent.OnTriggerEnter);
        private void OnTriggerEnter2D(Collider2D other) => OnCollider(other.gameObject, BarkTriggerEvent.OnTriggerEnter);
        private void OnTriggerExit(Collider other) => OnCollider(other.gameObject, BarkTriggerEvent.OnTriggerExit);
        private void OnTriggerExit2D(Collider2D other) => OnCollider(other.gameObject, BarkTriggerEvent.OnTriggerExit);

        private void OnCollider(GameObject other, BarkTriggerEvent kind)
        {
            if (m_When != kind) return;
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
            else if (m_Manager.Database != null && m_Manager.Database.FindConversation(m_Conversation) is Conversation c && !c.isBark)
                Debug.LogWarning($"{LogTag} '{name}': '{m_Conversation}' is not marked as a bark conversation (Dialogue Editor › its Bark toggle) — is that the conversation you meant?", this);
            if (m_UI == null) Debug.LogWarning($"{LogTag} '{name}': no Bark UI: barks run their actions but show nothing.", this);
            if (m_Range > 0f && m_Listener == null) Debug.LogWarning($"{LogTag} '{name}': a range is set but no listener: the range is ignored.", this);
        }
#endif
    }
}
