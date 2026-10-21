using System.Collections.Generic;
using UnityEngine;

namespace ND.Localization
{
    /// <summary>
    /// Project-wide configuration: available locales, the default locale, the tables reachable by name,
    /// and how the startup locale is chosen. Hand it to <see cref="LocalizationService.Initialize"/>
    /// (usually through a <see cref="LocalizationBootstrap"/> in the first scene).
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "ND Localization/Localization Settings", order = 1)]
    public class LocalizationSettings : ScriptableObject
    {
        public List<Locale> locales = new List<Locale>();

        [Tooltip("Used when no startup selector finds a locale, and as the last step of every fallback chain.")]
        public Locale defaultLocale;

        [Tooltip("Tables that code can find by name with LocalizationService.GetTable. Fields that reference a table directly do not need to be listed.")]
        public List<LocalizationTable> tables = new List<LocalizationTable>();

        [SerializeReference]
        public List<IStartupLocaleSelector> startupSelectors = new List<IStartupLocaleSelector>
        {
            new PlayerPrefsLocaleSelector(),
            new SystemLanguageLocaleSelector(),
        };

        [Tooltip("PlayerPrefs key that stores the player's choice. Empty = never remember it.")]
        public string playerPrefsKey = "nd.localization.locale";

        public Locale FindLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var locale in locales)
                if (locale != null && string.Equals(locale.code, code, System.StringComparison.OrdinalIgnoreCase))
                    return locale;
            return null;
        }

        public LocalizationTable FindTable(string tableName)
        {
            foreach (var table in tables)
                if (table != null && table.DisplayName == tableName)
                    return table;
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (defaultLocale == null)
                Debug.LogWarning($"[Localization] {name}: no default locale assigned.", this);
            else if (!locales.Contains(defaultLocale))
                Debug.LogWarning($"[Localization] {name}: default locale '{defaultLocale.code}' is not in the locale list.", this);

            var seen = new HashSet<string>();
            foreach (var locale in locales)
                if (locale != null && !seen.Add(locale.code))
                    Debug.LogWarning($"[Localization] {name}: locale code '{locale.code}' is listed twice.", this);
        }
#endif
    }
}
