using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>What conditions read and actions write. Implement it to back dialogue with your own world state.</summary>
    public interface IDialogueContext
    {
        bool TryGetVariable(string variableName, out VariableValue value);
        bool SetVariable(string variableName, VariableValue value);
        bool TryGetQuestState(string questName, out QuestState state);
        bool SetQuestState(string questName, QuestState state);
        /// <summary>A named gameplay event fired by a dialogue action (open a door, give an item…).</summary>
        void RaiseEvent(string eventName, string argument);
    }

    /// <summary>
    /// Default <see cref="IDialogueContext"/>: the variables and quests declared in a <see cref="DialogueDatabase"/>,
    /// starting at their initial values. Every change raises an event, so UI, quests and AI can react (and a
    /// behavior-tree blackboard can mirror it through an adapter). Names not declared in the database are rejected.
    /// </summary>
    public class DialogueWorld : IDialogueContext
    {
        private const string LogTag = "[DialogueWorld]";

        private readonly Dictionary<string, VariableValue> m_Variables = new Dictionary<string, VariableValue>();
        private readonly Dictionary<string, QuestState> m_Quests = new Dictionary<string, QuestState>();

        /// <summary>(name, old value, new value)</summary>
        public event Action<string, VariableValue, VariableValue> VariableChanged;
        /// <summary>(quest name, old state, new state)</summary>
        public event Action<string, QuestState, QuestState> QuestStateChanged;
        /// <summary>(event name, argument)</summary>
        public event Action<string, string> EventRaised;

        public DialogueWorld(DialogueDatabase database)
        {
            if (database == null)
            {
                Debug.LogError($"{LogTag} Created without a database: no variables or quests exist.");
                return;
            }
            foreach (var v in database.variables)
                if (v != null && !string.IsNullOrEmpty(v.name)) m_Variables[v.name] = v.initialValue;
            foreach (var q in database.quests)
                if (q != null && !string.IsNullOrEmpty(q.name)) m_Quests[q.name] = q.initialState;
            // Character attributes are variables named "Actor.attribute" (they win over a same-named world variable).
            foreach (var actor in database.AllActors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.name)) continue;
                foreach (var a in actor.AttributeDefinitions)
                    if (a != null && !string.IsNullOrEmpty(a.name)) m_Variables[actor.AttributeVariable(a.name)] = a.initialValue;
            }
        }

        public IEnumerable<string> VariableNames => m_Variables.Keys;
        public IEnumerable<string> QuestNames => m_Quests.Keys;

        public bool TryGetVariable(string variableName, out VariableValue value)
        {
            if (variableName != null && m_Variables.TryGetValue(variableName, out value)) return true;
            Debug.LogError($"{LogTag} Unknown variable '{variableName}'.");
            value = default;
            return false;
        }

        /// <summary>Sets a declared variable. Int and Float convert into each other; any other type change is rejected.</summary>
        public bool SetVariable(string variableName, VariableValue value)
        {
            if (variableName == null || !m_Variables.TryGetValue(variableName, out var old))
            {
                Debug.LogError($"{LogTag} {nameof(SetVariable)} rejected: unknown variable '{variableName}'.");
                return false;
            }
            if (!TryConvert(value, old.type, out var converted))
            {
                Debug.LogError($"{LogTag} {nameof(SetVariable)} rejected: '{variableName}' is {old.type}, got {value.type} ({value}).");
                return false;
            }
            if (old.Equals(converted) && old.type == converted.type) return true;
            m_Variables[variableName] = converted;
            VariableChanged?.Invoke(variableName, old, converted);
            return true;
        }

        private static bool TryConvert(VariableValue value, VariableType target, out VariableValue result)
        {
            result = value;
            if (value.type == target) return true;
            if (target == VariableType.Int && value.type == VariableType.Float) { result = VariableValue.Int(Mathf.RoundToInt(value.floatValue)); return true; }
            if (target == VariableType.Float && value.type == VariableType.Int) { result = VariableValue.Float(value.intValue); return true; }
            return false;
        }

        public bool TryGetQuestState(string questName, out QuestState state)
        {
            if (questName != null && m_Quests.TryGetValue(questName, out state)) return true;
            Debug.LogError($"{LogTag} Unknown quest '{questName}'.");
            state = default;
            return false;
        }

        public bool SetQuestState(string questName, QuestState state)
        {
            if (questName == null || !m_Quests.TryGetValue(questName, out var old))
            {
                Debug.LogError($"{LogTag} {nameof(SetQuestState)} rejected: unknown quest '{questName}'.");
                return false;
            }
            if (old == state) return true;
            m_Quests[questName] = state;
            QuestStateChanged?.Invoke(questName, old, state);
            return true;
        }

        public void RaiseEvent(string eventName, string argument)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogError($"{LogTag} {nameof(RaiseEvent)} rejected: empty event name.");
                return;
            }
            EventRaised?.Invoke(eventName, argument);
            DialogueEvents.Raise(eventName, argument);
        }

        #region Save / load

        [Serializable]
        public class Snapshot
        {
            [Serializable] public struct Variable { public string name; public VariableValue value; }
            [Serializable] public struct Quest { public string name; public QuestState state; }

            public List<Variable> variables = new List<Variable>();
            public List<Quest> quests = new List<Quest>();
        }

        /// <summary>Current values, for saving (e.g. JsonUtility.ToJson).</summary>
        public Snapshot Capture()
        {
            var snapshot = new Snapshot();
            foreach (var kv in m_Variables) snapshot.variables.Add(new Snapshot.Variable { name = kv.Key, value = kv.Value });
            foreach (var kv in m_Quests) snapshot.quests.Add(new Snapshot.Quest { name = kv.Key, state = kv.Value });
            return snapshot;
        }

        /// <summary>
        /// Applies a saved snapshot through the normal setters, so change events fire for every value that differs.
        /// Names that no longer exist in the database (a save from an older build) are skipped with a warning.
        /// </summary>
        public void Restore(Snapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogError($"{LogTag} {nameof(Restore)} rejected: snapshot is null.");
                return;
            }
            foreach (var v in snapshot.variables)
            {
                if (!m_Variables.ContainsKey(v.name ?? string.Empty)) { Debug.LogWarning($"{LogTag} {nameof(Restore)}: saved variable '{v.name}' is no longer declared; skipped."); continue; }
                SetVariable(v.name, v.value);
            }
            foreach (var q in snapshot.quests)
            {
                if (!m_Quests.ContainsKey(q.name ?? string.Empty)) { Debug.LogWarning($"{LogTag} {nameof(Restore)}: saved quest '{q.name}' is no longer declared; skipped."); continue; }
                SetQuestState(q.name, q.state);
            }
        }

        #endregion
    }
}
