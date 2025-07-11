// FILE: Blackboard.cs (THIS IS THE FIX)

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(fileName = "New Blackboard", menuName = "BehaviourTree/Blackboard")]
    public class Blackboard : ScriptableObject
    {
        public List<Key> keys = new List<Key>();

        public T GetValue<T>(string keyName)
        {
            var key = keys.FirstOrDefault(k => k.keyName == keyName);
            if (key != null)
            {
                if (key is Key<T> typedKey)
                {
                    return typedKey.GetValue();
                }
                Debug.LogWarning($"Key '{keyName}' found, but it is not of type {typeof(T).Name}. It is type '{key.GetValueType().Name}'.");
                return default;
            }
            Debug.LogWarning($"Key '{keyName}' not found in Blackboard '{this.name}'.");
            return default;
        }

        public bool SetValue<T>(string keyName, T value)
        {
            var key = keys.FirstOrDefault(k => k.keyName == keyName);
            if (key != null)
            {
                if (key is Key<T> typedKey)
                {
                    typedKey.SetValue(value);
                    return true;
                }
                Debug.LogWarning($"Key '{keyName}' found, but it cannot accept a value of type {typeof(T).Name}. It requires type '{key.GetValueType().Name}'.");
                return false;
            }
            Debug.LogWarning($"Key '{keyName}' not found in Blackboard '{this.name}'.");
            return false;
        }

        // --- THIS METHOD WAS MISSING IN YOUR PROJECT ---
        /// <summary>
        /// Finds a key by name and sets its value using a generic object.
        /// </summary>
        /// <returns>True if the key was found and the value was set successfully.</returns>
        public bool SetValueObject(string keyName, object value)
        {
            var key = keys.FirstOrDefault(k => k.keyName == keyName);
            if (key != null)
            {
                // This line calls the SetValueObject method on the Key instance
                key.SetValueObject(value);
                return true;
            }
            Debug.LogWarning($"Key '{keyName}' not found in Blackboard '{this.name}' when trying to set value.");
            return false;
        }

        public Blackboard Clone()
        {
            Blackboard clone = Instantiate(this);
            clone.name = $"{this.name} (Runtime Clone)";
            clone.keys = new List<Key>();

            foreach (Key originalKey in keys)
            {
                if (originalKey == null) continue;
                Key keyClone = Instantiate(originalKey);
                keyClone.name = originalKey.name; 
                keyClone.keyName = originalKey.keyName;
                clone.keys.Add(keyClone);
            }
            return clone;
        }
    }
}