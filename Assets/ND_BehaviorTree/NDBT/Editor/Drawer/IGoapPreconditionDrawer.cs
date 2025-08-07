// FILE: Editor/IGoapPreconditionDrawer.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.GOAP.Editor
{
    [CustomPropertyDrawer(typeof(IGoapPrecondition), true)]
    public class IGoapPreconditionDrawer : PropertyDrawer
    {
        private static List<Type> _preconditionTypes;
        private static string[] _typeNames;
        private static Dictionary<string, Type> _typeMap = new Dictionary<string, Type>();

        // Static constructor to find all implementing types once when the editor loads
        static IGoapPreconditionDrawer()
        {
            _preconditionTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => typeof(IGoapPrecondition).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
            
            _typeMap = _preconditionTypes.ToDictionary(t => t.FullName, t => t);

            // Prepend "None" for the dropdown and get user-friendly names
            _typeNames = new[] { "None (Select a Precondition)" }
                .Concat(_preconditionTypes.Select(t => ObjectNames.NicifyVariableName(t.Name)))
                .ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- 1. Draw the Type Selection Dropdown ---
            Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            int currentIndex = 0;
            string currentTypeName = property.managedReferenceFullTypename;

            if (!string.IsNullOrEmpty(currentTypeName) && _typeMap.ContainsKey(currentTypeName))
            {
                Type currentType = _typeMap[currentTypeName];
                // Find the index in our list (+1 because of "None")
                currentIndex = _preconditionTypes.FindIndex(t => t == currentType) + 1;
            }

            int newIndex = EditorGUI.Popup(dropdownRect, label.text, currentIndex, _typeNames);
            
            // --- 2. Handle Type Change ---
            if (newIndex != currentIndex)
            {
                if (newIndex == 0) // "None" was selected
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    // Create a new instance of the selected type
                    Type selectedType = _preconditionTypes[newIndex - 1]; // -1 to account for "None"
                    property.managedReferenceValue = Activator.CreateInstance(selectedType);
                }
                property.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndProperty();
                return; // Exit to avoid drawing fields of the old object this frame
            }

            // --- 3. Draw the Fields of the Concrete Class ---
            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                
                // Draw a box for visual clarity
                Rect boxRect = new Rect(position.x, 
                                        position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, 
                                        position.width, 
                                        GetPropertyHeight(property, label) - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing);
                
                GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
                
                // Iterate over the child properties and draw them inside the box
                var fieldRect = new Rect(boxRect.x + 5, boxRect.y + 5, boxRect.width - 10, EditorGUIUtility.singleLineHeight);
                
                foreach (SerializedProperty child in GetChildren(property))
                {
                    fieldRect.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(fieldRect, child, true);
                    fieldRect.y += fieldRect.height + EditorGUIUtility.standardVerticalSpacing;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // Helper to get all visible children of a SerializedProperty
        private IEnumerable<SerializedProperty> GetChildren(SerializedProperty property)
        {
            var p = property.Copy();
            var end = property.GetEndProperty();
            p.NextVisible(true);
            while (!SerializedProperty.EqualContents(p, end))
            {
                yield return p.Copy();
                if (!p.NextVisible(false))
                    break;
            }
        }
    
        // Calculate the total height needed for the drawer
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalHeight = EditorGUIUtility.singleLineHeight; // For the dropdown

            if (property.managedReferenceValue != null)
            {
                totalHeight += EditorGUIUtility.standardVerticalSpacing; // Space before the box
                totalHeight += 10; // Padding inside the box (5 top, 5 bottom)

                foreach (SerializedProperty child in GetChildren(property))
                {
                    totalHeight += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            return totalHeight;
        }
    }
}