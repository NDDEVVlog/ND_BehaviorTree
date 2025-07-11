

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(Blackboard))]
    public class BlackboardEditor : UnityEditor.Editor
    {
        private Blackboard blackboard;
        private SerializedProperty keysProperty;
        private static Dictionary<Type, Color> cachedKeyColors = new Dictionary<Type, Color>();

        private void OnEnable()
        {
            blackboard = (Blackboard)target;
            keysProperty = serializedObject.FindProperty("keys");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Blackboard Keys", EditorStyles.boldLabel);
            if (GUILayout.Button("Add New Key by Type"))
            {
                ShowAddKeyMenu();
            }
            EditorGUILayout.Space();

            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                SerializedProperty keyProp = keysProperty.GetArrayElementAtIndex(i);
                Key key = keyProp.objectReferenceValue as Key;

                if (key == null)
                {
                    EditorGUILayout.HelpBox("This key is missing or has been deleted. Please remove this entry.", MessageType.Warning);
                    if (GUILayout.Button("Remove Missing Entry"))
                    {
                        keysProperty.DeleteArrayElementAtIndex(i);
                        EditorUtility.SetDirty(blackboard);
                        break; 
                    }
                    continue;
                }

                SerializedObject keySerializedObject = new SerializedObject(key);
                keySerializedObject.Update();

                Color guiColor = GetColorForKey(key.GetType());
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = guiColor;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = originalColor;
                EditorGUILayout.BeginHorizontal();

                // --- MODIFIED: Key Name is now a button to trigger rename ---
                if (GUILayout.Button(new GUIContent(key.keyName, "Click to rename this key"), GUI.skin.label, GUILayout.Width(150)))
                {
                    // Pass the key and its index to the rename function
                    ShowRenameKeyWindow(key, i);
                }

                string typeLabel = key.GetType().Name.Replace("Key", "");
                EditorGUILayout.LabelField(new GUIContent(typeLabel, "The type of this key."), GUILayout.Width(60));

                DrawValueField(key, keySerializedObject);

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    RemoveKey(i);
                    break; // Exit loop as collection was modified
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                keySerializedObject.ApplyModifiedProperties();
            }

            serializedObject.ApplyModifiedProperties();
        }
        
        // --- NEW/REFACTORED ACTION METHODS ---

        private void ShowAddKeyMenu()
        {
            GenericMenu menu = new GenericMenu();
            Type baseType = typeof(Key);
            IEnumerable<Type> keyTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && t != typeof(Key<>));

            foreach (Type type in keyTypes)
            {
                string menuPath = type.Name.Replace("Key", "");
                menu.AddItem(new GUIContent(menuPath), false, () =>
                {
                    // Open the name dialog window
                    string initialName = FindUniqueName($"New {menuPath}");
                    EnterKeyNameWindow.ShowWindow(initialName, (newName) => {
                        AddKey(type, newName);
                    }, IsNameValid);
                });
            }
            menu.ShowAsContext();
        }

        private void AddKey(Type keyType, string keyName)
        {
            serializedObject.Update();
            Undo.RecordObject(blackboard, "Add New Key");

            Key newKey = (Key)ScriptableObject.CreateInstance(keyType);
            newKey.keyName = keyName;
            newKey.name = $"{keyName} ({keyType.Name})";

            AssetDatabase.AddObjectToAsset(newKey, blackboard);

            keysProperty.arraySize++;
            SerializedProperty newKeyProperty = keysProperty.GetArrayElementAtIndex(keysProperty.arraySize - 1);
            newKeyProperty.objectReferenceValue = newKey;
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssets(); // Safe to save here, as we are not in the middle of a loop
        }

        private void ShowRenameKeyWindow(Key oldKey, int index)
        {
            EnterKeyNameWindow.ShowWindow(oldKey.keyName, (newName) => {
                RenameKey(oldKey, index, newName);
            }, (name) => {
                // Name is valid if it's the same as the old one, or if it's unique
                return name == oldKey.keyName || IsNameValid(name);
            });
        }

        private void RenameKey(Key oldKey, int index, string newName)
        {
            // If the name hasn't changed, do nothing.
            if (oldKey.keyName == newName) return;

            serializedObject.Update();
            Undo.RecordObject(blackboard, "Rename Key");

            // 1. Get the old value
            object oldValue = oldKey.GetValueObject();

            // 2. Remove the old key asset completely
            AssetDatabase.RemoveObjectFromAsset(oldKey);
            Undo.DestroyObjectImmediate(oldKey);

            // 3. Create a new key with the new name
            Key newKey = (Key)ScriptableObject.CreateInstance(oldKey.GetType());
            newKey.keyName = newName;
            newKey.name = $"{newName} ({oldKey.GetType().Name})";
            newKey.SetValueObject(oldValue); // 4. Set the copied value

            AssetDatabase.AddObjectToAsset(newKey, blackboard);

            // 5. Assign the new key to the same spot in the list
            keysProperty.GetArrayElementAtIndex(index).objectReferenceValue = newKey;
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssets();
        }
        
        private void RemoveKey(int index)
        {
            serializedObject.Update();
            Key keyToRemove = keysProperty.GetArrayElementAtIndex(index).objectReferenceValue as Key;
            
            Undo.RecordObject(blackboard, "Remove Key");

            if (keyToRemove != null)
            {
                AssetDatabase.RemoveObjectFromAsset(keyToRemove);
                Undo.DestroyObjectImmediate(keyToRemove);
            }

            // This is safer than DeleteArrayElementAtIndex when also removing assets
            keysProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
            keysProperty.DeleteArrayElementAtIndex(index);
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssets();
        }
        
        // --- HELPER METHODS ---

        private bool IsNameValid(string name)
        {
            return !blackboard.keys.Any(k => k != null && k.keyName == name);
        }

        private string FindUniqueName(string baseName)
        {
            string name = baseName;
            int counter = 1;
            while (!IsNameValid(name))
            {
                name = $"{baseName} ({counter})";
                counter++;
            }
            return name;
        }

        private void DrawValueField(Key key, SerializedObject keySerializedObject)
        {
            SerializedProperty valueProp = keySerializedObject.FindProperty("value");
            if (valueProp == null) { EditorGUILayout.LabelField("No 'value' field found."); return; }
            // Make the value field expand to fill available space
            EditorGUILayout.PropertyField(valueProp, GUIContent.none, true, GUILayout.ExpandWidth(true));
        }
        
        public static Color GetColorForKey(Type keyType)
        {
            if (keyType == null) return Color.grey;
            if (cachedKeyColors.TryGetValue(keyType, out Color cachedColor)) { return cachedColor; }
            KeyColorAttribute colorAttribute = keyType.GetCustomAttribute<KeyColorAttribute>();
            if (colorAttribute != null)
            {
                Color foundColor = colorAttribute.Color;
                cachedKeyColors[keyType] = foundColor;
                return foundColor;
            }
            cachedKeyColors[keyType] = Color.white;
            return Color.white;
        }
    }
}