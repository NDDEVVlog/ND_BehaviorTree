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
            // Check for developer mode (using the previously discussed EditorDeveloperSettings)
            // If EditorDeveloperSettings doesn't exist or you don't want it, remove these lines.
            // _isCurrentlyInDeveloperMode = EditorDeveloperSettings.Instance.IsDeveloperModeEnabled; // UNCOMMENT IF USING DEV MODE

            // if (_isCurrentlyInDeveloperMode) // UNCOMMENT IF USING DEV MODE
            // {
            //     _isUnlocked = true; // Always unlocked in dev mode
            // }
            // else // UNCOMMENT IF USING DEV MODE
            // {
                _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);
            // } // UNCOMMENT IF USING DEV MODE


            // Cache SerializedProperty references
            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            // Find other properties...
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update(); // Always call this at the beginning

            // --- Optional: Developer Mode Check (uncomment if you implemented EditorDeveloperSettings) ---
            /*
            bool previousDevModeState = _isCurrentlyInDeveloperMode;
            _isCurrentlyInDeveloperMode = EditorDeveloperSettings.Instance.IsDeveloperModeEnabled;

            if (_isCurrentlyInDeveloperMode && !previousDevModeState) { // Dev mode was just enabled
                 _isUnlocked = true; // Automatically unlock
                 Repaint(); // Repaint to reflect change immediately
            } else if (!_isCurrentlyInDeveloperMode && previousDevModeState) { // Dev mode was just disabled
                _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false); // Revert to session state for password
                Repaint();
            }
            */
            // --- End of Optional Developer Mode Check ---


            // --- Password Section (Only if not in dev mode and not unlocked) ---
            // if (!_isCurrentlyInDeveloperMode && !_isUnlocked) // UNCOMMENT IF USING DEV MODE
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

            // --- Settings Fields Section ---
            // Fields are always drawn, but disabled if not unlocked (and not in dev mode)
            // bool enableGUI = _isCurrentlyInDeveloperMode || _isUnlocked; // UNCOMMENT IF USING DEV MODE
            bool enableGUI = _isUnlocked; // SIMPLIFIED: remove _isCurrentlyInDeveloperMode if not using dev mode

            // if (_isCurrentlyInDeveloperMode) // UNCOMMENT IF USING DEV MODE
            // {
            //     EditorGUILayout.HelpBox("Developer Mode: Settings are editable.", MessageType.Warning);
            // }

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

            // Draw other properties you've added and want to lock:
            // SerializedProperty someOtherProp = serializedObject.FindProperty("yourOtherFieldName");
            // EditorGUILayout.PropertyField(someOtherProp, new GUIContent("Your Other Setting"));

            // if (!_isCurrentlyInDeveloperMode && _isUnlocked) // UNCOMMENT IF USING DEV MODE
            if (_isUnlocked) // SIMPLIFIED
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Settings are currently editable. Remember to lock them if needed.", MessageType.None);
            }
        }
    }
}