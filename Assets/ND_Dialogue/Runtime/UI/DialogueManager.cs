using System;
using UnityEngine;
using UnityEngine.Events;

namespace ND.Dialogue
{
    public enum DialogueManagerState { NotReady, Ready }

    /// <summary>
    /// The scene's dialogue hub: one database, ONE world-state context shared by every conversation (so variables and
    /// quests persist between talks), one runner, and the view that shows it. Triggers and game code call
    /// <see cref="StartConversation(string)"/>; gameplay listens to <see cref="ConversationStarted"/> /
    /// <see cref="ConversationEnded"/> (e.g. to freeze the player).
    /// <para>Lifecycle: NotReady → Ready in Awake (synchronous: the context comes from the database). Calls before that
    /// are rejected with a logged reason.</para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("ND Dialogue/Dialogue Manager")]
    public class DialogueManager : MonoBehaviour, IDialogueDatabaseOwner
    {
        private const string LogTag = "[DialogueManager]";

        [SerializeField] private DialogueDatabase m_Database;
        [Tooltip("Shows the conversation. Empty = no view (drive the runner yourself).")]
        [SerializeField] private DialogueViewBase m_View;
        [Tooltip("Started when the scene starts (empty = none).")]
        [SerializeField] private string m_StartConversation;

        [SerializeField] private UnityEvent m_OnConversationStarted = new UnityEvent();
        [SerializeField] private UnityEvent m_OnConversationEnded = new UnityEvent();

        public DialogueManagerState State { get; private set; } = DialogueManagerState.NotReady;
        public bool IsReady => State == DialogueManagerState.Ready;
        public DialogueDatabase Database => m_Database;
        public IDialogueContext Context { get; private set; }
        public DialogueRunner Runner { get; private set; }
        public DialogueViewBase View => m_View;
        public bool IsTalking => Runner != null && Runner.IsRunning;

        /// <summary>(conversation) A conversation began.</summary>
        public event Action<Conversation> ConversationStarted;
        /// <summary>(conversation) A conversation ended (also when stopped).</summary>
        public event Action<Conversation> ConversationEnded;

        private void Awake() => Initialize();

        /// <summary>Creates the context and runner (NotReady → Ready). Called by Awake; safe to call again (no-op once ready).</summary>
        public bool Initialize()
        {
            if (IsReady) return true;
            if (m_Database == null)
            {
                Debug.LogError($"{LogTag} '{name}': no Dialogue Database assigned.", this);
                return false;
            }
            Context = m_Database.CreateContext();
            if (Context == null)
            {
                Debug.LogError($"{LogTag} '{name}': the database's schema source returned no context.", this);
                return false;
            }
            Runner = new DialogueRunner(m_Database, Context);
            Runner.ConversationEnded += OnRunnerEnded;
            State = DialogueManagerState.Ready;
            return true;
        }

        private void Start()
        {
            if (IsReady && !string.IsNullOrEmpty(m_StartConversation)) StartConversation(m_StartConversation);
        }

        private void OnDestroy()
        {
            if (Runner != null) Runner.ConversationEnded -= OnRunnerEnded;
            if (m_View != null) m_View.Detach();
        }

        /// <summary>Starts a conversation by title. False (logged) when not ready, already talking, or the title is unknown.</summary>
        public bool StartConversation(string title)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(StartConversation)}('{title}') rejected: state={State}.", this);
                return false;
            }
            var conversation = m_Database.FindConversation(title);
            if (conversation == null)
            {
                Debug.LogError($"{LogTag} {nameof(StartConversation)} rejected: no conversation titled '{title}' in '{m_Database.name}'.", this);
                return false;
            }
            return StartConversation(conversation.id);
        }

        public bool StartConversation(int conversationId, int entryId = Conversation.StartEntryId)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(StartConversation)}({conversationId}) rejected: state={State}.", this);
                return false;
            }
            if (Runner.IsRunning)
            {
                Debug.LogWarning($"{LogTag} {nameof(StartConversation)}({conversationId}) rejected: already in '{Runner.CurrentConversation?.title}'.", this);
                return false;
            }
            if (m_View != null) m_View.Attach(Runner); // before Start: the first line is shown by the view
            var conversation = m_Database.FindConversation(conversationId);
            ConversationStarted?.Invoke(conversation);
            m_OnConversationStarted.Invoke();
            if (Runner.Start(conversationId, entryId)) return true;
            // Start failed (logged by the runner): close what we opened.
            OnRunnerEnded(conversation);
            return false;
        }

        /// <summary>Ends the current conversation now.</summary>
        public void StopConversation()
        {
            if (Runner != null && Runner.IsRunning) Runner.Stop();
        }

        private void OnRunnerEnded(Conversation conversation)
        {
            if (m_View != null) m_View.Detach();
            ConversationEnded?.Invoke(conversation);
            m_OnConversationEnded.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Database == null) Debug.LogWarning($"{LogTag} '{name}': assign a Dialogue Database.", this);
            else if (!string.IsNullOrEmpty(m_StartConversation) && m_Database.FindConversation(m_StartConversation) == null)
                Debug.LogWarning($"{LogTag} '{name}': no conversation titled '{m_StartConversation}' in '{m_Database.name}'.", this);
        }
#endif
    }
}
