using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Key : ScriptableObject
    {
        [Tooltip("The logical name used to find this key. Must be unique within a Blackboard.")]
        public string keyName = string.Empty;
        public abstract object GetValueObject();

        // --- THESE TWO METHODS MUST EXIST ---
        public abstract void SetValueObject(object value);
        public abstract System.Type GetValueType();

        /// <summary>Raised after the value changed (not when set to an equal value).</summary>
        public event Action<Key> Changed;

        /// <summary>The blackboard this key belongs to (set by the blackboard; used to raise <see cref="Blackboard.KeyChanged"/>).</summary>
        [NonSerialized] internal Blackboard owner;

        protected void RaiseChanged()
        {
            Changed?.Invoke(this);
            if (owner != null) owner.NotifyKeyChanged(this);
        }
    }
}
