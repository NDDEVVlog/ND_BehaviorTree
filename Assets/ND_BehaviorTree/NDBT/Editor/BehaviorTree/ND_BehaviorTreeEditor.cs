using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(BehaviorTree))]
    public class ND_BehaviorTreeEditor : UnityEditor.Editor
    {
        SerializedProperty blackboardProp;
        SerializedProperty groupsProp;
        SerializedProperty notesProp;

        private void OnEnable()
        {
            blackboardProp = serializedObject.FindProperty("blackboard");
            groupsProp = serializedObject.FindProperty("groups");
            notesProp = serializedObject.FindProperty("notes");
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

            // Shown only while AI features are enabled and unlocked with the owner password (this session).
            if (Bridge.AiAccess.UiUnlocked && GUILayout.Button("Copy for AI"))
            {
                EditorGUIUtility.systemCopyBuffer = Bridge.CopyForAi.TreeText((BehaviorTree)target);
            }

            // Editor annotations are edited in the graph (so the open canvas stays in sync); shown read-only here.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Editor Annotations — {groupsProp.arraySize} group(s), {notesProp.arraySize} note(s)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(groupsProp, true);
                EditorGUILayout.PropertyField(notesProp, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
