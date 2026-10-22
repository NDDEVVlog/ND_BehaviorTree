using System;
using ND_BehaviorTree;
using ND.Quests;
using UnityEngine;

namespace ND.Dialogue.NDBT
{
    /// <summary>
    /// Dialogue world state backed by an ND_BehaviorTree <see cref="Blackboard"/> — the same values agents read.
    /// <list type="bullet">
    /// <item>Variables are keys of type bool / int / float / string / Vector2 / Vector3 / Color / object, found by name (the parent chain included).</item>
    /// <item>Quests live in a <see cref="QuestRuntime"/> (a scene's QuestManager's, or its own). An int key named
    /// <c>quest.&lt;name&gt;</c> mirrors a quest's <see cref="QuestState"/> both ways, so agents read it and may set it.</item>
    /// <item>Change notifications come from <see cref="Blackboard.KeyChanged"/> on the runtime blackboard.</item>
    /// <item>Character attributes (<c>Guard.trust</c>, when a database is given): a <see cref="BlackboardActor"/>'s live in
    /// its own blackboard (<see cref="BlackboardActor.RuntimeBlackboard"/>); a plain actor's are world keys with the full
    /// name (<c>Guard.trust</c>).</item>
    /// </list>
    /// Use the shared world instance (<see cref="ForWorld"/>) so dialogue and every agent see one set of values.
    /// </summary>
    public class BlackboardDialogueContext : IDialogueContext, IDialogueSchema, IObservableDialogueContext, IDialogueQuestHost, IDisposable
    {
        private const string LogTag = "[BlackboardDialogueContext]";
        public const string DefaultQuestPrefix = "quest.";

        public Blackboard Blackboard { get; }
        public string QuestKeyPrefix { get; }
        /// <summary>Resolves character attributes (null: every variable is a world key).</summary>
        public DialogueDatabase Database { get; }

        /// <summary>(event name, argument) from <see cref="RaiseEventAction"/>.</summary>
        public event Action<string, string> EventRaised;

        private Action<string, bool> m_StateChanged;

        /// <summary>
        /// (name, is quest) A world key changed — from dialogue or from any agent writing the blackboard — or a character
        /// attribute was set by dialogue. Listens to <see cref="Blackboard.KeyChanged"/> only while someone subscribes.
        /// </summary>
        public event Action<string, bool> StateChanged
        {
            add
            {
                if (m_StateChanged == null && Blackboard != null) Blackboard.KeyChanged += OnKeyChanged;
                m_StateChanged += value;
            }
            remove
            {
                m_StateChanged -= value;
                if (m_StateChanged == null && Blackboard != null) Blackboard.KeyChanged -= OnKeyChanged;
            }
        }

        private void OnKeyChanged(Key key)
        {
            if (key == null || m_StateChanged == null) return;
            bool quest = IsQuestKeyName(key.keyName);
            m_StateChanged(quest ? key.keyName.Substring(QuestKeyPrefix.Length) : key.keyName, quest);
        }

        /// <param name="blackboard">A runtime blackboard (or an asset in editor tooling: writes would then change the asset).</param>
        /// <param name="quests">The quests to use (a scene's QuestManager); null = its own runtime, made on first use.</param>
        public BlackboardDialogueContext(Blackboard blackboard, string questKeyPrefix = DefaultQuestPrefix, DialogueDatabase database = null, QuestRuntime quests = null)
        {
            m_SharedQuests = quests;
            if (blackboard == null) Debug.LogError($"{LogTag} Created without a blackboard: every variable lookup will fail.");
            Blackboard = blackboard;
            QuestKeyPrefix = questKeyPrefix ?? string.Empty;
            Database = database;
        }

        /// <summary>Context over the play-session instance of a shared world blackboard asset.</summary>
        public static BlackboardDialogueContext ForWorld(Blackboard worldAsset, string questKeyPrefix = DefaultQuestPrefix, DialogueDatabase database = null, QuestRuntime quests = null) =>
            new BlackboardDialogueContext(SharedBlackboards.GetRuntime(worldAsset), questKeyPrefix, database, quests);

