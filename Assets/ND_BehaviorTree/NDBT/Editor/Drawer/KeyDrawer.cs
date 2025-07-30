using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A custom property drawer for any field of type 'Key'. It automatically
    /// creates a dropdown populated with keys from the BehaviorTree's Blackboard.
    /// It also respects the [BlackboardKeyType] attribute to filter the list.
    /// </summary>
    [CustomPropertyDrawer(typeof(Key))]
    public class KeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- 1. Find the BehaviorTree and Blackboard ---
            // This is the tricky part. We need to find the main asset from the property's context.
            BehaviorTree tree = FindBehaviorTreeFromProperty(property);

            if (tree == null || tree.blackboard == null)
            {
                // Fallback to default field if no blackboard is found.
                EditorGUI.PropertyField(position, property, label);
                // Optionally show a help box below.
                var helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(helpRect, "No Blackboard found on the parent Behavior Tree asset.", MessageType.Warning);
                return;
            }

            // --- 2. Check for the filtering attribute and get the list of valid keys ---
            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            Type requiredType = keyTypeAttribute?.RequiredType;

            var allKeys = tree.blackboard.keys.Where(k => k != null).ToList();
            var validKeys = requiredType != null 
                ? allKeys.Where(k => k.GetValueType() == requiredType).ToList()
                : allKeys;

            if (validKeys.Count == 0)
            {
                EditorGUI.LabelField(position, label);
                var helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight * 2);
                string typeName = requiredType != null ? requiredType.Name : "any";
                EditorGUI.HelpBox(helpRect, $"No keys of type '{typeName}' found in the Blackboard.", MessageType.Warning);
                EditorGUI.EndProperty();
                return;
            }

            // --- 3. Prepare data for the popup dropdown ---
            var keyNames = validKeys.Select(k => k.keyName).ToList();
            keyNames.Insert(0, "[None]");

            // --- 4. Find the index of the currently selected key ---
            var currentKey = property.objectReferenceValue as Key;
            int currentIndex = currentKey != null ? validKeys.FindIndex(k => k == currentKey) + 1 : 0;
            
            // --- 5. Draw the popup field ---
            int newIndex = EditorGUI.Popup(position, label, currentIndex, keyNames.Select(s => new GUIContent(s)).ToArray());

            // --- 6. Update the property if the selection changed ---
            if (newIndex != currentIndex)
            {
                property.objectReferenceValue = (newIndex == 0) ? null : validKeys[newIndex - 1];
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // If we need to show a help box, reserve space for it.
            BehaviorTree tree = FindBehaviorTreeFromProperty(property);
            if (tree == null || tree.blackboard == null)
            {
                return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
            }

            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            Type requiredType = keyTypeAttribute?.RequiredType;
            if (tree.blackboard.keys.All(k => k == null || (requiredType != null && k.GetValueType() != requiredType)))
            {
                return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing;
            }

            return base.GetPropertyHeight(property, label);
        }

        private BehaviorTree FindBehaviorTreeFromProperty(SerializedProperty property)
        {
            // The serializedObject.targetObject could be a Node or the BehaviorTree itself.
            var target = property.serializedObject.targetObject;
            if (target is BehaviorTree tree)
            {
                return tree;
            }
            if (target is Node node)
            {
                // If the target is a node, get its main asset path.
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