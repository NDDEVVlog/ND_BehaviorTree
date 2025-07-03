// --- START OF FILE ND_BehaviorTreeView.ConnectionManagement.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public partial class ND_BehaviorTreeView
    {
        private void DrawConnectionsFromData()
        {
            foreach (Node parentNode in m_BTree.nodes)
            {
                ND_NodeEditor parentEditorNode = GetEditorNode(parentNode.id);
                if (parentEditorNode == null) continue;

                // Use the new polymorphic GetChildren() method
                List<Node> children = parentNode.GetChildren();
                if (children == null || children.Count == 0) continue;

                var outputPort = parentEditorNode.m_OutputPort;
                if (outputPort == null) continue;

                foreach(Node childNode in children)
                {
                    if (childNode == null) continue;
                    
                    ND_NodeEditor childEditorNode = GetEditorNode(childNode.id);
                    if (childEditorNode == null) continue;

                    Port inputPort = childEditorNode.m_InputPort;
                    if (inputPort != null)
                    {
                        var edge = outputPort.ConnectTo(inputPort);
                        AddElement(edge);
                    }
                }
            }
        }
        
        private void CreateDataForEdge(Edge edge)
        {
            if (!(edge.input?.node is ND_NodeEditor childEditorNode) || !(edge.output?.node is ND_NodeEditor parentEditorNode)) {
                Debug.LogError("[CreateDataForEdge] Edge has invalid input or output node editor. Cannot create data.");
                return;
            }

            Node parentNode = parentEditorNode.node;
            Node childNode = childEditorNode.node;
            
            // Use the new polymorphic AddChild() method
            parentNode.AddChild(childNode);
            
            // Optional sorting logic for composite nodes
            if (parentNode is CompositeNode composite)
            {
                composite.children.Sort((a,b) => GetEditorNode(a.id).GetPosition().x.CompareTo(GetEditorNode(b.id).GetPosition().x));
            }
        }

        private void RemoveDataForEdge(Edge e)
        {
            if (!(e.input?.node is ND_NodeEditor childEditorNode) || !(e.output?.node is ND_NodeEditor parentEditorNode))
            {
                return;
            }
        
            Node parentNode = parentEditorNode.node;
            Node childNode = childEditorNode.node;
            
            // Use the new polymorphic RemoveChild() method
            parentNode.RemoveChild(childNode);
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            var startNodeEditor = startPort.node as ND_NodeEditor;

            if (startNodeEditor == null) return compatiblePorts;

            ports.ForEach(port => {
                var portNodeEditor = port.node as ND_NodeEditor;
                if (portNodeEditor == null) return;

                // Basic validation: Don't connect to self, different directions, same port type
                if (startPort.node == port.node || startPort.direction == port.direction || startPort.portType != port.portType)
                {
                    return;
                }

                // --- Advanced Validation based on Node Type ---

                // Rule 1: An input port can only accept one connection.
                if (port.direction == Direction.Input && port.connected)
                {
                    return;
                }
                
                // Rule 2: Output ports on single-child nodes (like AuxiliaryNode) can only have one connection.
                if (startPort.direction == Direction.Output && startNodeEditor.node is AuxiliaryNode && startPort.connected)
                {
                    return;
                }

                compatiblePorts.Add(port);
            });

            return compatiblePorts;
        }
    }
}
// --- END OF FILE ND_BehaviorTreeView.ConnectionManagement.cs ---