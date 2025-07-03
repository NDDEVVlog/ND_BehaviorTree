using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(BehaviorTreeRunner))]
    public class BehaviorTreeRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            // Get the target runner
            BehaviorTreeRunner runner = (BehaviorTreeRunner)target;

            // Add a space and a button
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Behavior Tree Editor"))
            {
                if (runner.treeAsset != null)
                {
                    // Open the editor window, passing the runner instance
                    ND_BehaviorTreeEditorWindow.Open(runner);
                }
                else
                {
                    Debug.LogWarning("Assign a BehaviorTree asset before opening the editor.");
                }
            }
        }
    }
}