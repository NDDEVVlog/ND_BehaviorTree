using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A specialized editor view for all GOAP-related nodes.
    /// It inherits all functionality from ND_NodeEditor and only adds custom styling.
    /// </summary>
    public class ND_GOAPNodeEditor : ND_NodeEditor
    {
        public ND_GOAPNodeEditor(Node node, UnityEditor.SerializedObject BTObject, GraphView graphView,string styleSheetPath, string styleDefaultPath ) 
        : base(node, BTObject, graphView,styleSheetPath,styleDefaultPath)
        {
            // 1. Load the stylesheet for GOAP nodes.
            // Note: The path should come from your settings singleton.

            string goapStylePath = styleSheetPath;
            if (!string.IsNullOrEmpty(goapStylePath))
            {
                StyleSheet goapStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(goapStylePath);
                if (goapStyleSheet != null)
                {
                    this.styleSheets.Add(goapStyleSheet);
                }
            }

            // 2. Add USS classes to this specific node instance for styling.
            this.AddToClassList("goap-node");

            if (m_Node is GOAP.GOAPPlannerNode)
            {
                this.AddToClassList("goap-planner-node");
            }
            else if (m_Node is GOAP.GOAPActionNode)
            {
                this.AddToClassList("goap-action-node");
            }
        }


    }
}