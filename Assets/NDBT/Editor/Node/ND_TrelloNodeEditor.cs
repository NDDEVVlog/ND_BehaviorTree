// File: Assets/NDBT/Editor/NodeEditors/ND_TrelloNodeEditor.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using PlasticGui;
using System.Collections.Generic;

namespace ND_BehaviorTree.Editor
{
    public class ND_TrelloNodeEditor : ND_NodeEditor
    {
        private VisualElement collapseEara;
        private Button m_AddChildButton;
        
        public ND_TrelloNodeEditor(Node node, SerializedObject BTObject,GraphView graphView)
            : base(node, BTObject,graphView, ND_BehaviorTreeSetting.Instance.GetTrelloUXMLPath())
        {
            m_SerializedObject = BTObject;
            drawTrelloView = graphView;
            this.AddToClassList("trello-main-node-editor");


            m_AddChildButton = this.Q<Button>("add-child-task-button");
            if (m_AddChildButton != null)
            {
                m_AddChildButton.clicked += AddNewChildTask;
            }
            else
            {
                Debug.LogError("ND_TrelloNodeEditor: 'add-child-task-button' not found in UXML.");
            }

            m_DragableNodeContainer = this.Q<VisualElement>("draggable-nodes-container");
            if (m_DragableNodeContainer != null)
                Debug.Log("ND_TrelloNodeEditor: 'draggable-nodes-container' found in UXML.");
            
            PopulateExistingChildTasks();
        }

        private void PopulateExistingChildTasks()
        {
            if (m_DragableNodeContainer == null)
            {
                Debug.LogError("ND_TrelloNodeEditor: m_DragableNodeContainer is null. Cannot populate child tasks.");
                return;
            }

            TrelloNode trelloNodeData = node as TrelloNode;
            if (trelloNodeData == null)
            {
                Debug.LogError("ND_TrelloNodeEditor: Node data is not of type TrelloNode.");
                return;
            }
            
            m_DragableNodeContainer.Clear(); 

            if(trelloNodeData.childrenNode.Count >0)
            foreach (TrelloChildNode childData in trelloNodeData.childrenNode)
            {
                if (childData != null)
                    CreateAndAddChildEditor(childData);
            }
        }

        private void AddNewChildTask()
        {
            TrelloNode trelloNodeData = node as TrelloNode;
            if (trelloNodeData == null) return;
            
            Undo.RecordObject(m_SerializedObject.targetObject, "Add Trello Child Task");

            TrelloChildNode newChildData = ScriptableObject.CreateInstance<TrelloChildNode>();
            AssetDatabase.AddObjectToAsset(newChildData,node);

            newChildData.SetNewID (System.Guid.NewGuid().ToString());
            newChildData.task = $"New Task {trelloNodeData.childrenNode.Count + 1}";
            trelloNodeData.childrenNode.Add(newChildData);

            
            
            EditorUtility.SetDirty(m_SerializedObject.targetObject);
            m_SerializedObject.ApplyModifiedProperties(); 
            m_SerializedObject.Update();
            AssetDatabase.SaveAssets();

            CreateAndAddChildEditor(newChildData);
        }

        public void LoadTrelloChildren()
        {   
            TrelloNode trelloNodeData = node as TrelloNode;
            if (trelloNodeData == null) return;
            foreach (TrelloChildNode e in trelloNodeData.childrenNode)
            {
                CreateAndAddChildEditor(e);
                
            }
        }

        private ND_TrelloChild CreateAndAddChildEditor(TrelloChildNode childData)
        {
            if (m_DragableNodeContainer == null)
            {
                Debug.LogError("ND_TrelloNodeEditor: m_DragableNodeContainer is null. Cannot add child task editor.");
                return null;
            }

            ND_TrelloChild childEditor = new ND_TrelloChild(childData, m_SerializedObject, drawTrelloView);

            drawTrelloView.AddElement(childEditor);

            childEditor.AddToClassList("appeared");
            // this.GetFirstAncestorOfType<ND_DrawTrelloView>().AddElement(childEditor);
            m_DragableNodeContainer.Add(childEditor);

            childEditor.style.position = Position.Relative;
            childEditor.style.left = StyleKeyword.Auto;
            childEditor.style.top = StyleKeyword.Auto;
            childEditor.style.width = new StyleLength(new Length(80, LengthUnit.Percent));
            childEditor.style.marginBottom = 2;
            childEditor.UpdateNode();
            //childEditor.BringToFront(); 

            return childEditor;
        }
        
