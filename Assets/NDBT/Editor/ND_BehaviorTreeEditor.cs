using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace ND_BehaviorTree.Editor
{   
    [CustomEditor(typeof(BehaviorTree))]
    public class ND_BehaviorTreeEditor : UnityEditor.Editor
    {
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int index)
        {
            Object asset = EditorUtility.InstanceIDToObject(instanceID);
            if (asset.GetType() == typeof(BehaviorTree))
            {
                ND_BehaviorTreeEditorWindow.Open((BehaviorTree)asset);
                return true;
            }
            return false;
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open"))
            {
                ND_BehaviorTreeEditorWindow.Open((BehaviorTree)target);

            }
        }
    }
}
