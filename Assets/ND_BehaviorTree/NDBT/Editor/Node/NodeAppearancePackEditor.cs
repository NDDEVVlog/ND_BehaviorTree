using System;
using UnityEditor;
using UnityEngine;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(NodeAppearancePack))]
    public class NodeAppearancePackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Force Refresh Mappings", GUILayout.Height(30)))
            {
                ProcessMappings((NodeAppearancePack)target);
            }

            if (GUI.changed)
            {
                ProcessMappings((NodeAppearancePack)target);
                // Any edit (keys, UXML/USS assets, custom view, priority) can change how nodes look.
                ND_BTViewFactory.InvalidateCache();
            }
        }

        private void ProcessMappings(NodeAppearancePack pack)
        {
            bool hasChanged = false;

            foreach (var mapping in pack.mappings)
            {
                if (mapping.nodeScript != null)
                {
                    Type nodeType = mapping.nodeScript.GetClass();
                    if (nodeType != null)
                    {
                        string newName = nodeType.FullName;

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
                EditorUtility.SetDirty(pack);
                ND_BTViewFactory.InvalidateCache();
            }
        }
    }
}
