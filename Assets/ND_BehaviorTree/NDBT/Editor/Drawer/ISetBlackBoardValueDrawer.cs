// FILE: Editor/ISetBlackBoardValueDrawer.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ND_BehaviorTree; // Your namespace

[CustomPropertyDrawer(typeof(ISetBlackBoardValue), true)]
public class ISetBlackBoardValueDrawer : PropertyDrawer
{
    private static List<Type> _setterTypes;
    private static string[] _typeNames;
    private static Dictionary<string, Type> _typeMap = new Dictionary<string, Type>();

    // Static constructor to find all implementing types once
    static ISetBlackBoardValueDrawer()
    {
        _setterTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => typeof(ISetBlackBoardValue).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();
        
        // Create a mapping from full type name to Type object
        _typeMap = _setterTypes.ToDictionary(t => t.FullName, t => t);

        // Prepend "None" for the dropdown
        _typeNames = new[] { "None (Select a type...)" }.Concat(_setterTypes.Select(t => t.Name)).ToArray();
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
            // Find the index in the display names array (+1 because of "None")
            currentIndex = _setterTypes.FindIndex(t => t == currentType) + 1;
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
                Type selectedType = _setterTypes[newIndex - 1]; // -1 to account for "None"
                property.managedReferenceValue = Activator.CreateInstance(selectedType);
            }
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
            return; // Exit early to avoid drawing fields of the old object
        }

        // --- 3. Draw the Fields of the Concrete Class ---
        if (property.managedReferenceValue != null)
        {
            EditorGUI.indentLevel++;
            
            // Draw a box for clarity
            Rect boxRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, GetPropertyHeight(property, label) - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
            
            // Iterate over the children properties and draw them
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
            totalHeight += 10; // Padding inside the box

            foreach (SerializedProperty child in GetChildren(property))
            {
                totalHeight += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        return totalHeight;
    }
}