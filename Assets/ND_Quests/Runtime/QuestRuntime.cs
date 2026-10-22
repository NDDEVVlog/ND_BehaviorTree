using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;

namespace ND.Quests
{
    /// <summary>A quest as the log shows it: its state, progress, journal texts and steps.</summary>
    public sealed class QuestInfo
    {
        public string Name { get; internal set; }
        public string Title { get; internal set; }
        public string Summary { get; internal set; }
        public string Group { get; internal set; }
        public QuestState State { get; internal set; }
        public int Progress { get; internal set; }
        /// <summary>The counter's target (0 = no counter).</summary>
        public int Goal { get; internal set; }
        /// <summary>The definition (null for a quest known by id only, e.g. a world key without a journal).</summary>
        public QuestDefinition Definition { get; internal set; }
        public bool IsTracked { get; internal set; }
        public IReadOnlyList<QuestStepInfo> Steps { get; internal set; } = Array.Empty<QuestStepInfo>();
        /// <summary>"Kill goblins (3/20)" style suffix, empty without a counter.</summary>
        public string ProgressText => Goal > 0 ? $"{Progress}/{Goal}" : string.Empty;
    }

    public readonly struct QuestStepInfo
    {
        public readonly string Text;
        public readonly bool Done;

        public QuestStepInfo(string text, bool done) { Text = text; Done = done; }
    }

    /// <summary>
    /// The state of every quest: its <see cref="QuestState"/>, progress toward its goal and whether it is tracked —
    /// the one place that state lives. Pure C#: <see cref="QuestManager"/> holds one for a scene, a dialogue world
    /// without a manager holds its own. Definitions come from <see cref="QuestDatabase"/>s (and dialogue databases).
    /// <para>Changes raise <see cref="StateChanged"/>, <see cref="ProgressChanged"/>, <see cref="TrackedChanged"/> and
    /// <see cref="Changed"/>; <see cref="Completed"/> / <see cref="Failed"/> are the outcomes, silent while a save is
    /// applied (<see cref="Apply"/>). Step texts and ticks are evaluated when read; call <see cref="MarkDirty"/> when
    /// something they read (e.g. dialogue variables) changed.</para>
    /// </summary>
    public sealed class QuestRuntime
    {
        private const string LogTag = "[QuestRuntime]";
        private static readonly IQuestProgression s_DefaultProgression = new IncrementProgression();

        private sealed class Entry
        {
            public string Id;
            public QuestDefinition Definition;
            public string Source;
            public QuestState State;
            public int Progress;
            public bool Tracked;
            public QuestInfo Info;
        }

        private readonly Dictionary<string, Entry> m_ById = new Dictionary<string, Entry>();
        private readonly List<Entry> m_Order = new List<Entry>();
        private readonly List<QuestInfo> m_Infos = new List<QuestInfo>();
        private bool m_Dirty = true;
        private bool m_Building;

        /// <summary>(quest, from, to) A quest's state moved (also while loading: check <see cref="IsApplyingSave"/>).</summary>
        public event Action<QuestInfo, QuestState, QuestState> StateChanged;
        /// <summary>(quest) A quest was completed — not raised while a save is applied.</summary>
        public event Action<QuestInfo> Completed;
        /// <summary>(quest) A quest failed — not raised while a save is applied.</summary>
        public event Action<QuestInfo> Failed;
        /// <summary>(quest, old progress, new progress)</summary>
        public event Action<QuestInfo, int, int> ProgressChanged;
        /// <summary>(quest, tracked)</summary>
        public event Action<QuestInfo, bool> TrackedChanged;
        /// <summary>Anything shown in a quest log may have changed (state, progress, tracking, or the world steps read).</summary>
        public event Action Changed;

        /// <summary>The host system's state that step conditions read (e.g. a dialogue context); null = quests only.</summary>
        public object ConditionWorld { get; set; }

        /// <summary>True during <see cref="Apply"/>: listeners skip "quest started" toasts and rewards.</summary>
        public bool IsApplyingSave { get; private set; }

        /// <summary>Log reports that are refused as normal flow (progress for a quest that is not Active).</summary>
        public bool VerboseLogs { get; set; }

        public IEnumerable<string> Ids { get { foreach (var e in m_Order) yield return e.Id; } }
        public bool Has(string questName) => questName != null && m_ById.ContainsKey(questName);
        public QuestDefinition FindDefinition(string questName) => questName != null && m_ById.TryGetValue(questName, out var e) ? e.Definition : null;

        #region Registration

        /// <summary>
        /// Adds quests (from a <see cref="QuestDatabase"/> or a dialogue database) at their initial state. Adding the
        /// same definition again is a no-op; another definition with a known id is refused (logged).
        /// </summary>
        public void Register(IEnumerable<QuestDefinition> definitions, string source)
        {
            if (definitions == null) return;
            foreach (var definition in definitions) Register(definition, source);
        }

