// --- MODIFIED FILE: ND_AuxiliaryEditor.cs ---
using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEngine;
namespace ND_BehaviorTree.Editor
{
    public class ND_AuxiliaryEditor : ND_NodeEditor
    {
        public ND_AuxiliaryEditor(Node node, UnityEditor.SerializedObject BTObject, GraphView graphView) : base(node, BTObject, graphView)
        {
            this.AddToClassList("auxiliary-node-editor");

            Debug.Log("Create AuxiliaryEditorNode : " + node.typeName);
            // --- KEY FIX FOR LAYOUT ---
            // By default, a GraphView.Node is positioned absolutely. To make these
            // nodes behave like list items inside their parent's 'child-node-container',
            // we must change their positioning to Relative. This allows them to be
            // stacked vertically by the parent container's flexbox settings.
            style.position = Position.Relative;

            // We also clear any 'left' or 'top' values that might have been
            // set by the base Node class, allowing flexbox to position the element.
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
            if (m_Node is ServiceNode) this.AddToClassList("service-child");
            this.AddManipulator(new DoubleClickNodeManipulator(this));
        }
    }
}