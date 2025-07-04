// --- MODIFIED FILE: Blackboard.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEditor; // Keep for conditional compilation if needed
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(fileName = "New Blackboard", menuName = "BehaviourTree/Blackboard")]
    public class Blackboard : ScriptableObject
    {
        public List<Key> keys = new List<Key>();

        public T GetValue<T>(string keyName)
        {
            foreach (var key in keys)
            {
                if (key.name == keyName)
                {
                    if (key is Key<T> typedKey)
                    {
                        return typedKey.GetValue();
                    }
                    Debug.LogWarning($"Key '{keyName}' found, but it is not of type {typeof(T).Name}.");
                    return default;
                }
            }
            Debug.LogWarning($"Key '{keyName}' not found in Blackboard.");
            return default;
        }

        public bool SetValue<T>(string keyName, T value)
        {
            foreach (var key in keys)
            {
                if (key.name == keyName)
                {
                    if (key is Key<T> typedKey)
                    {
                        typedKey.SetValue(value);
                        return true;
                    }
                    Debug.LogWarning($"Key '{keyName}' found, but it cannot accept a value of type {typeof(T).Name}.");
                    return false;
                }
            }
            Debug.LogWarning($"Key '{keyName}' not found in Blackboard.");
            return false;
        }

        // --- MODIFIED: Implement Clone() method ---
        public Blackboard Clone()
        {
            Blackboard clone = Instantiate(this);
            clone.name = $"{this.name} (Runtime Clone)";
            clone.keys = new List<Key>();

            foreach (Key originalKey in keys)
            {
                // Instantiate creates a memory-clone of the ScriptableObject sub-asset
                Key keyClone = Instantiate(originalKey);
                keyClone.name = originalKey.name; // Keep the original asset name for lookups
                clone.keys.Add(keyClone);
            }
            return clone;
        }
    }
}