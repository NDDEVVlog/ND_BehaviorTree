using UnityEditor;
using UnityEngine;

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
                // When the button is clicked, immediately process the mappings
                ProcessMappings((NodeEditorConfig)target);
            }
            
            

            ProcessMappings((NodeEditorConfig)target);
        }

        private void ProcessMappings(NodeEditorConfig config)
        {
            bool hasChanged = false;
            
            // Loop through all the mappings in this config.
            foreach (var mapping in config.mappings)
            {
                if (mapping.nodeScript != null)
                {

                    System.Type nodeType = mapping.nodeScript.GetClass();
                    if (nodeType != null)
                    {
                        string newFullName = nodeType.AssemblyQualifiedName;

                        if (mapping.nodeTypeFullName != newFullName)
                        {

                            mapping.nodeTypeFullName = newFullName;
                            hasChanged = true; 
                            Debug.Log($"Updated mapping for '{mapping.nodeScript.name}' to '{newFullName}'");
                        }
                    }
                    else
                    {
                        if (mapping.nodeTypeFullName != null)
                        {
                            mapping.nodeTypeFullName = null;
                            hasChanged = true;
                            Debug.LogWarning($"Cleared invalid mapping for script '{mapping.nodeScript.name}'. Check for compile errors or missing assembly references.");
                        }
                    }
                }
                else
                {
                    // If the script field is empty, ensure the name is also empty.
                    if (mapping.nodeTypeFullName != null)
                    {
                        mapping.nodeTypeFullName = null;
                        hasChanged = true;
                    }
                }
            }
                
            if (hasChanged)
            {
                EditorUtility.SetDirty(config);
            }
        }
    }
}