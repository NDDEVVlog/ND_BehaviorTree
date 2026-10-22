using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ND.Quests;
using UnityEngine;

namespace ND.Dialogue
{
    public enum DialogueRunnerState { Idle, ShowingLine, WaitingForChoice, Ended }

    /// <summary>Which valid line a bark says: any at random, each in turn, or always the first (by link order).</summary>
    public enum BarkOrder { Random, Sequential, FirstValid }

    /// <summary>A line to display: who says what, with which emotion, and where it is in its entry (line 2 of 3…).</summary>
    public readonly struct DialogueLine
    {
        public readonly Conversation Conversation;
        public readonly DialogueEntry Entry;
        public readonly Actor Speaker;
        public readonly ActorEmotion Emotion;
        public readonly string Text;
        /// <summary>0-based index of this line in its entry.</summary>
        public readonly int LineIndex;
        /// <summary>How many lines the entry shows.</summary>
        public readonly int LineCount;
        /// <summary>The line's voice-over (null = none).</summary>
        public readonly AudioClip Voice;
        private readonly string m_SpeakerName;

        public DialogueLine(Conversation conversation, DialogueEntry entry, Actor speaker, ActorEmotion emotion, string text, int lineIndex = 0, int lineCount = 1, string speakerName = null, AudioClip voice = null)
        {
            Conversation = conversation;
            Entry = entry;
            Speaker = speaker;
            Emotion = emotion;
            Text = text;
            LineIndex = lineIndex;
            LineCount = lineCount;
            m_SpeakerName = speakerName;
            Voice = voice;
        }

        /// <summary>The speaker's name as shown in this database (its look's name, else the character's display name).</summary>
        public string SpeakerName => m_SpeakerName ?? Speaker?.ResolveDisplayName() ?? string.Empty;

        /// <summary>More lines of the same entry follow this one.</summary>
        public bool HasMoreLines => LineIndex + 1 < LineCount;
    }

    /// <summary>A player response offered in the choice menu.</summary>
    public readonly struct DialogueChoice
    {
        public readonly int Index;
        public readonly Conversation Conversation;
        public readonly DialogueEntry Entry;
        public readonly string Text;

        public DialogueChoice(int index, Conversation conversation, DialogueEntry entry, string text)
        {
            Index = index;
            Conversation = conversation;
            Entry = entry;
            Text = text;
        }
    }

    [Serializable]
    public class DialogueRunnerOptions
    {
        [Tooltip("After the player picks a choice, also show it as a spoken line.")]
        public bool showPlayerLines;
        [Tooltip("Show the choice menu even when only one response is available (off = pick it automatically).")]
        public bool alwaysShowChoiceMenu = true;
        [Tooltip("Guard against passthrough loops: entries entered without showing anything, per step.")]
        public int maxSilentSteps = 100;
    }

    /// <summary>
    /// Plays a conversation. Pure C#: the UI listens to <see cref="LineStarted"/> / <see cref="ChoicesPresented"/> /
    /// <see cref="ConversationEnded"/> and calls <see cref="Continue"/> / <see cref="Choose"/>.
    /// <para>Branching (same rules as Pixel Crushers' Dialogue System): from the current entry, links are grouped by
    /// <see cref="LinkPriority"/>; the highest group with at least one entry whose conditions hold is used. In that
    /// group, the first non-player entry wins; otherwise the player entries become the choice menu.</para>
    /// <para>Entering an entry runs its actions first. START, passthrough entries and (unless
    /// <see cref="DialogueRunnerOptions.showPlayerLines"/>) chosen player lines continue without stopping.</para>
    /// </summary>
    public class DialogueRunner
    {
        private const string LogTag = "[DialogueRunner]";
        private static readonly Regex s_VariableTag = new Regex(@"\[var=([^\]]+)\]", RegexOptions.Compiled);

