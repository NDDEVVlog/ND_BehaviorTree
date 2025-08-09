

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(BehaviorTreeRunner), true)]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        private bool runtimeBlackboardFoldout = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the Tree Asset field
            EditorGUILayout.PropertyField(serializedObject.FindProperty("treeAsset"));
            
            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // --- EDIT MODE: Show Overrides ---
            if (runner.treeAsset != null)
            {
                if (runner.treeAsset.blackboard == null)
                {
                    EditorGUILayout.HelpBox("The assigned Behavior Tree has no Blackboard asset. Overrides cannot be configured.", MessageType.Warning);
                }
                else
                {
                    // Draw the 'overrides' list. The KeyOverrideDrawer will automatically handle the UI for each element.
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("overrides"), true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a BehaviorTree asset to configure key overrides.", MessageType.Info);
            }

            // --- Button to open the main editor ---
            EditorGUI.BeginDisabledGroup(runner.treeAsset == null);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {
                ND_BehaviorTreeEditorWindow.Open(runner.treeAsset);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // --- PLAY MODE: Show Live Values ---
            if (Application.isPlaying)
            {
                DrawRuntimeBlackboard(runner);
                Repaint(); // Ensure the inspector updates continuously
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws the live blackboard values from the running tree in Play Mode.
        /// </summary>
        private void DrawRuntimeBlackboard(BehaviorTreeRunner runner)
        {
            Blackboard runtimeBlackboard = runner.RuntimeTree?.blackboard;

            if (runtimeBlackboard == null)
            {
                EditorGUILayout.HelpBox("Runtime Blackboard not yet available.", MessageType.Info);
                return;
            }

            runtimeBlackboardFoldout = EditorGUILayout.Foldout(runtimeBlackboardFoldout, "Runtime Blackboard (Live Values)", true, EditorStyles.boldLabel);
            if (runtimeBlackboardFoldout)
            {
                EditorGUI.indentLevel++;
                // Group by key name just in case of duplicates, though there shouldn't be any
                foreach (var key in runtimeBlackboard.keys.OrderBy(k => k.keyName))
                {
                    if (key != null)
                    {
                        DrawKeyField(key);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawKeyField(Key key)
        {
            SerializedObject keySO = new SerializedObject(key);
            SerializedProperty valueProperty = keySO.FindProperty("value");

            if (valueProperty == null)
            {
                EditorGUILayout.LabelField(key.keyName, "Error: Could not find 'value' property.");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            string keyTypeName = key.GetValueType()?.Name ?? "null";
            EditorGUILayout.LabelField(new GUIContent(key.keyName, $"Type: {keyTypeName}"), GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            EditorGUILayout.PropertyField(valueProperty, GUIContent.none, true);
            EditorGUILayout.EndHorizontal();

            if (keySO.hasModifiedProperties)
            {
                keySO.ApplyModifiedProperties();
            }
        }
    }
}