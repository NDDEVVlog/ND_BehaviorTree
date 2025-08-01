using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public static class NodeEditorFactory
    {
        // A cached list of all configurations found in the project.
        private static List<NodeEditorConfig> _allConfigs;

        // Finds and loads ALL configuration assets from the project.
        private static bool LoadAllConfigs()
        {
            // If we have already found and cached the configs, do nothing.
            if (_allConfigs != null) return true;

            _allConfigs = new List<NodeEditorConfig>();
            
            // Find all assets of the NodeEditorConfig type.
            var guids = AssetDatabase.FindAssets($"t:{nameof(NodeEditorConfig)}");
            
            if (guids.Length == 0)
            {
                Debug.LogWarning("No NodeEditorConfig assets found in the project. All nodes will use default editors.");
                return false;
            }

            Debug.Log($"[NodeEditorFactory] Found {guids.Length} NodeEditorConfig asset(s). Loading them now.");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<NodeEditorConfig>(path);
                if (config != null)
                {
                    _allConfigs.Add(config);
                }
            }
            
            return _allConfigs.Count > 0;
        }
        
        public static ND_NodeEditor CreateEditor(Node nodeData, SerializedObject serializedObject, GraphView graphView)
        {
            // Load all available configuration themes.
            LoadAllConfigs();

            Type nodeType = nodeData.GetType();
            NodeEditorMapping mapping = null;
            
            // --- MODIFIED SEARCH LOGIC ---
            // 1. Walk up the node's inheritance chain (from most specific to most general).
            Type currentType = nodeType;
            while(currentType != null)
            {
                // 2. For each type in the hierarchy, search through ALL loaded configs.
                foreach (var config in _allConfigs)
                {
                    // Find a mapping for the current type within this config.
                    var tempMapping = config.mappings.FirstOrDefault(m => m.nodeTypeFullName == currentType.AssemblyQualifiedName);
                    if (tempMapping != null)
                    {
                        // If found, it becomes the current winner.
                        // This implements the "last one found wins" rule if multiple configs have the same mapping.
                        mapping = tempMapping;
                    }
                }

                // 3. If we found a mapping for this level of specificity, stop searching.
                if (mapping != null)
                {
                    break;
                }
                
                // 4. If not, check the parent class.
                currentType = currentType.BaseType;
            }

            // Determine editor type from the winning mapping, or fall back to default.
            Type editorType = typeof(ND_NodeEditor); 
            if (mapping != null && !string.IsNullOrEmpty(mapping.editorTypeFullName))
            {
                editorType = Type.GetType(mapping.editorTypeFullName) ?? typeof(ND_NodeEditor);
            }

            // Load the single, global default UXML.
            string defaultUxmlPath = ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath();
            VisualTreeAsset uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(defaultUxmlPath);

            try
            {
                var editorInstance = (ND_NodeEditor)Activator.CreateInstance(editorType, nodeData, serializedObject, graphView);
                editorInstance.Clear();
            
                if (uxmlAsset != null)
                {
                    uxmlAsset.CloneTree(editorInstance);
                }

                editorInstance.InitializeNodeView(nodeData, serializedObject, graphView);
                return editorInstance;
            }
            catch (Exception e)
            {
                Debug.LogError($"CRITICAL FAILURE during editor instantiation for node <b>{nodeData.GetType().Name}</b>. Error: {e}");
                return null;
            }
        }
        
        // Call this if you want to force a re-scan of project assets (e.g., from a menu item).
        public static void InvalidateConfigCache()
        {
            _allConfigs = null;
        }
    }
}