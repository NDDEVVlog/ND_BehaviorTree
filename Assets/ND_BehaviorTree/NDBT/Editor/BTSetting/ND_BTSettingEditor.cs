using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(ND_BehaviorTreeSetting))]
    public class ND_BehaviorTreeSettingEditor : UnityEditor.Editor
    {
        private string _enteredPassword = "";
        private bool _isUnlocked = false;
        private const string UnlockSessionStateKey = "ND_BehaviorTreeSetting_IsUnlocked";

        private SerializedProperty _isLockEnabledProp;
        private SerializedProperty _defaultNodeUXMLProp;
        private SerializedProperty _graphViewStyleProp;
        private SerializedProperty _styleSheetsProp;

        private void OnEnable()
        {
            _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);

            _isLockEnabledProp = serializedObject.FindProperty("isLockEnabled");
            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            _styleSheetsProp = serializedObject.FindProperty("_styleSheets");
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update();

            if (_isLockEnabledProp.boolValue)
            {
                // --- LOCK FEATURE IS ENABLED ---
                HandleLockedState(settings);
            }
            else
            {
                // --- LOCK FEATURE IS DISABLED ---
                HandlePermanentlyUnlockedState();
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void HandleLockedState(ND_BehaviorTreeSetting settings)
        {
            if (!_isUnlocked)
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
                        Debug.Log("ND_BehaviorTree Settings Unlocked for editing.");
                        GUI.FocusControl(null);
                        Repaint(); 
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Incorrect Password", "The password you entered is incorrect.", "OK");
                        Debug.LogWarning("Incorrect password attempt for ND_BehaviorTree Settings.");
                    }
                }
                EditorGUILayout.Space();
            }

            EditorGUI.BeginDisabledGroup(!_isUnlocked);
            DrawSettingsFields();
            EditorGUI.EndDisabledGroup();

            if (_isUnlocked)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Lock Settings (This Session)"))
                {
                    _isUnlocked = false;
                    _enteredPassword = "";
                    SessionState.SetBool(UnlockSessionStateKey, false);
                    GUI.FocusControl(null);
                    Repaint();
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("This will permanently disable the password lock feature, making settings always accessible. You can re-enable it later.", MessageType.Warning);
                if (GUILayout.Button("Permanently Disable Lock"))
                {
                    if (EditorUtility.DisplayDialog("Disable Password Lock?", 
                        "Are you sure you want to disable the password lock? The settings will become permanently editable until the lock is re-enabled.", 
                        "Yes, Disable Lock", "Cancel"))
                    {
                        _isLockEnabledProp.boolValue = false;
                        _isUnlocked = false; // Reset session state as it's no longer relevant
                        SessionState.SetBool(UnlockSessionStateKey, false);
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void HandlePermanentlyUnlockedState()
        {
            EditorGUILayout.HelpBox("Password protection is currently disabled.", MessageType.Info);
            if (GUILayout.Button("Enable Password Lock"))
            {
                 _isLockEnabledProp.boolValue = true;
            }
            EditorGUILayout.Space();
            DrawSettingsFields();
        }

        private void DrawSettingsFields()
        {
            EditorGUILayout.LabelField("UXML Templates", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultNodeUXMLProp, new GUIContent("Default Node UXML"));

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("USS Stylesheets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_graphViewStyleProp, new GUIContent("GraphView Style"));

            EditorGUILayout.Space();
            if (_styleSheetsProp != null)
            {
                EditorGUILayout.PropertyField(_styleSheetsProp, new GUIContent("Node Styles"), true);
            }
            else
            {
                EditorGUILayout.HelpBox("StyleSheets property is null!", MessageType.Error);
            }

            // Show editable message if session is unlocked OR if the lock feature is disabled entirely
            if (_isUnlocked || (_isLockEnabledProp != null && !_isLockEnabledProp.boolValue))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Settings are currently editable.", MessageType.None);
            }
        }
    }
}