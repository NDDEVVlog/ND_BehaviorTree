using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Localization
{
    /// <summary>
    /// Non-generic face of a table, so editors and the settings registry can list tables of any value type.
    /// Entries are addressed by a stable <c>long</c> id; the string key is a rename-able label for humans.
    /// </summary>
    public abstract class LocalizationTable : ScriptableObject
    {
        public const long InvalidId = 0;

        [Tooltip("Name used by code to look this table up through LocalizationSettings (e.g. \"Dialogue\").")]
        public string tableName;

        public abstract Type ValueType { get; }
        public abstract int Count { get; }

        public abstract bool TryGetId(string key, out long id);
        public abstract bool TryGetKey(long id, out string key);
        public abstract bool Contains(long id);
        public abstract IEnumerable<long> Ids { get; }

        public abstract long AddEntry(string key);
        public abstract bool RenameEntry(long id, string newKey);
        public abstract bool RemoveEntry(long id);
        public abstract bool HasValue(long id, string localeCode);
        public abstract bool ClearValue(long id, string localeCode);

        /// <summary>Drops lookup caches. Call after edits made through SerializedObject / Undo.</summary>
        public abstract void InvalidateCache();

        public string DisplayName => string.IsNullOrEmpty(tableName) ? name : tableName;
    }

    /// <summary>Table of per-locale values of type <typeparamref name="T"/>. All locales live in one asset.</summary>
    public abstract class LocalizationTable<T> : LocalizationTable
    {
        [Serializable]
        public struct LocaleValue
        {
            public string locale;
            public T value;
        }

        [Serializable]
        public class Entry
        {
            public long id;
            public string key;
            [TextArea] public string comment;
            public List<LocaleValue> values = new List<LocaleValue>();
        }

        [SerializeField] private List<Entry> m_Entries = new List<Entry>();
        [SerializeField] private long m_NextId = 1;

        [NonSerialized] private Dictionary<long, Entry> m_ById;
        [NonSerialized] private Dictionary<string, long> m_IdByKey;

        public IReadOnlyList<Entry> Entries => m_Entries;
        public override Type ValueType => typeof(T);
        public override int Count => m_Entries.Count;

        public override IEnumerable<long> Ids
        {
            get { foreach (var e in m_Entries) yield return e.id; }
        }

        public override bool TryGetId(string key, out long id)
        {
            EnsureCache();
            if (key != null && m_IdByKey.TryGetValue(key, out id)) return true;
            id = InvalidId;
            return false;
        }

        public override bool TryGetKey(long id, out string key)
        {
            EnsureCache();
            if (m_ById.TryGetValue(id, out var e))
            {
                key = e.key;
                return true;
            }
            key = null;
            return false;
        }

        public override bool Contains(long id)
        {
            EnsureCache();
            return m_ById.ContainsKey(id);
        }

        public bool TryGetEntry(long id, out Entry entry)
        {
            EnsureCache();
            return m_ById.TryGetValue(id, out entry);
        }

        /// <summary>Raw lookup for one locale. No fallback; use <see cref="LocalizationService"/> for fallback-aware reads.</summary>
        public bool TryGetValue(long id, string localeCode, out T value)
        {
            if (TryGetEntry(id, out var e))
            {
                int i = IndexOf(e, localeCode);
                if (i >= 0)
                {
                    value = e.values[i].value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public override bool HasValue(long id, string localeCode) =>
            TryGetEntry(id, out var e) && IndexOf(e, localeCode) >= 0;

        public override long AddEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError($"[Localization] {DisplayName}: cannot add an entry with an empty key.", this);
                return InvalidId;
            }
            if (TryGetId(key, out _))
            {
                Debug.LogError($"[Localization] {DisplayName}: key '{key}' already exists.", this);
                return InvalidId;
            }
            var e = new Entry { id = m_NextId++, key = key };
            m_Entries.Add(e);
            m_ById[e.id] = e;
            m_IdByKey[e.key] = e.id;
            return e.id;
        }

        /// <summary>Adds an entry with a known id (CSV import keeps ids stable across machines).</summary>
        public long AddEntry(string key, long id)
        {
            if (id <= InvalidId || Contains(id))
            {
                Debug.LogError($"[Localization] {DisplayName}: id {id} is invalid or already used.", this);
                return InvalidId;
            }
            long next = m_NextId;
            m_NextId = id;
            long added = AddEntry(key);
            m_NextId = Math.Max(next, id + 1);
            return added;
        }

        public override bool RenameEntry(long id, string newKey)
        {
            if (!TryGetEntry(id, out var e))
            {
                Debug.LogError($"[Localization] {DisplayName}: rename failed, id {id} not found.", this);
                return false;
            }
            if (string.IsNullOrWhiteSpace(newKey) || (TryGetId(newKey, out long other) && other != id))
            {
                Debug.LogError($"[Localization] {DisplayName}: rename failed, key '{newKey}' is empty or already used.", this);
                return false;
            }
            m_IdByKey.Remove(e.key);
            e.key = newKey;
            m_IdByKey[newKey] = id;
            return true;
        }

        public override bool RemoveEntry(long id)
        {
            if (!TryGetEntry(id, out var e)) return false;
            m_Entries.Remove(e);
            m_ById.Remove(id);
            m_IdByKey.Remove(e.key);
            return true;
        }

        public void SetValue(long id, string localeCode, T value)
        {
            if (!TryGetEntry(id, out var e))
            {
                Debug.LogError($"[Localization] {DisplayName}: SetValue failed, id {id} not found.", this);
                return;
            }
            if (string.IsNullOrEmpty(localeCode))
            {
                Debug.LogError($"[Localization] {DisplayName}: SetValue failed, empty locale code for '{e.key}'.", this);
                return;
            }
            var lv = new LocaleValue { locale = localeCode, value = value };
            int i = IndexOf(e, localeCode);
            if (i >= 0) e.values[i] = lv;
            else e.values.Add(lv);
        }

        public override bool ClearValue(long id, string localeCode)
        {
            if (!TryGetEntry(id, out var e)) return false;
            int i = IndexOf(e, localeCode);
            if (i < 0) return false;
            e.values.RemoveAt(i);
            return true;
        }

        private static int IndexOf(Entry e, string localeCode)
        {
            for (int i = 0; i < e.values.Count; i++)
                if (e.values[i].locale == localeCode) return i;
            return -1;
        }

        private void EnsureCache()
        {
            if (m_ById != null) return;
            m_ById = new Dictionary<long, Entry>(m_Entries.Count);
            m_IdByKey = new Dictionary<string, long>(m_Entries.Count);
            foreach (var e in m_Entries)
            {
                if (e == null) continue;
                if (m_ById.ContainsKey(e.id))
                    Debug.LogError($"[Localization] {DisplayName}: duplicate id {e.id} (key '{e.key}').", this);
                else
                    m_ById[e.id] = e;
                if (e.key != null && !m_IdByKey.ContainsKey(e.key)) m_IdByKey[e.key] = e.id;
            }
        }

        public override void InvalidateCache()
        {
            m_ById = null;
            m_IdByKey = null;
        }

        protected virtual void OnValidate() => InvalidateCache();
        protected virtual void OnEnable() => InvalidateCache();
    }
}
