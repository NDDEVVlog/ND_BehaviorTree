using System;
using System.Collections.Generic;
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

        [Header("Save")]
        [Tooltip("Where Save/Load keep slots: JSON files in persistentDataPath, or PlayerPrefs. Code can set Storage instead.")]
        [SerializeField] private DialogueSaveLocation m_SaveLocation = DialogueSaveLocation.File;

        private IDialogueSaveStorage m_Storage;

        /// <summary>Where slots are kept (from the Save Location field unless code sets another, e.g. your game's save system).</summary>
        public IDialogueSaveStorage Storage
        {
            get => m_Storage ??= m_SaveLocation == DialogueSaveLocation.PlayerPrefs ? new PlayerPrefsSaveStorage() : (IDialogueSaveStorage)new FileSaveStorage();
            set => m_Storage = value;
        }

        /// <summary>(slot) After a successful Save.</summary>
        public event Action<string> Saved;
        /// <summary>(slot) After a successful Load (values applied, conversation resumed if one was saved).</summary>
        public event Action<string> Loaded;

        public DialogueManagerState State { get; private set; } = DialogueManagerState.NotReady;
        public bool IsReady => State == DialogueManagerState.Ready;
        public DialogueDatabase Database => m_Database;
        public IDialogueContext Context { get; private set; }
        public DialogueRunner Runner { get; private set; }
        /// <summary>The player's quest journal over this manager's world (quest log and tracker UIs read it).</summary>
        public QuestLog QuestLog { get; private set; }
        /// <summary>Says barks (<see cref="Bark"/>): same world and memory as <see cref="Runner"/>, never blocks it.</summary>
        public DialogueRunner BarkRunner { get; private set; }
        public DialogueViewBase View => m_View;
        public bool IsTalking => Runner != null && Runner.IsRunning;

        /// <summary>(conversation) A conversation began.</summary>
        public event Action<Conversation> ConversationStarted;
        /// <summary>(conversation) A conversation ended (also when stopped).</summary>
        public event Action<Conversation> ConversationEnded;
        /// <summary>(line) A line is shown (sequencers and other listeners follow the conversation here).</summary>
        public event Action<DialogueLine> LineStarted;

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
            BarkRunner = new DialogueRunner(m_Database, Context, null, Runner.Memory); // barks during a conversation too
            QuestLog = new QuestLog(m_Database, Context, Runner.Memory);
            Runner.ConversationEnded += OnRunnerEnded;
            Runner.LineStarted += OnRunnerLine;
            State = DialogueManagerState.Ready;
            return true;
        }

        private void Start()
        {
            if (IsReady && !string.IsNullOrEmpty(m_StartConversation)) StartConversation(m_StartConversation);
        }

        private void OnDestroy()
        {
            if (Runner != null)
            {
                Runner.ConversationEnded -= OnRunnerEnded;
                Runner.LineStarted -= OnRunnerLine;
            }
            QuestLog?.Dispose();
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

        #region Save / load

        /// <summary>
        /// Everything the dialogue saves (variables, quests, tracked quests, seen lines, conversation position) as of the
        /// last <see cref="CaptureSave"/>, <see cref="Save"/>, <see cref="ApplySave"/> or <see cref="Load"/>; null before
        /// the first one. Not live: call <see cref="CaptureSave"/> for the current state.
        /// </summary>
        public DialogueSaveData SaveData { get; private set; }

        /// <summary>The current state as save data (variables, quests, seen lines, the running conversation's position).</summary>
        public DialogueSaveData CaptureSave()
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(CaptureSave)} rejected: state={State}.", this);
                return null;
            }
            var data = DialogueSaveSystem.Capture(m_Database, Context, Runner.Memory, Runner);
            if (data != null && QuestLog != null) data.trackedQuests.AddRange(QuestLog.TrackedQuests);
            if (data != null) SaveData = data;
            return data;
        }

        /// <summary>
        /// Applies save data: a running conversation is stopped, values and seen lines are restored, and a conversation
        /// that was running when saved resumes at the same line (its actions are not run again).
        /// </summary>
        public bool ApplySave(DialogueSaveData data)
        {
            if (!IsReady || data == null)
            {
                Debug.LogWarning($"{LogTag} {nameof(ApplySave)} rejected: state={State}, data={(data != null)}.", this);
                return false;
            }
            StopConversation();
            if (QuestLog != null) QuestLog.Quiet = true;
            DialogueSaveSystem.Apply(m_Database, Context, Runner.Memory, data);
            if (QuestLog != null)
            {
                QuestLog.SetTracked(data.trackedQuests);
                QuestLog.Quiet = false; // no "quest started" toasts for loaded states
            }
            SaveData = data;
            if (data.position != null && data.position.active) Resume(data.position);
            return true;
        }

        public bool Save(string slot)
        {
            var data = CaptureSave();
            if (data == null) return false;
            if (!Storage.Write(slot, DialogueSaveSystem.ToJson(data))) return false; // the storage logged why
            Saved?.Invoke(slot);
            return true;
        }

        public bool Load(string slot)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(Load)}('{slot}') rejected: state={State}.", this);
                return false;
            }
            if (!Storage.TryRead(slot, out string json))
            {
                Debug.LogWarning($"{LogTag} {nameof(Load)}('{slot}') rejected: no such save.", this);
                return false;
            }
            var data = DialogueSaveSystem.FromJson(json);
            if (data == null || !ApplySave(data)) return false;
            Loaded?.Invoke(slot);
            return true;
        }

        public bool HasSave(string slot) => Storage.Exists(slot);

        public bool DeleteSave(string slot) => Storage.Delete(slot);

        private void Resume(DialoguePosition position)
        {
            var conversation = m_Database.FindConversation(position.conversationId);
            if (m_View != null) m_View.Attach(Runner);
            ConversationStarted?.Invoke(conversation);
            m_OnConversationStarted.Invoke();
            if (!Runner.Resume(position)) OnRunnerEnded(conversation); // logged by the runner
        }

        #endregion

        /// <summary>
        /// Picks and says a bark of <paramref name="conversationTitle"/> (see <see cref="DialogueRunner.Bark"/>): returns its
        /// lines for a bark UI, or an empty list when no line is valid now. Logged when not ready or the title is unknown.
        /// </summary>
        public List<DialogueLine> Bark(string conversationTitle, BarkOrder order, ref int sequence)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(Bark)}('{conversationTitle}') rejected: state={State}.", this);
                return new List<DialogueLine>();
            }
            var conversation = m_Database.FindConversation(conversationTitle);
            if (conversation == null)
            {
                Debug.LogError($"{LogTag} {nameof(Bark)} rejected: no conversation titled '{conversationTitle}' in '{m_Database.name}'.", this);
                return new List<DialogueLine>();
            }
            var lines = BarkRunner.Bark(conversation.id, order, ref sequence);
            if (lines.Count > 0) QuestLog?.Refresh(); // a bark's actions may move quests
            return lines;
        }

        /// <summary>Ends the current conversation now.</summary>
        public void StopConversation()
        {
            if (Runner != null && Runner.IsRunning) Runner.Stop();
        }

        private void OnRunnerLine(DialogueLine line) => LineStarted?.Invoke(line);

        private void OnRunnerEnded(Conversation conversation)
        {
            if (m_View != null) m_View.Detach();
            QuestLog?.Refresh(); // for worlds that cannot report changes themselves
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
