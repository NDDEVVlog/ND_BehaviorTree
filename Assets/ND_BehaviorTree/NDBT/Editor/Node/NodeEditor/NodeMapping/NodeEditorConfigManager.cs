using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A central manager that holds all available editor configurations (themes)
    /// and specifies which one is currently active.
    /// </summary>
    [CreateAssetMenu(fileName = "NodeEditorConfigManager", menuName = "ND_BehaviorTree/Editor Configuration Manager")]
    public class NodeEditorConfigManager : ScriptableObject
    {
        [Tooltip("The configuration that is currently active and will be used by the editor.")]
        public NodeEditorConfig activeConfig;

        [Tooltip("A list of all available editor configurations/themes. Drag your theme assets here.")]
        public List<NodeEditorConfig> allConfigs = new List<NodeEditorConfig>();
        
        // --- ADD THIS STATIC ACCESSOR ---
        
        private static NodeEditorConfigManager _instance;
        public static NodeEditorConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // The asset MUST be in a "Resources" folder for this to work.
                    _instance = Resources.Load<NodeEditorConfigManager>(nameof(NodeEditorConfigManager));

                    if (_instance == null)
                    {
                        Debug.LogError($"[NodeEditorConfigManager] No '{nameof(NodeEditorConfigManager)}' asset found in any 'Resources' folder. Please create one.");
                    }
                }
                return _instance;
            }
        }
    }
}