using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Localization.Editor
{
    /// <summary>Spreadsheet-style inspector: one row per key, one column per locale. Edits go through the table API with Undo.</summary>
    [CustomEditor(typeof(LocalizationTable), true)]
    public class LocalizationTableEditor : UnityEditor.Editor
    {
        private const int PageSize = 50;
        private const float KeyWidth = 180f;
        private const float CellWidth = 220f;
        private const float ButtonWidth = 22f;

        private string m_Search = string.Empty;
        private string m_NewKey = string.Empty;
        private int m_Page;
        private Vector2 m_Scroll;

        private LocalizationTable Table => (LocalizationTable)target;

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            // Undo rewrites the serialized list behind the table's back; its id/key lookup must be rebuilt.
            foreach (var t in targets)
                if (t is LocalizationTable table) table.InvalidateCache();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            var table = Table;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(LocalizationTable.tableName)));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawToolbar(table);

            var codes = LocaleCodes.For(table);
            if (codes.Count == 0)
            {
                EditorGUILayout.HelpBox("No Locale assets in the project. Create one with Create > ND Localization > Locale.", MessageType.Info);
                return;
            }

            var rows = FilteredIds(table);
            int pages = Mathf.Max(1, (rows.Count + PageSize - 1) / PageSize);
            m_Page = Mathf.Clamp(m_Page, 0, pages - 1);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, GUILayout.MinHeight(200f));
            DrawHeader(codes);
            int end = Mathf.Min(rows.Count, (m_Page + 1) * PageSize);
            for (int i = m_Page * PageSize; i < end; i++)
            {
                if (!DrawRow(table, rows[i], codes)) break; // row removed: list changed, redraw next frame
            }
            EditorGUILayout.EndScrollView();

            if (pages > 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(m_Page == 0))
                        if (GUILayout.Button("◀", GUILayout.Width(30f))) m_Page--;
                    GUILayout.Label($"Page {m_Page + 1}/{pages}  ({rows.Count} keys)", EditorStyles.centeredGreyMiniLabel);
                    using (new EditorGUI.DisabledScope(m_Page >= pages - 1))
                        if (GUILayout.Button("▶", GUILayout.Width(30f))) m_Page++;
                }
            }
        }

        private void DrawToolbar(LocalizationTable table)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                m_Search = EditorGUILayout.TextField(m_Search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Refresh locales", EditorStyles.miniButton, GUILayout.Width(100f)))
                    LocaleCodes.Invalidate();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                m_NewKey = EditorGUILayout.TextField("New key", m_NewKey);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(m_NewKey)))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(50f)))
                    {
                        Undo.RecordObject(table, "Add Localization Key");
                        if (table.AddEntry(m_NewKey.Trim()) != LocalizationTable.InvalidId)
                        {
                            EditorUtility.SetDirty(table);
                            m_NewKey = string.Empty;
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
            if (table is StringTable strings)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Import CSV…")) LocalizationCsvMenu.ImportInteractive(strings);
                    if (GUILayout.Button("Export CSV…")) LocalizationCsvMenu.ExportInteractive(strings);
                }
            }
        }

        private List<long> FilteredIds(LocalizationTable table)
        {
            var ids = new List<long>(table.Count);
            foreach (long id in table.Ids)
            {
                if (string.IsNullOrEmpty(m_Search)) { ids.Add(id); continue; }
                if (table.TryGetKey(id, out string key) && key.IndexOf(m_Search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    ids.Add(id);
                else if (table is StringTable s && MatchesAnyValue(s, id))
                    ids.Add(id);
            }
            return ids;
        }

        private bool MatchesAnyValue(StringTable table, long id)
        {
            if (!table.TryGetEntry(id, out var entry)) return false;
            foreach (var v in entry.values)
                if (v.value != null && v.value.IndexOf(m_Search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static void DrawHeader(List<string> codes)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(ButtonWidth + 4f);
                GUILayout.Label("Key", EditorStyles.boldLabel, GUILayout.Width(KeyWidth));
                foreach (var code in codes)
                    GUILayout.Label(code, EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            }
        }

        /// <returns>false when the row was deleted.</returns>
        private bool DrawRow(LocalizationTable table, long id, List<string> codes)
        {
            table.TryGetKey(id, out string key);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("✕", GUILayout.Width(ButtonWidth)))
                {
                    if (EditorUtility.DisplayDialog("Delete key", $"Delete '{key}' from {table.DisplayName}? References to it will break.", "Delete", "Cancel"))
                    {
                        Undo.RecordObject(table, "Delete Localization Key");
                        table.RemoveEntry(id);
                        EditorUtility.SetDirty(table);
                        return false;
                    }
                }

                EditorGUI.BeginChangeCheck();
                string renamed = EditorGUILayout.DelayedTextField(key, GUILayout.Width(KeyWidth));
                if (EditorGUI.EndChangeCheck() && renamed != key)
                {
                    Undo.RecordObject(table, "Rename Localization Key");
                    if (table.RenameEntry(id, renamed.Trim())) EditorUtility.SetDirty(table);
                }

                foreach (var code in codes)
                    DrawCell(table, id, code);
            }
            return true;
        }

        private static void DrawCell(LocalizationTable table, long id, string code)
        {
            var width = GUILayout.Width(CellWidth);
            switch (table)
            {
                case StringTable s:
                {
                    s.TryGetValue(id, code, out string text);
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorGUILayout.TextArea(text ?? string.Empty, width);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(s, "Edit Localized String");
                        if (string.IsNullOrEmpty(edited)) s.ClearValue(id, code);
                        else s.SetValue(id, code, edited);
                        EditorUtility.SetDirty(s);
                    }
                    break;
                }
                case AssetTable a:
                {
                    a.TryGetValue(id, code, out Object obj);
                    EditorGUI.BeginChangeCheck();
                    var edited = EditorGUILayout.ObjectField(obj, typeof(Object), false, width);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(a, "Edit Localized Asset");
                        if (edited == null) a.ClearValue(id, code);
                        else a.SetValue(id, code, edited);
                        EditorUtility.SetDirty(a);
                    }
                    break;
                }
                case ColorTable c:
                {
                    using (new EditorGUILayout.HorizontalScope(width))
                    {
                        if (c.TryGetValue(id, code, out Color color))
                        {
                            EditorGUI.BeginChangeCheck();
                            color = EditorGUILayout.ColorField(color);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(c, "Edit Localized Color");
                                c.SetValue(id, code, color);
                                EditorUtility.SetDirty(c);
                            }
                            if (GUILayout.Button("✕", GUILayout.Width(ButtonWidth)))
                            {
                                Undo.RecordObject(c, "Clear Localized Color");
                                c.ClearValue(id, code);
                                EditorUtility.SetDirty(c);
                            }
                        }
                        else if (GUILayout.Button("+ set"))
                        {
                            Undo.RecordObject(c, "Set Localized Color");
                            c.SetValue(id, code, Color.white);
                            EditorUtility.SetDirty(c);
                        }
                    }
                    break;
                }
                default:
                    GUILayout.Label(table.HasValue(id, code) ? "(set)" : "—", width);
                    break;
            }
        }
    }
}
