// File: ND_DTSettingEditor.cs
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(ND_BehaviorTreeSetting))]
    public class ND_DTSettingEditor : UnityEditor.Editor
    {
        private string _enteredPassword = "";
        private bool _isUnlocked = false;
        private const string UnlockSessionStateKey = "ND_DrawTrelloSetting_IsUnlocked";

        private SerializedProperty _defaultNodeUXMLProp;
        private SerializedProperty _graphViewStyleProp;
        // Add SerializedProperty fields for any other settings you want to control

        private bool _isCurrentlyInDeveloperMode = false; // For the developer mode bypass

        private void OnEnable()
        {

                _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);



            // Cache SerializedProperty references
            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            // Find other properties...
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update(); // Always call this at the beginning


            if (!_isUnlocked) // SIMPLIFIED: remove !_isCurrentlyInDeveloperMode if not using dev mode
            {
                EditorGUILayout.HelpBox("Enter the password to enable editing.", MessageType.Info);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Password:", GUILayout.Width(70));
                _enteredPassword = EditorGUILayout.PasswordField(_enteredPassword);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Unlock Settings"))
                {
                    if (_enteredPassword == settings.enableSettingPassword)
                    {
                        _isUnlocked = true;
                        SessionState.SetBool(UnlockSessionStateKey, true);
                        _enteredPassword = ""; 
                        Debug.Log("ND_DrawTrello Settings Unlocked for editing.");
                        GUI.FocusControl(null); // Remove focus from password field
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Incorrect Password", "The password you entered is incorrect.", "OK");
                        Debug.LogWarning("Incorrect password attempt for ND_DrawTrello Settings.");
                    }
                }
                EditorGUILayout.Space();
            }

           
            bool enableGUI = _isUnlocked; // SIMPLIFIED: remove _isCurrentlyInDeveloperMode if not using dev mode


            EditorGUI.BeginDisabledGroup(!enableGUI); // If enableGUI is false, fields will be disabled (readonly)
            {
                DrawSettingsFields();
            }
            EditorGUI.EndDisabledGroup();


            // --- Lock Button (Only if unlocked and not in dev mode) ---
            // if (_isUnlocked && !_isCurrentlyInDeveloperMode) // UNCOMMENT IF USING DEV MODE
            if (_isUnlocked) // SIMPLIFIED: remove !_isCurrentlyInDeveloperMode if not using dev mode
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Lock Settings"))
                {
                    _isUnlocked = false;
                    _enteredPassword = ""; 
                    SessionState.SetBool(UnlockSessionStateKey, false);
                    GUI.FocusControl(null); 
                }
            }
            
            serializedObject.ApplyModifiedProperties(); // Always call this at the end
        }

        private void DrawSettingsFields()
        {
            EditorGUILayout.PropertyField(_defaultNodeUXMLProp, new GUIContent("Default Node UXML"));
            EditorGUILayout.PropertyField(_graphViewStyleProp, new GUIContent("GraphView StyleSheet"));

            if (_isUnlocked) // SIMPLIFIED
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Settings are currently editable. Remember to lock them if needed.", MessageType.None);
            }
        }
    }
}