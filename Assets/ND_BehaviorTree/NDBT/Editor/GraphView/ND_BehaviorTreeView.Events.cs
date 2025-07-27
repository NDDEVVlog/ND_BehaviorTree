// --- MODIFIED FILE: ND_BehaviorTreeView.Events.cs ---

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// This partial class handles GraphView events, such as element changes,
    /// deletion requests, and animated element removal.
    /// </summary>
    public partial class ND_BehaviorTreeView
    {
        private GraphViewChange OnGraphViewInternalChange(GraphViewChange graphViewChange)
        {
            // If our custom deletion method is running, it will handle all data and Undo logic.
            // We must return here to prevent this callback from interfering and causing duplicate operations.
            if (m_isDeletingProgrammatically)
            {
                return graphViewChange;
            }

            bool hasViewMadeChanges = false;

            if (graphViewChange.movedElements != null && graphViewChange.movedElements.Any())
            {
                var parentsToReorder = new HashSet<CompositeNode>();
                Undo.RecordObject(m_serialLizeObject.targetObject, "Moved Graph Elements");
                
                foreach (ND_NodeEditor editorNode in graphViewChange.movedElements.OfType<ND_NodeEditor>())
                {
                    editorNode.SavePosition();

                    // Find the parent of the moved node to trigger a sort
                    if (editorNode.m_InputPort != null && editorNode.m_InputPort.connected)
                    {
                        // A node can only have one parent
                        var parentEdge = editorNode.m_InputPort.connections.FirstOrDefault();
                        if (parentEdge?.output?.node is ND_NodeEditor parentEditorNode)
                        {
                            if (parentEditorNode.node is CompositeNode compositeParent)
                            {
                                parentsToReorder.Add(compositeParent);
                            }
                        }
                    }
                }
                
                // Re-sort the children list for each affected parent
                foreach (var parentNode in parentsToReorder)
                {
                    SortChildrenByPosition(parentNode);
                }

                hasViewMadeChanges = true;
            }

            if (graphViewChange.elementsToRemove != null && graphViewChange.elementsToRemove.Any())
            {
                List<Edge> edgesRemovedByGraphView = graphViewChange.elementsToRemove.OfType<Edge>().ToList();
                List<ND_NodeEditor> nodesRemovedByGraphView = graphViewChange.elementsToRemove.OfType<ND_NodeEditor>().ToList();

                if (edgesRemovedByGraphView.Any() || nodesRemovedByGraphView.Any())
                {
                    Undo.RecordObject(m_serialLizeObject.targetObject, "Graph Elements Removed by View");
                    // These methods now modify the Node.children list directly.
                    foreach (Edge edge in edgesRemovedByGraphView) RemoveDataForEdge(edge);
                    foreach (ND_NodeEditor node in nodesRemovedByGraphView) RemoveDataForNode(node);
                    hasViewMadeChanges = true;
                }
            }

            if (graphViewChange.edgesToCreate != null && graphViewChange.edgesToCreate.Any())
            {
                Undo.RecordObject(m_serialLizeObject.targetObject, "Created Graph Connections");
                foreach (Edge edge in graphViewChange.edgesToCreate)
                {
                    Debug.Log("Create Connect");
                    // This method now modifies the Node.children list directly.
                    CreateDataForEdge(edge);
                }
                hasViewMadeChanges = true;
            }

            if (hasViewMadeChanges)
            {
                EditorUtility.SetDirty(m_BTree);
                if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
            }
            return graphViewChange;
        }
        
        private void OnDeleteSelectionKeyPressed(string operationName, AskUser askUser)
        {
            List<GraphElement> elementsToDelete = selection.OfType<GraphElement>().ToList();
            if (elementsToDelete.Count > 0)
            {
                InitiateAnimatedRemoveElements(elementsToDelete);
                ClearSelection();
            }
        }

        private void OnNodeCreationRequest(NodeCreationContext context)
        {
            OpenSearchWindow(context.screenMousePosition);
            
        }
        
        public async void InitiateAnimatedRemoveElements(List<GraphElement> elements)
        {
            if (elements == null || !elements.Any()) return;

            m_isDeletingProgrammatically = true;
            try
            {
                // Use a HashSet to gather all unique elements that need to be removed.
                var allElementsToDelete = new HashSet<GraphElement>(elements);
                var nodesInSelection = elements.OfType<ND_NodeEditor>().ToList();

                // For each selected node, find its connected edges and add them to the deletion set.
                foreach (var node in nodesInSelection)
                {
                    if (node.m_InputPort != null)
                    {
                        // Must ToList() because the underlying collection will be modified during removal.
                        foreach (var edge in node.m_InputPort.connections.ToList()) 
                        {
                            allElementsToDelete.Add(edge);
                        }
                    }
                    if (node.m_OutputPort != null)
                    {
                        // Must ToList() because the underlying collection will be modified during removal.
                        foreach (var edge in node.m_OutputPort.connections.ToList())
                        {
                            allElementsToDelete.Add(edge);
                        }
                    }
                }
                
                // Now separate the final, complete set into nodes and edges.
                List<ND_NodeEditor> nodesToAnimateAndRemove = allElementsToDelete.OfType<ND_NodeEditor>().ToList();
                List<Edge> edgesToRemove = allElementsToDelete.OfType<Edge>().ToList();

                Undo.RecordObject(m_serialLizeObject.targetObject, "Delete Graph Elements");
                
                // Animate only the nodes before they are removed.
                await GraphViewAnimationHelper.AnimateAndPrepareForRemoval(nodesToAnimateAndRemove);
                
                bool dataChanged = false;
                
                // It's safer to remove edges first, as they reference nodes.
                foreach (var edge in edgesToRemove)
                {
                    if (edge.parent != null)
                    {
                        RemoveDataForEdge(edge);
                        RemoveElement(edge);
                        dataChanged = true;
                    }
                }
                
                foreach (var nodeEditor in nodesToAnimateAndRemove)
                {
                    if (nodeEditor.parent != null)
                    {
                        RemoveDataForNode(nodeEditor);
                        RemoveElement(nodeEditor);
                        dataChanged = true;
                    }
                }
                
                if (dataChanged)
                {
                    m_serialLizeObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(m_BTree);
                    if (m_editorWindow != null) m_editorWindow.SetUnsavedChanges(true);
                    this.MarkDirtyRepaint();
                }
            }
            finally
            {
                // Ensure the flag is always reset, even if an exception occurs.
                m_isDeletingProgrammatically = false;
            }
        }
    }
}