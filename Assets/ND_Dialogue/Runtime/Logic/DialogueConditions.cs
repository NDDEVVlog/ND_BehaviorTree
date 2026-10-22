using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    public enum CompareOp { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }

    /// <summary>Gate on an entry. All conditions of an entry must hold for it to be reachable.</summary>
    [Serializable]
    public abstract class DialogueCondition
    {
        public abstract bool Evaluate(IDialogueContext context);

        /// <summary>Adds problems (unknown names, unparsable values) to <paramref name="issues"/>, prefixed with <paramref name="where"/>.</summary>
        public virtual void Validate(IDialogueSchema schema, string where, List<string> issues) { }

        /// <summary>One-line summary for editors and logs.</summary>
        public abstract string Describe();

        public static bool Compare(VariableValue left, CompareOp op, VariableValue right, out bool result)
        {
            result = false;
            if (op == CompareOp.Equal) { result = left.Equals(right); return true; }
            if (op == CompareOp.NotEqual) { result = !left.Equals(right); return true; }
            if (!left.IsNumber || !right.IsNumber) return false; // ordering is only defined for numbers
            double a = left.AsNumber, b = right.AsNumber;
            switch (op)
            {
                case CompareOp.Less: result = a < b; break;
                case CompareOp.LessOrEqual: result = a <= b; break;
                case CompareOp.Greater: result = a > b; break;
                default: result = a >= b; break;
            }
            return true;
        }

        public static string Symbol(CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Equal: return "==";
                case CompareOp.NotEqual: return "!=";
                case CompareOp.Less: return "<";
                case CompareOp.LessOrEqual: return "<=";
                case CompareOp.Greater: return ">";
                default: return ">=";
            }
        }
    }

    /// <summary>Compares a variable with a value written as text (parsed with the variable's type: true, 3, 2.5, word).</summary>
    [Serializable]
    public class VariableCondition : DialogueCondition
    {
        [DialogueVariable] public string variable;
        public CompareOp op = CompareOp.Equal;
        [Tooltip("Parsed with the variable's type: true, 3, 2.5, word, 1,2,3 (vector), #FF8800 (colour). Not used for Object variables.")]
        public string value = "true";
        [Tooltip("Object variables: the reference to compare with (empty = is null).")]
        public UnityEngine.Object objectValue;

        public override bool Evaluate(IDialogueContext context)
        {
            if (!context.TryGetVariable(variable, out var current)) return false;
            if (!VariableValue.Build(current.type, value, objectValue, out var expected))
            {
                Debug.LogError($"[Dialogue] Condition '{Describe()}': '{value}' is not a valid {current.type}.");
                return false;
            }
            if (!Compare(current, op, expected, out bool result))
            {
                Debug.LogError($"[Dialogue] Condition '{Describe()}': {Symbol(op)} needs numbers, '{variable}' is {current.type}.");
                return false;
            }
            return result;
        }

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (!schema.TryGetVariableType(variable, out var type)) { issues.Add($"{where}: condition uses unknown variable '{variable}'."); return; }
            if (!VariableValue.Build(type, value, objectValue, out _)) issues.Add($"{where}: condition value '{value}' is not a valid {type} for '{variable}'.");
            else if (op != CompareOp.Equal && op != CompareOp.NotEqual && type != VariableType.Int && type != VariableType.Float)
                issues.Add($"{where}: condition {Symbol(op)} on {type} variable '{variable}' (only numbers can be ordered).");
        }

        public override string Describe() => $"{variable} {Symbol(op)} {(objectValue != null ? objectValue.name : value)}";
    }

    [Serializable]
    public class QuestStateCondition : DialogueCondition
    {
        [DialogueQuest] public string quest;
        [Tooltip("Equal or NotEqual.")]
        public CompareOp op = CompareOp.Equal;
        public QuestState state = QuestState.Active;

        public override bool Evaluate(IDialogueContext context)
        {
            if (!context.TryGetQuestState(quest, out var current)) return false;
            switch (op)
            {
                case CompareOp.Equal: return current == state;
                case CompareOp.NotEqual: return current != state;
                default:
                    Debug.LogError($"[Dialogue] Condition '{Describe()}': quest states only support == and !=.");
                    return false;
            }
        }

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (!schema.HasQuest(quest)) issues.Add($"{where}: condition uses unknown quest '{quest}'.");
            if (op != CompareOp.Equal && op != CompareOp.NotEqual) issues.Add($"{where}: quest condition '{quest}' uses {Symbol(op)}; only == and != are supported.");
        }

        public override string Describe() => $"quest {quest} {Symbol(op)} {state}";
    }

    /// <summary>
    /// Whether a line was already seen (shown, or chosen by the player) — "the first time / again" without a variable.
    /// Only meaningful while a conversation runs (the runner knows what was seen; it is saved with the game).
    /// </summary>
    [Serializable]
    public class EntrySeenCondition : DialogueCondition
    {
        [Tooltip("Conversation of the entry. Empty = the conversation being played.")]
        [DialogueConversation] public string conversation;
        public int entryId;
        [Tooltip("True: holds once the entry was seen. False: holds until it is seen.")]
        public bool seen = true;

        public override bool Evaluate(IDialogueContext context)
        {
            if (!(context is IDialogueMemory memory))
            {
                Debug.LogWarning($"[EntrySeenCondition] '{Describe()}' evaluated outside a conversation (no runner memory): false.");
                return false;
            }
            int conversationId = memory.CurrentConversationId;
            if (!string.IsNullOrEmpty(conversation))
            {
                if (!(context is IDialogueConversationLookup lookup) || !lookup.TryGetConversationId(conversation, out conversationId))
                {
                    Debug.LogError($"[EntrySeenCondition] No conversation titled '{conversation}'.");
                    return false;
                }
            }
            return memory.WasSeen(conversationId, entryId) == seen;
        }

        public override string Describe() => $"{(seen ? "seen" : "not seen")} {(string.IsNullOrEmpty(conversation) ? string.Empty : conversation)}#{entryId}";
    }

    /// <summary>Lets conditions turn a conversation title into its id (the runner's context implements it).</summary>
    public interface IDialogueConversationLookup
    {
        bool TryGetConversationId(string title, out int conversationId);
    }

    /// <summary>True when at least one nested condition is true (an entry's own list is AND).</summary>
    [Serializable]
    public class AnyCondition : DialogueCondition
    {
        [SerializeReference] public List<DialogueCondition> conditions = new List<DialogueCondition>();

        public override bool Evaluate(IDialogueContext context)
        {
            foreach (var c in conditions)
                if (c != null && c.Evaluate(context)) return true;
            return false;
        }

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (conditions.Count == 0) issues.Add($"{where}: 'any of' condition is empty (always false).");
            foreach (var c in conditions)
            {
                if (c == null) issues.Add($"{where}: empty slot in 'any of' condition.");
                else c.Validate(schema, where, issues);
            }
        }

        public override string Describe()
        {
            var parts = new List<string>();
            foreach (var c in conditions) parts.Add(c != null ? c.Describe() : "<empty>");
            return "any of (" + string.Join(" | ", parts) + ")";
        }
    }
}
