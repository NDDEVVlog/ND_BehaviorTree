// --- MODIFIED FILE: Key.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Key : ScriptableObject
    {

        public string keyName = string.Empty;
        [SerializeField]
        private string category = string.Empty;

        [SerializeField]
        private string description = string.Empty;

        public abstract object GetValueObject();

        // --- NEW ABSTRACT METHODS ---
        public abstract void SetValueObject(object value);
        public abstract System.Type GetValueType();
    }
}