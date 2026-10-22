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

        /// <summary>A text field with a grey hint while empty.</summary>
        public static string HintField(Rect rect, string value, string hint, string tooltip = null)
        {
            string edited = EditorGUI.TextField(rect, new GUIContent(string.Empty, tooltip ?? hint), value ?? string.Empty);
            if (string.IsNullOrEmpty(edited)) EditorGUI.LabelField(rect, " " + hint, EditorStyles.centeredGreyMiniLabel);
            return edited;
        }

        public static readonly string[] CompareLabels = { "=", "≠", "<", "≤", ">", "≥" };

        /// <summary>Menu label of a condition/action type: "Variable", "Quest State", "Any Of (OR group)", "Set Variable"…</summary>
        public static string TypeLabel(Type type)
        {
            if (type == typeof(AnyCondition)) return "Any Of (OR group)";
            string name = type.Name;
            if (name.EndsWith("Condition") && name.Length > "Condition".Length) name = name.Substring(0, name.Length - "Condition".Length);
            else if (name.EndsWith("Action") && name.Length > "Action".Length) name = name.Substring(0, name.Length - "Action".Length);
            return ObjectNames.NicifyVariableName(name);
        }

        /// <summary>Concrete condition/action types for "+ Add" menus (the OR group last).</summary>
        public static IEnumerable<Type> ConcreteTypes(Type baseType) =>
            TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t == typeof(AnyCondition) ? 1 : 0).ThenBy(t => t.Name);

        /// <summary>Appends a new <paramref name="type"/> to a [SerializeReference] list through a fresh SerializedObject (undoable).</summary>
        public static void AppendManagedReference(Object target, string listPath, Type type)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var list = so.FindProperty(listPath);
            if (list == null || !list.isArray)
            {
                Debug.LogError($"[Dialogue Editor] Cannot add {type.Name}: '{listPath}' no longer exists on '{target.name}'.");
                return;
            }
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
            so.ApplyModifiedProperties();
        }
    }

    /// <summary><c>● [variable ▾] [op ▾] [value]</c> — the value control follows the variable's type.</summary>
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
            DialogueNameButton.Draw(cols[0], variable, database, DialogueNameButton.Kind.Variable);
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

    /// <summary><c>quest ● [quest ▾] [is / is not ▾] [state ▾]</c></summary>
    [CustomPropertyDrawer(typeof(QuestStateCondition))]
    public class QuestStateConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var op = property.FindPropertyRelative(nameof(QuestStateCondition.op));
            var cols = DialogueRowGUI.Columns(position, 40f, 0.5f, 56f, 0.5f);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(cols[0], "quest", EditorStyles.miniLabel);
            DialogueNameButton.Draw(cols[1], property.FindPropertyRelative(nameof(QuestStateCondition.quest)), database, DialogueNameButton.Kind.Quest);
            int isNot = op.enumValueIndex == (int)CompareOp.NotEqual ? 1 : 0;
            int picked = EditorGUI.Popup(cols[2], isNot, new[] { "is", "is not" });
            op.enumValueIndex = picked == 1 ? (int)CompareOp.NotEqual : (int)CompareOp.Equal;
            EditorGUI.PropertyField(cols[3], property.FindPropertyRelative(nameof(QuestStateCondition.state)), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }

    /// <summary>
    /// An "OR" group drawn as a framed box: <c>ANY of these is true (OR)</c>, one row per condition (each with its own
    /// one-line drawer and ✕), and "+ or…" to add one.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnyCondition))]
    public class AnyConditionDrawer : PropertyDrawer
    {
        private const float Pad = 4f;
        private const float RemoveWidth = 20f;
        private static readonly Color Frame = new Color(1f, 0.78f, 0.3f, 0.55f);
        private static readonly Color Fill = new Color(1f, 0.78f, 0.3f, 0.06f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight, spacing = EditorGUIUtility.standardVerticalSpacing;
            var list = property.FindPropertyRelative(nameof(AnyCondition.conditions));
            float h = Pad + line + spacing; // header
            if (list != null)
                for (int i = 0; i < list.arraySize; i++)
                    h += ElementHeight(list.GetArrayElementAtIndex(i)) + spacing;
            return h + line + Pad; // add button
        }

        private static float ElementHeight(SerializedProperty element) =>
            element.managedReferenceValue == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(element, GUIContent.none, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight, spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.DrawRect(position, Fill);
            EditorGUI.DrawRect(new Rect(position.x, position.y, 3f, position.height), Frame);

            var inner = new Rect(position.x + 8f, position.y + Pad, position.width - 10f, line);
            EditorGUI.LabelField(inner, new GUIContent("ANY of these is true  (OR)", "The group holds when at least one row holds. An empty group never holds."), EditorStyles.miniBoldLabel);
            inner.y += line + spacing;

            var list = property.FindPropertyRelative(nameof(AnyCondition.conditions));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                float h = ElementHeight(element);
                var rowRect = new Rect(inner.x, inner.y, inner.width - RemoveWidth - 2f, h);
                if (element.managedReferenceValue == null) EditorGUI.LabelField(rowRect, "(empty)", EditorStyles.miniLabel);
                else EditorGUI.PropertyField(rowRect, element, GUIContent.none, true);
                if (GUI.Button(new Rect(inner.xMax - RemoveWidth, inner.y, RemoveWidth, line), new GUIContent("✕", "Remove from the group")))
                {
                    list.DeleteArrayElementAtIndex(i);
                    break;
                }
                inner.y += h + spacing;
            }
            EditorGUI.indentLevel = indent;

            if (list != null && list.arraySize == 0)
                EditorGUI.LabelField(new Rect(inner.x + 70f, inner.y, inner.width - 70f, line), "empty group: never true", EditorStyles.centeredGreyMiniLabel);
            if (GUI.Button(new Rect(inner.x, inner.y, 64f, line), new GUIContent("+ or…", "Add a condition to the group"), EditorStyles.miniButton) && list != null)
            {
                var target = property.serializedObject.targetObject;
                string path = list.propertyPath;
                var menu = new GenericMenu();
                foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(DialogueCondition)))
                {
                    var t = type;
                    menu.AddItem(new GUIContent(DialogueRowGUI.TypeLabel(t)), false, () =>
                    {
                        DialogueRowGUI.AppendManagedReference(target, path, t);
                        DialogueNameButton.NotifyEdited(target);
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>[seen / not seen ▾] [this conversation ▾] [#7 "Halt!" ▾]</c></summary>
    [CustomPropertyDrawer(typeof(EntrySeenCondition))]
    public class EntrySeenConditionDrawer : PropertyDrawer
    {
        private const string ThisConversation = "this conversation";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var seen = property.FindPropertyRelative(nameof(EntrySeenCondition.seen));
            var conversationProp = property.FindPropertyRelative(nameof(EntrySeenCondition.conversation));
            var entryProp = property.FindPropertyRelative(nameof(EntrySeenCondition.entryId));
            var cols = DialogueRowGUI.Columns(position, 70f, 0.4f, 0.6f);
            EditorGUI.BeginProperty(position, label, property);

            seen.boolValue = EditorGUI.Popup(cols[0], seen.boolValue ? 0 : 1, new[] { "seen", "not seen" }) == 0;

            var titles = new List<string> { ThisConversation };
            if (database != null) titles.AddRange(database.conversations.Where(c => c != null).Select(c => c.title));
            int current = string.IsNullOrEmpty(conversationProp.stringValue) ? 0 : titles.IndexOf(conversationProp.stringValue);
            if (current < 0) { titles.Add("⚠ " + conversationProp.stringValue); current = titles.Count - 1; }
            int picked = EditorGUI.Popup(cols[1], current, titles.ToArray());
            if (picked != current && !titles[picked].StartsWith("⚠")) conversationProp.stringValue = picked == 0 ? string.Empty : titles[picked];

            var conversation = database == null ? null
                : string.IsNullOrEmpty(conversationProp.stringValue) ? ConversationOf(property, database) : database.FindConversation(conversationProp.stringValue);
            if (conversation == null)
                entryProp.intValue = EditorGUI.IntField(cols[2], entryProp.intValue);
            else
            {
                var entries = conversation.entries.Where(e => e != null && e.id != Conversation.StartEntryId).ToList();
                var labels = entries.Select(e => $"#{e.id} {Preview(e)}").ToList();
                int index = entries.FindIndex(e => e.id == entryProp.intValue);
                if (index < 0) { labels.Insert(0, $"⚠ #{entryProp.intValue}"); index = 0; entries.Insert(0, null); }
                int chosen = EditorGUI.Popup(cols[2], index, labels.ToArray());
                if (chosen != index && entries[chosen] != null) entryProp.intValue = entries[chosen].id;
            }
            EditorGUI.EndProperty();
        }

        private static string Preview(DialogueEntry entry)
        {
            string text = !string.IsNullOrEmpty(entry.title) ? entry.title : DialogueTextEditing.Read(entry.text, null) ?? (entry.isPassthrough ? "(routing)" : string.Empty);
            text = text.Replace('/', '∕'); // a '/' would make a submenu
            return text.Length > 32 ? text.Substring(0, 31) + "…" : text;
        }

        /// <summary>The conversation that holds the condition ("conversations.Array.data[N]…"), when it is in one.</summary>
        private static Conversation ConversationOf(SerializedProperty property, DialogueDatabase database)
        {
            var match = System.Text.RegularExpressions.Regex.Match(property.propertyPath, @"^conversations\.Array\.data\[(\d+)\]");
            if (!match.Success) return null;
            int index = int.Parse(match.Groups[1].Value);
            return index < database.conversations.Count ? database.conversations[index] : null;
        }
    }

    /// <summary><c>● [variable ▾] [= / += ▾] [value]</c></summary>
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
            DialogueNameButton.Draw(cols[0], variable, database, DialogueNameButton.Kind.Variable);
            var type = DialogueRowGUI.TypeOf(database, variable.stringValue);
            bool addable = type == null || type == VariableType.Int || type == VariableType.Float || type == VariableType.Vector2 || type == VariableType.Vector3;
            int current = addable ? mode.enumValueIndex : 0;
            mode.enumValueIndex = EditorGUI.Popup(cols[1], current, addable ? new[] { "=", "+=" } : new[] { "=" });
            DialogueRowGUI.ValueField(cols[2], type, property.FindPropertyRelative(nameof(SetVariableAction.value)),
                property.FindPropertyRelative(nameof(SetVariableAction.objectValue)));
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>quest ● [quest ▾] → [state ▾]</c></summary>
    [CustomPropertyDrawer(typeof(SetQuestStateAction))]
    public class SetQuestStateActionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var database = DialogueRowGUI.DatabaseOf(property);
            var cols = DialogueRowGUI.Columns(position, 40f, 0.55f, 18f, 0.45f);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(cols[0], "quest", EditorStyles.miniLabel);
            DialogueNameButton.Draw(cols[1], property.FindPropertyRelative(nameof(SetQuestStateAction.quest)), database, DialogueNameButton.Kind.Quest);
            EditorGUI.LabelField(cols[2], "→");
            EditorGUI.PropertyField(cols[3], property.FindPropertyRelative(nameof(SetQuestStateAction.state)), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }

    /// <summary><c>⚡ ● [event ▾] [argument]</c></summary>
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
            DialogueNameButton.Draw(cols[1], eventName, database, DialogueNameButton.Kind.Event);
            var argument = property.FindPropertyRelative(nameof(RaiseEventAction.argument));
            var hint = database?.FindEvent(eventName.stringValue)?.argumentHint;
            argument.stringValue = DialogueRowGUI.HintField(cols[2], argument.stringValue, string.IsNullOrEmpty(hint) ? "argument" : hint,
                string.IsNullOrEmpty(hint) ? "Argument (optional)" : hint);
            EditorGUI.EndProperty();
        }
    }

    /// <summary>A variable (or a character attribute) on one line: <c>● [name] [type ▾][value] [note]</c>.</summary>
    [CustomPropertyDrawer(typeof(VariableDefinition))]
    public class VariableDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative(nameof(VariableDefinition.initialValue));
            var type = (VariableType)value.FindPropertyRelative(nameof(VariableValue.type)).enumValueIndex;
            var cols = DialogueRowGUI.Columns(position, 10f, 0.32f, 0.5f, 0.18f);
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.DrawRect(new Rect(cols[0].x + 1f, cols[0].y + 5f, 8f, 8f), DialogueVariableStyle.ForType(type));
            var name = property.FindPropertyRelative(nameof(VariableDefinition.name));
            name.stringValue = DialogueRowGUI.HintField(cols[1], name.stringValue, "name");
            EditorGUI.PropertyField(cols[2], value, GUIContent.none);
            var note = property.FindPropertyRelative(nameof(VariableDefinition.description));
            note.stringValue = DialogueRowGUI.HintField(cols[3], note.stringValue, "note", note.stringValue);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>A quest: <c>● [id] starts [state ▾]</c>, then its player-facing title and a note.</summary>
    [CustomPropertyDrawer(typeof(QuestDefinition))]
    public class QuestDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var title = property.FindPropertyRelative(nameof(QuestDefinition.title));
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUI.GetPropertyHeight(title, true) + spacing * 3;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight, spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var cols = DialogueRowGUI.Columns(new Rect(position.x, position.y, position.width, line), 10f, 0.55f, 40f, 0.45f);
            EditorGUI.DrawRect(new Rect(cols[0].x + 1f, cols[0].y + 5f, 8f, 8f), DialogueVariableStyle.QuestColor);
            var name = property.FindPropertyRelative(nameof(QuestDefinition.name));
            name.stringValue = DialogueRowGUI.HintField(cols[1], name.stringValue, "quest id");
            EditorGUI.LabelField(cols[2], "starts", EditorStyles.miniLabel);
            EditorGUI.PropertyField(cols[3], property.FindPropertyRelative(nameof(QuestDefinition.initialState)), GUIContent.none);

            var title = property.FindPropertyRelative(nameof(QuestDefinition.title));
            float th = EditorGUI.GetPropertyHeight(title, true);
            var rest = new Rect(position.x + 14f, position.y + line + spacing, position.width - 14f, th);
            EditorGUI.PropertyField(rest, title, new GUIContent("Title shown to players"), true);
            rest.y += th + spacing;
            rest.height = line;
            var note = property.FindPropertyRelative(nameof(QuestDefinition.description));
            note.stringValue = DialogueRowGUI.HintField(rest, note.stringValue, "note for designers");
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>An event: <c>⚡ [name] [argument hint] [note]</c>.</summary>
    [CustomPropertyDrawer(typeof(EventDefinition))]
    public class EventDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var cols = DialogueRowGUI.Columns(position, 16f, 0.35f, 0.3f, 0.35f);
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var old = GUI.contentColor;
            GUI.contentColor = DialogueVariableStyle.EventColor;
            EditorGUI.LabelField(cols[0], "⚡");
            GUI.contentColor = old;
            var name = property.FindPropertyRelative(nameof(EventDefinition.name));
            name.stringValue = DialogueRowGUI.HintField(cols[1], name.stringValue, "event name");
            var hint = property.FindPropertyRelative(nameof(EventDefinition.argumentHint));
            hint.stringValue = DialogueRowGUI.HintField(cols[2], hint.stringValue, "argument means…", "What the argument means for this event (shown to designers).");
            var note = property.FindPropertyRelative(nameof(EventDefinition.description));
            note.stringValue = DialogueRowGUI.HintField(cols[3], note.stringValue, "note", note.stringValue);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>An emotion: a thumbnail, <c>[id] [portrait]</c> / <c>[tint] Per language…</c>; the per-language overrides fold below.</summary>
    [CustomPropertyDrawer(typeof(ActorEmotion))]
    public class ActorEmotionDrawer : PropertyDrawer
    {
        private const float ThumbSize = 38f;

        private static float TopHeight => Mathf.Max(ThumbSize, EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float h = TopHeight;
            if (!property.isExpanded) return h;
            h += spacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ActorEmotion.localizedPortrait)), true);
            h += spacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ActorEmotion.localizedTint)), true);
            return h + spacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight, spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var portrait = property.FindPropertyRelative(nameof(ActorEmotion.portrait));
            var thumb = new Rect(position.x, position.y, ThumbSize, ThumbSize);
            EditorGUI.DrawRect(thumb, new Color(0f, 0f, 0f, 0.25f));
            if (portrait.objectReferenceValue is Sprite sprite && sprite.texture != null)
            {
                var r = sprite.textureRect;
                var uv = new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(thumb, sprite.texture, uv, true);
            }

            var right = new Rect(thumb.xMax + 6f, position.y, position.width - ThumbSize - 6f, line);
            var top = DialogueRowGUI.Columns(right, 0.4f, 0.6f);
            var id = property.FindPropertyRelative(nameof(ActorEmotion.id));
            id.stringValue = DialogueRowGUI.HintField(top[0], id.stringValue, "emotion id");
            EditorGUI.ObjectField(top[1], portrait, typeof(Sprite), GUIContent.none);
            right.y += line + spacing;
            var bottom = DialogueRowGUI.Columns(right, 0.4f, 0.6f);
            EditorGUI.PropertyField(bottom[0], property.FindPropertyRelative(nameof(ActorEmotion.tint)), GUIContent.none);
            property.isExpanded = EditorGUI.Foldout(bottom[1], property.isExpanded, new GUIContent("Per language…", "Portrait / tint that change with the language (Asset / Color tables)."), true);

            if (property.isExpanded)
            {
                var rest = new Rect(position.x + 12f, position.y + TopHeight + spacing, position.width - 12f, 0f);
                foreach (var name in new[] { nameof(ActorEmotion.localizedPortrait), nameof(ActorEmotion.localizedTint) })
                {
                    var p = property.FindPropertyRelative(name);
                    rest.height = EditorGUI.GetPropertyHeight(p, true);
                    EditorGUI.PropertyField(rest, p, true);
                    rest.y += rest.height + spacing;
                }
            }
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