        private readonly DialogueDatabase m_Database;
        private readonly IDialogueContext m_Context;
        private readonly DialogueRunnerOptions m_Options;
        private readonly List<DialogueChoice> m_Choices = new List<DialogueChoice>();

        private Conversation m_Conversation;
        private DialogueEntry m_Entry;

        public DialogueRunnerState State { get; private set; } = DialogueRunnerState.Idle;
        public bool IsRunning => State == DialogueRunnerState.ShowingLine || State == DialogueRunnerState.WaitingForChoice;
        public Conversation CurrentConversation => m_Conversation;
        public DialogueEntry CurrentEntry => m_Entry;
        public DialogueLine CurrentLine { get; private set; }
        public IReadOnlyList<DialogueChoice> CurrentChoices => m_Choices;

        public event Action<DialogueLine> LineStarted;
        public event Action<IReadOnlyList<DialogueChoice>> ChoicesPresented;
        public event Action<Conversation> ConversationEnded;
        /// <summary>(conversation, entry) An entry was entered, before its actions run — also silent ones (START, passthrough, chosen lines).</summary>
        public event Action<Conversation, DialogueEntry> EntryEntered;

        /// <summary>
        /// Optional: how texts are read (default: <c>GetText()</c>, i.e. the running localization). Tools that run
        /// dialogue without it (the editor's test chat) read the table directly for one locale.
        /// </summary>
        public Func<ND.Localization.LocalizableString, string> TextResolver { get; set; }

        /// <summary>Optional: how localized voice clips are read (default: the running localization). The editor reads the table for one locale.</summary>
        public Func<ND.Localization.LocalizedAudioClip, AudioClip> VoiceResolver { get; set; }

        /// <summary>Entries seen so far (shown, or chosen by the player). Part of a save.</summary>
        public DialogueMemory Memory { get; }

        /// <summary>What conditions and actions get: the context, plus <see cref="IDialogueMemory"/>.</summary>
        private readonly RunnerContext m_Eval;

        /// <param name="options">Null = the database's <see cref="DialogueDatabase.runnerOptions"/>.</param>
        /// <param name="memory">Null = a new, empty memory.</param>
        public DialogueRunner(DialogueDatabase database, IDialogueContext context, DialogueRunnerOptions options = null, DialogueMemory memory = null)
        {
            m_Database = database;
            m_Context = context;
            Memory = memory ?? new DialogueMemory();
            m_Eval = new RunnerContext(this);
            m_Options = options ?? (database != null ? database.runnerOptions : null) ?? new DialogueRunnerOptions();
            if (database == null) Debug.LogError($"{LogTag} Created without a database.");
            if (context == null) Debug.LogError($"{LogTag} Created without a context: conditions and actions cannot run.");
        }

        public bool Start(string conversationTitle)
        {
            var conversation = m_Database != null ? m_Database.FindConversation(conversationTitle) : null;
            if (conversation == null)
            {
                Debug.LogError($"{LogTag} {nameof(Start)} rejected: no conversation titled '{conversationTitle}'.");
                return false;
            }
            return Start(conversation.id);
        }

        /// <summary>Starts at START, or at <paramref name="entryId"/> (e.g. to resume a saved position).</summary>
        public bool Start(int conversationId, int entryId = Conversation.StartEntryId)
        {
            if (m_Database == null || m_Context == null)
            {
                Debug.LogError($"{LogTag} {nameof(Start)} rejected: database={(m_Database != null)}, context={(m_Context != null)}.");
                return false;
            }
            if (IsRunning)
            {
                Debug.LogWarning($"{LogTag} {nameof(Start)} rejected: state={State} in '{m_Conversation?.title}'. Call Stop first.");
                return false;
            }
            var conversation = m_Database.FindConversation(conversationId);
            var entry = conversation?.FindEntry(entryId);
            if (entry == null)
            {
                Debug.LogError($"{LogTag} {nameof(Start)} rejected: conversation {conversationId} / entry {entryId} not found.");
                return false;
            }
            m_Choices.Clear();
            Enter(conversation, entry, 0);
            return true;
        }

