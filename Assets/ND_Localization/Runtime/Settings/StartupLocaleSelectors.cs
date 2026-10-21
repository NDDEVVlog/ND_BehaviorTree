using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Localization
{
    /// <summary>Picks the locale to start with. Selectors run in order; the first non-null result wins.</summary>
    public interface IStartupLocaleSelector
    {
        Locale Select(LocalizationSettings settings);
    }

    /// <summary>Restores the locale the player picked last time (written by <see cref="LocalizationService.SetLocale(Locale)"/>).</summary>
    [Serializable]
    public class PlayerPrefsLocaleSelector : IStartupLocaleSelector
    {
        public Locale Select(LocalizationSettings settings)
        {
            if (string.IsNullOrEmpty(settings.playerPrefsKey)) return null;
            string code = PlayerPrefs.GetString(settings.playerPrefsKey, string.Empty);
            return string.IsNullOrEmpty(code) ? null : settings.FindLocale(code);
        }
    }

    /// <summary>Matches the device language (<see cref="Application.systemLanguage"/>) against the locale codes.</summary>
    [Serializable]
    public class SystemLanguageLocaleSelector : IStartupLocaleSelector
    {
        public Locale Select(LocalizationSettings settings) => SelectFor(settings, Application.systemLanguage);

        public static Locale SelectFor(LocalizationSettings settings, SystemLanguage language)
        {
            if (!s_Codes.TryGetValue(language, out var candidates)) return null;
            foreach (var code in candidates)
            {
                var exact = settings.FindLocale(code);
                if (exact != null) return exact;
            }
            // "zh-CN" device vs a project that only has "zh" (or the reverse): match on the language part.
            string language2 = LanguagePart(candidates[0]);
            foreach (var locale in settings.locales)
                if (locale != null && LanguagePart(locale.code) == language2)
                    return locale;
            return null;
        }

        private static string LanguagePart(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            int dash = code.IndexOf('-');
            return (dash < 0 ? code : code.Substring(0, dash)).ToLowerInvariant();
        }

        private static readonly Dictionary<SystemLanguage, string[]> s_Codes = new Dictionary<SystemLanguage, string[]>
        {
            { SystemLanguage.Afrikaans, new[] { "af" } },
            { SystemLanguage.Arabic, new[] { "ar" } },
            { SystemLanguage.Basque, new[] { "eu" } },
            { SystemLanguage.Belarusian, new[] { "be" } },
            { SystemLanguage.Bulgarian, new[] { "bg" } },
            { SystemLanguage.Catalan, new[] { "ca" } },
            { SystemLanguage.Chinese, new[] { "zh-CN", "zh-Hans", "zh" } },
            { SystemLanguage.ChineseSimplified, new[] { "zh-CN", "zh-Hans", "zh" } },
            { SystemLanguage.ChineseTraditional, new[] { "zh-TW", "zh-Hant", "zh-HK" } },
            { SystemLanguage.Czech, new[] { "cs" } },
            { SystemLanguage.Danish, new[] { "da" } },
            { SystemLanguage.Dutch, new[] { "nl" } },
            { SystemLanguage.English, new[] { "en", "en-US", "en-GB" } },
            { SystemLanguage.Estonian, new[] { "et" } },
            { SystemLanguage.Finnish, new[] { "fi" } },
            { SystemLanguage.French, new[] { "fr", "fr-FR" } },
            { SystemLanguage.German, new[] { "de", "de-DE" } },
            { SystemLanguage.Greek, new[] { "el" } },
            { SystemLanguage.Hebrew, new[] { "he" } },
            { SystemLanguage.Hindi, new[] { "hi" } },
            { SystemLanguage.Hungarian, new[] { "hu" } },
            { SystemLanguage.Icelandic, new[] { "is" } },
            { SystemLanguage.Indonesian, new[] { "id" } },
            { SystemLanguage.Italian, new[] { "it" } },
            { SystemLanguage.Japanese, new[] { "ja" } },
            { SystemLanguage.Korean, new[] { "ko" } },
            { SystemLanguage.Latvian, new[] { "lv" } },
            { SystemLanguage.Lithuanian, new[] { "lt" } },
            { SystemLanguage.Norwegian, new[] { "no", "nb" } },
            { SystemLanguage.Polish, new[] { "pl" } },
            { SystemLanguage.Portuguese, new[] { "pt", "pt-BR", "pt-PT" } },
            { SystemLanguage.Romanian, new[] { "ro" } },
            { SystemLanguage.Russian, new[] { "ru" } },
            { SystemLanguage.SerboCroatian, new[] { "sr", "hr" } },
            { SystemLanguage.Slovak, new[] { "sk" } },
            { SystemLanguage.Slovenian, new[] { "sl" } },
            { SystemLanguage.Spanish, new[] { "es", "es-ES", "es-MX" } },
            { SystemLanguage.Swedish, new[] { "sv" } },
            { SystemLanguage.Thai, new[] { "th" } },
            { SystemLanguage.Turkish, new[] { "tr" } },
            { SystemLanguage.Ukrainian, new[] { "uk" } },
            { SystemLanguage.Vietnamese, new[] { "vi" } },
        };
    }
}
