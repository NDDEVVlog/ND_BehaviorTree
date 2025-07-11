
using UnityEditor;
using UnityEngine;
using System;

namespace ND_BehaviorTree.Editor
{
    public class EnterKeyNameWindow : EditorWindow
    {
        private string keyName = "New Key";
        private Action<string> onConfirm;
        private Func<string, bool> isNameValid;

        public static void ShowWindow(string initialName, Action<string> onConfirmCallback, Func<string, bool> validationCallback)
        {
            EnterKeyNameWindow window = GetWindow<EnterKeyNameWindow>(true, "Enter Key Name", true);
            window.minSize = new Vector2(300, 80);
            window.maxSize = new Vector2(300, 80);
            window.keyName = initialName;
            window.onConfirm = onConfirmCallback;
            window.isNameValid = validationCallback;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Please enter a unique name for the key:");
            
            GUI.SetNextControlName("NameField");
            keyName = EditorGUILayout.TextField(keyName);
            
            // Set focus to the text field when the window opens
            if (Event.current.type == EventType.Layout)
            {
                 EditorGUI.FocusTextInControl("NameField");
            }

            EditorGUILayout.BeginHorizontal();

            // Disable confirm button if the name is invalid (e.g., empty or duplicate)
            bool nameIsValid = !string.IsNullOrWhiteSpace(keyName) && (isNameValid?.Invoke(keyName) ?? true);
            EditorGUI.BeginDisabledGroup(!nameIsValid);

            if (GUILayout.Button("Confirm"))
            {
                onConfirm?.Invoke(keyName);
                Close();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
            
            // Handle the 'Enter' key to confirm
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && nameIsValid)
            {
                onConfirm?.Invoke(keyName);
                Close();
            }
        }
    }
}