        /// <summary>Shows the entry's next line, or moves past the entry after its last line.</summary>
        public bool Continue()
        {
            if (State != DialogueRunnerState.ShowingLine)
            {
                Debug.LogWarning($"{LogTag} {nameof(Continue)} rejected: state={State}.");
                return false;
            }
            if (CurrentLine.HasMoreLines && CurrentLine.Entry == m_Entry)
            {
                ShowLine(m_Conversation, m_Entry, CurrentLine.Speaker, CurrentLine.LineIndex + 1);
                return true;
            }
            Advance(m_Conversation, m_Entry, 0);
            return true;
        }

        public bool Choose(int index)
        {
            if (State != DialogueRunnerState.WaitingForChoice)
            {
                Debug.LogWarning($"{LogTag} {nameof(Choose)} rejected: state={State}.");
                return false;
            }
            if (index < 0 || index >= m_Choices.Count)
            {
                Debug.LogWarning($"{LogTag} {nameof(Choose)} rejected: index {index}, {m_Choices.Count} choice(s).");
                return false;
            }
            var choice = m_Choices[index];
            m_Choices.Clear();
            Enter(choice.Conversation, choice.Entry, 0, chosenByPlayer: true);
            return true;
        }

        /// <summary>Where the runner is, for a save (inactive when no conversation runs).</summary>
        public DialoguePosition CapturePosition() => new DialoguePosition
        {
            active = IsRunning,
            conversationId = IsRunning ? m_Conversation.id : 0,
            entryId = IsRunning ? m_Entry.id : 0,
            lineIndex = State == DialogueRunnerState.ShowingLine ? CurrentLine.LineIndex : 0,
            waitingForChoice = State == DialogueRunnerState.WaitingForChoice,
        };

        /// <summary>
        /// Puts the runner back where a save left it WITHOUT running the entry's actions again: shows that line of the
        /// entry, or (<see cref="DialoguePosition.waitingForChoice"/>) offers the choices after it again.
        /// </summary>
        public bool Resume(DialoguePosition position)
        {
            if (m_Database == null || m_Context == null || position == null || !position.active)
            {
                Debug.LogError($"{LogTag} {nameof(Resume)} rejected: database={(m_Database != null)}, context={(m_Context != null)}, position={(position == null ? "null" : position.active ? "active" : "inactive")}.");
                return false;
            }
            if (IsRunning)
            {
                Debug.LogWarning($"{LogTag} {nameof(Resume)} rejected: state={State} in '{m_Conversation?.title}'. Call Stop first.");
                return false;
            }
            var conversation = m_Database.FindConversation(position.conversationId);
            var entry = conversation?.FindEntry(position.entryId);
            if (entry == null)
            {
                Debug.LogError($"{LogTag} {nameof(Resume)} rejected: conversation {position.conversationId} / entry {position.entryId} not found (the database changed since the save?).");
                return false;
            }
            m_Choices.Clear();
            m_Conversation = conversation;
            m_Entry = entry;
            if (position.waitingForChoice)
            {
                Advance(conversation, entry, 0);
                return IsRunning;
            }
            var speaker = m_Database.FindActor(entry.actorId);
            if (speaker == null || entry.LineCount == 0)
            {
                Debug.LogError($"{LogTag} {nameof(Resume)} rejected: entry {entry.id} of '{conversation.title}' shows no line (speaker={(speaker != null ? speaker.name : "unknown")}, lines={entry.LineCount}).");
                return false;
            }
            ShowLine(conversation, entry, speaker, Mathf.Clamp(position.lineIndex, 0, entry.LineCount - 1));
            return true;
        }

        /// <summary>The runner's context for conditions/actions: the world, plus what the runner remembers.</summary>
        private sealed class RunnerContext : IDialogueContext, IDialogueMemory, IDialogueConversationLookup, IDialogueQuestHost
        {
            private readonly DialogueRunner m_Runner;
            public RunnerContext(DialogueRunner runner) { m_Runner = runner; }

