

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace ND_BehaviorTree.Editor
{
    // The second parameter 'true' tells the editor to apply to all child classes of BehaviorTreeRunner.
    [CustomEditor(typeof(BehaviorTreeRunner), true)]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            
            DrawDefaultInspector();

            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // --- Button to open the main editor ---
            // The button is disabled if no tree asset is assigned.
            EditorGUI.BeginDisabledGroup(runner.treeAsset == null);
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {

                ND_BehaviorTreeEditorWindow.Open(runner.treeAsset); 
            }
            EditorGUI.EndDisabledGroup();

            if (runner.treeAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a BehaviorTree asset to enable the editor button and blackboard overrides.", MessageType.Info);
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

            // Allow editing only if an override is provided, to prevent accidental modification of the shared asset.
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

            // Access the live, cloned blackboard from the runtime tree instance.
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
        }

        /// <summary>
        /// Draws a generic editor field for any Key type.
        /// This method works for both asset-based keys (Edit Mode) and in-memory cloned keys (Play Mode).
        /// </summary>
        private void DrawKeyField(Key key)
        {
            // We serialize the key object itself to edit its 'value' field.
            SerializedObject keySO = new SerializedObject(key);
            SerializedProperty valueProperty = keySO.FindProperty("value");

            if (valueProperty == null)
            {
                EditorGUILayout.LabelField(key.name, "Error: Could not find 'value' property.");
                return;
            }
            
            EditorGUILayout.BeginHorizontal();

            string keyTypeName = key.GetValueType()?.Name ?? "null";
            EditorGUILayout.LabelField(new GUIContent(key.name, $"Type: {keyTypeName}"), GUILayout.Width(EditorGUIUtility.labelWidth - 5));

            // EditorGUILayout.PropertyField automatically draws the correct editor GUI for the property type.
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