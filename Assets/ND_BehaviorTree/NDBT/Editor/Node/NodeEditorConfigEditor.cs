using System;
using UnityEditor;
using UnityEngine;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(NodeEditorConfig))]
    public class NodeEditorConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Force Refresh Mappings", GUILayout.Height(30)))
            {
                ProcessMappings((NodeEditorConfig)target);
            }

            if (GUI.changed)
            {
                ProcessMappings((NodeEditorConfig)target);
            }
        }

        private void ProcessMappings(NodeEditorConfig config)
        {
            bool hasChanged = false;
            
            foreach (var mapping in config.mappings)
            {
                if (mapping.nodeScript != null)
                {
                    Type nodeType = mapping.nodeScript.GetClass();
                    if (nodeType != null)
                    {
                        // SỬA Ở ĐÂY: Chỉ lấy tên ngắn (Name) thay vì AssemblyQualifiedName hay FullName
                        string newName = nodeType.Name;

                        if (mapping.nodeTypeFullName != newName)
                        {
                            mapping.nodeTypeFullName = newName;
                            hasChanged = true; 
                        }
                    }
                    else if (mapping.nodeTypeFullName != null)
                    {
                        mapping.nodeTypeFullName = null;
                        hasChanged = true;
                    }
                }
                else if (mapping.nodeTypeFullName != null)
                {
                    mapping.nodeTypeFullName = null;
                    hasChanged = true;
                }
            }
                
            if (hasChanged)
            {
                EditorUtility.SetDirty(config);
                ND_BTViewFactory.InvalidateCache();
            }
        }
    }
}