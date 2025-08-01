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

        private SerializedProperty _defaultNodeUXMLProp;
        private SerializedProperty _graphViewStyleProp;
        private SerializedProperty _styleSheetsProp;

        private void OnEnable()
        {
            _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);
            Debug.Log($"OnEnable: IsUnlocked={_isUnlocked}");

            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            _styleSheetsProp = serializedObject.FindProperty("_styleSheets");

            Debug.Log($"Properties bound: defaultNodeUXML={(_defaultNodeUXMLProp != null)}, graphViewStyle={(_graphViewStyleProp != null)}, styleSheets={(_styleSheetsProp != null)}");
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update();
            //Debug.Log($"Inspector GUI: IsUnlocked={_isUnlocked}");

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
                        Repaint(); // Force Inspector refresh
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
            {
                DrawSettingsFields();
            }
            EditorGUI.EndDisabledGroup();

            if (_isUnlocked)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Lock Settings"))
                {
                    _isUnlocked = false;
                    _enteredPassword = "";
                    SessionState.SetBool(UnlockSessionStateKey, false);
                    GUI.FocusControl(null);
                    Repaint();
                }
            }
            
            serializedObject.ApplyModifiedProperties();
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
                //Debug.Log($"StyleSheets array size: {_styleSheetsProp.arraySize}");
            }
            else
            {
                EditorGUILayout.HelpBox("StyleSheets property is null!", MessageType.Error);
            }

            if (_isUnlocked)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Settings are currently editable. Remember to lock them if needed.", MessageType.None);
            }
        }
    }
}