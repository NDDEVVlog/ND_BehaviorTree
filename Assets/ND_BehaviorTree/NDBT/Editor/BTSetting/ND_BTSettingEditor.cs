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
        private SerializedProperty _historyCapacityProp;

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
            _historyCapacityProp = serializedObject.FindProperty("historyCapacity");
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
            EditorGUILayout.LabelField("Theme", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Active Theme", "Switch the whole look (nodes, canvas, edges, ports) in one click. Values below can be tweaked afterwards."));
                if (EditorGUILayout.DropdownButton(new GUIContent(Theme.ND_BTThemeCatalog.ActiveName ?? "Custom"), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    Theme.ND_BTThemeCatalog.FillMenu(menu);
                    menu.ShowAsContext();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global UI Templates", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultNodeUXMLProp);
            EditorGUILayout.PropertyField(_defaultNodeStyleProp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("attachmentStyle"), new GUIContent("Decorator / Service Style", "Stylesheet for decorator / service cards and key slots, applied on top of ND_NodeAttachments.uss. Set by the theme."));
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Graph History", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_historyCapacityProp, new GUIContent("History Capacity (steps)"));
            if (GUILayout.Button("Clear Graph History (all trees)"))
            {
                Graph.GraphHistory.ClearAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI Features", EditorStyles.boldLabel);
            bool aiEnabled = EditorGUILayout.Toggle(
                new GUIContent("Enable AI Access (CLI / MCP)", "Shows the AI Access page and 'Copy for AI' actions, and allows the ndbt bridge to run once unlocked with the AI owner password. Off by default."),
                Bridge.AiAccess.FeatureEnabled);
            if (aiEnabled != Bridge.AiAccess.FeatureEnabled) Bridge.AiAccess.SetFeatureEnabled(aiEnabled);
            if (Bridge.AiAccess.FeatureEnabled && GUILayout.Button("Open AI Access settings →"))
            {
                Bridge.AiAccessSettingsProvider.Open();
            }
        }
    }
}