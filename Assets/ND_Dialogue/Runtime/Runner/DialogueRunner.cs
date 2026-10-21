using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ND.Dialogue
{
    public enum DialogueRunnerState { Idle, ShowingLine, WaitingForChoice, Ended }

    /// <summary>A line to display: who says what, with which emotion.</summary>
    public readonly struct DialogueLine
    {
        public readonly Conversation Conversation;
        public readonly DialogueEntry Entry;
        public readonly Actor Speaker;
        public readonly ActorEmotion Emotion;
        public readonly string Text;

        public DialogueLine(Conversation conversation, DialogueEntry entry, Actor speaker, ActorEmotion emotion, string text)
        {
            Conversation = conversation;
            Entry = entry;
            Speaker = speaker;
            Emotion = emotion;
            Text = text;
        }
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

        /// <param name="options">Null = the database's <see cref="DialogueDatabase.runnerOptions"/>.</param>
        public DialogueRunner(DialogueDatabase database, IDialogueContext context, DialogueRunnerOptions options = null)
        {
            m_Database = database;
            m_Context = context;
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

        /// <summary>Moves past the shown line.</summary>
        public bool Continue()
        {
            if (State != DialogueRunnerState.ShowingLine)
            {
                Debug.LogWarning($"{LogTag} {nameof(Continue)} rejected: state={State}.");
                return false;
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
            foreach (var action in entry.actions)
            {
                if (action == null) { Debug.LogError($"{LogTag} '{conversation.title}' entry {entry.id}: empty action slot skipped."); continue; }
                action.Execute(m_Context);
            }

            var speaker = m_Database.FindActor(entry.actorId);
            bool silent = entry.id == Conversation.StartEntryId || entry.isPassthrough
                || (chosenByPlayer && !m_Options.showPlayerLines);
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

            var line = new DialogueLine(conversation, entry, speaker, speaker.FindEmotion(entry.emotion), ResolveText(entry.text, conversation, entry));
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

        private List<(Conversation conversation, DialogueEntry entry)> ValidLinksOfHighestPriority(Conversation conversation, DialogueEntry from)
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
                    if (AllHold(link.conditions) && AllHold(target.conditions)) result.Add((targetConversation, target));
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
                if (!condition.Evaluate(m_Context)) return false;
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
            string raw = text.GetText(); // plain text, or the translation once connected to a table
            return raw == null ? string.Empty : SubstituteVariables(raw);
        }

        /// <summary>Replaces [var=name] with the variable's current value.</summary>
        public string SubstituteVariables(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("[var=", StringComparison.Ordinal) < 0) return text;
            return s_VariableTag.Replace(text, m => m_Context.TryGetVariable(m.Groups[1].Value.Trim(), out var v) ? v.ToString() : m.Value);
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
