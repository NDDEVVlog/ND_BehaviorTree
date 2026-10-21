using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>Effect run when an entry is entered (before its line is shown).</summary>
    [Serializable]
    public abstract class DialogueAction
    {
        public abstract void Execute(IDialogueContext context);
        public virtual void Validate(IDialogueSchema schema, string where, List<string> issues) { }
        public abstract string Describe();
    }

    [Serializable]
    public class SetVariableAction : DialogueAction
    {
        public enum Mode { Set, Add }

        [DialogueVariable] public string variable;
        [Tooltip("Add: numbers (Int / Float) and vectors only.")]
        public Mode mode = Mode.Set;
        [Tooltip("Parsed with the variable's type: true, 3, 2.5, word, 1,2,3 (vector), #FF8800 (colour). Not used for Object variables.")]
        public string value = "true";
        [Tooltip("Object variables: the reference to assign (empty = null).")]
        public UnityEngine.Object objectValue;

        public override void Execute(IDialogueContext context)
        {
            if (!context.TryGetVariable(variable, out var current)) return;
            if (!VariableValue.Build(current.type, value, objectValue, out var parsed))
            {
                Debug.LogError($"[Dialogue] Action '{Describe()}': '{value}' is not a valid {current.type}.");
                return;
            }
            if (mode == Mode.Set)
            {
                context.SetVariable(variable, parsed);
                return;
            }
            switch (current.type)
            {
                case VariableType.Int: context.SetVariable(variable, VariableValue.Int(current.intValue + parsed.intValue)); break;
                case VariableType.Float: context.SetVariable(variable, VariableValue.Float(current.floatValue + parsed.floatValue)); break;
                case VariableType.Vector2:
                case VariableType.Vector3: context.SetVariable(variable, new VariableValue { type = current.type, vectorValue = current.vectorValue + parsed.vectorValue }); break;
                default: Debug.LogError($"[Dialogue] Action '{Describe()}': Add needs a number or a vector, '{variable}' is {current.type}."); break;
            }
        }

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (!schema.TryGetVariableType(variable, out var type)) { issues.Add($"{where}: action uses unknown variable '{variable}'."); return; }
            if (!VariableValue.Build(type, value, objectValue, out _)) issues.Add($"{where}: action value '{value}' is not a valid {type} for '{variable}'.");
            if (mode == Mode.Add && type != VariableType.Int && type != VariableType.Float && type != VariableType.Vector2 && type != VariableType.Vector3) issues.Add($"{where}: Add on {type} variable '{variable}'.");
        }

        public override string Describe()
        {
            string shown = objectValue != null ? objectValue.name : value;
            return mode == Mode.Set ? $"{variable} = {shown}" : $"{variable} += {shown}";
        }
    }

    [Serializable]
    public class SetQuestStateAction : DialogueAction
    {
        [DialogueQuest] public string quest;
        public QuestState state = QuestState.Active;

        public override void Execute(IDialogueContext context) => context.SetQuestState(quest, state);

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (!schema.HasQuest(quest)) issues.Add($"{where}: action uses unknown quest '{quest}'.");
        }

        public override string Describe() => $"quest {quest} → {state}";
    }

    /// <summary>Fires a named gameplay event (listen on <see cref="DialogueWorld.EventRaised"/> or your own context).</summary>
    [Serializable]
    public class RaiseEventAction : DialogueAction
    {
        [DialogueEvent] public string eventName;
        public string argument;

        public override void Execute(IDialogueContext context) => context.RaiseEvent(eventName, argument);

        public override void Validate(IDialogueSchema schema, string where, List<string> issues)
        {
            if (string.IsNullOrEmpty(eventName)) { issues.Add($"{where}: event action has no event name."); return; }
            bool declaresEvents = false;
            foreach (var name in schema.EventNames)
            {
                declaresEvents = true;
                if (name == eventName) return;
            }
            if (declaresEvents) issues.Add($"{where}: event '{eventName}' is not declared in the database's Events.");
        }

        public override string Describe() => string.IsNullOrEmpty(argument) ? $"event {eventName}" : $"event {eventName}({argument})";
    }
}
