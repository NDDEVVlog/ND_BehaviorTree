
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(fileName = "New Blackboard", menuName = "ND_BehaviorTree/Blackboard")]
    public class Blackboard : ScriptableObject
    {
        public List<Key> keys = new List<Key>();

        [Tooltip("Shared keys (world state: quests, flags, relationships…). A key not found here is looked up in the parent. " +
                 "At runtime every blackboard with the same parent asset reads and writes ONE shared instance of it " +
                 "(see SharedBlackboards), so agents, dialogue and quests see the same values.")]
        public Blackboard parent;

        private const int MaxParentDepth = 8;

        /// <summary>
        /// Raised after one of THIS blackboard's own keys changed value (keys found through <see cref="parent"/> raise it
        /// on the parent). Subscribe on the runtime instance, e.g. <c>SharedBlackboards.GetRuntime(worldAsset)</c>.
        /// </summary>
        public event Action<Key> KeyChanged;

        internal void NotifyKeyChanged(Key key) => KeyChanged?.Invoke(key);

        // Name -> key lookup cache (runtime only). Rebuilt lazily when the key count changes,
        // when a cached entry was destroyed/renamed, or when a miss is confirmed by a linear scan.
        [NonSerialized] private Dictionary<string, Key> m_KeyCache;
        [NonSerialized] private int m_CachedKeyCount = -1;
        // Warning de-duplication (per blackboard instance, keyed by key name; no allocation on repeat misses).
        [NonSerialized] private HashSet<string> m_WarnedMissing;
        [NonSerialized] private HashSet<string> m_WarnedTypeMismatch;

        /// <summary>
        /// Finds a key by its logical name (Key.keyName): this blackboard first, then the <see cref="parent"/> chain.
        /// O(1) after the first lookup; no allocation on the hot path.
        /// When several keys share a name, the first one in <see cref="keys"/> wins, and an own key hides a parent key.
        /// </summary>
        public bool TryGetKey(string keyName, out Key key)
        {
            var board = this;
            for (int depth = 0; board != null && depth < MaxParentDepth; depth++)
            {
                if (board.TryGetOwnKey(keyName, out key)) return true;
                board = board.parent;
            }
            key = null;
            return false;
        }

        /// <summary>Own keys, then parent keys whose name is not hidden by a nearer key. For editors and debug views.</summary>
        public IEnumerable<Key> AllKeys
        {
            get
            {
                var seen = new HashSet<string>();
                var board = this;
                for (int depth = 0; board != null && depth < MaxParentDepth; depth++)
                {
                    foreach (var k in board.keys)
                        if (k != null && seen.Add(k.keyName ?? string.Empty)) yield return k;
                    board = board.parent;
                }
            }
        }

        /// <summary>True when <paramref name="key"/> is one of this blackboard's keys or of its parent chain.</summary>
        public bool ContainsInChain(Key key)
        {
            if (key == null) return false;
            var board = this;
            for (int depth = 0; board != null && depth < MaxParentDepth; depth++)
            {
                if (board.keys.Contains(key)) return true;
                board = board.parent;
            }
            return false;
        }

        private bool TryGetOwnKey(string keyName, out Key key)
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
                k.owner = this;
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
            // All clones share one runtime instance of the parent asset, so world keys are written in one place.
            clone.parent = parent != null ? SharedBlackboards.GetRuntime(parent) : null;

            foreach (Key originalKey in keys)
            {
                if (originalKey == null) continue;
                Key keyClone = Instantiate(originalKey);
                keyClone.name = originalKey.name;
                keyClone.keyName = originalKey.keyName;
                keyClone.owner = clone;
                clone.keys.Add(keyClone);
            }
            return clone;
        }

        private void OnEnable()
        {
            foreach (var k in keys) if (k != null) k.owner = this;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var board = parent;
            for (int depth = 0; board != null; depth++, board = board.parent)
            {
                if (board == this || depth >= MaxParentDepth)
                {
                    Debug.LogWarning($"[Blackboard] '{name}': the parent chain loops back or is deeper than {MaxParentDepth}; lookups stop after {MaxParentDepth} levels.", this);
                    return;
                }
            }
        }
#endif
    }
}
