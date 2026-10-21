using UnityEngine;

namespace ND.Localization
{
    /// <summary>A language/region the game can be shown in, identified by a code such as "en", "vi" or "zh-CN".</summary>
    [CreateAssetMenu(fileName = "Locale", menuName = "ND Localization/Locale", order = 0)]
    public class Locale : ScriptableObject
    {
        [Tooltip("Identifier used as the column name in tables and CSV files, e.g. en, vi, zh-CN.")]
        public string code = "en";

        [Tooltip("Name shown to players in a language picker, written in that language (e.g. Tiếng Việt).")]
        public string displayName = "English";

        [Tooltip("Locale to read from when an entry has no value for this one. Leave empty to fall back to the default locale only.")]
        public Locale fallback;

        public override string ToString() => string.IsNullOrEmpty(code) ? name : code;
    }
}
