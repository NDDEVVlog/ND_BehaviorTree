using System;
using UnityEngine;

namespace ND.Localization
{
    public enum LocalizationState
    {
        NotReady,
        Ready,
    }

    /// <summary>
    /// Runtime entry point. Holds the selected locale and resolves table values through the fallback chain
    /// (selected → its fallbacks → default locale).
    /// Initialization is synchronous: tables are plain assets referenced directly, nothing is streamed.
    /// <see cref="LocaleChanged"/> also fires once from <see cref="Initialize"/>, so a consumer that subscribed
    /// before initialization still receives the first locale.
    /// </summary>
    public static class LocalizationService
    {
        private const string LogTag = "[Localization]";

        public static LocalizationState State { get; private set; } = LocalizationState.NotReady;
        public static bool IsReady => State == LocalizationState.Ready;
        public static LocalizationSettings Settings { get; private set; }
        public static Locale SelectedLocale { get; private set; }

        /// <summary>Raised after the selected locale changes, including the first selection in <see cref="Initialize"/>.</summary>
        public static event Action<Locale> LocaleChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            State = LocalizationState.NotReady;
            Settings = null;
            SelectedLocale = null;
            LocaleChanged = null;
        }

        public static void Initialize(LocalizationSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError($"{LogTag} {nameof(Initialize)} rejected: settings is null.");
                return;
            }

            Locale start = null;
            foreach (var selector in settings.startupSelectors)
            {
                if (selector == null) continue;
                start = selector.Select(settings);
                if (start != null) break;
            }
            if (start == null) start = settings.defaultLocale;
            if (start == null)
            {
                Debug.LogError($"{LogTag} {nameof(Initialize)} failed: no startup selector matched and '{settings.name}' has no default locale.", settings);
                return;
            }

            Settings = settings;
            State = LocalizationState.Ready;
            Apply(start, remember: false);
        }

        /// <summary>Returns to NotReady. Subscribers to <see cref="LocaleChanged"/> are kept.</summary>
        public static void Shutdown()
        {
            State = LocalizationState.NotReady;
            Settings = null;
            SelectedLocale = null;
        }

        public static bool SetLocale(Locale locale)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(SetLocale)} rejected: state={State}.");
                return false;
            }
            if (locale == null || !Settings.locales.Contains(locale))
            {
                Debug.LogWarning($"{LogTag} {nameof(SetLocale)} rejected: locale '{(locale != null ? locale.code : "null")}' is not in '{Settings.name}'.");
                return false;
            }
            if (locale != SelectedLocale) Apply(locale, remember: true);
            return true;
        }

        public static bool SetLocale(string code)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(SetLocale)} rejected: state={State}.");
                return false;
            }
            var locale = Settings.FindLocale(code);
            if (locale == null)
            {
                Debug.LogWarning($"{LogTag} {nameof(SetLocale)} rejected: no locale with code '{code}' in '{Settings.name}'.");
                return false;
            }
            return SetLocale(locale);
        }

        private static void Apply(Locale locale, bool remember)
        {
            SelectedLocale = locale;
            if (remember && !string.IsNullOrEmpty(Settings.playerPrefsKey))
                PlayerPrefs.SetString(Settings.playerPrefsKey, locale.code);
            LocaleChanged?.Invoke(locale);
        }

        /// <summary>
        /// Reads an entry for the selected locale, walking the fallback chain.
        /// Returns false (with a logged reason) when not ready, the entry does not exist,
        /// or no locale in the chain has a value.
        /// </summary>
        public static bool TryGetValue<T>(LocalizationTable<T> table, long id, out T value)
        {
            value = default;
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(TryGetValue)} rejected: state={State}, table={(table != null ? table.DisplayName : "null")}, id={id}.");
                return false;
            }
            if (table == null)
            {
                Debug.LogError($"{LogTag} {nameof(TryGetValue)} failed: table is null (id={id}).");
                return false;
            }
            if (!table.Contains(id))
            {
                Debug.LogError($"{LogTag} {nameof(TryGetValue)} failed: table '{table.DisplayName}' has no entry with id {id}.", table);
                return false;
            }

            // Bounded walk: a fallback cycle (A → B → A) stops instead of looping forever.
            var locale = SelectedLocale;
            int steps = Settings.locales.Count + 1;
            bool triedDefault = false;
            while (locale != null && steps-- > 0)
            {
                if (table.TryGetValue(id, locale.code, out value)) return true;
                if (locale == Settings.defaultLocale) triedDefault = true;
                locale = locale.fallback;
            }
            if (!triedDefault && Settings.defaultLocale != null && table.TryGetValue(id, Settings.defaultLocale.code, out value))
                return true;

            table.TryGetKey(id, out string key);
            Debug.LogWarning($"{LogTag} Missing translation: table='{table.DisplayName}', key='{key}', locale='{SelectedLocale.code}' (fallbacks included).", table);
            return false;
        }

        /// <summary>Looks up a string by table name and key. Returns null and logs when it cannot be resolved.</summary>
        public static string GetString(string tableName, string key)
        {
            var table = GetTable<StringTable>(tableName);
            if (table == null) return null;
            if (!table.TryGetId(key, out long id))
            {
                Debug.LogError($"{LogTag} {nameof(GetString)} failed: table '{tableName}' has no key '{key}'.", table);
                return null;
            }
            return TryGetValue(table, id, out string value) ? value : null;
        }

        public static T GetTable<T>(string tableName) where T : LocalizationTable
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{LogTag} {nameof(GetTable)} rejected: state={State}, table='{tableName}'.");
                return null;
            }
            var table = Settings.FindTable(tableName);
            if (table == null)
            {
                Debug.LogError($"{LogTag} {nameof(GetTable)} failed: '{Settings.name}' lists no table named '{tableName}'.", Settings);
                return null;
            }
            if (table is T typed) return typed;
            Debug.LogError($"{LogTag} {nameof(GetTable)} failed: table '{tableName}' is a {table.GetType().Name}, not a {typeof(T).Name}.", table);
            return null;
        }

        /// <summary><c>string.Format</c> that logs a bad pattern instead of throwing; returns the raw pattern in that case.</summary>
        public static string Format(string pattern, object[] args)
        {
            if (pattern == null || args == null || args.Length == 0) return pattern;
            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException e)
            {
                Debug.LogError($"{LogTag} Bad format pattern \"{pattern}\" with {args.Length} argument(s): {e.Message}");
                return pattern;
            }
        }
    }
}
