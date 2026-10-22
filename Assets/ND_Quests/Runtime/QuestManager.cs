using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Quests
{
    public enum QuestManagerState { NotReady, Ready }

    /// <summary>
    /// The scene's quests, for every system: dialogue sets states and reads them, gameplay reports progress, UI shows
    /// them, saves capture them — all through one <see cref="QuestRuntime"/>. Loads its <see cref="QuestDatabase"/>s
    /// in Awake (early: it runs before other scripts) and is Ready from then on.
    /// <para>Systems that come with their own definitions (a dialogue database's quests) add them with
    /// <see cref="Register"/>. Methods with a single string parameter can be called from UnityEvents.</para>
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("ND Quests/Quest Manager")]
    public class QuestManager : MonoBehaviour
    {
        private const string LogTag = "[QuestManager]";

        [SerializeField] private List<QuestDatabase> m_Databases = new List<QuestDatabase>();
        [Tooltip("Log progress reports that are refused as normal flow (quest not Active).")]
        [SerializeField] private bool m_VerboseLogs;

        public QuestManagerState State { get; private set; } = QuestManagerState.NotReady;
        public bool IsReady => State == QuestManagerState.Ready;
        public QuestRuntime Runtime { get; private set; }
        public IReadOnlyList<QuestDatabase> Databases => m_Databases;

        /// <summary>Raised once, when the manager becomes Ready (late subscribers check <see cref="IsReady"/>).</summary>
        public event Action Ready;

        private void Awake() => Initialize();

        /// <summary>Creates the runtime and loads the databases (NotReady → Ready). Safe to call again (no-op once ready).</summary>
        public bool Initialize()
        {
            if (IsReady) return true;
            Runtime = new QuestRuntime { VerboseLogs = m_VerboseLogs };
            for (int i = 0; i < m_Databases.Count; i++)
            {
                var database = m_Databases[i];
                if (database == null) { Debug.LogError($"{LogTag} '{name}': empty Quest Database slot {i}.", this); continue; }
                Runtime.Register(database.quests, database.name);
            }
            State = QuestManagerState.Ready;
            Ready?.Invoke();
            return true;
        }

        /// <summary>Adds a database's quests at runtime (e.g. one a dialogue database references).</summary>
        public bool Register(QuestDatabase database)
        {
            if (!Guard(nameof(Register)) || database == null) return false;
            Runtime.Register(database.quests, database.name);
            return true;
        }

        public QuestSaveData CaptureSave() => Guard(nameof(CaptureSave)) ? Runtime.Capture() : null;
        public bool ApplySave(QuestSaveData data) => Guard(nameof(ApplySave)) && Runtime.Apply(data);

        // ---- UnityEvent-friendly shortcuts (one string parameter)

        public void StartQuest(string questName) { if (Guard(nameof(StartQuest))) Runtime.SetState(questName, QuestState.Active); }
        public void CompleteQuest(string questName) { if (Guard(nameof(CompleteQuest))) Runtime.SetState(questName, QuestState.Completed); }
        public void FailQuest(string questName) { if (Guard(nameof(FailQuest))) Runtime.SetState(questName, QuestState.Failed); }
        /// <summary>Reports 1 to the quest (see <see cref="QuestRuntime.ReportProgress"/>).</summary>
        public void ReportProgress(string questName) { if (Guard(nameof(ReportProgress))) Runtime.ReportProgress(questName, 1); }

        private bool Guard(string caller)
        {
            if (IsReady) return true;
            Debug.LogWarning($"{LogTag} '{name}': {caller} rejected: state={State}.", this);
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Databases.Contains(null)) Debug.LogWarning($"{LogTag} '{name}': the Quest Databases list has an empty slot.", this);
        }
#endif
    }
}
