using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Codice.CM.Common.Tree;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public partial class ND_BehaviorTreeView
    {

        internal void RemoveNodeDataOutOfGraph(ND_NodeEditor nodeVisualEditor)
        {
            TreeNodes.Remove(nodeVisualEditor);
            NodeDictionary.Remove(nodeVisualEditor.node.id);
            m_BTree.nodes.Remove(nodeVisualEditor.node);

        }
        public void AddNode(Node nodeData, ND_NodeEditor nodeEditor)
        {
            nodeEditor.SetPosition(nodeData.position); // Set visual position from data
            TreeNodes.Add(nodeEditor); // Add to local list of editor nodes

            NodeDictionary.Add(nodeData.id, nodeEditor); // Add to lookup dictionary for quick access
            AddElement(nodeEditor); // Add to GraphView's visual element hierarchy
            
            if (!m_BTree.nodes.Contains(nodeData))
                m_BTree.nodes.Add(nodeData);
        }

    }
}