            public bool TryGetVariable(string variableName, out VariableValue value) => m_Runner.m_Context.TryGetVariable(variableName, out value);
            public bool SetVariable(string variableName, VariableValue value) => m_Runner.m_Context.SetVariable(variableName, value);
            public bool TryGetQuestState(string questName, out QuestState state) => m_Runner.m_Context.TryGetQuestState(questName, out state);
            public bool SetQuestState(string questName, QuestState state) => m_Runner.m_Context.SetQuestState(questName, state);
            public void RaiseEvent(string eventName, string argument) => m_Runner.m_Context.RaiseEvent(eventName, argument);
            public bool WasSeen(int conversationId, int entryId) => m_Runner.Memory.WasSeen(conversationId, entryId);
            public int CurrentConversationId => m_Runner.m_Conversation != null ? m_Runner.m_Conversation.id : 0;
            public QuestRuntime Quests => (m_Runner.m_Context as IDialogueQuestHost)?.Quests;

            public bool TryGetConversationId(string title, out int conversationId)
            {
                var found = m_Runner.m_Database != null ? m_Runner.m_Database.FindConversation(title) : null;
                conversationId = found != null ? found.id : 0;
                return found != null;
            }
        }

        /// <summary>
        /// A bark: one line said in passing, without starting the conversation (gameplay keeps going, nothing waits for
        /// Continue). From START's valid links (same rules as a conversation: priorities, conditions, hubs), the NPC
        /// lines are the candidates; <paramref name="order"/> picks one. Its actions run and it counts as seen. Returns
        /// its lines (all of an entry's lines, in order), or an empty list when no line is valid right now.
        /// Does not touch the conversation being played (use a separate runner for barks during a conversation).
        /// </summary>
        /// <param name="sequence">For <see cref="BarkOrder.Sequential"/>: the caller's counter, advanced on each bark.</param>
        /// <param name="avoidEntryId">For <see cref="BarkOrder.Random"/>: an entry not to pick when another one is valid (e.g. the last bark).</param>
        public List<DialogueLine> Bark(int conversationId, BarkOrder order, ref int sequence, int avoidEntryId = -1)
        {
            var lines = new List<DialogueLine>();
            if (m_Database == null || m_Context == null)
            {
                Debug.LogError($"{LogTag} {nameof(Bark)} rejected: database={(m_Database != null)}, context={(m_Context != null)}.");
                return lines;
            }
            if (IsRunning)
            {
                Debug.LogWarning($"{LogTag} {nameof(Bark)} rejected: this runner plays '{m_Conversation?.title}' (state={State}); bark with another runner.");
                return lines;
            }
            var conversation = m_Database.FindConversation(conversationId);
            var start = conversation?.StartEntry;
            if (start == null)
            {
                Debug.LogError($"{LogTag} {nameof(Bark)} rejected: conversation {conversationId} not found (or it has no START).");
                return lines;
            }
            var candidates = new List<(Conversation conversation, DialogueEntry entry)>();
            foreach (var (c, e) in ValidLinksOfHighestPriority(conversation, start))
                if (!IsPlayerResponse(e) && e.LineCount > 0) candidates.Add((c, e));
            if (candidates.Count == 0) return lines; // nothing to say right now: conditions decide, not an error

            int index;
            switch (order)
            {
                case BarkOrder.Random: 
                    if (candidates.Count > 1 && avoidEntryId >= 0) candidates.RemoveAll(c => c.entry.id == avoidEntryId && c.conversation.id == conversationId); // one match at most: one candidate stays
                    index = UnityEngine.Random.Range(0, candidates.Count);
                    break;
                case BarkOrder.Sequential: index = Mathf.Abs(sequence++) % candidates.Count; break;
                default: index = 0; break;
            }
            var (barkConversation, entry) = candidates[index];
            var speaker = m_Database.FindActor(entry.actorId);
            if (speaker == null)
            {
                Debug.LogError($"{LogTag} {nameof(Bark)} '{barkConversation.title}' entry {entry.id}: unknown actor id {entry.actorId}.");
                return lines;
            }

            m_Conversation = barkConversation;
            m_Entry = entry;
            EntryEntered?.Invoke(barkConversation, entry);
            foreach (var action in entry.actions)
            {
                if (action == null) { Debug.LogError($"{LogTag} '{barkConversation.title}' entry {entry.id}: empty action slot skipped."); continue; }
                action.Execute(m_Eval);
            }
            entry.OnEnter(m_Eval);
            Memory.MarkSeen(barkConversation.id, entry.id);
            for (int i = 0; i < entry.LineCount; i++)
                lines.Add(new DialogueLine(barkConversation, entry, speaker, m_Database.FindEmotion(speaker, entry.LineEmotion(i)),
                    ResolveText(entry.LineText(i), barkConversation, entry), i, entry.LineCount, m_Database.DisplayNameOf(speaker), entry.LineVoice(i, VoiceResolver)));
            Barked?.Invoke(lines);
            return lines;
        }

