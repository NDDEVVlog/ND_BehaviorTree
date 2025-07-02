// --- START OF FILE ND_BehaviorTreeView.NodeManagement.cs ---

using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// This partial class handles all node management functionality for the Behavior Tree View.
    /// This includes creating, adding, drawing, removing, and finding nodes.
    /// </summary>
    public partial class ND_BehaviorTreeView
    {
        /// <summary>
        /// The low-level function to add a node's data and editor to the graph's collections and visual hierarchy.
        /// </summary>
        public void AddNode(Node nodeData, ND_NodeEditor nodeEditor)
        {
            nodeEditor.SetPosition(nodeData.position);
            TreeNodes.Add(nodeEditor);
            NodeDictionary.Add(nodeData.id, nodeEditor);
            AddElement(nodeEditor);

            if (!m_BTree.nodes.Contains(nodeData))
                m_BTree.nodes.Add(nodeData);
        }
        
        /// <summary>
        /// Adds a new node (both data and visual representation) to the graph, typically called by the search provider.
        /// </summary>
        public void AddNewNodeFromSearch(Node nodeData)
        {
            if (nodeData == null)
            {
                Debug.LogError("[AddNewNodeFromSearch] nodeData is null. Cannot add node.");
                return;
            }

            Undo.RecordObject(m_serialLizeObject.targetObject, "Added Node: " + nodeData.GetType().Name);
            AssetDatabase.AddObjectToAsset(nodeData, m_BTree);
            m_BTree.nodes.Add(nodeData);

            AddNodeVisuals(nodeData, animate: true);

            BindSerializedObject();
            EditorUtility.SetDirty(m_BTree);
            AssetDatabase.SaveAssets();
            if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
        }

        /// <summary>
        /// Adds the visual representation of a node to the graph.
        /// </summary>
        private void AddNodeVisuals(Node nodeData, bool animate = true)
        {
            if (nodeData == null) { Debug.LogError("[AddNodeVisuals] nodeData is null."); return; }
            if (string.IsNullOrEmpty(nodeData.typeName)) nodeData.typeName = nodeData.GetType().AssemblyQualifiedName;

            if (NodeDictionary.ContainsKey(nodeData.id))
            {
                Debug.LogWarning($"[AddNodeVisuals] Node editor for ID '{nodeData.id}' already exists. Skipping visual add.");
                return;
            }

            ND_NodeEditor nodeEditor;

            if (nodeData is AuxiliaryNode auxiliaryData)
            {
                nodeEditor = new ND_AuxiliaryEditor(auxiliaryData, m_serialLizeObject, this);
            }
            else
            {
                 nodeEditor = new ND_NodeEditor(nodeData, m_serialLizeObject, this);
            }
           
            AddNode(nodeData, nodeEditor);

            if (animate)
            {
                GraphViewAnimationHelper.AnimateNodeAppear(nodeEditor);
            }
            else
            {
                nodeEditor.AddToClassList("appeared");
            }
        }
        
        public void AddChildNode(CompositeNode parentNode, Type childType)
        {
            Undo.RecordObject(m_BTree, "Add Auxiliary Node");

            Node childNode = (Node)ScriptableObject.CreateInstance(childType);
            childNode.name = childType.Name;
            
            AssetDatabase.AddObjectToAsset(childNode, m_BTree);
            
            if (childNode is DecoratorNode decorator)
            {
                parentNode.decorators.Add(decorator);
            }
            else if (childNode is ServiceNode service)
            {
                parentNode.services.Add(service);
            }
            else
            {
                 Debug.LogWarning($"Trying to add unsupported child type '{childType.Name}' to a CompositeNode.");
                 return;
            }
            
            m_serialLizeObject.Update();

            if (NodeDictionary.TryGetValue(parentNode.id, out ND_NodeEditor parentEditor))
            {
                parentEditor.DrawChildren(parentNode, this);
            }
            
            EditorUtility.SetDirty(m_BTree);
            if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
            AssetDatabase.SaveAssets();
        }

        private void DrawNodesFromData()
        {
            foreach (Node nodeData in m_BTree.nodes)
            {
                if (nodeData == null)
                {
                    Debug.LogWarning("[DrawNodesFromData] Encountered a null Node instance in m_BTree.nodes list.");
                    continue;
                }
                AddNodeVisuals(nodeData, animate: false);
            }
        }
        
        private void RemoveDataForNode(ND_NodeEditor editorNode)
        {
            if (editorNode == null || editorNode.node == null) return;
            
            m_BTree.nodes.Remove(editorNode.node);
            NodeDictionary.Remove(editorNode.node.id);
            TreeNodes.Remove(editorNode);
        }

        public ND_NodeEditor GetEditorNode(string nodeID)
        {
            NodeDictionary.TryGetValue(nodeID, out ND_NodeEditor node);
            return node;
        }
    }
}
// --- END OF FILE ND_BehaviorTreeView.NodeManagement.cs ---