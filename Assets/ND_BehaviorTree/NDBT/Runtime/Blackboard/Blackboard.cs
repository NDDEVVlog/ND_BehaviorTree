
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(fileName = "New Blackboard", menuName = "ND_BehaviorTree/Blackboard")]
    public class Blackboard : ScriptableObject
    {
        public List<Key> keys = new List<Key>();

        // Name -> key lookup cache (runtime only). Rebuilt lazily when the key count changes,
        // when a cached entry was destroyed/renamed, or when a miss is confirmed by a linear scan.
        [NonSerialized] private Dictionary<string, Key> m_KeyCache;
        [NonSerialized] private int m_CachedKeyCount = -1;
        // Warning de-duplication (per blackboard instance, keyed by key name; no allocation on repeat misses).
        [NonSerialized] private HashSet<string> m_WarnedMissing;
        [NonSerialized] private HashSet<string> m_WarnedTypeMismatch;

        /// <summary>
        /// Finds a key by its logical name (Key.keyName). O(1) after the first lookup; no allocation on the hot path.
        /// When several keys share a name, the first one in <see cref="keys"/> wins.
        /// </summary>
        public bool TryGetKey(string keyName, out Key key)
        {
            key = null;
            if (keyName == null) return false;

            if (m_KeyCache == null || m_CachedKeyCount != keys.Count) RebuildKeyCache();

            if (m_KeyCache.TryGetValue(keyName, out key))
            {
                // A destroyed (Unity-null) or renamed key means the cache is stale.
                if (key != null && key.keyName == keyName) return true;
                RebuildKeyCache();
                return m_KeyCache.TryGetValue(keyName, out key) && key != null;
            }

            // Miss: confirm with a linear scan so a key replaced/renamed in place (same count) is still found.
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                if (k != null && k.keyName == keyName)
                {
                    RebuildKeyCache();
                    return m_KeyCache.TryGetValue(keyName, out key) && key != null;
                }
            }
            key = null;
            return false;
        }

        private void RebuildKeyCache()
        {
            if (m_KeyCache == null) m_KeyCache = new Dictionary<string, Key>();
            else m_KeyCache.Clear();

            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                if (k == null || k.keyName == null) continue;
                if (!m_KeyCache.ContainsKey(k.keyName)) m_KeyCache.Add(k.keyName, k);
            }
            m_CachedKeyCount = keys.Count;
        }

        private static bool FirstTime(ref HashSet<string> set, string keyName)
        {
            if (set == null) set = new HashSet<string>();
            return set.Add(keyName ?? string.Empty);
        }

        private void WarnMissing(string keyName, string suffix = "")
        {
            if (FirstTime(ref m_WarnedMissing, keyName))
                Debug.LogWarning($"Key '{keyName}' not found in Blackboard '{this.name}'{suffix}. (Logged once per key.)", this);
        }

        public T GetValue<T>(string keyName)
        {
            if (TryGetKey(keyName, out var key))
            {
                if (key is Key<T> typedKey)
                {
                    return typedKey.GetValue();
                }
                if (FirstTime(ref m_WarnedTypeMismatch, keyName))
                    Debug.LogWarning($"Key '{keyName}' found, but it is not of type {typeof(T).Name}. It is type '{key.GetValueType().Name}'. (Logged once per key.)", this);
                return default;
            }
            WarnMissing(keyName);
            return default;
        }

        public bool SetValue<T>(string keyName, T value)
        {
            if (TryGetKey(keyName, out var key))
            {
                if (key is Key<T> typedKey)
                {
                    typedKey.SetValue(value);
                    return true;
                }
                if (FirstTime(ref m_WarnedTypeMismatch, keyName))
                    Debug.LogWarning($"Key '{keyName}' found, but it cannot accept a value of type {typeof(T).Name}. It requires type '{key.GetValueType().Name}'. (Logged once per key.)", this);
                return false;
            }
            WarnMissing(keyName);
            return false;
        }

        /// <summary>
        /// Finds a key by name and sets its value using a generic object.
        /// </summary>
        /// <returns>True if the key was found and the value was set successfully.</returns>
        public bool SetValueObject(string keyName, object value)
        {
            if (TryGetKey(keyName, out var key))
            {
                // This line calls the SetValueObject method on the Key instance
                key.SetValueObject(value);
                return true;
            }
            WarnMissing(keyName, " when trying to set value");
            return false;
        }

        public Blackboard Clone()
        {
            Blackboard clone = Instantiate(this);
            clone.name = $"{this.name} (Runtime Clone)";
            clone.keys = new List<Key>();
            // Fresh caches for the new instance.
            clone.m_KeyCache = null;
            clone.m_CachedKeyCount = -1;
            clone.m_WarnedMissing = null;
            clone.m_WarnedTypeMismatch = null;

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
