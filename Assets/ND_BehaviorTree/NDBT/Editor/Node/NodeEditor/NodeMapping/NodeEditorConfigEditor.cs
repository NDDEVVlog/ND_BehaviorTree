using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomEditor(typeof(NodeEditorConfig))]
    public class NodeEditorConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields first
            DrawDefaultInspector();

            EditorGUILayout.Space();
            
            // Add a prominent button to manually refresh the data
            if (GUILayout.Button("Force Refresh Mappings", GUILayout.Height(30)))
            {
                // When the button is clicked, immediately process the mappings
                ProcessMappings((NodeEditorConfig)target);
            }
            
            // Also, automatically process mappings whenever the inspector is drawn.
            // This catches cases where assembly references might have just been fixed.
            ProcessMappings((NodeEditorConfig)target);
        }

        private void ProcessMappings(NodeEditorConfig config)
        {
            bool hasChanged = false;
            
            // Loop through all the mappings in this config.
            foreach (var mapping in config.mappings)
            {
                // If a node script has been assigned...
                if (mapping.nodeScript != null)
                {
                    // ...get its Type.
                    System.Type nodeType = mapping.nodeScript.GetClass();
                    if (nodeType != null)
                    {
                        string newFullName = nodeType.AssemblyQualifiedName;
                        // Check if the stored name is incorrect or missing.
                        if (mapping.nodeTypeFullName != newFullName)
                        {
                            // Store the correct full name. This is the crucial step.
                            mapping.nodeTypeFullName = newFullName;
                            hasChanged = true; // Mark that we made a change.
                            Debug.Log($"Updated mapping for '{mapping.nodeScript.name}' to '{newFullName}'");
                        }
                    }
                    else
                    {
                        // The script is assigned but doesn't resolve to a valid Type.
                        // This can happen after deleting a script or assembly reference issues.
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
                
            // If any of the names were updated, mark the asset as "dirty"
            // so Unity knows to save the changes to disk.
            if (hasChanged)
            {
                EditorUtility.SetDirty(config);
            }
        }
    }
}