        public bool Register(QuestDefinition definition, string source)
        {
            if (definition == null || string.IsNullOrEmpty(definition.name))
            {
                Debug.LogError($"{LogTag} {nameof(Register)} rejected: a quest of '{source}' has no name.");
                return false;
            }
            if (m_ById.TryGetValue(definition.name, out var existing))
            {
                if (existing.Definition == definition) return true;
                if (existing.Definition == null)
                {
                    existing.Definition = definition; // a known id gets its journal
                    existing.Source = source;
                    MarkDirty();
                    return true;
                }
                Debug.LogError($"{LogTag} {nameof(Register)} rejected: quest '{definition.name}' of '{source}' is already defined by '{existing.Source}'.");
                return false;
            }
            Add(new Entry { Id = definition.name, Definition = definition, Source = source, State = definition.initialState, Tracked = definition.initialState == QuestState.Active && definition.trackWhenActive });
            return true;
        }

        /// <summary>A quest known by id only (no journal), e.g. a key of another world. No-op if it exists.</summary>
        public void RegisterId(string questName, QuestState state, string source)
        {
            if (string.IsNullOrEmpty(questName) || m_ById.ContainsKey(questName)) return;
            Add(new Entry { Id = questName, Source = source, State = state });
        }

        private void Add(Entry entry)
        {
            m_ById[entry.Id] = entry;
            m_Order.Add(entry);
            MarkDirty();
        }

        #endregion

        #region State and progress

        public bool TryGetState(string questName, out QuestState state)
        {
            state = default;
            var entry = Entry_(questName, nameof(TryGetState));
            if (entry == null) return false;
            state = entry.State;
            return true;
        }

        /// <summary>Moves a quest to <paramref name="state"/>. Becoming Active tracks it when its definition says so.</summary>
        public bool SetState(string questName, QuestState state)
        {
            var entry = Entry_(questName, nameof(SetState));
            if (entry == null) return false;
            var old = entry.State;
            if (old == state) return true;
            entry.State = state;
            if (state == QuestState.Active && entry.Definition != null && entry.Definition.trackWhenActive && !IsApplyingSave) entry.Tracked = true;
            MarkDirty();
            var info = Find(questName, true);
            StateChanged?.Invoke(info, old, state);
            if (!IsApplyingSave)
            {
                if (state == QuestState.Completed) Completed?.Invoke(info);
                else if (state == QuestState.Failed) Failed?.Invoke(info);
            }
            return true;
        }

        public bool TryGetProgress(string questName, out int progress)
        {
            progress = 0;
            var entry = Entry_(questName, nameof(TryGetProgress));
            if (entry == null) return false;
            progress = entry.Progress;
            return true;
        }

        /// <summary>
        /// Reports <paramref name="value"/> to an Active quest (its progression decides: add it, keep the highest…).
        /// Reaching the goal completes the quest when its definition says so. False when the quest is not Active or
        /// the report does not count — normal for gameplay that reports to every quest it may concern.
        /// </summary>
        public bool ReportProgress(string questName, int value = 1)
        {
            var entry = Entry_(questName, nameof(ReportProgress));
            if (entry == null) return false;
            if (entry.State != QuestState.Active)
            {
                if (VerboseLogs) Debug.Log($"{LogTag} {nameof(ReportProgress)}('{questName}', {value}) ignored: state={entry.State}.");
                return false;
            }
            var progression = entry.Definition?.progression ?? s_DefaultProgression;
            if (!progression.TryUpdate(entry.Progress, value, out int updated))
            {
                if (VerboseLogs) Debug.Log($"{LogTag} {nameof(ReportProgress)}('{questName}', {value}) did not count ({progression.GetType().Name}, progress {entry.Progress}).");
                return false;
            }
            int goal = entry.Definition?.goal ?? 0;
            SetProgressInternal(entry, goal > 0 ? Mathf.Min(updated, goal) : updated);
            if (goal > 0 && entry.Progress >= goal && entry.Definition.completeAtGoal) SetState(questName, QuestState.Completed);
            return true;
        }

        /// <summary>Sets the progress directly (debugging, loading). Does not complete the quest.</summary>
        public bool SetProgress(string questName, int progress)
        {
            var entry = Entry_(questName, nameof(SetProgress));
            if (entry == null) return false;
            SetProgressInternal(entry, Mathf.Max(0, progress));
            return true;
        }

        private void SetProgressInternal(Entry entry, int progress)
        {
            int old = entry.Progress;
            if (old == progress) return;
            entry.Progress = progress;
            MarkDirty();
            ProgressChanged?.Invoke(Find(entry.Id, true), old, progress);
        }

        #endregion

        #region Tracking

        public bool IsTracked(string questName) => questName != null && m_ById.TryGetValue(questName, out var e) && e.Tracked;

        public IEnumerable<string> TrackedQuests { get { foreach (var e in m_Order) if (e.Tracked) yield return e.Id; } }

        /// <summary>Shows / hides a quest in the on-screen tracker.</summary>
        public void Track(string questName, bool tracked)
        {
            var entry = Entry_(questName, nameof(Track));
            if (entry == null || entry.Tracked == tracked) return;
            entry.Tracked = tracked;
            MarkDirty();
            TrackedChanged?.Invoke(Find(questName, true), tracked);
        }