        /// <summary>Reads a bool / int / float / string / Vector2 / Vector3 / Color / object key as a <see cref="VariableValue"/>; false for other key types.</summary>
        /// <summary>The boxed value to write into <paramref name="key"/>; false when an object does not fit the key's type (e.g. a Material into a Transform key).</summary>
        private static bool TryBox(Key key, VariableValue value, out object boxed)
        {
            switch (value.type)
            {
                case VariableType.Bool: boxed = value.boolValue; return true;
                case VariableType.Int: boxed = value.intValue; return true;
                case VariableType.Float: boxed = value.floatValue; return true;
                case VariableType.Vector2: boxed = (Vector2)value.vectorValue; return true;
                case VariableType.Vector3: boxed = (Vector3)value.vectorValue; return true;
                case VariableType.Color: boxed = value.colorValue; return true;
                case VariableType.Object:
                    boxed = value.objectValue;
                    return value.objectValue == null || key.GetValueType().IsInstanceOfType(value.objectValue);
                default: boxed = value.stringValue; return true;
            }
        }

        public static bool TryReadValue(Key key, out VariableValue value)
        {
            value = default;
            if (key == null || !TryMapType(key.GetValueType(), out var type)) return false;
            // Typed keys are read without boxing (conditions poll them every frame).
            switch (key)
            {
                case Key<bool> b: value = VariableValue.Bool(b.GetValue()); return true;
                case Key<int> i: value = VariableValue.Int(i.GetValue()); return true;
                case Key<float> f: value = VariableValue.Float(f.GetValue()); return true;
                case Key<Vector2> v2: value = VariableValue.Vector2(v2.GetValue()); return true;
                case Key<Vector3> v3: value = VariableValue.Vector3(v3.GetValue()); return true;
                case Key<Color> c: value = VariableValue.Color(c.GetValue()); return true;
                case Key<string> s: value = VariableValue.String(s.GetValue()); return true;
            }
            object raw = key.GetValueObject();
            switch (type)
            {
                case VariableType.Bool: value = VariableValue.Bool((bool)raw); break;
                case VariableType.Int: value = VariableValue.Int((int)raw); break;
                case VariableType.Float: value = VariableValue.Float((float)raw); break;
                case VariableType.Vector2: value = VariableValue.Vector2((Vector2)raw); break;
                case VariableType.Vector3: value = VariableValue.Vector3((Vector3)raw); break;
                case VariableType.Color: value = VariableValue.Color((Color)raw); break;
                case VariableType.Object: value = VariableValue.Object(raw as UnityEngine.Object); break;
                default: value = VariableValue.String((string)raw); break;
            }
            return true;
        }

        #region Variables

        public static bool TryMapType(Type valueType, out VariableType type)
        {
            if (valueType == typeof(bool)) { type = VariableType.Bool; return true; }
            if (valueType == typeof(int)) { type = VariableType.Int; return true; }
            if (valueType == typeof(float)) { type = VariableType.Float; return true; }
            if (valueType == typeof(string)) { type = VariableType.String; return true; }
            if (valueType == typeof(Vector2)) { type = VariableType.Vector2; return true; }
            if (valueType == typeof(Vector3)) { type = VariableType.Vector3; return true; }
            if (valueType == typeof(Color)) { type = VariableType.Color; return true; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(valueType)) { type = VariableType.Object; return true; }
            type = default;
            return false;
        }

        // Variable name → the Blackboard Actor and attribute it names (null actor: a world key). Resolving allocates
        // (substrings, attribute lists), and conditions are polled every frame, so it is done once per name. The runtime
        // board itself is looked up each time: agents bind and unbind it.
        private readonly System.Collections.Generic.Dictionary<string, (BlackboardActor actor, string attribute)> m_AttributeOf =
            new System.Collections.Generic.Dictionary<string, (BlackboardActor actor, string attribute)>();

        private bool TryResolveBlackboardAttribute(string variableName, out BlackboardActor actor, out string attribute)
        {
            if (!m_AttributeOf.TryGetValue(variableName, out var resolved))
            {
                resolved = Database != null && Database.TryResolveAttribute(variableName, out var found, out var definition) && found is BlackboardActor blackboardActor
                    ? (blackboardActor, definition.name)
                    : (null, null);
                m_AttributeOf[variableName] = resolved;
            }
            (actor, attribute) = resolved;
            return actor != null;
        }

