using ND.Quests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Plays a conversation in the editor without Play mode and reports, step by step, what a player would see and what
    /// happened underneath: lines (with speaker, emotion, entry id, line i/n), the player's replies, entries passed and
    /// their actions, variables and quests that changed, events fired. Used by the Test Dialogue chat and by the AI
    /// bridge (dlg-test-*), so both show exactly the same run.
    /// <para>Its world is separate from the scene: events are recorded, not sent to scene listeners. The world and the
    /// seen lines persist across Start calls until <see cref="ResetWorld"/>.</para>
    /// </summary>
    public sealed class DialogueTestSession
    {
        public enum ItemKind { Line, Reply, Action, Route, Variable, Quest, Event, Start, End, Voice }

        /// <summary>One thing that happened, in order.</summary>
        public sealed class Item
        {
            public ItemKind Kind;
            public string Text;
            public DialogueEntry Entry;
            public Actor Speaker;
            public ActorEmotion Emotion;
            public int LineIndex;
            public int LineCount = 1;
            /// <summary>A line's voice clip (Line and Voice items), or null.</summary>
            public AudioClip Voice;

            /// <summary>The line as the game shows it once typed: markup turned into rich text (see <see cref="LineTimeline"/>).</summary>
            public string Shown => LineTimeline.Final(Text);

            public override string ToString()
            {
                switch (Kind)
                {
                    case ItemKind.Line:
                    {
                        string emotion = Emotion != null ? $" [{Emotion.id}]" : string.Empty;
                        string part = LineCount > 1 ? $" {LineIndex + 1}/{LineCount}" : string.Empty;
                        return $"#{Entry?.id}{part} {Speaker?.name ?? "?"}{emotion}: \"{Text}\"";
                    }
                    case ItemKind.Reply: return $"#{Entry?.id} {Speaker?.name ?? "player"} (choice): \"{Text}\"";
                    default: return Text;
                }
            }
        }

        /// <summary>Raised by <see cref="ResetWorld"/> outside Play mode (a plugin drops cached runtime state, e.g. shared blackboards).</summary>
        public static event Action WorldResetting;

        private readonly List<Item> m_Transcript = new List<Item>();
        private TestContext m_Context;
        private bool m_Ended;

        public DialogueDatabase Database { get; }
        /// <summary>Texts are read in this locale (no running localization in the editor).</summary>
        public string Locale { get; set; }
        public DialogueRunner Runner { get; private set; }
        public IReadOnlyList<Item> Transcript => m_Transcript;
        /// <summary>The last error (bad conversation / entry id…), or null.</summary>
        public string Error { get; private set; }

        public DialogueTestSession(DialogueDatabase database, string locale)
        {
            Database = database;
            Locale = locale;
        }

        /// <summary>
        /// Starts <paramref name="conversation"/> (title or id) at <paramref name="entryId"/>. Returns this step's items,
        /// or null with <see cref="Error"/> set.
        /// </summary>
        public List<Item> Start(Conversation conversation, int entryId = Conversation.StartEntryId)
        {
            Error = null;
            if (Database == null) return Fail("No database.");
            if (conversation == null) return Fail("No such conversation.");
            var entry = conversation.FindEntry(entryId);
            if (entry == null) return Fail($"'{conversation.title}' has no entry #{entryId}.");
            if (!EnsureRunner()) return null;
            if (Runner.IsRunning) Runner.Stop();
            var items = new List<Item> { Add(ItemKind.Start, $"▶ {conversation.title}" + (entry.id == Conversation.StartEntryId ? string.Empty : $"  — from #{entry.id}")) };
            items.AddRange(Step(() => Runner.Start(conversation.id, entry.id)));
            return items;
        }

        public List<Item> Continue()
        {
            Error = null;
            if (Runner == null || Runner.State != DialogueRunnerState.ShowingLine) return Fail($"Nothing to continue (state: {Runner?.State.ToString() ?? "not started"}).");
            return Step(() => Runner.Continue());
        }

        public List<Item> Choose(int index)
        {
            Error = null;
            if (Runner == null || Runner.State != DialogueRunnerState.WaitingForChoice) return Fail($"No choice to make (state: {Runner?.State.ToString() ?? "not started"}).");
            if (index < 0 || index >= Runner.CurrentChoices.Count) return Fail($"Choice {index + 1} does not exist ({Runner.CurrentChoices.Count} choice(s)).");
            var choice = Runner.CurrentChoices[index];
            bool spoken = Database.runnerOptions?.showPlayerLines ?? false;
            return Step(() =>
            {
                // Shown as the player's reply unless the runner speaks chosen lines itself.
                if (!spoken) Add(ItemKind.Reply, choice.Text, choice.Entry, Database.FindActor(choice.Entry.actorId));
                Runner.Choose(index);
            });
        }

        private int m_BarkSequence;

        /// <summary>
        /// Says one bark of <paramref name="conversation"/> the way a Dialogue Barker would (a valid START line, its
        /// actions run), without a conversation. Returns the items, or null with <see cref="Error"/> set.
        /// </summary>
        public List<Item> Bark(Conversation conversation, BarkOrder order = BarkOrder.Random)
        {
            Error = null;
            if (Database == null) return Fail("No database.");
            if (conversation == null) return Fail("No such conversation.");
            if (!EnsureRunner()) return null;
            if (Runner.IsRunning) return Fail("A conversation is playing: stop it first (barks wait while talking).");
            return Step(() =>
            {
                Add(ItemKind.Start, $"💬 bark: {conversation.title}");
                var lines = Runner.Bark(conversation.id, order, ref m_BarkSequence);
                if (lines.Count == 0) Add(ItemKind.End, "— no line is valid now: the barker says nothing —");
                foreach (var line in lines) AddLine(line);
            });
        }

        public List<Item> Stop()
        {
            Error = null;
            if (Runner == null || !Runner.IsRunning) return new List<Item>();
            return Step(() => Runner.Stop());
        }

        /// <summary>New world (initial values), nothing seen, empty transcript.</summary>
        public void ResetWorld()
        {
            if (Runner != null && Runner.IsRunning) Runner.Stop();
            if (!EditorApplication.isPlaying)
            {
                try { WorldResetting?.Invoke(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
            Runner = null;
            m_Context = null;
            m_Transcript.Clear();
        }

        /// <summary>Creates the test world now (it is otherwise created by the first Start), e.g. to read or edit values first.</summary>
        public bool EnsureWorld()
        {
            Error = null;
            return Database != null && EnsureRunner();
        }

        public bool TryGetVariable(string name, out VariableValue value)
        {
            value = default;
            return m_Context != null && m_Context.Inner.TryGetVariable(name, out value);
        }

        public bool TryGetQuestState(string name, out QuestState state)
        {
            state = default;
            return m_Context != null && m_Context.Inner.TryGetQuestState(name, out state);
        }

        /// <summary>
        /// "What if": sets a variable (<c>trust</c>, text parsed with its type) or a quest (<c>quest key</c>, a state name)
        /// of the test world. The change is listed like any other; the conversation sees it from its next step.
        /// Returns the items, or null with <see cref="Error"/> set.
        /// </summary>
        public List<Item> SetValue(string name, string text)
        {
            if (!EnsureWorld()) return Error != null ? null : Fail("No database.");
            const string questPrefix = "quest ";
            if (name != null && name.StartsWith(questPrefix, StringComparison.Ordinal))
            {
                string quest = name.Substring(questPrefix.Length);
                if (!Enum.TryParse(text, true, out QuestState state) || !Enum.IsDefined(typeof(QuestState), state)) return Fail($"'{text}' is not a quest state ({string.Join(", ", Enum.GetNames(typeof(QuestState)))}).");
                if (!m_Context.Inner.TryGetQuestState(quest, out _)) return Fail($"Unknown quest '{quest}'.");
                return Step(() => m_Context.Inner.SetQuestState(quest, state));
            }
            if (!m_Context.Inner.TryGetVariable(name, out var current)) return Fail($"Unknown variable '{name}'.");
            if (!VariableValue.TryParse(current.type, text, out var value)) return Fail($"'{text}' is not a valid {current.type} for '{name}'.");
            return Step(() => m_Context.Inner.SetVariable(name, value));
        }

        /// <summary>The current choices as "1. text (#id)", empty when none.</summary>
        public List<string> ChoiceLabels() =>
            Runner == null || Runner.State != DialogueRunnerState.WaitingForChoice
                ? new List<string>()
                : Runner.CurrentChoices.Select(c => $"{c.Index + 1}. {c.Text} (#{c.Entry.id})").ToList();

        /// <summary>Values of every variable and quest of the test world.</summary>
        public Dictionary<string, string> Values()
        {
            var values = new Dictionary<string, string>();
            if (m_Context == null) return values;
            var schema = Database.Schema;
            foreach (var name in schema.VariableNames.Distinct().OrderBy(n => n))
                if (m_Context.Inner.TryGetVariable(name, out var v)) values[name] = v.ToString();
            foreach (var name in schema.QuestNames.Distinct().OrderBy(n => n))
                if (m_Context.Inner.TryGetQuestState(name, out var s)) values["quest " + name] = s.ToString();
            return values;
        }

        public static string Format(IEnumerable<Item> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                bool detail = item.Kind != ItemKind.Line && item.Kind != ItemKind.Reply && item.Kind != ItemKind.Start && item.Kind != ItemKind.End;
                sb.Append(detail ? "    " : string.Empty).Append(item).Append('\n');
            }
            return sb.ToString();
        }

        private bool EnsureRunner()
        {
            if (Runner != null) return true;
            var inner = Database.CreateContext();
            if (inner == null)
            {
                Error = "The database's world source returned no context (see Console).";
                return false;
            }
            m_Context = new TestContext(inner);
            Runner = new DialogueRunner(Database, m_Context) { TextResolver = ReadText, VoiceResolver = ReadVoice };
            // Recorded as they happen, so the transcript keeps the real order (route, actions, then the line).
            Runner.EntryEntered += OnEntryEntered;
            Runner.LineStarted += AddLine;
            Runner.ConversationEnded += _ => m_Ended = true;
            return true;
        }

        private string ReadText(ND.Localization.LocalizableString text) =>
            DialogueTextEditing.Read(text, Locale) ?? (text != null && text.IsLocalized ? $"<no {Locale} text>" : string.Empty);

        private AudioClip ReadVoice(ND.Localization.LocalizedAudioClip clip) =>
            clip.Table != null && clip.Table.TryGetValue(clip.EntryId, Locale, out UnityEngine.Object value) ? value as AudioClip : null;

        /// <summary>A line, then its voice and the &lt;event&gt;s its typing raises (the same timeline as the game's typewriter).</summary>
        private void AddLine(DialogueLine line)
        {
            m_Transcript.Add(new Item
            {
                Kind = ItemKind.Line, Text = line.Text, Entry = line.Entry, Speaker = line.Speaker, Emotion = line.Emotion,
                LineIndex = line.LineIndex, LineCount = line.LineCount, Voice = line.Voice
            });
            if (line.Voice != null)
                Add(ItemKind.Voice, $"🔊 voice: {line.Voice.name} ({line.Voice.length.ToString("0.0", CultureInfo.InvariantCulture)} s)", line.Entry).Voice = line.Voice;
            if (string.IsNullOrEmpty(line.Text) || line.Text.IndexOf('<') < 0) return;
            var timeline = LineTimeline.Build(line.Text, LineTimeline.DefaultCharactersPerSecond, line.Voice != null ? line.Voice.length : 0f);
            foreach (var e in timeline.Events)
                Add(ItemKind.Event, $"⚡ {e.Name}{(string.IsNullOrEmpty(e.Argument) ? string.Empty : $"({e.Argument})")}  — in the line, at {e.Time.ToString("0.00", CultureInfo.InvariantCulture)} s", line.Entry);
        }

        /// <summary>Runs one runner call, then lists, in order: lines shown, entries passed and their actions, changes, events, the end.</summary>
        private List<Item> Step(Action action)
        {
            int first = m_Transcript.Count;
            var before = Snapshot();
            m_Context.Events.Clear();
            m_Ended = false;
            // A new Start is not a jump from the conversation played before it.
            m_LastConversationId = Runner.IsRunning ? Runner.CurrentConversation?.id ?? -1 : -1;
            action();

            var after = Snapshot();
            foreach (var kv in after.variables)
                if (!before.variables.TryGetValue(kv.Key, out var old) || old != kv.Value)
                    Add(ItemKind.Variable, $"✎ {kv.Key}:  {old ?? "—"} → {kv.Value}");
            foreach (var kv in after.quests)
                if (!before.quests.TryGetValue(kv.Key, out var old) || old != kv.Value)
                    Add(ItemKind.Quest, $"◆ quest {kv.Key}:  {old} → {kv.Value}");
            foreach (var (name, argument) in m_Context.Events)
                Add(ItemKind.Event, $"⚡ {name}{(string.IsNullOrEmpty(argument) ? string.Empty : $"({argument})")}");
            if (m_Ended) Add(ItemKind.End, "— end of conversation —");
            return m_Transcript.GetRange(first, m_Transcript.Count - first);
        }

        private int m_LastConversationId = -1;

        /// <summary>An entry was entered: note a jump to another conversation, the route taken, and the actions it runs.</summary>
        private void OnEntryEntered(Conversation conversation, DialogueEntry entry)
        {
            if (m_LastConversationId >= 0 && conversation.id != m_LastConversationId)
                Add(ItemKind.Route, $"↗ continued in '{conversation.title}'", entry);
            m_LastConversationId = conversation.id;
            bool silent = entry.id == Conversation.StartEntryId || entry.isPassthrough;
            if (entry.isPassthrough && entry.id != Conversation.StartEntryId)
                Add(ItemKind.Route, $"↪ via #{entry.id}{(string.IsNullOrEmpty(entry.title) ? string.Empty : $" «{entry.title}»")}", entry);
            if (entry.actions.Count > 0)
                Add(ItemKind.Action, $"▸ #{entry.id}{(silent ? " (routing)" : string.Empty)} does: " + string.Join(";  ", entry.actions.Select(a => a?.Describe() ?? "(empty)")), entry);
            if (!string.IsNullOrWhiteSpace(entry.sequence))
                Add(ItemKind.Action, $"🎬 #{entry.id} sequence: " + string.Join(";  ", DialogueSequence.Parse(entry.sequence).Select(c => c.ToString())), entry);
        }

        private Item Add(ItemKind kind, string text, DialogueEntry entry = null, Actor speaker = null)
        {
            var item = new Item { Kind = kind, Text = text, Entry = entry, Speaker = speaker };
            m_Transcript.Add(item);
            return item;
        }

        private List<Item> Fail(string error)
        {
            Error = error;
            return null;
        }

        private (Dictionary<string, string> variables, Dictionary<string, QuestState> quests) Snapshot()
        {
            var variables = new Dictionary<string, string>();
            var quests = new Dictionary<string, QuestState>();
            if (m_Context == null) return (variables, quests);
            var schema = Database.Schema;
            foreach (var name in schema.VariableNames.Distinct())
                if (m_Context.Inner.TryGetVariable(name, out var value)) variables[name] = value.ToString();
            foreach (var name in schema.QuestNames.Distinct())
                if (m_Context.Inner.TryGetQuestState(name, out var state)) quests[name] = state;
            return (variables, quests);
        }

        /// <summary>The database's world, with events kept here instead of reaching the scene.</summary>
        private sealed class TestContext : IDialogueContext, IDialogueQuestHost
        {
            public readonly IDialogueContext Inner;
            public readonly List<(string name, string argument)> Events = new List<(string, string)>();

            public TestContext(IDialogueContext inner) { Inner = inner; }

            public bool TryGetVariable(string variableName, out VariableValue value) => Inner.TryGetVariable(variableName, out value);
            public bool SetVariable(string variableName, VariableValue value) => Inner.SetVariable(variableName, value);
            public bool TryGetQuestState(string questName, out QuestState state) => Inner.TryGetQuestState(questName, out state);
            public bool SetQuestState(string questName, QuestState state) => Inner.SetQuestState(questName, state);
            public void RaiseEvent(string eventName, string argument) => Events.Add((eventName, argument));
            public QuestRuntime Quests => (Inner as IDialogueQuestHost)?.Quests;
        }
    }
}
