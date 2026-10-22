using System;
using System.Collections.Generic;
using ND.Quests;
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
    /// A context that tells when its state changes — from dialogue or from gameplay. Quest logs, trackers and HUDs
    /// subscribe instead of polling.
    /// </summary>
    public interface IObservableDialogueContext
    {
        /// <summary>(variable or quest name, true when it is a quest) A value changed.</summary>
        event Action<string, bool> StateChanged;
    }

    /// <summary>
    /// A context whose quests live in a <see cref="QuestRuntime"/> (progress, tracking, the quest log). Dialogue's quest
    /// actions and conditions go through it; a scene's QuestManager can provide the runtime so every system shares it.
    /// </summary>
    public interface IDialogueQuestHost
    {
        QuestRuntime Quests { get; }
    }


    /// <summary>
    /// Default <see cref="IDialogueContext"/>: the variables declared in a <see cref="DialogueDatabase"/> (and its
    /// characters' attributes), starting at their initial values, and its quests in a <see cref="QuestRuntime"/> — its
    /// own, or a scene's QuestManager's so every system shares them. Every change raises an event, so UI, quests and
    /// AI can react. Names not declared are rejected.
    /// </summary>
    public class DialogueWorld : IDialogueContext, IObservableDialogueContext, IDialogueQuestHost, IDisposable
    {
        private const string LogTag = "[DialogueWorld]";

        private readonly Dictionary<string, VariableValue> m_Variables = new Dictionary<string, VariableValue>();
        private readonly QuestRuntime m_Quests;

        /// <summary>(name, old value, new value)</summary>
        public event Action<string, VariableValue, VariableValue> VariableChanged;
        /// <summary>(quest name, old state, new state) — also for changes made outside dialogue (gameplay, a quest manager).</summary>
        public event Action<string, QuestState, QuestState> QuestStateChanged;
        /// <summary>(event name, argument)</summary>
        public event Action<string, string> EventRaised;
        /// <summary>(name, is quest) Any variable or quest changed.</summary>
        public event Action<string, bool> StateChanged;

        /// <param name="quests">The quests to use (a scene's QuestManager); null = a runtime of its own. The database's quests are added to it.</param>
        public DialogueWorld(DialogueDatabase database, QuestRuntime quests = null)
        {
            m_Quests = quests ?? new QuestRuntime();
            m_Quests.StateChanged += OnQuestStateChanged;
            if (m_Quests.ConditionWorld == null) m_Quests.ConditionWorld = this; // quest steps read these variables
            if (database == null)
            {
                Debug.LogError($"{LogTag} Created without a database: no variables or quests exist.");
                return;
            }
            foreach (var v in database.variables)
                if (v != null && !string.IsNullOrEmpty(v.name)) m_Variables[v.name] = v.initialValue;
            m_Quests.Register(database.AllQuests, database.name);
            // Character attributes are variables named "Actor.attribute" (they win over a same-named world variable).
            foreach (var actor in database.AllActors)
            {
                if (actor == null || string.IsNullOrEmpty(actor.name)) continue;
                foreach (var a in actor.AttributeDefinitions)
                    if (a != null && !string.IsNullOrEmpty(a.name)) m_Variables[actor.AttributeVariable(a.name)] = a.initialValue;
            }
        }

        public QuestRuntime Quests => m_Quests;
        public IEnumerable<string> VariableNames => m_Variables.Keys;
        public IEnumerable<string> QuestNames => m_Quests.Ids;

        /// <summary>Stops listening to the quest runtime (call it when a shared runtime outlives this world).</summary>
        public void Dispose()
        {
            m_Quests.StateChanged -= OnQuestStateChanged;
            if (m_Quests.ConditionWorld == this) m_Quests.ConditionWorld = null;
        }

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
            StateChanged?.Invoke(variableName, false);
            m_Quests.MarkDirty(); // quest steps may read it
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

        public bool TryGetQuestState(string questName, out QuestState state) => m_Quests.TryGetState(questName, out state);

        public bool SetQuestState(string questName, QuestState state) => m_Quests.SetState(questName, state);

        private void OnQuestStateChanged(QuestInfo quest, QuestState from, QuestState to)
        {
            QuestStateChanged?.Invoke(quest.Name, from, to);
            StateChanged?.Invoke(quest.Name, true);
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

            public List<Variable> variables = new List<Variable>();
            public QuestSaveData quests = new QuestSaveData();
        }

        /// <summary>Current values, for saving (e.g. JsonUtility.ToJson).</summary>
        public Snapshot Capture()
        {
            var snapshot = new Snapshot { quests = m_Quests.Capture() };
            foreach (var kv in m_Variables) snapshot.variables.Add(new Snapshot.Variable { name = kv.Key, value = kv.Value });
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
            if (snapshot.quests != null) m_Quests.Apply(snapshot.quests);
        }

        #endregion
    }
}
