// FILE: KeyGeneric.cs

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

        // --- THIS IMPLEMENTATION MUST EXIST ---
        public override void SetValueObject(object newValue)
        {
            if (newValue == null)
            {
                if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null)
                {
                    SetValue((T)newValue);
                }
                else
                {
                    Debug.LogWarning($"Cannot set value of key '{keyName}' to null because its type '{typeof(T).Name}' is a non-nullable value type.");
                }
                return;
            }

            if (newValue is T typedValue)
            {
                SetValue(typedValue);
            }
            else
            {
                try
                {
                    T convertedValue = (T)Convert.ChangeType(newValue, typeof(T));
                    SetValue(convertedValue);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to set key '{keyName}'. Cannot convert type '{newValue.GetType().Name}' to '{typeof(T).Name}'. Error: {e.Message}", this);
                }
            }
        }

        public override System.Type GetValueType()
        {
            return typeof(T);
        }
    }
}