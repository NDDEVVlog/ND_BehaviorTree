// --- CORRECTED FILE: BehaviorTreeRunnerEditor.cs ---

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ND_BehaviorTree.Editor
{
    // The second parameter 'true' tells the editor to apply to all child classes of BehaviorTreeRunner.
    [CustomEditor(typeof(BehaviorTreeRunner), true)]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        private bool blackboardFoldout = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // --- Button to open the main editor ---
            EditorGUI.BeginDisabledGroup(runner.treeAsset == null);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {
                ND_BehaviorTreeEditorWindow.Open(runner.treeAsset);
            }
            EditorGUI.EndDisabledGroup();

            if (runner.treeAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a BehaviorTree asset to enable blackboard editing.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);

            // --- DYNAMIC GUI: Switch between Edit Mode and Play Mode ---
            if (Application.isPlaying)
            {
                DrawRuntimeBlackboard(runner);
            }
            else
            {
                DrawInitialBlackboard(runner);
            }

            // This ensures the inspector updates continuously during play mode to show live values.
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
        
        /// <summary>
        /// Draws the blackboard values for setting up initial state in Edit Mode.
        /// </summary>
        private void DrawInitialBlackboard(BehaviorTreeRunner runner)
        {
            Blackboard targetBlackboard = runner.treeAsset?.blackboard;

            if (targetBlackboard == null)
            {
                EditorGUILayout.HelpBox("The assigned Behavior Tree asset does not have a Blackboard.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("You are editing default values on the shared Blackboard asset. These changes will affect all runners using this tree.", MessageType.Info);

            blackboardFoldout = EditorGUILayout.Foldout(blackboardFoldout, "Blackboard Initial Values", true, EditorStyles.boldLabel);
            if (blackboardFoldout)
            {
                EditorGUI.indentLevel++;
                SerializedObject blackboardSO = new SerializedObject(targetBlackboard);

                foreach (var key in targetBlackboard.keys.OrderBy(k => k.keyName)) // Optional: Sort by keyName
                {
                    if (key != null)
                    {
                        DrawKeyField(key);
                    }
                }
                
                if (blackboardSO.hasModifiedProperties)
                {
                    blackboardSO.ApplyModifiedProperties();
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws the live blackboard values from the running tree in Play Mode.
        /// </summary>
        private void DrawRuntimeBlackboard(BehaviorTreeRunner runner)
        {
            Blackboard runtimeBlackboard = runner.RuntimeTree?.blackboard;

            if (runtimeBlackboard == null)
            {
                EditorGUILayout.HelpBox("Runtime Blackboard is not available. (Tree might not be initialized yet).", MessageType.Info);
                return;
            }

            blackboardFoldout = EditorGUILayout.Foldout(blackboardFoldout, "Runtime Blackboard (Live Values)", true, EditorStyles.boldLabel);
            if (blackboardFoldout)
            {
                EditorGUI.indentLevel++;
                foreach (var key in runtimeBlackboard.keys.OrderBy(k => k.keyName)) // Optional: Sort by keyName
                {
                    if (key != null)
                    {
                        DrawKeyField(key);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws a generic editor field for any Key type.
        /// </summary>
        private void DrawKeyField(Key key)
        {
            SerializedObject keySO = new SerializedObject(key);
            SerializedProperty valueProperty = keySO.FindProperty("value");

            // *** FIX IS APPLIED HERE ***
            if (valueProperty == null)
            {
                // Use key.keyName here as well for consistency in error messages.
                EditorGUILayout.LabelField(key.keyName, "Error: Could not find 'value' property.");
                return;
            }

            EditorGUILayout.BeginHorizontal();

            string keyTypeName = key.GetValueType()?.Name ?? "null";
            
            // *** AND THE MAIN FIX IS APPLIED HERE ***
            // Use key.keyName instead of key.name to show the logical name of the key.
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