using System;
using UnityEngine;

namespace ND.Localization
{
    /// <summary>Serializable pointer to one table entry. Non-generic base so one property drawer serves every kind.</summary>
    [Serializable]
    public abstract class LocalizedReference
    {
        [SerializeField] protected long m_EntryId;

        public long EntryId => m_EntryId;
        public abstract LocalizationTable TableBase { get; }
        public bool IsEmpty => TableBase == null || m_EntryId == LocalizationTable.InvalidId;

        public override string ToString()
        {
            if (IsEmpty) return "<empty>";
            return TableBase.TryGetKey(m_EntryId, out string key)
                ? $"{TableBase.DisplayName}/{key}"
                : $"{TableBase.DisplayName}/<missing id {m_EntryId}>";
        }
    }

    /// <summary>
    /// Reference to an entry of a <typeparamref name="TTable"/> whose resolved value is converted to <typeparamref name="TResult"/>.
    /// <para><see cref="Changed"/>: the handler is called immediately if localization is ready, then again on every
    /// locale change. Unsubscribe when the owner is disabled — the subscription is held by a static event.</para>
    /// </summary>
    [Serializable]
    public abstract class LocalizedReference<TTable, TValue, TResult> : LocalizedReference
        where TTable : LocalizationTable<TValue>
    {
        [SerializeField] private TTable m_Table;

        [NonSerialized] private Action<TResult> m_Changed;

        public TTable Table => m_Table;
        public override LocalizationTable TableBase => m_Table;

        public void SetReference(TTable table, long entryId)
        {
            m_Table = table;
            m_EntryId = entryId;
            Refresh();
        }

        public bool SetReference(TTable table, string key)
        {
            if (table == null || !table.TryGetId(key, out long id))
            {
                Debug.LogError($"[Localization] {nameof(SetReference)} failed: key '{key}' not found in '{(table != null ? table.DisplayName : "null")}'.");
                return false;
            }
            SetReference(table, id);
            return true;
        }

        public event Action<TResult> Changed
        {
            add
            {
                if (value == null) return;
                bool first = m_Changed == null;
                m_Changed += value;
                if (first) LocalizationService.LocaleChanged += OnLocaleChanged;
                if (LocalizationService.IsReady) value(Resolve());
            }
            remove
            {
                m_Changed -= value;
                if (m_Changed == null) LocalizationService.LocaleChanged -= OnLocaleChanged;
            }
        }

        /// <summary>Resolves for the selected locale. On failure returns false with the reason already logged.</summary>
        public bool TryGetValue(out TResult result)
        {
            if (IsEmpty)
            {
                Debug.LogWarning($"[Localization] {GetType().Name} is not assigned (table={(m_Table != null ? m_Table.DisplayName : "null")}, id={m_EntryId}).");
                result = default;
                return false;
            }
            if (LocalizationService.TryGetValue(m_Table, m_EntryId, out TValue raw))
                return TryConvert(raw, out result);
            result = default;
            return false;
        }

        /// <summary>Resolved value, or default (null for strings/assets) when it cannot be resolved — the reason is logged.</summary>
        public TResult Resolve() => TryGetValue(out var result) ? result : default;

        /// <summary>Re-sends the current value to <see cref="Changed"/> subscribers, e.g. after format arguments change.</summary>
        public void Refresh()
        {
            if (m_Changed != null && LocalizationService.IsReady) m_Changed(Resolve());
        }

        protected abstract bool TryConvert(TValue raw, out TResult result);

        private void OnLocaleChanged(Locale _) => m_Changed?.Invoke(Resolve());
    }
}
