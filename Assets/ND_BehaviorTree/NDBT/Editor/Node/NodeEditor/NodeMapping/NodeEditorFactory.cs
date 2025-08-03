using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    public static class NodeEditorFactory
    {
        private static List<NodeEditorConfig> _availableConfigs;

        private static bool LoadConfigsFromManager()
        {
            if (_availableConfigs != null) return true;

            var manager = NodeEditorConfigManager.Instance;
            if (manager == null) return false;

            _availableConfigs = manager.allConfigs;

            if (_availableConfigs == null || _availableConfigs.Count == 0)
            {
                Debug.LogWarning("[NodeEditorFactory] The NodeEditorConfigManager has an empty 'allConfigs' list.");
                return false;
            }

            _availableConfigs = _availableConfigs.Where(c => c != null).ToList();
            return _availableConfigs.Count > 0;
        }

        public static ND_NodeEditor CreateEditor(Node nodeData, SerializedObject serializedObject, GraphView graphView)
        {
            if (!LoadConfigsFromManager())
            {
                Debug.LogError("Cannot create node editor because no valid configurations were loaded from the NodeEditorConfigManager.");
                return null;
            }

            Type nodeType = nodeData.GetType();
            NodeEditorConfig winningConfig = null;
            NodeEditorMapping winningMapping = null;
            
            Type currentType = nodeType;
            while (currentType != null && winningConfig == null)
            {
                foreach (var config in _availableConfigs) 
                {

                    var foundMapping = config.mappings.FirstOrDefault(m => m.nodeTypeFullName == currentType.AssemblyQualifiedName);
                    if (foundMapping != null)
                    {
                        winningConfig = config;
                        winningMapping = foundMapping;
                        break; 
                    }
                }

                if (winningConfig == null)
                {
                    currentType = currentType.BaseType; 
                }
            }
            
            Type editorType = typeof(ND_NodeEditor); 
            if (winningConfig != null && winningConfig.editorScript != null)
            {
                Type customEditorType = winningConfig.editorScript.GetClass();
                if (customEditorType != null && typeof(ND_NodeEditor).IsAssignableFrom(customEditorType))
                {
                    editorType = customEditorType;
                }
            }
            
            string styleEntryPath = null;
            if (winningConfig != null && winningMapping != null && !string.IsNullOrEmpty(winningMapping.styleSheetStringEntry))
            {
                string styleKey = winningMapping.styleSheetStringEntry;
                styleEntryPath = winningConfig.GetStyleSheetPath(styleKey);
            }
            
            
            try
            {
                var editorInstance = (ND_NodeEditor)Activator.CreateInstance(editorType, nodeData, serializedObject, graphView, styleEntryPath);
                editorInstance.AddBottomPortStyleSheet( ND_BehaviorTreeSetting.Instance.GetStyleSheetPath("Port"));

                return editorInstance;
            }
            catch (Exception e)
            {
                Debug.LogError($"CRITICAL FAILURE creating editor for node <b>{nodeData.GetType().Name}</b> with editor type <b>{editorType.Name}</b>. Error: {e.GetBaseException()}");
                return null;
            }
        }

        public static void InvalidateConfigCache()
        {
            _availableConfigs = null;
        }
    }
}