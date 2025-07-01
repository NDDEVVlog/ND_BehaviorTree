// --- File: Assets/Editor/BlackboardEditor.cs ---

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

// This tells Unity to use this class to draw the Inspector for Blackboard objects.
[CustomEditor(typeof(Blackboard))]
public class BlackboardEditor : Editor
{
    private Blackboard blackboard;
    private SerializedProperty keysProperty;

    // This is called when the Inspector is first opened.
    private void OnEnable()
    {
        // Get the target object (the Blackboard asset being inspected).
        blackboard = (Blackboard)target;
        
        // Find the "keys" list property so we can modify it.
        keysProperty = serializedObject.FindProperty("keys");
    }

    // This is where we draw the custom Inspector GUI.
    public override void OnInspectorGUI()
    {
        // Update the serializedObject to match the actual object.
        serializedObject.Update();

        // Draw the default list of keys. This is useful for reordering or seeing what you have.
        EditorGUILayout.PropertyField(keysProperty, true);

        EditorGUILayout.Space(); // Add a little visual separation.

        // Draw our custom "Add New Key" button.
        if (GUILayout.Button("Add New Key by Type"))
        {
            ShowAddKeyMenu();
        }

        // Apply any changes made in the Inspector to the actual object.
        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddKeyMenu()
    {
        // Create a new dropdown menu.
        GenericMenu menu = new GenericMenu();

        // Find all types that inherit from Key and are not abstract.
        Type baseType = typeof(Key);
        IEnumerable<Type> keyTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract);

        // Populate the menu with all found key types.
        foreach (Type type in keyTypes)
        {
            // The menu path will be something like "Float" or "Int"
            string menuPath = type.Name.Replace("Key", ""); 
            
            // Add an item to the menu. When clicked, it calls the `AddKey` function with the selected type.
            menu.AddItem(new GUIContent(menuPath), false, () => AddKey(type));
        }

        // Display the menu.
        menu.ShowAsContext();
    }

    private void AddKey(Type keyType)
    {
        // Start recording an undo action. This is good practice.
        Undo.RecordObject(blackboard, "Add New Key");

        // 1. Create an instance of the selected Key type.
        Key newKey = (Key)ScriptableObject.CreateInstance(keyType);
        newKey.name = $"New {keyType.Name}";

        // 2. Add the new key as a sub-asset to the blackboard.
        // This keeps the project clean, nesting the key inside the blackboard asset.
        AssetDatabase.AddObjectToAsset(newKey, blackboard);

        // 3. Add the new key to the blackboard's list.
        keysProperty.arraySize++;
        SerializedProperty newKeyProperty = keysProperty.GetArrayElementAtIndex(keysProperty.arraySize - 1);
        newKeyProperty.objectReferenceValue = newKey;

        // 4. Save all changes and mark the blackboard as "dirty" to ensure changes are saved.
        EditorUtility.SetDirty(blackboard);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Make sure the new key is visible in the Inspector immediately.
        serializedObject.ApplyModifiedProperties();
    }
}