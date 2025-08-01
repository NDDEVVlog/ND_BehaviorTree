using System;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomPropertyDrawer(typeof(NodeEditorMapping))]
    public class NodeEditorMappingDrawer : PropertyDrawer
    {
        private const float PADDING = 5f;
        private const float SPACING = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var boxRect = new Rect(position.x, position.y + SPACING, position.width, GetPropertyHeight(property, label) - PADDING);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            var contentRect = new Rect(boxRect.x + PADDING, boxRect.y + PADDING, boxRect.width - PADDING * 2, EditorGUIUtility.singleLineHeight);

            var nodeScriptProp = property.FindPropertyRelative("nodeScript");
            var editorScriptProp = property.FindPropertyRelative("editorScript");
            var nodeTypeFullNameProp = property.FindPropertyRelative("nodeTypeFullName");
            var editorTypeFullNameProp = property.FindPropertyRelative("editorTypeFullName");

            // --- Draw Node Script Field ---
            contentRect = DrawScriptField(contentRect, nodeScriptProp, nodeTypeFullNameProp, typeof(Node), "Node Type");

            // --- Draw Editor Script Field ---
            contentRect.y += EditorGUIUtility.singleLineHeight + SPACING;
            DrawScriptField(contentRect, editorScriptProp, editorTypeFullNameProp, typeof(ND_NodeEditor), "Editor Type");
            
            // The UXML field is no longer drawn.

            EditorGUI.EndProperty();
        }

        private Rect DrawScriptField(Rect position, SerializedProperty scriptProperty, SerializedProperty fullNameProperty, Type baseType, string label)
        {
            // This method remains the same as before.
            EditorGUI.BeginChangeCheck();
            EditorGUI.ObjectField(position, scriptProperty, typeof(MonoScript), new GUIContent(label));
            if (EditorGUI.EndChangeCheck())
            {
                var monoScript = scriptProperty.objectReferenceValue as MonoScript;
                if (monoScript != null)
                {
                    Type scriptType = monoScript.GetClass();
                    if (scriptType != null && baseType.IsAssignableFrom(scriptType) && !scriptType.IsAbstract)
                    {
                        fullNameProperty.stringValue = scriptType.AssemblyQualifiedName;
                    }
                    else
                    {
                        Debug.LogWarning($"The script '{monoScript.name}' must derive from '{baseType.Name}' and cannot be abstract. Assignment failed.");
                        scriptProperty.objectReferenceValue = null;
                        fullNameProperty.stringValue = "";
                    }
                }
                else { fullNameProperty.stringValue = ""; }
            }
            if (scriptProperty.objectReferenceValue != null && string.IsNullOrEmpty(fullNameProperty.stringValue))
            {
                var errorPosition = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.HelpBox(errorPosition, $"Invalid Script: Must be a non-abstract class derived from {baseType.Name}.", MessageType.Warning);
                position.height += EditorGUIUtility.singleLineHeight * 2;
            }
            return position;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Calculate height for 2 fields instead of 3.
            float height = (EditorGUIUtility.singleLineHeight + SPACING) * 2 + PADDING * 2;
            
            // Warning box logic remains the same.
            var nodeScriptProp = property.FindPropertyRelative("nodeScript");
            var nodeTypeFullNameProp = property.FindPropertyRelative("nodeTypeFullName");
            if (nodeScriptProp.objectReferenceValue != null && string.IsNullOrEmpty(nodeTypeFullNameProp.stringValue))
            {
                 height += EditorGUIUtility.singleLineHeight * 2;
            }
            var editorScriptProp = property.FindPropertyRelative("editorScript");
            var editorTypeFullNameProp = property.FindPropertyRelative("editorTypeFullName");
            if (editorScriptProp.objectReferenceValue != null && string.IsNullOrEmpty(editorTypeFullNameProp.stringValue))
            {
                height += EditorGUIUtility.singleLineHeight * 2;
            }
            return height;
        }
    }
}