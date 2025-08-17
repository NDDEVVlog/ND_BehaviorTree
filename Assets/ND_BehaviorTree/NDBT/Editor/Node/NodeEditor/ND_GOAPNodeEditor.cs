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
        public ND_GOAPNodeEditor(Node node, UnityEditor.SerializedObject BTObject, GraphView graphView, string styleSheetPath)
        : base(node, BTObject, graphView, styleSheetPath)
        {
            
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
        protected override void LoadStyleSheet(string styleSheetPath)
        {
            string goapStylePath = styleSheetPath;
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleDefaultPath);
            this.styleSheets.Add(styleSheet);
            if (string.IsNullOrEmpty(goapStylePath))
            {
                goapStylePath = ND_BehaviorTreeSetting.Instance.GetStyleSheetPath("GOAP");
            }
            StyleSheet goapStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(goapStylePath);
            if (goapStyleSheet != null)
            {
                this.styleSheets.Add(goapStyleSheet);
            }
        }

    }
}