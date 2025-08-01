using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A ScriptableObject that holds the configuration for the node editor factory.
    /// It specifies default assets and a list of custom mappings for specific node types.
    /// </summary>
    [CreateAssetMenu(fileName = "NodeEditorConfig", menuName = "ND_BehaviorTree/Editor Configuration")]
    public class NodeEditorConfig : ScriptableObject
    {
        [Header("Default Fallbacks")]
        [Tooltip("The default UXML file to use for any node that doesn't have a specific mapping.")]
        public VisualTreeAsset defaultUxml;
        
        // The default editor type will be hardcoded as ND_NodeEditor in the factory.

        [Header("Custom Mappings")]
        [Tooltip("Define custom editor classes and UXML files for specific Node types by dragging scripts and assets here.")]
        public List<NodeEditorMapping> mappings = new List<NodeEditorMapping>();
    }
}