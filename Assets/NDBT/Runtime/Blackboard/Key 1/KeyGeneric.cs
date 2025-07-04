// --- MODIFIED FILE: KeyGeneric.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public abstract class Key<T> : Key
    {
        [SerializeField]
        private T value;

        public event Action ValueChanged;
        
        public override object GetValueObject()
        {
            return value;
        }

        public T GetValue()
        {
            return value;
        }

        public void SetValue(T newValue)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(value, newValue))
            {
                this.value = newValue;
                ValueChanged?.Invoke();
            }
        }

        // --- NEW IMPLEMENTATIONS ---
        public override void SetValueObject(object newValue)
        {
            if (newValue is T typedValue)
            {
                SetValue(typedValue);
            }
        }

        public override System.Type GetValueType()
        {
            return typeof(T);
        }
    }
}