        public override bool DragPerform(DragPerformEvent evt, System.Collections.Generic.IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            if (m_DragableNodeContainer == null) return false;

             string targetNodeTitle = (this.node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "TRELLO_NODE_UNKNOWN (Target)";

            string draggedItemInfo = GetDraggedItemTitle(selection); // Using local or base's GetDraggedItemTitle
            string logPrefix = $"<color=purple>DragPerform (Trello '{targetNodeTitle}')</color> for item <b>{draggedItemInfo}</b>: ";

            Debug.Log($"{logPrefix} Initiated.");

            var droppedNodeEditor = selection.FirstOrDefault() as ND_NodeEditor;
            if (droppedNodeEditor is ND_TrelloChild trelloChildEditor)
            {
                if (CanAcceptDrop(selection.ToList())) 
                {
                    var childData = trelloChildEditor.node as TrelloChildNode;
                    
                    TrelloNode trelloNodeData = this.node as TrelloNode;

                    // Check if child is contained in List



                    if (childData != null && trelloNodeData != null)
                    {
                        var oldParentVisual = trelloChildEditor.parent;
                        ND_TrelloNodeEditor oldParentEditor = null;
                        if (oldParentVisual is VisualElement ve)
                        { // Check if parent is part of another TrelloNode
                            var parentNodeEditorCandidate = ve.GetFirstAncestorOfType<ND_TrelloNodeEditor>();
                            if (parentNodeEditorCandidate != null && parentNodeEditorCandidate != this)
                            {
                                oldParentEditor = parentNodeEditorCandidate;
                            }
                        }

                        Undo.RecordObject(m_SerializedObject.targetObject, "Move Trello Child Task");

                        if (oldParentEditor != null)
                        {
                            TrelloNode oldTrelloParentData = oldParentEditor.node as TrelloNode;
                            oldTrelloParentData?.childrenNode.Remove(childData);
                        }

                        if (!trelloNodeData.childrenNode.Contains(childData))
                        {
                            trelloNodeData.childrenNode.Add(childData);
                            EditorUtility.SetDirty(m_SerializedObject.targetObject);
                            m_SerializedObject.ApplyModifiedProperties();
                            m_SerializedObject.Update();

                            trelloChildEditor.RemoveFromHierarchy();
                            m_DragableNodeContainer.Add(trelloChildEditor);

                            this.GetFirstAncestorOfType<ND_BehaviorTreeView>().RemoveNodeDataOutOfGraph(trelloChildEditor);


                            trelloChildEditor.style.position = Position.Relative;
                            trelloChildEditor.style.left = StyleKeyword.Auto;
                            trelloChildEditor.style.top = StyleKeyword.Auto;
                            trelloChildEditor.style.width = new StyleLength(new Length(80, LengthUnit.Percent));
                            trelloChildEditor.style.marginBottom = 2;
                            evt.StopPropagation();
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                  
                    }
                }
            }
            return base.DragPerform(evt, selection, dropTarget, dragSource);
        }

        public override bool DragLeave(DragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource)
        {
            string nodeTitle = (m_Node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "UNKNOWN_NODE";
            Debug.Log($"<color=orange>DragLeave</color> Node: <b>'{nodeTitle}'</b>.");
            // leftTarget is the element the drag pointer is leaving.
            if (leftTarget == this || this.Contains(leftTarget as VisualElement)) // Check if leaving this node or one of its children
            {

                this.RemoveFromClassList("drag-over-target");
                m_DragableNodeContainer?.RemoveFromClassList("drop-zone-highlight");

                var droppedNodeEditor = selection.FirstOrDefault() as ND_NodeEditor;

                if (droppedNodeEditor is ND_TrelloChild trelloChildEditor)
                {

                    var childData = trelloChildEditor.node as TrelloChildNode;
                    
                    TrelloNode trelloNodeData = this.node as TrelloNode;


                    if (trelloNodeData.childrenNode.Contains(childData))
                    {
                        trelloNodeData.childrenNode.Remove(childData);
                        this.GetFirstAncestorOfType<ND_BehaviorTreeView>().AddNode(childData, droppedNodeEditor);
                        trelloChildEditor.style.position = Position.Relative;
                        trelloChildEditor.style.left = StyleKeyword.None;
                        trelloChildEditor.style.top = StyleKeyword.None;
                        trelloChildEditor.style.width = new StyleLength(new Length(150, LengthUnit.Pixel));
                        return true;
                    }

                    //m_DragableNodeContainer.Remove(droppedNodeEditor);
                    
                }

            }
            return true; // Usually true to allow event to propagate if needed
        }

        public void RemoveChildTaskData(TrelloChildNode childDataToRemove)
        {
            TrelloNode trelloNodeData = node as TrelloNode;
            if (trelloNodeData == null || childDataToRemove == null) return;

            Undo.RecordObject(m_SerializedObject.targetObject, "Remove Trello Child Task");
            trelloNodeData.childrenNode.Remove(childDataToRemove);
            EditorUtility.SetDirty(m_SerializedObject.targetObject);
            m_SerializedObject.ApplyModifiedProperties();
            m_SerializedObject.Update();
        }
    }
}