using UnityEngine;
using UnityEditor;

namespace ND_BehaviorTree.Editor
{
    [System.Serializable]
    public class NodeEditorMapping
    {
        [Tooltip("Drag the Node data script file here (e.g., GOAPActionNode.cs).")]
        public MonoScript nodeScript;
        
        [Tooltip("Drag the custom Editor script file here (e.g., ND_GOAPNodeEditor.cs). Can be null to use the default.")]
        public MonoScript editorScript;

        // The UXML Asset field has been removed as requested.

        [HideInInspector] public string nodeTypeFullName;
        [HideInInspector] public string editorTypeFullName;
    }
}