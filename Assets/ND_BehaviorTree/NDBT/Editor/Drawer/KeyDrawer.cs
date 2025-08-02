// FILE: Editor/KeyDrawer.cs

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomPropertyDrawer(typeof(Key), true)]
    public class KeyDrawer : PropertyDrawer
    {
        // Define a constant for the spacing to keep it consistent.
        private const float k_VerticalSpacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            BehaviorTree tree = FindBehaviorTreeFromProperty(property);

            if (tree == null || tree.blackboard == null)
            {
                var helpRect = position;
                helpRect.height = EditorGUIUtility.singleLineHeight * 2;
                EditorGUI.HelpBox(helpRect, "No Blackboard found on parent Behavior Tree.", MessageType.Warning);
                EditorGUI.EndProperty();
                return;
            }

            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            Type requiredType = keyTypeAttribute?.RequiredType;

            var allKeys = tree.blackboard.keys.Where(k => k != null).ToList();
            var validKeys = requiredType != null 
                ? allKeys.Where(k => k.GetValueType() == requiredType || (k.GetValueType().IsSubclassOf(requiredType))).ToList()
                : allKeys;

            if (validKeys.Count == 0)
            {
                // --- THIS BLOCK IS MODIFIED ---

                // 1. Draw the main label for the field (e.g., "Float Key")
                var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(labelRect, label);

                // 2. Calculate the position for the help box below the label, with added spacing.
                var helpRect = new Rect(
                    position.x, 
                    position.y + EditorGUIUtility.singleLineHeight + k_VerticalSpacing, // <-- ADDED SPACE HERE
                    position.width, 
                    EditorGUIUtility.singleLineHeight * 2
                );

                // 3. Draw the help box.
                string typeName = requiredType != null ? requiredType.Name : "any";
                EditorGUI.HelpBox(helpRect, $"No keys of type '{typeName}' found in the Blackboard.", MessageType.Warning);

                // --- END OF MODIFICATION ---
                
                EditorGUI.EndProperty();
                return;
            }

            // --- This part remains unchanged ---
            var keyNames = validKeys.Select(k => k.keyName).ToList();
            keyNames.Insert(0, "[None]");

            var currentKey = property.objectReferenceValue as Key;
            int currentIndex = currentKey != null ? validKeys.FindIndex(k => k == currentKey) + 1 : 0;
            
            Rect popupPosition = EditorGUI.PrefixLabel(position, label);
            int newIndex = EditorGUI.Popup(popupPosition, currentIndex, keyNames.ToArray());

            if (newIndex != currentIndex)
            {
                property.objectReferenceValue = (newIndex == 0) ? null : validKeys[newIndex - 1];
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            BehaviorTree tree = FindBehaviorTreeFromProperty(property);
            if (tree == null || tree.blackboard == null)
            {
                return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
            }

            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            Type requiredType = keyTypeAttribute?.RequiredType;
            var allKeys = tree.blackboard.keys.Where(k => k != null).ToList();
            var validKeys = requiredType != null 
                ? allKeys.Where(k => k.GetValueType() == requiredType || (k.GetValueType().IsSubclassOf(requiredType))).ToList()
                : allKeys;
            
            // --- THIS BLOCK IS MODIFIED TO ACCOUNT FOR THE NEW SPACE ---
            if (validKeys.Count == 0)
            {
                // Height Breakdown:
                // 1. Height of the label field.
                // 2. The custom vertical space we added.
                // 3. Height of the 2-line help box.
                // 4. Standard spacing after the entire control.
                return EditorGUIUtility.singleLineHeight + k_VerticalSpacing + (EditorGUIUtility.singleLineHeight * 2) + EditorGUIUtility.standardVerticalSpacing;
            }
            // --- END OF MODIFICATION ---

            return base.GetPropertyHeight(property, label);
        }

        private BehaviorTree FindBehaviorTreeFromProperty(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target is BehaviorTree tree)
            {
                return tree;
            }
            if (target is Node node)
            {
                string assetPath = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    return AssetDatabase.LoadMainAssetAtPath(assetPath) as BehaviorTree;
                }
            }
            return null;
        }
    }
}