// File: ND_BehaviorTreeSettingEditor.cs
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

        // --- SerializedProperty fields for ALL settings ---
        private SerializedProperty _defaultNodeUXMLProp;
        private SerializedProperty _graphViewStyleProp;
        private SerializedProperty _nodeDefaultUSSProp;
        private SerializedProperty _auxiliaryUSSProp;
        private SerializedProperty _goapUSSProp;
        private SerializedProperty _edgeUSSProp;

        private void OnEnable()
        {
            _isUnlocked = SessionState.GetBool(UnlockSessionStateKey, false);

            // --- Cache ALL SerializedProperty references ---
            _defaultNodeUXMLProp = serializedObject.FindProperty("defaultNodeUXML");
            _graphViewStyleProp = serializedObject.FindProperty("graphViewStyle");
            _nodeDefaultUSSProp = serializedObject.FindProperty("nodeDefaultUSS");
            _auxiliaryUSSProp = serializedObject.FindProperty("auxiliaryUSS");
            _goapUSSProp = serializedObject.FindProperty("goapUSS");
            _edgeUSSProp = serializedObject.FindProperty("edgeUSS");
        }

        public override void OnInspectorGUI()
        {
            ND_BehaviorTreeSetting settings = (ND_BehaviorTreeSetting)target;
            serializedObject.Update(); // Always call this at the beginning

            // --- Password Lock Section (No changes needed here) ---
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
                        GUI.FocusControl(null); // Remove focus from password field
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Incorrect Password", "The password you entered is incorrect.", "OK");
                        Debug.LogWarning("Incorrect password attempt for ND_BehaviorTree Settings.");
                    }
                }
                EditorGUILayout.Space();
            }

            // --- Main Settings Section ---
            EditorGUI.BeginDisabledGroup(!_isUnlocked);
            {
                DrawSettingsFields();
            }
            EditorGUI.EndDisabledGroup();

            // --- Lock Button (No changes needed here) ---
            if (_isUnlocked)
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
            // --- Draw ALL settings fields ---
            EditorGUILayout.LabelField("UXML Templates", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultNodeUXMLProp, new GUIContent("Default Node UXML"));

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("USS Stylesheets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_graphViewStyleProp, new GUIContent("GraphView Style"));
            EditorGUILayout.PropertyField(_nodeDefaultUSSProp, new GUIContent("Node Default Style"));
            EditorGUILayout.PropertyField(_auxiliaryUSSProp, new GUIContent("Auxiliary Node Style"));
            EditorGUILayout.PropertyField(_goapUSSProp, new GUIContent("GOAP Node Style"));
            EditorGUILayout.PropertyField(_edgeUSSProp, new GUIContent("Edge Style"));

            if (_isUnlocked)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Settings are currently editable. Remember to lock them if needed.", MessageType.None);
            }
        }
    }
}