using System.Collections.Generic;
using UnityEditor;

namespace ND.Localization.Editor
{
    /// <summary>Locale codes to show as columns: every Locale asset in the project, plus any code a table already uses.</summary>
    public static class LocaleCodes
    {
        private static List<string> s_ProjectCodes;

        static LocaleCodes()
        {
            EditorApplication.projectChanged += () => s_ProjectCodes = null;
        }

        public static IReadOnlyList<string> Project
        {
            get
            {
                if (s_ProjectCodes != null) return s_ProjectCodes;
                s_ProjectCodes = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(Locale)))
                {
                    var locale = AssetDatabase.LoadAssetAtPath<Locale>(AssetDatabase.GUIDToAssetPath(guid));
                    if (locale != null && !string.IsNullOrEmpty(locale.code) && !s_ProjectCodes.Contains(locale.code))
                        s_ProjectCodes.Add(locale.code);
                }
                s_ProjectCodes.Sort(System.StringComparer.OrdinalIgnoreCase);
                return s_ProjectCodes;
            }
        }

        public static List<string> For(LocalizationTable table)
        {
            var codes = new List<string>(Project);
            switch (table)
            {
                case StringTable s: AddUsed(s, codes); break;
                case AssetTable a: AddUsed(a, codes); break;
                case ColorTable c: AddUsed(c, codes); break;
            }
            return codes;
        }

        private static void AddUsed<T>(LocalizationTable<T> table, List<string> codes)
        {
            foreach (var entry in table.Entries)
                foreach (var v in entry.values)
                    if (!string.IsNullOrEmpty(v.locale) && !codes.Contains(v.locale))
                        codes.Add(v.locale);
        }

        public static void Invalidate() => s_ProjectCodes = null;
    }
}
