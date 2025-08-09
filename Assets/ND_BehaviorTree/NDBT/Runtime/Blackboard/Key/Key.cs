
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
    }
}