        /// <summary>(lines) A bark was said (see <see cref="Bark"/>).</summary>
        public event Action<IReadOnlyList<DialogueLine>> Barked;

        /// <summary>Ends the conversation now (raises <see cref="ConversationEnded"/> if one was running).</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            End();
        }

        private void Enter(Conversation conversation, DialogueEntry entry, int silentSteps, bool chosenByPlayer = false)
        {
            m_Conversation = conversation;
            m_Entry = entry;
            EntryEntered?.Invoke(conversation, entry);
            foreach (var action in entry.actions)
            {
                if (action == null) { Debug.LogError($"{LogTag} '{conversation.title}' entry {entry.id}: empty action slot skipped."); continue; }
                action.Execute(m_Eval);
            }
            entry.OnEnter(m_Eval);

            var speaker = m_Database.FindActor(entry.actorId);
            bool silent = entry.id == Conversation.StartEntryId || entry.isPassthrough
                || (chosenByPlayer && !m_Options.showPlayerLines);
            if (chosenByPlayer || (!silent && speaker != null)) Memory.MarkSeen(conversation.id, entry.id);
            if (silent)
            {
                Advance(conversation, entry, silentSteps + 1);
                return;
            }
            if (speaker == null)
            {
                Debug.LogError($"{LogTag} '{conversation.title}' entry {entry.id}: unknown actor id {entry.actorId}. Ending.");
                End();
                return;
            }

            ShowLine(conversation, entry, speaker, 0);
        }

        private void ShowLine(Conversation conversation, DialogueEntry entry, Actor speaker, int index)
        {
            var line = new DialogueLine(conversation, entry, speaker, m_Database.FindEmotion(speaker, entry.LineEmotion(index)),
                ResolveText(entry.LineText(index), conversation, entry), index, entry.LineCount, m_Database.DisplayNameOf(speaker), entry.LineVoice(index, VoiceResolver));
            CurrentLine = line;
            State = DialogueRunnerState.ShowingLine;
            LineStarted?.Invoke(line);
        }

        private void Advance(Conversation conversation, DialogueEntry from, int silentSteps)
        {
            if (silentSteps > m_Options.maxSilentSteps)
            {
                Debug.LogError($"{LogTag} '{conversation.title}': {silentSteps} entries in a row showed nothing (passthrough loop?). Ending.");
                End();
                return;
            }

            var valid = ValidLinksOfHighestPriority(conversation, from);
            if (valid.Count == 0)
            {
                End();
                return;
            }

            foreach (var (c, e) in valid)
            {
                if (IsPlayerResponse(e)) continue;
                Enter(c, e, silentSteps);
                return;
            }

            if (valid.Count == 1 && !m_Options.alwaysShowChoiceMenu)
            {
                Enter(valid[0].conversation, valid[0].entry, silentSteps, chosenByPlayer: true);
                return;
            }

            m_Choices.Clear();
            foreach (var (c, e) in valid)
            {
                var label = e.menuText != null && e.menuText.HasContent ? e.menuText : e.text;
                m_Choices.Add(new DialogueChoice(m_Choices.Count, c, e, ResolveText(label, c, e)));
            }
            m_Conversation = conversation;
            m_Entry = from;
            State = DialogueRunnerState.WaitingForChoice;
            ChoicesPresented?.Invoke(m_Choices);
        }

