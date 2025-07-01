// --- File: Blackboard.cs (Updated) ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New Blackboard", menuName = "AI/Blackboard")]
public class Blackboard : ScriptableObject
{
    public List<Key> keys = new List<Key>();

    // --- ADD THESE HELPER METHODS ---

    /// <summary>
    /// Gets the value of a key by its name.
    /// </summary>
    /// <typeparam name="T">The expected type of the key's value.</typeparam>
    /// <param name="keyName">The asset name of the key.</param>
    /// <returns>The value of the key, or default(T) if not found or type mismatch.</returns>
    public T GetValue<T>(string keyName)
    {
        foreach (var key in keys)
        {
            if (key.name == keyName)
            {
                // Check if the key is of the correct generic type Key<T>
                if (key is Key<T> typedKey)
                {
                    return typedKey.GetValue();
                }
                // Log an error if the name matches but the type is wrong
                Debug.LogWarning($"Key '{keyName}' found, but it is not of type {typeof(T).Name}.");
                return default;
            }
        }
        Debug.LogWarning($"Key '{keyName}' not found in Blackboard.");
        return default; // default(T) is null for classes, 0 for int, false for bool, etc.
    }

    /// <summary>
    /// Sets the value of a key by its name.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="keyName">The asset name of the key.</param>
    /// <param name="value">The new value to assign.</param>
    /// <returns>True if the key was found and value was set, otherwise false.</returns>
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
}