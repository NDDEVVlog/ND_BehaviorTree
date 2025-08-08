using System.Diagnostics;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


namespace ND_BehaviorTree.Editor
{
    public class UnityNode : ND_NodeEditor
    {
        public UnityNode(Node node, SerializedObject btObject, GraphView graphView, string styleSheetPath)
            : base(node, btObject, graphView, styleSheetPath)
        {
            
            
        }

        public override void BuildTitle()
        {
            BuildDynamicTitleFromAttribute();
        }
    }
}