        private bool TryFindVariableKey(string variableName, out Key key, out VariableType type)
        {
            type = default;
            key = null;
            if (variableName == null)
            {
                Debug.LogError($"{LogTag} Variable name is null.");
                return false;
            }
            if (TryResolveBlackboardAttribute(variableName, out var blackboardActor, out var attribute))
            {
                var board = blackboardActor.RuntimeBlackboard();
                if (board == null || !board.TryGetKey(attribute, out key))
                {
                    Debug.LogError($"{LogTag} Attribute '{variableName}': character '{blackboardActor.name}' has no runtime blackboard with key '{attribute}'.");
                    return false;
                }
            }
            else if (Blackboard == null || !Blackboard.TryGetKey(variableName, out key))
            {
                Debug.LogError($"{LogTag} Unknown variable '{variableName}' (no such key on '{(Blackboard != null ? Blackboard.name : "null")}' or its parents).");
                return false;
            }
            if (!TryMapType(key.GetValueType(), out type))
            {
                Debug.LogError($"{LogTag} Key '{variableName}' holds {key.GetValueType().Name}; dialogue variables must be bool, int, float, string, Vector2, Vector3, Color or a Unity object.");
                return false;
            }
            return true;
        }

        public bool TryGetVariable(string variableName, out VariableValue value)
        {
            value = default;
            return TryFindVariableKey(variableName, out var key, out _) && TryReadValue(key, out value);
        }

        /// <summary>Writes the key (Int and Float convert into each other; any other type change is rejected).</summary>
        public bool SetVariable(string variableName, VariableValue value)
        {
            if (!TryFindVariableKey(variableName, out var key, out var type)) return false;
            object boxed;
            if (type == value.type)
            {
                if (!TryBox(key, value, out boxed))
                {
                    Debug.LogError($"{LogTag} {nameof(SetVariable)} rejected: '{variableName}' holds {key.GetValueType().Name}, got {(value.objectValue != null ? value.objectValue.GetType().Name : "?")} ({value}).");
                    return false;
                }
            }
            else if (type == VariableType.Int && value.type == VariableType.Float) boxed = Mathf.RoundToInt(value.floatValue);
            else if (type == VariableType.Float && value.type == VariableType.Int) boxed = (float)value.intValue;
            else
            {
                Debug.LogError($"{LogTag} {nameof(SetVariable)} rejected: '{variableName}' is {type}, got {value.type} ({value}).");
                return false;
            }
            key.SetValueObject(boxed);
            // A Blackboard Actor's attribute lives on its own board (not the world board we listen to): report it here.
            if (TryResolveBlackboardAttribute(variableName, out _, out _)) m_StateChanged?.Invoke(variableName, false);
            return true;
        }

        public bool TryGetVariableType(string variableName, out VariableType type)
        {
            type = default;
            return Blackboard != null && Blackboard.TryGetKey(variableName, out var key) && TryMapType(key.GetValueType(), out type);
        }

        /// <summary>Keys usable as variables (bool / int / float / string / Vector2 / Vector3 / Color / object, parents included), quest keys excluded.</summary>
        public System.Collections.Generic.IEnumerable<string> VariableNames
        {
            get
            {
                if (Blackboard == null) yield break;
                foreach (var key in Blackboard.AllKeys)
                    if (!IsQuestKeyName(key.keyName) && TryMapType(key.GetValueType(), out _)) yield return key.keyName;
            }
        }

        private bool IsQuestKeyName(string keyName) =>
            !string.IsNullOrEmpty(QuestKeyPrefix) && keyName != null && keyName.StartsWith(QuestKeyPrefix, StringComparison.Ordinal);

        #endregion

        #region Quests

        public string QuestKeyName(string questName) => QuestKeyPrefix + questName;

        private readonly QuestRuntime m_SharedQuests;
        private QuestRuntime m_Quests;
        private bool m_Mirroring;

