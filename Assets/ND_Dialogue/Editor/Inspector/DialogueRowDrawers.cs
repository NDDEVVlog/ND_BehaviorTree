using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Shared pieces of the one-line condition/action drawers: where names come from, and a value field that matches
    /// the variable's type (checkbox for bool, number fields, xyz for vectors, colour picker, object slot).
    /// </summary>
    public static class DialogueRowGUI
    {
        public const float Gap = 4f;

        /// <summary>The database a property belongs to (the database itself, or an IDialogueDatabaseOwner like a trigger).</summary>
        public static DialogueDatabase DatabaseOf(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            return target as DialogueDatabase ?? (target as IDialogueDatabaseOwner)?.Database;
        }

        /// <summary>A name popup over <paramref name="names"/>; an unknown current value stays visible as "⚠ name". Free text when there are no names.</summary>
        public static void NamePopup(Rect rect, SerializedProperty property, IEnumerable<string> names, string placeholder)
        {
            var list = names.Distinct().OrderBy(n => n).ToList();
            if (list.Count == 0)
            {
                property.stringValue = EditorGUI.TextField(rect, property.stringValue);
                return;
            }
            string current = property.stringValue ?? string.Empty;
            var options = new List<string>(list);
            int index = options.IndexOf(current);
            if (index < 0)
            {
                options.Insert(0, string.IsNullOrEmpty(current) ? placeholder : $"⚠ {current}");
                index = 0;
            }
            int picked = EditorGUI.Popup(rect, index, options.ToArray());
            if (picked != index && list.Contains(options[picked])) property.stringValue = options[picked];
        }

        /// <summary>
        /// Edits a value written as text (VariableCondition/SetVariableAction.value) with the control of
        /// <paramref name="type"/>; Object variables use <paramref name="objectProp"/> instead. Unknown type: a text box.
        /// </summary>
        public static void ValueField(Rect rect, VariableType? type, SerializedProperty textProp, SerializedProperty objectProp)
        {
            string text = textProp.stringValue ?? string.Empty;
            switch (type)
            {
                case VariableType.Bool:
                {
                    bool.TryParse(text, out bool b);
                    var toggleRect = new Rect(rect.x, rect.y, 18f, rect.height);
                    bool nb = EditorGUI.Toggle(toggleRect, b);
                    EditorGUI.LabelField(new Rect(rect.x + 20f, rect.y, rect.width - 20f, rect.height), nb ? "true" : "false", EditorStyles.miniLabel);
                    if (nb != b || !bool.TryParse(text, out _)) textProp.stringValue = nb ? "true" : "false";
                    break;
                }
                case VariableType.Int:
                {
                    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i);
                    int ni = EditorGUI.IntField(rect, i);
                    if (ni != i || text != i.ToString(CultureInfo.InvariantCulture)) textProp.stringValue = ni.ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case VariableType.Float:
                {
                    float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
                    float nf = EditorGUI.FloatField(rect, f);
                    if (!Mathf.Approximately(nf, f) || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        textProp.stringValue = nf.ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case VariableType.Vector2:
                case VariableType.Vector3:
                {
                    VariableValue.TryParse(type.Value, text, out var parsed);
                    Vector4 v = parsed.vectorValue;
                    EditorGUI.BeginChangeCheck();
                    Vector4 nv = type == VariableType.Vector2 ? (Vector4)EditorGUI.Vector2Field(rect, GUIContent.none, v) : EditorGUI.Vector3Field(rect, GUIContent.none, v);
                    if (EditorGUI.EndChangeCheck())
                    {
                        var ci = CultureInfo.InvariantCulture;
                        textProp.stringValue = type == VariableType.Vector2
                            ? string.Format(ci, "{0},{1}", nv.x, nv.y)
                            : string.Format(ci, "{0},{1},{2}", nv.x, nv.y, nv.z);
                    }
                    break;
                }
                case VariableType.Color:
                {
                    VariableValue.TryParse(VariableType.Color, text, out var parsed);
                    EditorGUI.BeginChangeCheck();
                    var c = EditorGUI.ColorField(rect, GUIContent.none, parsed.colorValue, true, true, false);
                    if (EditorGUI.EndChangeCheck()) textProp.stringValue = "#" + ColorUtility.ToHtmlStringRGBA(c);
                    break;
                }
                case VariableType.Object:
                    EditorGUI.ObjectField(rect, objectProp, typeof(Object), GUIContent.none);
                    break;
                default:
                    textProp.stringValue = EditorGUI.TextField(rect, text);
                    break;
            }
        }

        public static VariableType? TypeOf(DialogueDatabase database, string variable)
        {
            if (database == null || string.IsNullOrEmpty(variable)) return null;
            return database.Schema.TryGetVariableType(variable, out var type) ? type : (VariableType?)null;
        }

        /// <summary>Splits <paramref name="rect"/> into columns of the given relative widths (fixed widths when &gt; 1).</summary>
        public static Rect[] Columns(Rect rect, params float[] widths)
        {
            float fixedTotal = widths.Where(w => w > 1f).Sum() + Gap * (widths.Length - 1);
            float flexible = Mathf.Max(0f, rect.width - fixedTotal);
            float relativeTotal = widths.Where(w => w <= 1f).Sum();
            var result = new Rect[widths.Length];
            float x = rect.x;
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i] > 1f ? widths[i] : relativeTotal > 0f ? flexible * widths[i] / relativeTotal : 0f;
                result[i] = new Rect(x, rect.y, w, EditorGUIUtility.singleLineHeight);
                x += w + Gap;
            }
            return result;
        }

        public static readonly string[] CompareLabels = { "=", "≠", "<", "≤", ">", "≥" };
    }

    /// <summary><c>[variable ▾] [op ▾] [value]</c> — the value control follows the variable's type.</summary>
    [CustomPropertyDrawer(typeof(VariableCondition))]
    public class VariableConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var variable = property.FindPropertyRelative(nameof(VariableCondition.variable));
            var op = property.FindPropertyRelative(nameof(VariableCondition.op));
            var cols = DialogueRowGUI.Columns(position, 0.45f, 44f, 0.55f);

            EditorGUI.BeginProperty(position, label, property);
            DialogueRowGUI.NamePopup(cols[0], variable, database != null ? database.Schema.VariableNames : Enumerable.Empty<string>(), "(variable)");
            var type = DialogueRowGUI.TypeOf(database, variable.stringValue);
            bool orderable = type == null || type == VariableType.Int || type == VariableType.Float;
            int count = orderable ? 6 : 2;
            int current = Mathf.Clamp(op.enumValueIndex, 0, count - 1);
            op.enumValueIndex = EditorGUI.Popup(cols[1], current, DialogueRowGUI.CompareLabels.Take(count).ToArray());
            DialogueRowGUI.ValueField(cols[2], type, property.FindPropertyRelative(nameof(VariableCondition.value)),
                property.FindPropertyRelative(nameof(VariableCondition.objectValue)));
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>[quest ▾] [is / is not ▾] [state ▾]</c></summary>
    [CustomPropertyDrawer(typeof(QuestStateCondition))]
    public class QuestStateConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var op = property.FindPropertyRelative(nameof(QuestStateCondition.op));
            var cols = DialogueRowGUI.Columns(position, 46f, 0.5f, 56f, 0.5f);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(cols[0], "quest", EditorStyles.miniLabel);
            DialogueRowGUI.NamePopup(cols[1], property.FindPropertyRelative(nameof(QuestStateCondition.quest)),
                database != null ? database.Schema.QuestNames : Enumerable.Empty<string>(), "(quest)");
            int isNot = op.enumValueIndex == (int)CompareOp.NotEqual ? 1 : 0;
            int picked = EditorGUI.Popup(cols[2], isNot, new[] { "is", "is not" });
            op.enumValueIndex = picked == 1 ? (int)CompareOp.NotEqual : (int)CompareOp.Equal;
            EditorGUI.PropertyField(cols[3], property.FindPropertyRelative(nameof(QuestStateCondition.state)), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>[variable ▾] [= / += ▾] [value]</c></summary>
    [CustomPropertyDrawer(typeof(SetVariableAction))]
    public class SetVariableActionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var variable = property.FindPropertyRelative(nameof(SetVariableAction.variable));
            var mode = property.FindPropertyRelative(nameof(SetVariableAction.mode));
            var cols = DialogueRowGUI.Columns(position, 0.45f, 44f, 0.55f);
            EditorGUI.BeginProperty(position, label, property);
            DialogueRowGUI.NamePopup(cols[0], variable, database != null ? database.Schema.VariableNames : Enumerable.Empty<string>(), "(variable)");
            var type = DialogueRowGUI.TypeOf(database, variable.stringValue);
            bool addable = type == null || type == VariableType.Int || type == VariableType.Float || type == VariableType.Vector2 || type == VariableType.Vector3;
            int current = addable ? mode.enumValueIndex : 0;
            mode.enumValueIndex = EditorGUI.Popup(cols[1], current, addable ? new[] { "=", "+=" } : new[] { "=" });
            DialogueRowGUI.ValueField(cols[2], type, property.FindPropertyRelative(nameof(SetVariableAction.value)),
                property.FindPropertyRelative(nameof(SetVariableAction.objectValue)));
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>quest [quest ▾] → [state ▾]</c></summary>
    [CustomPropertyDrawer(typeof(SetQuestStateAction))]
    public class SetQuestStateActionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var cols = DialogueRowGUI.Columns(position, 46f, 0.55f, 18f, 0.45f);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(cols[0], "quest", EditorStyles.miniLabel);
            DialogueRowGUI.NamePopup(cols[1], property.FindPropertyRelative(nameof(SetQuestStateAction.quest)),
                database != null ? database.Schema.QuestNames : Enumerable.Empty<string>(), "(quest)");
            EditorGUI.LabelField(cols[2], "→");
            EditorGUI.PropertyField(cols[3], property.FindPropertyRelative(nameof(SetQuestStateAction.state)), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>⚡ [event ▾] [argument]</c></summary>
    [CustomPropertyDrawer(typeof(RaiseEventAction))]
    public class RaiseEventActionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var cols = DialogueRowGUI.Columns(position, 18f, 0.55f, 0.45f);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(cols[0], "⚡");
            var eventName = property.FindPropertyRelative(nameof(RaiseEventAction.eventName));
            DialogueRowGUI.NamePopup(cols[1], eventName, database != null ? database.Schema.EventNames : Enumerable.Empty<string>(), "(event)");
            var argument = property.FindPropertyRelative(nameof(RaiseEventAction.argument));
            var hint = database?.FindEvent(eventName.stringValue)?.argumentHint;
            argument.stringValue = EditorGUI.TextField(cols[2], new GUIContent(string.Empty, string.IsNullOrEmpty(hint) ? "Argument (optional)" : hint), argument.stringValue);
            if (string.IsNullOrEmpty(argument.stringValue))
                EditorGUI.LabelField(cols[2], string.IsNullOrEmpty(hint) ? " argument" : $" {hint}", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.EndProperty();
        }
    }
}