        private bool IsPlayerResponse(DialogueEntry entry)
        {
            if (entry.isPassthrough) return false;
            var actor = m_Database.FindActor(entry.actorId);
            return actor != null && actor.isPlayer;
        }

        private const int MaxHubDepth = 16;

        /// <summary>
        /// The links of the highest priority with at least one valid target. A link into a hub (if its conditions and
        /// the hub's hold) stands for the hub's own valid targets, which join that group in place of the hub.
        /// </summary>
        private List<(Conversation conversation, DialogueEntry entry)> ValidLinksOfHighestPriority(Conversation conversation, DialogueEntry from, int hubDepth = 0)
        {
            var result = new List<(Conversation, DialogueEntry)>();
            for (var priority = LinkPriority.High; priority >= LinkPriority.Low; priority--)
            {
                foreach (var link in from.links)
                {
                    if (link.priority != priority) continue;
                    var targetConversation = m_Database.FindConversation(link.conversationId);
                    var target = targetConversation?.FindEntry(link.entryId);
                    if (target == null)
                    {
                        Debug.LogError($"{LogTag} '{conversation.title}' entry {from.id}: link to missing conversation {link.conversationId} / entry {link.entryId}; skipped.");
                        continue;
                    }
                    if (!AllHold(link.conditions) || !AllHold(target.conditions)) continue;
                    if (!target.IsHub)
                    {
                        result.Add((targetConversation, target));
                        continue;
                    }
                    if (hubDepth >= MaxHubDepth)
                    {
                        Debug.LogError($"{LogTag} '{targetConversation.title}' hub {target.id}: more than {MaxHubDepth} hubs in a row (a hub loop?); skipped.");
                        continue;
                    }
                    foreach (var reached in ValidLinksOfHighestPriority(targetConversation, target, hubDepth + 1))
                        if (!result.Contains(reached)) result.Add(reached);
                }
                if (result.Count > 0) break;
            }
            return result;
        }

        /// <summary>True when every (non-empty) condition holds: the link's, then the target entry's.</summary>
        private bool AllHold(System.Collections.Generic.List<DialogueCondition> conditions)
        {
            if (conditions == null) return true; // links saved before link conditions existed
            foreach (var condition in conditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(m_Eval)) return false;
            }
            return true;
        }

        private string ResolveText(ND.Localization.LocalizableString text, Conversation conversation, DialogueEntry entry)
        {
            if (text == null || !text.HasContent)
            {
                Debug.LogWarning($"{LogTag} '{conversation.title}' entry {entry.id} has no text.");
                return string.Empty;
            }
            string raw = TextResolver != null ? TextResolver(text) : text.GetText(); // plain text, or the translation once connected to a table
            return raw == null ? string.Empty : SubstituteVariables(raw);
        }

        /// <summary>Replaces [var=name] with the variable's current value.</summary>
        public string SubstituteVariables(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("[var=", StringComparison.Ordinal) < 0) return text;
            return s_VariableTag.Replace(text, m => m_Eval.TryGetVariable(m.Groups[1].Value.Trim(), out var v) ? v.ToString() : m.Value);
        }

        private void End()
        {
            var ended = m_Conversation;
            m_Choices.Clear();
            State = DialogueRunnerState.Ended;
            ConversationEnded?.Invoke(ended);
        }
    }
}
