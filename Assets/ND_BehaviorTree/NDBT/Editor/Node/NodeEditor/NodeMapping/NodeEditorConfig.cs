using System.Collections.Generic;
using UnityEditor;
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
        // [Header("Default Fallbacks")]
        // [Tooltip("The default UXML file to use for any node that doesn't have a specific mapping.")]
        // public VisualTreeAsset defaultUxml;

        // The default editor type will be hardcoded as ND_NodeEditor in the factory.

        [Tooltip("Drag the custom Editor script file here (e.g., ND_GOAPNodeEditor.cs). Can be null to use the default.")]
        public MonoScript editorScript;


        [Header("Custom Mappings")]
        [Tooltip("Define custom editor classes and UXML files for specific Node types by dragging scripts and assets here.")]
        public List<NodeEditorMapping> mappings = new List<NodeEditorMapping>();

        [Header("Node Style Sheets")]
        [Tooltip("Dictionary mapping node types to their corresponding USS files.")]
        [SerializeField]
        private List<StyleSheetEntry> _styleSheets;
        
         public bool TryGetStyleSheet(string nodeType, out StyleSheet styleSheet)
        {
            styleSheet = null;
            if (string.IsNullOrEmpty(nodeType))
                return false;

            foreach (var entry in _styleSheets)
            {
                if (entry.nodeType == nodeType && entry.styleSheet != null)
                {
                    styleSheet = entry.styleSheet;
                    return true;
                }
            }
            return false;
        }
        
        public string GetStyleSheetPath(string nodeType)
        {
            if (TryGetStyleSheet(nodeType, out StyleSheet styleSheet) && styleSheet != null)
            {
                return AssetDatabase.GetAssetPath(styleSheet);
            }
            return null;
        }
        
    }
}