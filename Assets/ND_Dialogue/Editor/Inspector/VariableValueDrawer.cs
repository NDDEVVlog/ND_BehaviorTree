using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>One line: the type, then the field that type uses (a vector field for Vector3, an object field for Object…).</summary>
    [CustomPropertyDrawer(typeof(VariableValue))]
    public class VariableValueDrawer : PropertyDrawer
    {
        private const float TypeWidth = 78f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var rect = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var typeProp = property.FindPropertyRelative(nameof(VariableValue.type));
            var typeRect = new Rect(rect.x, rect.y, TypeWidth, rect.height);
            var valueRect = new Rect(rect.x + TypeWidth + 2f, rect.y, rect.width - TypeWidth - 2f, rect.height);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            var type = (VariableType)typeProp.enumValueIndex;
            switch (type)
            {
                case VariableType.Bool: Field(valueRect, property, nameof(VariableValue.boolValue)); break;
                case VariableType.Int: Field(valueRect, property, nameof(VariableValue.intValue)); break;
                case VariableType.Float: Field(valueRect, property, nameof(VariableValue.floatValue)); break;
                case VariableType.String: Field(valueRect, property, nameof(VariableValue.stringValue)); break;
                case VariableType.Color: Field(valueRect, property, nameof(VariableValue.colorValue)); break;
                case VariableType.Object:
                {
                    var objProp = property.FindPropertyRelative(nameof(VariableValue.objectValue));
                    EditorGUI.ObjectField(valueRect, objProp, typeof(Object), GUIContent.none);
                    break;
                }
                case VariableType.Vector2:
                case VariableType.Vector3:
                {
                    var vec = property.FindPropertyRelative(nameof(VariableValue.vectorValue));
                    EditorGUI.BeginChangeCheck();
                    Vector4 v = vec.vector4Value;
                    if (type == VariableType.Vector2) v = EditorGUI.Vector2Field(valueRect, GUIContent.none, v);
                    else v = EditorGUI.Vector3Field(valueRect, GUIContent.none, v);
                    if (EditorGUI.EndChangeCheck()) vec.vector4Value = v;
                    break;
                }
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void Field(Rect rect, SerializedProperty property, string name) =>
            EditorGUI.PropertyField(rect, property.FindPropertyRelative(name), GUIContent.none);
    }
}
