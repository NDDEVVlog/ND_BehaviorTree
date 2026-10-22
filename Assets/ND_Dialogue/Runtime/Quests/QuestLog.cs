using System;
using System.Collections.Generic;
using System.Linq;
using ND.Localization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>A quest as the log shows it: its state, journal texts and steps.</summary>
    public sealed class QuestInfo
    {
        public string Name { get; internal set; }
        public string Title { get; internal set; }
        public string Summary { get; internal set; }
        public string Group { get; internal set; }
        public QuestState State { get; internal set; }
        /// <summary>The journal entry in the database (null when the world has the quest but no journal for it).</summary>
        public QuestDefinition Definition { get; internal set; }
        public bool IsTracked { get; internal set; }
        public IReadOnlyList<QuestStepInfo> Steps { get; internal set; } = Array.Empty<QuestStepInfo>();
    }

    public readonly struct QuestStepInfo
    {
        public readonly string Text;
        public readonly bool Done;

        public QuestStepInfo(string text, bool done) { Text = text; Done = done; }
    }

    /// <summary>
    /// The player's quest journal over a dialogue world: every quest the world knows, its state, the journal texts and
    /// steps of the database (<see cref="QuestDefinition"/>), and which quests are tracked on screen.
    /// <para>It listens to the context (<see cref="IObservableDialogueContext"/>) and raises <see cref="Changed"/> after
    /// any change, and <see cref="QuestStateChanged"/> for each quest whose state moved (for "Quest started" toasts).
    /// Contexts that cannot tell call for <see cref="Refresh"/> from outside (the manager does it after each line).</para>
    /// </summary>
    public sealed class QuestLog : IDisposable
    {
        private const string LogTag = "[QuestLog]";

        private readonly DialogueDatabase m_Database;
        private readonly IDialogueContext m_Context;
        private readonly IDialogueContext m_Eval;
        private readonly IObservableDialogueContext m_Observable;
        private readonly HashSet<string> m_Tracked = new HashSet<string>();
        private readonly Dictionary<string, QuestState> m_LastStates = new Dictionary<string, QuestState>();
        private List<QuestInfo> m_Quests = new List<QuestInfo>();
        private bool m_Refreshing;

        /// <summary>After any refresh that changed something in the log.</summary>
        public event Action Changed;
        /// <summary>(quest, old state, new state) A quest's state moved.</summary>
        public event Action<QuestInfo, QuestState, QuestState> QuestStateChanged;

        /// <summary>Every quest the world knows (journal order first, then the others), refreshed.</summary>
        public IReadOnlyList<QuestInfo> Quests => m_Quests;
        public IEnumerable<string> TrackedQuests => m_Tracked;

        /// <summary>While true, refreshes raise no <see cref="QuestStateChanged"/> (e.g. while a save is applied).</summary>
        public bool Quiet { get; set; }

        /// <param name="memory">Optional: what the runner has seen, so steps can use Entry Seen conditions.</param>
        public QuestLog(DialogueDatabase database, IDialogueContext context, DialogueMemory memory = null)
        {
            m_Database = database;
            m_Context = context;
            if (database == null || context == null) Debug.LogError($"{LogTag} Created without database={(database != null)} / context={(context != null)}: the log stays empty.");
            m_Eval = memory != null && context != null ? new MemoryContext(context, memory) : context;
            m_Observable = context as IObservableDialogueContext;
            if (m_Observable != null) m_Observable.StateChanged += OnStateChanged;
            Refresh(silent: true);
        }

        public void Dispose()
        {
            if (m_Observable != null) m_Observable.StateChanged -= OnStateChanged;
        }

        public QuestInfo Find(string questName) => m_Quests.FirstOrDefault(q => q.Name == questName);

        public bool IsTracked(string questName) => m_Tracked.Contains(questName);

        /// <summary>Shows / hides a quest in the on-screen tracker.</summary>
        public void Track(string questName, bool tracked)
        {
            if (string.IsNullOrEmpty(questName)) return;
            bool changed = tracked ? m_Tracked.Add(questName) : m_Tracked.Remove(questName);
            if (changed) Refresh();
        }

        /// <summary>Replaces the tracked quests (e.g. from a save) without raising state toasts.</summary>
        public void SetTracked(IEnumerable<string> questNames)
        {
            m_Tracked.Clear();
            if (questNames != null) foreach (var n in questNames) if (!string.IsNullOrEmpty(n)) m_Tracked.Add(n);
            Refresh(silent: true);
        }

        private void OnStateChanged(string name, bool isQuest) => Refresh();

        /// <summary>
        /// Rebuilds the log from the world. Quests whose state moved raise <see cref="QuestStateChanged"/> (unless
        /// <paramref name="silent"/>, e.g. after loading), and start being tracked when they become Active and their
        /// journal says so.
        /// </summary>
        public void Refresh(bool silent = false)
        {
            if (m_Refreshing || m_Database == null || m_Context == null) return; // conditions may read the context while we rebuild
            m_Refreshing = true;
            try
            {
                var moved = new List<(QuestInfo quest, QuestState from, QuestState to)>();
                var quests = new List<QuestInfo>();
                var worldQuests = m_Database.Schema.QuestNames.Distinct().ToList();
                var ordered = m_Database.quests.Where(q => q != null && worldQuests.Contains(q.name)).Select(q => q.name)
                    .Concat(worldQuests.Where(n => m_Database.FindQuest(n) == null).OrderBy(n => n));
                foreach (var name in ordered)
                {
                    if (!m_Context.TryGetQuestState(name, out var state)) continue; // the context logged why
                    var definition = m_Database.FindQuest(name);
                    bool known = m_LastStates.TryGetValue(name, out var old);
                    bool becameActive = state == QuestState.Active && (!known || old != QuestState.Active);
                    if (becameActive && definition != null && definition.trackWhenActive) m_Tracked.Add(name);
                    var info = Build(name, state, definition);
                    if (known && old != state) moved.Add((info, old, state));
                    m_LastStates[name] = state;
                    quests.Add(info);
                }
                m_Quests = quests;
                if (!silent && !Quiet)
                    foreach (var (quest, from, to) in moved) QuestStateChanged?.Invoke(quest, from, to);
                Changed?.Invoke();
            }
            finally
            {
                m_Refreshing = false;
            }
        }

        private QuestInfo Build(string name, QuestState state, QuestDefinition definition)
        {
            var steps = new List<QuestStepInfo>();
            if (definition != null)
            {
                foreach (var step in definition.steps)
                {
                    if (step == null || !AllHold(step.visibleWhen)) continue;
                    bool done = step.doneWhen.Count == 0 ? state == QuestState.Completed : AllHold(step.doneWhen);
                    steps.Add(new QuestStepInfo(Text(step.text) ?? string.Empty, done));
                }
            }
            return new QuestInfo
            {
                Name = name,
                State = state,
                Definition = definition,
                Title = Text(definition?.title) ?? name,
                Summary = Text(definition?.summary) ?? string.Empty,
                Group = string.IsNullOrEmpty(definition?.group) ? "Main" : definition.group,
                IsTracked = m_Tracked.Contains(name),
                Steps = steps,
            };
        }

        private bool AllHold(List<DialogueCondition> conditions)
        {
            foreach (var c in conditions)
                if (c != null && !c.Evaluate(m_Eval)) return false;
            return true;
        }

        /// <summary>The text (plain, or the current translation); null when empty.</summary>
        private static string Text(LocalizableString text)
        {
            if (text == null || !text.HasContent) return null;
            string value = text.GetText();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>The context plus what the runner has seen (for Entry Seen conditions in steps).</summary>
        private sealed class MemoryContext : IDialogueContext, IDialogueMemory
        {
            private readonly IDialogueContext m_Inner;
            private readonly DialogueMemory m_Memory;
            public MemoryContext(IDialogueContext inner, DialogueMemory memory) { m_Inner = inner; m_Memory = memory; }

            public bool TryGetVariable(string variableName, out VariableValue value) => m_Inner.TryGetVariable(variableName, out value);
            public bool SetVariable(string variableName, VariableValue value) => m_Inner.SetVariable(variableName, value);
            public bool TryGetQuestState(string questName, out QuestState state) => m_Inner.TryGetQuestState(questName, out state);
            public bool SetQuestState(string questName, QuestState state) => m_Inner.SetQuestState(questName, state);
            public void RaiseEvent(string eventName, string argument) => m_Inner.RaiseEvent(eventName, argument);
            public bool WasSeen(int conversationId, int entryId) => m_Memory.WasSeen(conversationId, entryId);
            public int CurrentConversationId => 0;
        }
    }
}
