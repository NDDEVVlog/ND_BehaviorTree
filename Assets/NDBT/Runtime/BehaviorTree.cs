using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(menuName = "BehaviourTree/Trees")]
    public class BehaviorTree : ScriptableObject
    {   
        [SerializeField]
        private List<Node> m_nodes;

        [SerializeField] public List<ND_BTConnection> m_connection;

        public List<ND_BTConnection> connections => m_connection;

        public List<Node> nodes => m_nodes;

        private Dictionary<string, Node> nodeDictionary;

        public BehaviorTree()
        {
            m_nodes = new List<Node>();
            m_connection = new List<ND_BTConnection>();
        }

        public void Init()
        {
            nodeDictionary = new Dictionary<string, Node>();
            foreach (Node n in nodes)
            {
                nodeDictionary.Add(n.id, n);
            }
        }

        internal Node GetRootNode()
        {
            RootNode[] rootNodes = nodes.OfType<RootNode>().ToArray();
            if (rootNodes.Length <= 0)
            {
                Debug.LogError("No RootNode found");
                return null;
            }
            return rootNodes[0];
        }

        public Node GetNode(string nextNodeID)
        {
            if (nodeDictionary.TryGetValue(nextNodeID, out Node node))
            {
                return node;
            }
            return null;
        }
        public Node GetNodeFromOutputConnection(string outputNodeId, int index)
        {
            foreach (ND_BTConnection connection in connections)
            {
                if (connection.outputPort.nodeID == outputNodeId && connection.outputPort.portIndex == index)
                {
                    string nodeID = connection.inputPort.nodeID;
                    Node inputNode = nodeDictionary[nodeID];
                    return inputNode;
                }
            }
            return null;
        }
    }
}
