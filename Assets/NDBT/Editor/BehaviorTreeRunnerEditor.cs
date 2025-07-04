// --- MODIFIED FILE: BehaviorTreeRunnerEditor.cs ---

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(BehaviorTreeRunner))]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default fields like 'Tree Asset' and 'Blackboard Override'
            DrawDefaultInspector();

            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // --- Button to open the main editor ---
            EditorGUI.BeginDisabledGroup(runner.treeAsset == null);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {
                // This part would need a reference to your custom editor window class
                // ND_BehaviorTreeEditorWindow.Open(runner); 
            }
            EditorGUI.EndDisabledGroup();

            if (runner.treeAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a BehaviorTree asset to enable editor and blackboard overrides.", MessageType.Info);
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
        }

        /// <summary>
        /// Draws the blackboard values for setting up initial state in Edit Mode.
        /// </summary>
        private void DrawInitialBlackboard(BehaviorTreeRunner runner)
        {
            EditorGUILayout.LabelField("Blackboard Initial Values", EditorStyles.boldLabel);

            Blackboard targetBlackboard = runner.blackboardOverride;
            if (targetBlackboard == null)
            {
                targetBlackboard = runner.treeAsset?.blackboard;
            }

            if (targetBlackboard == null)
            {
                EditorGUILayout.HelpBox("No Blackboard is assigned to the Tree Asset or the Override slot.", MessageType.Warning);
                return;
            }

            if (runner.blackboardOverride != null)
            {
                EditorGUILayout.HelpBox("Editing values on the assigned 'Blackboard Override' asset. This asset will be cloned at runtime.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Displaying default values from the Tree Asset's blackboard. To override, create and assign a 'Blackboard Override' asset.", MessageType.Info);
            }

            // Prevent accidental editing of the shared blackboard asset
            EditorGUI.BeginDisabledGroup(runner.blackboardOverride == null);

            SerializedObject blackboardSO = new SerializedObject(targetBlackboard);
            
            foreach (var key in targetBlackboard.keys.OrderBy(k => k.name))
            {
                if (key != null)
                {
                    DrawKeyField(key);
                }
            }
            
            EditorGUI.EndDisabledGroup();

            if (blackboardSO.hasModifiedProperties)
            {
                blackboardSO.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Draws the live blackboard values from the running tree in Play Mode.
        /// </summary>
        private void DrawRuntimeBlackboard(BehaviorTreeRunner runner)
        {
            EditorGUILayout.LabelField("Runtime Blackboard (Live Values)", EditorStyles.boldLabel);

            Blackboard runtimeBlackboard = runner.RuntimeTree?.blackboard;

            if (runtimeBlackboard == null)
            {
                EditorGUILayout.HelpBox("Runtime Blackboard is not available. (Tree might not be initialized yet).", MessageType.Info);
                return;
            }
            
            // In play mode, we are always allowed to edit the live values for debugging.
            // No need for BeginDisabledGroup.
            
            foreach (var key in runtimeBlackboard.keys.OrderBy(k => k.name))
            {
                if (key != null)
                {
                    DrawKeyField(key);
                }
            }
            
            // Make the editor update constantly in play mode to reflect changes
            if (Application.isPlaying) {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Draws a generic editor field for any Key type.
        /// This method works for both asset-based keys (Edit Mode) and in-memory cloned keys (Play Mode).
        /// </summary>
        private void DrawKeyField(Key key)
        {
            SerializedObject keySO = new SerializedObject(key);
            SerializedProperty valueProperty = keySO.FindProperty("value");

            if (valueProperty == null)
            {
                EditorGUILayout.LabelField(key.name, "Error: Could not find 'value' property.");
                return;
            }

            // Draw a single row for the key: [Name Label] [Value Field]
            EditorGUILayout.BeginHorizontal();

            string keyTypeName = key.GetValueType().Name;
            EditorGUILayout.LabelField(new GUIContent(key.name, $"Type: {keyTypeName}"), GUILayout.Width(150));

            // EditorGUILayout.PropertyField automatically draws the correct editor GUI
            EditorGUILayout.PropertyField(valueProperty, GUIContent.none, true);

            EditorGUILayout.EndHorizontal();

            // Apply changes to the key's SerializedObject
            if (keySO.hasModifiedProperties)
            {
                keySO.ApplyModifiedProperties();
            }
        }
    }
}