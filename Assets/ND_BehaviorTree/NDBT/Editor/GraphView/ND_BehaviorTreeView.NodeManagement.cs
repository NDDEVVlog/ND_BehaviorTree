// --- FILE: ND_BehaviorTreeView.NodeManagement.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public partial class ND_BehaviorTreeView
    {
        public void AddNode(Node nodeData, ND_NodeEditor nodeEditor)
        {
            nodeEditor.SetPosition(nodeData.position);
            TreeNodes.Add(nodeEditor);
            NodeDictionary.Add(nodeData.id, nodeEditor);
            AddElement(nodeEditor);

            if (!m_BTree.nodes.Contains(nodeData))
                m_BTree.nodes.Add(nodeData);
        }
        
        public void AddNewNodeFromSearch(Node nodeData)
        {
            if (nodeData == null)
            {
                Debug.LogError("[AddNewNodeFromSearch] nodeData is null. Cannot add node.");
                return;
            }

            Undo.RecordObject(m_BTree, "Added Node: " + nodeData.GetType().Name);
            AssetDatabase.AddObjectToAsset(nodeData, m_BTree);
            m_BTree.nodes.Add(nodeData);

            AddNodeVisuals(nodeData, animate: true);

            BindSerializedObject();
            EditorUtility.SetDirty(m_BTree);
            AssetDatabase.SaveAssets();
            if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
        }
        
        //start adjust from here
        private ND_NodeEditor CreateNodeEditorView(Node nodeData)
        {
            return NodeEditorFactory.CreateEditor(nodeData, m_serialLizeObject, this);
        }

        private void AddNodeVisuals(Node nodeData, bool animate = true)
        {
            if (nodeData == null) { Debug.LogError("[AddNodeVisuals] nodeData is null."); return; }
            
            if (NodeDictionary.ContainsKey(nodeData.id))
            {
                Debug.LogWarning($"[AddNodeVisuals] Node editor for ID '{nodeData.id}' already exists. Skipping visual add.");
                return;
            }

            var nodeEditor = CreateNodeEditorView(nodeData);
           
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
        
        public void AddServiceToNode(CompositeNode parentNode, ServiceNode serviceNode)
        {
            Undo.RecordObject(parentNode, "Add Service Node");
            parentNode.services.Add(serviceNode);

            m_serialLizeObject.Update();
            
            if (NodeDictionary.TryGetValue(parentNode.id, out ND_NodeEditor parentEditor))
            {
                parentEditor.DrawServices(parentNode, this);
            }
            
            EditorUtility.SetDirty(parentNode);
            if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
        }


        private void DrawNodesFromData()
        {
            foreach (Node nodeData in m_BTree.nodes.ToList()) 
            {
                if (nodeData == null)
                {
                    Debug.LogWarning("[DrawNodesFromData] Encountered a null Node instance in m_BTree.nodes list. It will be removed.");
                    m_BTree.nodes.Remove(null);
                    continue;
                }
                
                bool isAttachedService = m_BTree.nodes
                    .OfType<CompositeNode>()
                    .Any(c => c.services.Contains(nodeData as ServiceNode));

                if (!isAttachedService)
                {
                    AddNodeVisuals(nodeData, animate: false);
                }
            }
        }
        
        private void RemoveDataForNode(ND_NodeEditor editorNode)
        {
            if (editorNode == null || editorNode.node == null) return;

            Node nodeToDelete = editorNode.node;
        
            if (nodeToDelete is DecoratorNode decorator && decorator.child != null)
            {
                var parentEditorNode = editorNode.m_InputPort.connections.FirstOrDefault()?.output.node as ND_NodeEditor;
                if (parentEditorNode != null)
                {
                    var childEditorNode = GetEditorNode(decorator.child.id);
                    if (childEditorNode != null)
                    {
                        RemoveDataForEdge(editorNode.m_InputPort.connections.First());
                        RemoveDataForEdge(editorNode.m_OutputPort.connections.First());
                        
                        AddEdgeToData(parentEditorNode.m_OutputPort, childEditorNode.m_InputPort);
                    }
                }
            }
            else 
            {
                var connectedEdges = new List<Edge>();
                if (editorNode.m_InputPort != null) connectedEdges.AddRange(editorNode.m_InputPort.connections);
                if (editorNode.m_OutputPort != null) connectedEdges.AddRange(editorNode.m_OutputPort.connections);
        
                foreach (var edge in connectedEdges)
                {
                    RemoveDataForEdge(edge);
                }
            }
            
            m_BTree.nodes.Remove(nodeToDelete);
            NodeDictionary.Remove(nodeToDelete.id);
            TreeNodes.Remove(editorNode);
            
            Undo.DestroyObjectImmediate(nodeToDelete);
        }

        public ND_NodeEditor GetEditorNode(string nodeID)
        {
            NodeDictionary.TryGetValue(nodeID, out ND_NodeEditor node);
            return node;
        }

        public void DeleteNode(Node node)
        {
            if(node == null) return;
            
            if (NodeDictionary.TryGetValue(node.id, out ND_NodeEditor editorNode))
            {
                Undo.RecordObject(m_BTree, "Delete Node");
                RemoveDataForNode(editorNode);
                RemoveElement(editorNode);
                EditorUtility.SetDirty(m_BTree);
                if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
            }
        }
        
        public void SortChildrenByPosition(CompositeNode composite)
        {
            if (composite == null || composite.children == null) return;
    
            composite.children.Sort((a, b) => {
                var editorA = GetEditorNode(a.id);
                var editorB = GetEditorNode(b.id);
        
                if (editorA == null || editorB == null) return 0;
        
                return editorA.GetPosition().x.CompareTo(editorB.GetPosition().x);
            });
        }
    }
}