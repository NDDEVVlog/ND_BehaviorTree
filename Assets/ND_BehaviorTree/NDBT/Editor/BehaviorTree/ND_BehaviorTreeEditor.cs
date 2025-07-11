using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace ND_BehaviorTree.Editor
{   
    [CustomEditor(typeof(BehaviorTree))]
    public class ND_BehaviorTreeEditor : UnityEditor.Editor
    {
        SerializedProperty blackboardProp;

        private void OnEnable()
        {
            blackboardProp = serializedObject.FindProperty("blackboard");
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            Object asset = EditorUtility.InstanceIDToObject(instanceID);
            if (asset is BehaviorTree tree)
            {
                ND_BehaviorTreeEditorWindow.Open(tree);
                return true;
            }
            return false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the Blackboard field
            EditorGUILayout.PropertyField(blackboardProp);

            // Draw "Open" button
            if (GUILayout.Button("Open"))
            {
                ND_BehaviorTreeEditorWindow.Open((BehaviorTree)target);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
