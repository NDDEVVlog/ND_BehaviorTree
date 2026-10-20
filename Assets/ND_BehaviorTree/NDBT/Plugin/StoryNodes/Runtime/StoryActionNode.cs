using UnityEngine;

namespace ND_BehaviorTree.Story
{
    /// <summary>
    /// Base for Story actions: sentence-style nodes ([StorySentence]) whose values are typed inline or bound to keys.
    /// Provides <see cref="Self"/> (the agent, or the GameObject in <see cref="selfKey"/>) and <see cref="Read{T}"/>.
    /// </summary>
    public abstract class StoryActionNode : ActionNode
    {
        [Tooltip("Who performs the action. Unbound = this agent (Self).")]
        [BlackboardKeyType(typeof(GameObject))] public Key selfKey;

        [System.NonSerialized] private bool m_KeyErrorLogged;

        /// <summary>The acting GameObject: the bound selfKey value, else the agent running the tree.</summary>
        protected GameObject Self
        {
            get
            {
                if (selfKey == null) return GetOwnerTreeGameObject();
                var bound = Read<GameObject>(selfKey, null);
                if (bound == null) LogKeyError($"'{selfKey.keyName}' (self) holds no GameObject.");
                return bound;
            }
        }

        /// <summary>The bound key's value when <paramref name="key"/> is set, otherwise the typed <paramref name="value"/>.</summary>
        protected T Read<T>(Key key, T value)
        {
            if (key == null) return value;
            if (blackboard != null && blackboard.TryGetKey(key.keyName, out var found) && found is Key<T> typed) return typed.GetValue();
            LogKeyError($"key '{key.keyName}' is missing from the runtime blackboard or is not {typeof(T).Name}.");
            return default;
        }

        protected void LogKeyError(string message)
        {
            if (m_KeyErrorLogged) return;
            m_KeyErrorLogged = true;
            Debug.LogError($"[{GetType().Name}] '{name}': {message} (Logged once.)", this);
        }
    }
}
