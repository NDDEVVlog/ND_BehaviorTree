using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    public enum DialogueTriggerEvent
    {
        /// <summary>A <see cref="DialogueInteractor"/> in range presses its use key (talk to an NPC).</summary>
        OnUse,
        /// <summary>When the scene starts.</summary>
        OnStart,
        /// <summary>When a collider with the trigger tag enters this object's trigger collider (3D or 2D).</summary>
        OnTriggerEnter,
    }

    /// <summary>
    /// Starts a conversation on a <see cref="DialogueManager"/>: when a player uses it (talk to this NPC), when the
    /// scene starts, or when something enters its trigger collider. Optional conditions (the same conditions as
    /// dialogue entries, checked against the manager's world) and once-only.
    /// </summary>
    [ExecuteAlways] // registers in edit mode too, so tools and edit-mode tests see it; it never fires outside Play
    [AddComponentMenu("ND Dialogue/Dialogue Trigger")]
    public class DialogueTrigger : MonoBehaviour, IDialogueDatabaseOwner
    {
        private const string LogTag = "[DialogueTrigger]";

        [SerializeField] private DialogueManager m_Manager;
        [SerializeField] private DialogueTriggerEvent m_When = DialogueTriggerEvent.OnUse;
        [DialogueConversation, SerializeField] private string m_Conversation;

        [Header("Use (talk)")]
        [Tooltip("The character this object is: its display name appears in the prompt.")]
        [DialogueActor, SerializeField] private string m_Actor;
        [SerializeField] private string m_PromptVerb = "Talk to";
        [Tooltip("How close a DialogueInteractor must be (metres).")]
        [SerializeField] private float m_UseRange = 2.5f;

        [Header("Trigger collider")]
        [Tooltip("OnTriggerEnter: only colliders with this tag (empty = any).")]
        [SerializeField] private string m_TriggerTag = "Player";

        [Header("Rules")]
        [Tooltip("Fire only the first time.")]
        [SerializeField] private bool m_Once;
        [Tooltip("All must hold (checked against the manager's world state).")]
        [SerializeReference] private List<DialogueCondition> m_Conditions = new List<DialogueCondition>();

        private bool m_Fired;

        private static readonly List<DialogueTrigger> s_Enabled = new List<DialogueTrigger>();

        /// <summary>Every enabled trigger (interactors search this list).</summary>
        public static IReadOnlyList<DialogueTrigger> Enabled => s_Enabled;

        public DialogueManager Manager { get => m_Manager; set => m_Manager = value; }
        public DialogueDatabase Database => m_Manager != null ? m_Manager.Database : null;
        public DialogueTriggerEvent When { get => m_When; set => m_When = value; }
        public string Conversation { get => m_Conversation; set => m_Conversation = value; }
        public string Actor { get => m_Actor; set => m_Actor = value; }
        public float UseRange { get => m_UseRange; set => m_UseRange = value; }
        public bool Once { get => m_Once; set => m_Once = value; }
        public List<DialogueCondition> Conditions => m_Conditions;
        public bool HasFired => m_Fired;

        /// <summary>"Talk to Gate Guard" — the verb plus the character's display name (or this object's name).</summary>
        public string PromptLabel
        {
            get
            {
                var actor = Database != null && !string.IsNullOrEmpty(m_Actor) ? Database.FindActor(m_Actor) : null;
                return $"{m_PromptVerb} {(actor != null ? Database.DisplayNameOf(actor) : name)}".Trim();
            }
        }

        private void OnEnable()
        {
            if (!s_Enabled.Contains(this)) s_Enabled.Add(this);
        }

        private void OnDisable() => s_Enabled.Remove(this);

        private void Start()
        {
            if (Application.isPlaying && m_When == DialogueTriggerEvent.OnStart) Fire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Application.isPlaying && m_When == DialogueTriggerEvent.OnTriggerEnter && TagMatches(other.gameObject)) Fire();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Application.isPlaying && m_When == DialogueTriggerEvent.OnTriggerEnter && TagMatches(other.gameObject)) Fire();
        }

        private bool TagMatches(GameObject go) => string.IsNullOrEmpty(m_TriggerTag) || go.CompareTag(m_TriggerTag);

        /// <summary>
        /// Whether <see cref="Fire"/> would start the conversation now. Never logs (interactors poll it every frame);
        /// <paramref name="reason"/> says why not.
        /// </summary>
        public bool CanFire(out string reason) => Check(true, out reason);

        /// <summary><see cref="CanFire(out string)"/> without building the reason: no allocation (polled every frame).</summary>
        public bool CanFire() => Check(false, out _);

        private bool Check(bool explain, out string reason)
        {
            reason = null;
            if (m_Manager == null) { reason = "no manager"; return false; }
            if (!m_Manager.IsReady) { if (explain) reason = $"manager state={m_Manager.State}"; return false; }
            if (m_Manager.IsTalking) { reason = "already talking"; return false; }
            if (m_Once && m_Fired) { reason = "already used (once)"; return false; }
            if (string.IsNullOrEmpty(m_Conversation)) { reason = "no conversation"; return false; }
            for (int i = 0; i < m_Conditions.Count; i++)
            {
                var condition = m_Conditions[i];
                if (condition == null) continue;
                if (!condition.Evaluate(m_Manager.Context)) { if (explain) reason = $"condition failed: {condition.Describe()}"; return false; }
            }
            return true;
        }

        /// <summary>Starts the conversation if <see cref="CanFire"/>; otherwise logs why (for explicit calls).</summary>
        public bool Fire()
        {
            if (!CanFire(out string reason))
            {
                Debug.LogWarning($"{LogTag} '{name}' did not start '{m_Conversation}': {reason}.", this);
                return false;
            }
            if (!m_Manager.StartConversation(m_Conversation)) return false;
            m_Fired = true;
            return true;
        }

        /// <summary>Forgets that a once-only trigger fired (e.g. after loading a save).</summary>
        public void ResetFired() => m_Fired = false;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Dialogue Manager.", this);
            else if (Database != null && !string.IsNullOrEmpty(m_Conversation) && Database.FindConversation(m_Conversation) == null)
                Debug.LogWarning($"{LogTag} '{name}': no conversation titled '{m_Conversation}' in '{Database.name}'.", this);
            else if (Database != null && Database.FindConversation(m_Conversation) is Conversation c && c.isBark)
                Debug.LogWarning($"{LogTag} '{name}': '{m_Conversation}' is a bark conversation (said in passing by a Dialogue Barker), not one to talk through.", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (m_When != DialogueTriggerEvent.OnUse) return;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, m_UseRange);
        }
#endif
    }
}