        /// <summary>
        /// The quests (made on first use): the given runtime, else one of this context. Every <c>quest.x</c> key is a
        /// quest; a runtime that already knows it writes its state into the key, otherwise the key's value is the start
        /// state. The database's quests add their journals. From then on key and quest follow each other.
        /// </summary>
        public QuestRuntime Quests
        {
            get
            {
                if (m_Quests != null) return m_Quests;
                m_Quests = m_SharedQuests ?? new QuestRuntime();
                if (m_Quests.ConditionWorld == null) m_Quests.ConditionWorld = this;
                if (Blackboard != null)
                {
                    foreach (var key in Blackboard.AllKeys)
                    {
                        if (!(key is IntKey intKey) || !IsQuestKeyName(key.keyName)) continue;
                        string questName = key.keyName.Substring(QuestKeyPrefix.Length);
                        if (m_Quests.Has(questName)) WriteKey(intKey, m_Quests.TryGetState(questName, out var state) ? state : QuestState.Inactive);
                        else if (TryReadState(intKey, questName, out var initial)) m_Quests.RegisterId(questName, initial, Blackboard.name);
                    }
                    Blackboard.KeyChanged += OnQuestKeyChanged;
                }
                if (Database != null) m_Quests.Register(Database.AllQuests, Database.name);
                m_Quests.StateChanged += OnQuestStateChanged;
                return m_Quests;
            }
        }

        /// <summary>Stops the key ↔ quest mirroring (call it when a shared runtime outlives this context).</summary>
        public void Dispose()
        {
            if (m_Quests == null) return;
            m_Quests.StateChanged -= OnQuestStateChanged;
            if (Blackboard != null) Blackboard.KeyChanged -= OnQuestKeyChanged;
            if (m_Quests.ConditionWorld == this) m_Quests.ConditionWorld = null;
        }

        public bool TryGetQuestState(string questName, out QuestState state)
        {
            state = default;
            var quests = Quests;
            // A quest key holding no QuestState was reported when the quests were read; it is not a quest.
            if (!quests.Has(questName) && Blackboard != null && Blackboard.TryGetKey(QuestKeyName(questName), out var key) && key is IntKey) return false;
            return quests.TryGetState(questName, out state);
        }

        public bool SetQuestState(string questName, QuestState state) => Quests.SetState(questName, state);

        /// <summary>A quest moved: its key follows (agents read it).</summary>
        private void OnQuestStateChanged(QuestInfo quest, QuestState from, QuestState to)
        {
            if (m_Mirroring || Blackboard == null || !Blackboard.TryGetKey(QuestKeyName(quest.Name), out var key) || !(key is IntKey intKey)) return;
            WriteKey(intKey, to);
        }

        /// <summary>An agent wrote a quest key: the quest follows.</summary>
        private void OnQuestKeyChanged(Key key)
        {
            if (m_Mirroring || !(key is IntKey intKey) || !IsQuestKeyName(key.keyName)) return;
            string questName = key.keyName.Substring(QuestKeyPrefix.Length);
            if (!m_Quests.Has(questName) || !TryReadState(intKey, questName, out var state)) return;
            m_Mirroring = true;
            try { m_Quests.SetState(questName, state); }
            finally { m_Mirroring = false; }
        }

        private void WriteKey(IntKey key, QuestState state)
        {
            if (key.GetValue() == (int)state) return;
            m_Mirroring = true;
            try { key.SetValue((int)state); }
            finally { m_Mirroring = false; }
        }

        private bool TryReadState(IntKey key, string questName, out QuestState state)
        {
            int raw = key.GetValue();
            state = (QuestState)raw;
            if (Enum.IsDefined(typeof(QuestState), raw)) return true;
            Debug.LogError($"{LogTag} Quest '{questName}' holds {raw}, which is not a {nameof(QuestState)}.");
            return false;
        }

        public bool HasQuest(string questName) =>
            Blackboard != null && Blackboard.TryGetKey(QuestKeyName(questName), out var key) && key is IntKey;

        /// <summary>Events are declared in the database, not the blackboard.</summary>
        public System.Collections.Generic.IEnumerable<string> EventNames { get { yield break; } }

        /// <summary>Quest names from the int keys named <c>quest.&lt;name&gt;</c>.</summary>
        public System.Collections.Generic.IEnumerable<string> QuestNames
        {
            get
            {
                if (Blackboard == null) yield break;
                foreach (var key in Blackboard.AllKeys)
                    if (key is IntKey && IsQuestKeyName(key.keyName)) yield return key.keyName.Substring(QuestKeyPrefix.Length);
            }
        }

        #endregion

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
    }
}
