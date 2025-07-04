// --- MODIFIED FILE: KeyGeneric.cs ---

using System;
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
            // Ensure the object can be cast to type T before setting
            if (newValue is T typedValue)
            {
                SetValue(typedValue);
            }
            // Also handle cases where Unity's editor fields might not match exactly (e.g. IntField for a float key)
            else
            {
                try
                {
                    T convertedValue = (T)System.Convert.ChangeType(newValue, typeof(T));
                    SetValue(convertedValue);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to set key value. Cannot convert type '{newValue.GetType().Name}' to '{typeof(T).Name}'. Error: {e.Message}");
                }
            }
        }

        public override System.Type GetValueType()
        {
            return typeof(T);
        }
    }
}