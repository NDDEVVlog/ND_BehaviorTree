using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;
namespace ND_BehaviorTree.Editor
{
    public class ND_AuxiliaryEditor : ND_NodeEditor
    {
        public ND_AuxiliaryEditor(Node node, UnityEditor.SerializedObject BTObject, GraphView graphView,string styleSheetPath) 
        : base(node, BTObject, graphView,styleSheetPath)    
        {
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if(styleSheet == null) styleSheet =  AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetStyleSheetPath("Auxiliary"));
            this.styleSheets.Add(styleSheet);
            this.AddToClassList("auxiliary-node-editor");


            style.position = Position.Relative;


            style.left = StyleKeyword.Auto;
            style.top = StyleKeyword.Auto;

            // An auxiliary node inside a composite shouldn't be independently movable.
            capabilities &= ~Capabilities.Movable;

            // The provided USS already hides the ports, but we can disable them for good measure.
            var topPortContainer = this.Q<VisualElement>("top-port");
            var bottomPortContainer = this.Q<VisualElement>("bottom-port");
            if (topPortContainer != null) topPortContainer.SetEnabled(false);
            if (bottomPortContainer != null) bottomPortContainer.SetEnabled(false);

            if (m_Node is DecoratorNode) this.AddToClassList("decorator-child");
            if (m_Node is ServiceNode)
            {
                m_Ports.Clear();
                this.AddToClassList("service-child");
            }

            this.AddManipulator(new DoubleClickNodeManipulator(this));
        }

        public override void DrawPort(NodeInfoAttribute info, VisualElement topPortContainer, VisualElement bottomPortContainer)
        {
            if (m_Node is DecoratorNode)
                base.DrawPort(info, topPortContainer, bottomPortContainer);
            else
            {
                topPortContainer.style.display =DisplayStyle.None;
                bottomPortContainer.style.display =DisplayStyle.None;
            }
        }
    }
}