        /// <summary>Replaces the tracked quests (from a save). Unknown ids are skipped with a warning.</summary>
        public void SetTracked(IEnumerable<string> questNames)
        {
            var wanted = new HashSet<string>(questNames ?? Array.Empty<string>());
            foreach (var name in wanted)
                if (!m_ById.ContainsKey(name ?? string.Empty)) Debug.LogWarning($"{LogTag} {nameof(SetTracked)}: unknown quest '{name}' skipped.");
            foreach (var e in m_Order) e.Tracked = wanted.Contains(e.Id);
            MarkDirty();
        }

        #endregion

        #region Log view

        /// <summary>Every quest in registration order, with steps evaluated now.</summary>
        public IReadOnlyList<QuestInfo> Quests
        {
            get
            {
                Rebuild();
                return m_Infos;
            }
        }

        public QuestInfo Find(string questName) => Find(questName, true);

        private QuestInfo Find(string questName, bool built)
        {
            if (questName == null || !m_ById.TryGetValue(questName, out var entry)) return null;
            if (built) Rebuild();
            return entry.Info;
        }

        /// <summary>Something the steps read changed (e.g. a dialogue variable): the log rebuilds when read next. Raises <see cref="Changed"/>.</summary>
        public void MarkDirty()
        {
            m_Dirty = true;
            Changed?.Invoke();
        }

        private void Rebuild()
        {
            if (!m_Dirty || m_Building) return; // conditions may read the runtime while it rebuilds
            m_Building = true;
            try
            {
                m_Dirty = false;
                m_Infos.Clear();
                var context = new QuestConditionContext(this, ConditionWorld);
                foreach (var e in m_Order)
                {
                    var d = e.Definition;
                    var info = e.Info ?? new QuestInfo();
                    info.Name = e.Id;
                    info.Definition = d;
                    info.State = e.State;
                    info.Progress = e.Progress;
                    info.Goal = d?.goal ?? 0;
                    info.IsTracked = e.Tracked;
                    info.Title = Text(d?.title) ?? e.Id;
                    info.Summary = Text(d?.summary) ?? string.Empty;
                    info.Group = string.IsNullOrEmpty(d?.group) ? "Main" : d.group;
                    info.Steps = d == null ? Array.Empty<QuestStepInfo>() : Steps(d, e.State, context);
                    e.Info = info;
                    m_Infos.Add(info);
                }
            }
            finally
            {
                m_Building = false;
            }
        }

        private static List<QuestStepInfo> Steps(QuestDefinition definition, QuestState state, QuestConditionContext context)
        {
            var steps = new List<QuestStepInfo>();
            foreach (var step in definition.steps)
            {
                if (step == null || !AllHold(step.visibleWhen, context)) continue;
                bool done = step.doneWhen.Count == 0 ? state == QuestState.Completed : AllHold(step.doneWhen, context);
                steps.Add(new QuestStepInfo(Text(step.text) ?? string.Empty, done));
            }
            return steps;
        }

        private static bool AllHold(List<IQuestCondition> conditions, QuestConditionContext context)
        {
            foreach (var c in conditions)
                if (c != null && !c.IsMet(context)) return false;
            return true;
        }

        /// <summary>The text (plain, or the current translation); null when empty.</summary>
        private static string Text(LocalizableString text)
        {
            if (text == null || !text.HasContent) return null;
            string value = text.GetText();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        #endregion

        #region Save

        public QuestSaveData Capture()
        {
            var data = new QuestSaveData();
            foreach (var e in m_Order)
            {
                data.quests.Add(new QuestSaveData.Quest { name = e.Id, state = e.State, progress = e.Progress });
                if (e.Tracked) data.tracked.Add(e.Id);
            }
            return data;
        }

        /// <summary>
        /// Puts states, progress and tracking back. Raises <see cref="StateChanged"/> with <see cref="IsApplyingSave"/>
        /// set (no toasts, no <see cref="Completed"/> / <see cref="Failed"/>). Quests the save does not name keep their
        /// state; names that no longer exist are skipped with a warning.
        /// </summary>
        public bool Apply(QuestSaveData data)
        {
            if (data == null)
            {
                Debug.LogError($"{LogTag} {nameof(Apply)} rejected: no data.");
                return false;
            }
            IsApplyingSave = true;
            try
            {
                foreach (var q in data.quests)
                {
                    if (!Has(q.name)) { Debug.LogWarning($"{LogTag} {nameof(Apply)}: saved quest '{q.name}' no longer exists; skipped."); continue; }
                    SetState(q.name, q.state);
                    SetProgress(q.name, q.progress);
                }
                SetTracked(data.tracked);
            }
            finally
            {
                IsApplyingSave = false;
            }
            return true;
        }

        #endregion

        private Entry Entry_(string questName, string caller)
        {
            if (questName != null && m_ById.TryGetValue(questName, out var entry)) return entry;
            Debug.LogError($"{LogTag} {caller}: unknown quest '{questName}'.");
            return null;
        }
    }
}
