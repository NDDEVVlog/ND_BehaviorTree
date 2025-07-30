using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    public class EditorInputDialog : EditorWindow
    {
        // --- FIX: Removed our own 'title' variable ---
        // private string title; 
        private string description;
        private string inputText;
        private string okButtonText;
        private string cancelButtonText;
        private bool wasCancelled;

        public static string Show(string windowTitle, string description, string initialText, string okButton = "OK", string cancelButton = "Cancel")
        {
            var window = GetWindow<EditorInputDialog>(true, windowTitle, true);
            window.minSize = new Vector2(350, 120);
            window.maxSize = new Vector2(350, 120);
            
            // --- FIX: Use the inherited titleContent property ---
            window.titleContent = new GUIContent(windowTitle);
            
            window.description = description;
            window.inputText = initialText;
            window.okButtonText = okButton;
            window.cancelButtonText = cancelButton;
            window.wasCancelled = true;

            window.ShowModal();

            return window.wasCancelled ? initialText : window.inputText;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);

            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);
            GUI.FocusControl("InputField");

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(okButtonText, GUILayout.Width(100)))
            {
                wasCancelled = false;
                Close();
            }

            if (GUILayout.Button(cancelButtonText, GUILayout.Width(100)))
            {
                wasCancelled = true;
                Close();
            }
            EditorGUILayout.EndHorizontal();

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    wasCancelled = false;
                    Close();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    wasCancelled = true;
                    Close();
                }
            }
        }
    }
}