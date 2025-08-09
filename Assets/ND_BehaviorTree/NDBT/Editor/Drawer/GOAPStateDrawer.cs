using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;

namespace ND_BehaviorTree.GOAP.Editor
{
    [CustomPropertyDrawer(typeof(GOAPState))]
    public class GOAPStateDrawer : PropertyDrawer
    {
        private const float PADDING = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- 1. Find the Blackboard ---
            // This is a bit complex as drawers don't have direct context. We find the main asset.
            BehaviorTree tree = null;
            if (property.serializedObject.targetObject is Node node)
            {
                 string assetPath = AssetDatabase.GetAssetPath(node);
                 if (!string.IsNullOrEmpty(assetPath))
                 {
                    tree = AssetDatabase.LoadMainAssetAtPath(assetPath) as BehaviorTree;
                 }
            }
            
            // --- 2. Get all the sub-properties we need ---
            var keyNameProp = property.FindPropertyRelative("key");
            var valueTypeProp = property.FindPropertyRelative("valueType");
            var comparisonProp = property.FindPropertyRelative("comparison");

            // --- 3. Define Rects for our controls ---
            var keyRect = new Rect(position.x, position.y, position.width * 0.3f - PADDING, EditorGUIUtility.singleLineHeight);
            var comparisonRect = new Rect(keyRect.xMax + PADDING, position.y, position.width * 0.25f - PADDING, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(comparisonRect.xMax + PADDING, position.y, position.width * 0.45f, EditorGUIUtility.singleLineHeight);
            
            // --- 4. Draw the Key Dropdown ---
            if (tree != null && tree.blackboard != null && tree.blackboard.keys.Count > 0)
            {
                var allKeys = tree.blackboard.keys.Where(k => k != null).ToList();
                var keyNames = allKeys.Select(k => k.keyName).ToArray();
                int currentIndex = Array.IndexOf(keyNames, keyNameProp.stringValue);

                int newIndex = EditorGUI.Popup(keyRect, currentIndex, keyNames);

                if (newIndex != currentIndex)
                {
                    // User selected a new key. Update the key name property.
                    keyNameProp.stringValue = keyNames[newIndex];
                    
                    // IMPORTANT: Automatically set the valueType based on the selected Key's type!
                    var selectedKey = allKeys[newIndex];
                    var keyType = selectedKey.GetValueType();

                    if (keyType == typeof(bool)) valueTypeProp.enumValueIndex = (int)GOAPValueType.Bool;
                    else if (keyType == typeof(int)) valueTypeProp.enumValueIndex = (int)GOAPValueType.Int;
                    else if (keyType == typeof(float)) valueTypeProp.enumValueIndex = (int)GOAPValueType.Float;
                    else if (keyType == typeof(GameObject)) valueTypeProp.enumValueIndex = (int)GOAPValueType.GameObject;
                    else if (keyType == typeof(string)) valueTypeProp.enumValueIndex = (int)GOAPValueType.String;
                    // Add other types here
                }
            }
            else
            {
                // Fallback to a simple text field if no blackboard is found
                EditorGUI.PropertyField(keyRect, keyNameProp, GUIContent.none);
            }

            // --- 5. Draw the Comparison and Value fields based on the now-correct valueType ---
            GOAPValueType currentType = (GOAPValueType)valueTypeProp.enumValueIndex;
            
            // Hide comparison for types that don't need it (Bool, GameObject, etc.)
            if (currentType == GOAPValueType.Bool || currentType == GOAPValueType.GameObject || currentType == GOAPValueType.String)
            {
                comparisonProp.enumValueIndex = (int)GOAPComparisonType.IsEqualTo; // Force equality
                // Redraw value rect to take up the empty space
                valueRect.x = comparisonRect.x;
                valueRect.width += comparisonRect.width + PADDING;
            }
            else
            {
                EditorGUI.PropertyField(comparisonRect, comparisonProp, GUIContent.none);
            }

            // Draw the correct value field
            string valueFieldName = GOAPValueHelper.GetValueFieldName(currentType);
            if (!string.IsNullOrEmpty(valueFieldName))
            {
                var valueProp = property.FindPropertyRelative(valueFieldName);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);
            }
            else
            {
                EditorGUI.LabelField(valueRect, "N/A");
            }
            
            EditorGUI.EndProperty();
        }
    }
}