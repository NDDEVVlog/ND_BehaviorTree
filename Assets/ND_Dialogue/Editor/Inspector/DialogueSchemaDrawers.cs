using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Shared popup for [DialogueVariable] / [DialogueQuest] string fields: names come from the owning database's
    /// <see cref="DialogueDatabase.Schema"/> (its own lists, or e.g. a blackboard). A value that is not in the schema
    /// stays visible as "⚠ name (missing)". With no names known the field is a plain text box.
    /// </summary>
    public abstract class DialogueSchemaNameDrawer : PropertyDrawer
    {
        protected abstract IEnumerable<string> NamesFrom(IDialogueSchema schema);
        protected abstract string Kind { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, $"[Dialogue{Kind}] needs a string field.");
                return;
            }

            var target = property.serializedObject.targetObject;
            var database = target as DialogueDatabase ?? (target as IDialogueDatabaseOwner)?.Database;
            var names = database != null ? NamesFrom(database.Schema).Distinct().OrderBy(n => n).ToList() : new List<string>();
            if (names.Count == 0)
            {
                EditorGUI.PropertyField(position, property, new GUIContent(label.text, $"No {Kind.ToLowerInvariant()}s are declared: type the name."));
                return;
            }

            string current = property.stringValue ?? string.Empty;
            var options = new List<string>(names);
            int index = options.IndexOf(current);
            if (index < 0)
            {
                options.Insert(0, string.IsNullOrEmpty(current) ? $"(choose a {Kind.ToLowerInvariant()})" : $"⚠ {current} (missing)");
                index = 0;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(position, label.text, index, options.ToArray());
            if (EditorGUI.EndChangeCheck() && names.Contains(options[picked])) property.stringValue = options[picked];
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(DialogueVariableAttribute))]
    public class DialogueVariableDrawer : DialogueSchemaNameDrawer
    {
        protected override IEnumerable<string> NamesFrom(IDialogueSchema schema) => schema.VariableNames;
        protected override string Kind => "Variable";
    }

    [CustomPropertyDrawer(typeof(DialogueQuestAttribute))]
    public class DialogueQuestDrawer : DialogueSchemaNameDrawer
    {
        protected override IEnumerable<string> NamesFrom(IDialogueSchema schema) => schema.QuestNames;
        protected override string Kind => "Quest";
    }

    [CustomPropertyDrawer(typeof(DialogueActorAttribute))]
    public class DialogueActorDrawer : DialogueSchemaNameDrawer
    {
        // Actor names are not part of the schema: read them from the database the schema was taken from.
        private DialogueDatabase m_Database;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = property.serializedObject.targetObject;
            m_Database = target as DialogueDatabase ?? (target as IDialogueDatabaseOwner)?.Database;
            base.OnGUI(position, property, label);
        }

        protected override IEnumerable<string> NamesFrom(IDialogueSchema schema) =>
            m_Database != null ? m_Database.actors.Where(a => a != null && !string.IsNullOrEmpty(a.name)).Select(a => a.name) : Enumerable.Empty<string>();

        protected override string Kind => "Actor";
    }

    [CustomPropertyDrawer(typeof(DialogueConversationAttribute))]
    public class DialogueConversationDrawer : DialogueSchemaNameDrawer
    {
        private DialogueDatabase m_Database;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = property.serializedObject.targetObject;
            m_Database = target as DialogueDatabase ?? (target as IDialogueDatabaseOwner)?.Database;
            base.OnGUI(position, property, label);
        }

        protected override IEnumerable<string> NamesFrom(IDialogueSchema schema) =>
            m_Database != null ? m_Database.conversations.Where(c => c != null && !string.IsNullOrEmpty(c.title)).Select(c => c.title) : Enumerable.Empty<string>();

        protected override string Kind => "Conversation";
    }

    [CustomPropertyDrawer(typeof(DialogueEventAttribute))]
    public class DialogueEventDrawer : DialogueSchemaNameDrawer
    {
        protected override IEnumerable<string> NamesFrom(IDialogueSchema schema) => schema.EventNames;
        protected override string Kind => "Event";
    }
}
