// --- MODIFIED FILE: BehaviorTreeRunnerEditor.cs ---

using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(BehaviorTreeRunner))]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // Disable button if no asset is assigned
            EditorGUI.BeginDisabledGroup(runner.treeAsset == null);
            
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {
                // Open the editor window, which will automatically handle
                // whether it's in play mode (debug) or edit mode.
                ND_BehaviorTreeEditorWindow.Open(runner);
            }

            EditorGUI.EndDisabledGroup();
            
            if (runner.treeAsset == null)
            {
                 EditorGUILayout.HelpBox("Assign a BehaviorTree asset to enable the editor.", MessageType.Info);
            }
        }
    }
}