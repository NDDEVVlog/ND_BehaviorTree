using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ND.Localization.Editor
{
    public static class LocalizationCsvMenu
    {
        [MenuItem("CONTEXT/StringTable/Export CSV…")]
        private static void ExportContext(MenuCommand command) => ExportInteractive((StringTable)command.context);

        [MenuItem("CONTEXT/StringTable/Import CSV…")]
        private static void ImportContext(MenuCommand command) => ImportInteractive((StringTable)command.context);

        public static void ExportInteractive(StringTable table)
        {
            string path = EditorUtility.SaveFilePanel("Export CSV", string.Empty, table.DisplayName + ".csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            string csv = LocalizationCsv.Export(table, LocaleCodes.For(table));
            // BOM so Excel opens Vietnamese/CJK text as UTF-8.
            File.WriteAllText(path, csv, new UTF8Encoding(true));
            Debug.Log($"[Localization] Exported {table.Count} keys from '{table.DisplayName}' to {path}", table);
        }

        public static void ImportInteractive(StringTable table)
        {
            string path = EditorUtility.OpenFilePanel("Import CSV", string.Empty, "csv");
            if (string.IsNullOrEmpty(path)) return;

            int choice = EditorUtility.DisplayDialogComplex("Import CSV",
                $"Import '{Path.GetFileName(path)}' into '{table.DisplayName}'.\n\nWhat should an empty cell do?",
                "Clear translation", "Cancel", "Keep existing");
            if (choice == 1) return;

            Undo.RecordObject(table, "Import Localization CSV");
            var report = LocalizationCsv.Import(table, File.ReadAllText(path, Encoding.UTF8), clearEmptyCells: choice == 0);
            EditorUtility.SetDirty(table);

            Debug.Log($"[Localization] Imported '{Path.GetFileName(path)}' into '{table.DisplayName}': {report}", table);
            foreach (var error in report.errors)
                Debug.LogError($"[Localization] CSV import ({Path.GetFileName(path)}): {error}", table);
        }
    }
}
