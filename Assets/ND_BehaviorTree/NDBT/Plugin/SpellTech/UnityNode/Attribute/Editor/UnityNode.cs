// FILE: Editor/UnityNode.cs (VẪN GIỮ NGUYÊN)
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
            UnityEngine. Debug.Log("UnityNode");
            
            
        }

        public override void BuildTitle()
        {
            // Ghi đè hành vi mặc định và gọi helper để xây dựng title động
            BuildDynamicTitleFromAttribute();
        }
    }
}