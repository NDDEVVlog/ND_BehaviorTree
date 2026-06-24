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
        private SerializedProperty _activeConfigProp;
        private SerializedProperty _defaultNodeUXMLProp;
        private SerializedProperty _defaultNodeStyleProp;
        private SerializedProperty _graphViewStyleProp;
        private SerializedProperty _graphBackgroundColorProp;
        private SerializedProperty _gridColorProp;
        private SerializedProperty _edgeThemeProp;
        private SerializedProperty _portThemeProp;
        private SerializedProperty _blackboardThemeProp;

        private void OnEnable()
        {
            _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);

            _isLockEnabledProp = serializedObject.FindProperty("isLockEnabled");
            _activeConfigProp = serializedObject.FindProperty("activeConfig");
            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _defaultNodeStyleProp = serializedObject.FindProperty("defaultNodeStyle");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            _graphBackgroundColorProp = serializedObject.FindProperty("graphBackgroundColor");
            _gridColorProp = serializedObject.FindProperty("gridColor");
            _edgeThemeProp = serializedObject.FindProperty("edgeTheme");
            _portThemeProp = serializedObject.FindProperty("portTheme");
            _blackboardThemeProp = serializedObject.FindProperty("blackboardTheme");
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update();

            if (_isLockEnabledProp.boolValue)
                HandleLockedState(settings);
            else
                HandlePermanentlyUnlockedState();
            
            serializedObject.ApplyModifiedProperties();
        }

        private void HandleLockedState(ND_BehaviorTreeSetting settings)
        {
            if (!_isUnlocked)
            {
                EditorGUILayout.HelpBox("Enter password to enable editing.", MessageType.Info);
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
                        GUI.FocusControl(null);
                        Repaint(); 
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Incorrect password.", "OK");
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
                if (GUILayout.Button("Permanently Disable Lock"))
                {
                    if (EditorUtility.DisplayDialog("Disable Lock?", "Disable password permanently?", "Yes", "Cancel"))
                    {
                        _isLockEnabledProp.boolValue = false;
                        _isUnlocked = false; 
                        SessionState.SetBool(UnlockSessionStateKey, false);
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void HandlePermanentlyUnlockedState()
        {
            EditorGUILayout.HelpBox("Password protection disabled.", MessageType.Info);
            if (GUILayout.Button("Enable Password Lock")) _isLockEnabledProp.boolValue = true;
            EditorGUILayout.Space();
            DrawSettingsFields();
        }

        private void DrawSettingsFields()
        {
            EditorGUILayout.LabelField("Core Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_activeConfigProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global UI Templates", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultNodeUXMLProp);
            EditorGUILayout.PropertyField(_defaultNodeStyleProp);
            EditorGUILayout.PropertyField(_graphViewStyleProp);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Graph Theme", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_graphBackgroundColorProp);
            EditorGUILayout.PropertyField(_gridColorProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Component Themes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_edgeThemeProp);
            EditorGUILayout.PropertyField(_portThemeProp);
            EditorGUILayout.PropertyField(_blackboardThemeProp);
        }
    }
}