using System;
using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.GraphUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// How variables and quests look in the editor: a colour per variable (a plugin's colour, e.g. the blackboard key
    /// type colour, else the colour of its type — the same palette as the behavior-tree keys) and a searchable picker
    /// grouped as <c>Characters / Guard / trust</c> and <c>World / player / gold</c>.
    /// </summary>
    public static class DialogueVariableStyle
    {
        public static readonly Color QuestColor = new Color(1f, 0.78f, 0.3f);
        public static readonly Color EventColor = new Color(0.4f, 0.9f, 0.85f);
        public static readonly Color UnknownColor = new Color(0.6f, 0.6f, 0.6f);

        /// <summary>Colour of a value type (matches the ND_BehaviorTree key colours of the same type).</summary>
        public static Color ForType(VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool: return new Color(1f, 0.1347f, 0.25f);
                case VariableType.Int: return new Color(0.5f, 0.8f, 1f);
                case VariableType.Float: return new Color(0.5f, 1f, 0.5f);
                case VariableType.String: return new Color(0.85f, 0.35f, 0.55f);
                case VariableType.Vector2: return new Color(0.8f, 0.6f, 0.2f);
                case VariableType.Vector3: return new Color(1f, 0.7f, 0.2f);
                case VariableType.Color: return new Color(0.3f, 0.9f, 0.9f);
                case VariableType.Object: return new Color(1f, 0.3f, 0.7f);
                default: return UnknownColor;
            }
        }

        /// <summary>Colour of a variable: a plugin's (e.g. its blackboard key type), else its type's, else grey (unknown).</summary>
        public static Color Of(DialogueDatabase database, string variable)
        {
            if (database == null || string.IsNullOrEmpty(variable)) return UnknownColor;
            var plugin = DialoguePluginRegistry.VariableColor(database, variable);
            if (plugin.HasValue) return plugin.Value;
            return database.Schema.TryGetVariableType(variable, out var type) ? ForType(type) : UnknownColor;
        }

        /// <summary>Folder path of a variable in the picker: <c>Characters/Guard/trust</c> for attributes, else <c>World/a/b</c>.</summary>
        public static string PickerPath(DialogueDatabase database, string variable)
        {
            if (database != null && database.TryResolveAttribute(variable, out var actor, out var attribute))
                return $"Characters/{actor.name}/{attribute.name}";
            return "World/" + variable.Replace(Actor.AttributeSeparator, '/');
        }

        /// <summary>"Int" / "Bool"… or "?" when the schema does not know the variable.</summary>
        public static string TypeLabel(DialogueDatabase database, string variable) =>
            database != null && database.Schema.TryGetVariableType(variable, out var type) ? type.ToString() : "?";

        public static List<GraphSearchPopup.Entry> VariableEntries(DialogueDatabase database)
        {
            if (database == null) return new List<GraphSearchPopup.Entry>();
            return database.Schema.VariableNames.Where(n => !string.IsNullOrEmpty(n)).Distinct()
                .Select(n => new GraphSearchPopup.Entry(PickerPath(database, n), null, n, $"{n}  ·  {TypeLabel(database, n)}", Of(database, n)))
                .ToList();
        }

        public static List<GraphSearchPopup.Entry> QuestEntries(DialogueDatabase database)
        {
            if (database == null) return new List<GraphSearchPopup.Entry>();
            return database.Schema.QuestNames.Where(n => !string.IsNullOrEmpty(n)).Distinct()
                .Select(n => new GraphSearchPopup.Entry(n, null, n, $"quest '{n}'", QuestColor))
                .ToList();
        }

        public static List<GraphSearchPopup.Entry> EventEntries(DialogueDatabase database)
        {
            if (database == null) return new List<GraphSearchPopup.Entry>();
            return database.Schema.EventNames.Where(n => !string.IsNullOrEmpty(n)).Distinct()
                .Select(n => new GraphSearchPopup.Entry(n, null, n, database.FindEvent(n)?.description, EventColor))
                .ToList();
        }
    }

    /// <summary>IMGUI name button: a colour dot, the current name, and a click that opens the searchable picker.</summary>
    public static class DialogueNameButton
    {
        public enum Kind { Variable, Quest, Event }

        /// <summary>Raised after a pick (or a deferred menu edit) was written to the asset: the dialogue editor redraws its graph.</summary>
        public static event Action<Object> Edited;

        public static void NotifyEdited(Object target) => Edited?.Invoke(target);

        private static GUIStyle s_Style;
        private static GUIStyle Style => s_Style ??= new GUIStyle(EditorStyles.popup) { padding = new RectOffset(16, 14, 1, 2), clipping = TextClipping.Clip };

        public static void Draw(Rect rect, SerializedProperty property, DialogueDatabase database, Kind kind)
        {
            string current = property.stringValue ?? string.Empty;
            var names = kind == Kind.Variable ? database?.Schema.VariableNames : kind == Kind.Quest ? database?.Schema.QuestNames : database?.Schema.EventNames;
            var known = names?.ToList() ?? new List<string>();
            if (known.Count == 0)
            {
                // Nothing declared: type the name.
                property.stringValue = EditorGUI.TextField(rect, current);
                return;
            }

            bool missing = !string.IsNullOrEmpty(current) && !known.Contains(current);
            string placeholder = kind == Kind.Variable ? "(variable)" : kind == Kind.Quest ? "(quest)" : "(event)";
            string text = string.IsNullOrEmpty(current) ? placeholder : missing ? "⚠ " + current : current;
            string tooltip = kind == Kind.Variable && !missing && current.Length > 0 ? $"{current}  ·  {DialogueVariableStyle.TypeLabel(database, current)}"
                : missing ? $"'{current}' is not declared." : string.Empty;

            if (GUI.Button(rect, new GUIContent(text, tooltip), Style))
                Open(rect, property, database, kind);

            Color dot = missing || current.Length == 0 ? DialogueVariableStyle.UnknownColor
                : kind == Kind.Variable ? DialogueVariableStyle.Of(database, current)
                : kind == Kind.Quest ? DialogueVariableStyle.QuestColor : DialogueVariableStyle.EventColor;
            var dotRect = new Rect(rect.x + 5f, rect.y + (rect.height - 8f) * 0.5f, 8f, 8f);
            EditorGUI.DrawRect(dotRect, dot);
        }

        private static void Open(Rect rect, SerializedProperty property, DialogueDatabase database, Kind kind)
        {
            var target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            var entries = kind == Kind.Variable ? DialogueVariableStyle.VariableEntries(database)
                : kind == Kind.Quest ? DialogueVariableStyle.QuestEntries(database) : DialogueVariableStyle.EventEntries(database);
            Vector2 screen = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.yMax));
            GraphSearchPopup.Show(screen, kind.ToString(), entries, data => Write(target, path, (string)data));
        }

        /// <summary>Writes the pick through a fresh SerializedObject (the drawer's one may be gone by now): undoable.</summary>
        public static void Write(Object target, string propertyPath, string value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyPath);
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
            {
                Debug.LogError($"[Dialogue Editor] Cannot set '{value}': '{propertyPath}' no longer exists on '{target.name}'.");
                return;
            }
            prop.stringValue = value;
            so.ApplyModifiedProperties();
            Edited?.Invoke(target);
        }
    }
}
