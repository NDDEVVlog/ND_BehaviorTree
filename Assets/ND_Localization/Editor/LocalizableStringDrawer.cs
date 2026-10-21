using UnityEditor;
using UnityEngine;

namespace ND.Localization.Editor
{
    /// <summary>
    /// Plain text box, plus the table/key picker below it to connect the text to a table later.
    /// While connected, the plain text is shown as a writer's note and the translation is what the game uses.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizableString))]
    public class LocalizableStringDrawer : PropertyDrawer
    {
        private const float TextLines = 3f;
        private readonly LocalizedReferenceDrawer m_Reference = new LocalizedReferenceDrawer();

        private static bool IsLocalized(SerializedProperty property) =>
            property.FindPropertyRelative("m_Table").objectReferenceValue != null;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return line * TextLines + EditorGUIUtility.standardVerticalSpacing + m_Reference.GetPropertyHeight(property, new GUIContent("Localize"));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var textProp = property.FindPropertyRelative("m_Text");
            float textHeight = EditorGUIUtility.singleLineHeight * TextLines;
            bool localized = IsLocalized(property);

            var textRect = new Rect(position.x, position.y, position.width, textHeight);
            var labelRect = new Rect(textRect.x, textRect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, new GUIContent(label.text + (localized ? " (note)" : string.Empty),
                localized ? "Connected to a table: the game shows the translation. This text is a note for writers." : label.tooltip));
            var areaRect = new Rect(textRect.x + EditorGUIUtility.labelWidth, textRect.y, textRect.width - EditorGUIUtility.labelWidth, textHeight);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.TextArea(areaRect, textProp.stringValue);
            if (EditorGUI.EndChangeCheck()) textProp.stringValue = text;

            var referenceRect = new Rect(position.x, textRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width,
                position.height - textHeight - EditorGUIUtility.standardVerticalSpacing);
            m_Reference.OnGUI(referenceRect, property, new GUIContent(localized ? "Localized" : "Localize", "Pick a table and key to translate this text."));
        }
    }
}
