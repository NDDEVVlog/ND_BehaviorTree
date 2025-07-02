// --- START OF FILE ND_BehaviorTreeView.ConnectionManagement.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// This partial class handles all connection (edge) management for the Behavior Tree View.
    /// This includes drawing, creating, removing, and validating connections.
    /// </summary>
    public partial class ND_BehaviorTreeView
    {
        private void DrawConnectionsFromData()
        {
            foreach (ND_BTConnection connectionData in m_BTree.connections)
            {
                MakeConnectionVisualsOnly(connectionData);
            }
        }

        private void MakeConnectionVisualsOnly(ND_BTConnection connectionData)
        {
            ND_NodeEditor inNodeEd = GetEditorNode(connectionData.inputPort.nodeID);
            ND_NodeEditor outNodeEd = GetEditorNode(connectionData.outputPort.nodeID);

            if (inNodeEd == null || outNodeEd == null ||
                connectionData.inputPort.portIndex >= inNodeEd.Ports.Count || connectionData.outputPort.portIndex >= outNodeEd.Ports.Count ||
                connectionData.inputPort.portIndex < 0 || connectionData.outputPort.portIndex < 0) return;

            Port localInPort = inNodeEd.Ports[connectionData.inputPort.portIndex];
            Port localOutPort = outNodeEd.Ports[connectionData.outputPort.portIndex];
            
            ND_CustomEdge edge = new ND_CustomEdge { userData = connectionData };
            
            edge.output = localOutPort;
            edge.input = localInPort;

            if (!string.IsNullOrEmpty(connectionData.edgeText)) edge.Text = connectionData.edgeText;

            edge.input.Connect(edge); 
            edge.output.Connect(edge);
            
            AddElement(edge);
            ConnectionDictionary.Add(edge, connectionData);
        }
        
        private void CreateDataForEdge(Edge edge)
        {
            if (!(edge.input?.node is ND_NodeEditor inputNodeEditor) || !(edge.output?.node is ND_NodeEditor outputNodeEditor)) {
                Debug.LogError("[CreateDataForEdge] Edge has invalid input or output node editor. Cannot create data.");
                return;
            }
            int inputIndex = inputNodeEditor.Ports.IndexOf(edge.input);
            int outputIndex = outputNodeEditor.Ports.IndexOf(edge.output);

            if (inputIndex == -1 || outputIndex == -1) {
                Debug.LogError("[CreateDataForEdge] Could not find port index for the new edge. Cannot create data.");
                return;
            }

            ND_BTConnection connection = new ND_BTConnection(inputNodeEditor.node.id, inputIndex, outputNodeEditor.node.id, outputIndex);
            m_BTree.connections.Add(connection);
            ConnectionDictionary.Add(edge, connection);
        }

        private void RemoveDataForEdge(Edge e)
        {
            if (ConnectionDictionary.TryGetValue(e, out ND_BTConnection connection))
            {
                m_BTree.connections.Remove(connection);
                ConnectionDictionary.Remove(e);
            }
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> allPorts = new List<Port>();
            List<Port> compatiblePorts = new List<Port>();

            if (TreeNodes == null) return compatiblePorts;

            foreach (var nodeEditor in TreeNodes)
            {
                if (nodeEditor == null || nodeEditor.Ports == null) continue;
                allPorts.AddRange(nodeEditor.Ports);
            }

            foreach (Port p in allPorts)
            {
                if (p == startPort || p.node == startPort.node || startPort.direction == p.direction) continue;
                if (startPort.portType == p.portType)
                {
                    compatiblePorts.Add(p);
                }
            }
            return compatiblePorts;
        }
    }
}
// --- END OF FILE ND_BehaviorTreeView.ConnectionManagement.cs ---