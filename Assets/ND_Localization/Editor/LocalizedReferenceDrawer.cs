using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ND.Localization.Editor
{
    /// <summary>
    /// Draws every <see cref="LocalizedReference"/> as: table field + searchable key picker,
    /// with a foldout previewing the value for each locale.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizedReference), true)]
    public class LocalizedReferenceDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + Gap;
            if (!property.isExpanded) return line * 2f;
            var table = property.FindPropertyRelative("m_Table").objectReferenceValue as LocalizationTable;
            int rows = table != null ? LocaleCodes.For(table).Count : 0;
            return line * (2 + Mathf.Max(rows, 1));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var tableProp = property.FindPropertyRelative("m_Table");
            var idProp = property.FindPropertyRelative("m_EntryId");
            float h = EditorGUIUtility.singleLineHeight;

            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, h);
            property.isExpanded = EditorGUI.Foldout(new Rect(row.x, row.y, EditorGUIUtility.labelWidth, h), property.isExpanded, label, true);
            var field = new Rect(row.x + EditorGUIUtility.labelWidth + Gap, row.y, row.width - EditorGUIUtility.labelWidth - Gap, h);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(field, tableProp, GUIContent.none);
            if (EditorGUI.EndChangeCheck()) idProp.longValue = LocalizationTable.InvalidId;

            row.y += h + Gap;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            var table = tableProp.objectReferenceValue as LocalizationTable;
            var keyRect = EditorGUI.PrefixLabel(row, new GUIContent("Key"));
            EditorGUI.indentLevel = 0;
            using (new EditorGUI.DisabledScope(table == null || tableProp.hasMultipleDifferentValues))
            {
                string shown = KeyLabel(table, idProp);
                if (EditorGUI.DropdownButton(keyRect, new GUIContent(shown), FocusType.Keyboard))
                {
                    var serializedObject = property.serializedObject;
                    string path = idProp.propertyPath;
                    new KeyDropdown(table, id =>
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty(path).longValue = id;
                        serializedObject.ApplyModifiedProperties();
                    }).Show(keyRect);
                }
            }
            EditorGUI.indentLevel = indent + 1;

            if (property.isExpanded)
            {
                row.y += h + Gap;
                if (table == null || !table.Contains(idProp.longValue))
                {
                    EditorGUI.LabelField(row, " ", "Pick a table and a key to preview.", EditorStyles.miniLabel);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        foreach (var code in LocaleCodes.For(table))
                        {
                            DrawPreview(row, table, idProp.longValue, code);
                            row.y += h + Gap;
                        }
                    }
                }
            }
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static string KeyLabel(LocalizationTable table, SerializedProperty idProp)
        {
            if (idProp.hasMultipleDifferentValues) return "—";
            if (table == null || idProp.longValue == LocalizationTable.InvalidId) return "None";
            return table.TryGetKey(idProp.longValue, out string key) ? key : $"<missing id {idProp.longValue}>";
        }

        private static void DrawPreview(Rect row, LocalizationTable table, long id, string code)
        {
            var label = new GUIContent(code);
            switch (table)
            {
                case StringTable s:
                    EditorGUI.TextField(row, label, s.TryGetValue(id, code, out var text) ? text : "<missing>");
                    break;
                case AssetTable a:
                    a.TryGetValue(id, code, out var obj);
                    EditorGUI.ObjectField(row, label, obj, typeof(Object), false);
                    break;
                case ColorTable c:
                    if (c.TryGetValue(id, code, out var color)) EditorGUI.ColorField(row, label, color);
                    else EditorGUI.TextField(row, label, "<missing>");
                    break;
                default:
                    EditorGUI.LabelField(row, label, new GUIContent(table.HasValue(id, code) ? "(set)" : "<missing>"));
                    break;
            }
        }

        private class KeyDropdown : AdvancedDropdown
        {
            private readonly LocalizationTable m_Table;
            private readonly System.Action<long> m_OnPick;

            public KeyDropdown(LocalizationTable table, System.Action<long> onPick) : base(new AdvancedDropdownState())
            {
                m_Table = table;
                m_OnPick = onPick;
                minimumSize = new Vector2(260f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(m_Table.DisplayName);
                root.AddChild(new IdItem("None", LocalizationTable.InvalidId));
                var keys = new List<(string key, long id)>();
                foreach (long id in m_Table.Ids)
                    if (m_Table.TryGetKey(id, out string key)) keys.Add((key, id));
                foreach (var (key, id) in keys.OrderBy(k => k.key, System.StringComparer.OrdinalIgnoreCase))
                    root.AddChild(new IdItem(key, id));
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is IdItem idItem) m_OnPick(idItem.EntryId);
            }

            private class IdItem : AdvancedDropdownItem
            {
                public readonly long EntryId;
                public IdItem(string name, long id) : base(name) => EntryId = id;
            }
        }
    }
}
