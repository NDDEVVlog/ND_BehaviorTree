// File: Assets/NDBT/Editor/NodeEditors/ND_TrelloChild.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements; // For PropertyField if you query it
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public class ND_TrelloChild : ND_NodeEditor
    {
        private Button m_editButton;
        private Label m_taskNameLabel;
        private Toggle isCompleteToggle;
        public ND_TrelloChild(ND_BehaviorTree.Node node, SerializedObject BTObject, GraphView graphView)
            : base(node, BTObject, graphView) // Base loads its shell UXML
        {
            // ND_NodeEditor's InitializeNodeView has run:
            // - m_SerializedObject is set.
            // - m_treeNode is set.
            // - FetchSerializedPropertyForCurrentNode() has been called, so m_serializedProperty should be set
            //   if this TrelloChildNode's data can be found as a SerializedProperty relative to m_SerializedObject.

            // Load and clone the *content* UXML
            VisualTreeAsset contentVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ND_BehaviorTreeSetting.Instance.GetTrelloChildPath());
            if (contentVisualTree != null)
            {
                var contentRoot = contentVisualTree.CloneTree();

                this.mainContainer.Clear(); // Clear default content of the shell
                this.mainContainer.Add(contentRoot); // Add our custom card content

                StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetTrelloUSSPath());
                this.styleSheets.Add(styleSheet);

                m_SerializedObject = BTObject;
                m_taskNameLabel = contentRoot.Q<Label>("taskNameLabel");




            }
            else
            {
                Debug.LogError($"ND_TrelloChild ({this.node?.id}): Could not load content UXML from {ND_BehaviorTreeSetting.Instance.GetTrelloChildPath()}");
            }

            this.AddToClassList("trello-child-node-view");

            // this.capabilities &= ~Capabilities.Movable;
            // this.capabilities &= ~Capabilities.Deletable;
            // this.capabilities &= ~Capabilities.Stackable;
            // this.capabilities &= ~Capabilities.Collapsible;

            var baseTitleElement = this.Q<VisualElement>("title"); // From the shell UXML
            if (baseTitleElement != null)
            {
                baseTitleElement.style.display = DisplayStyle.None;
            }

            var baseExtensionContainer = this.Q<VisualElement>("extension"); // From the shell UXML
            if (baseExtensionContainer != null)
            {
                baseExtensionContainer.style.display = DisplayStyle.None;
            }
            isCompleteToggle = this.Q<Toggle>("isCompleteToggle");

            if (isCompleteToggle != null)
            {




                isCompleteToggle.RegisterValueChangedCallback(evt =>
                {
                    TrelloChildNode trelloNode = node as TrelloChildNode;
                    trelloNode.isComplete = evt.newValue;
                    Debug.Log($"ND_TrelloChild: isComplete changed to {evt.newValue}");
                });



            }

            // Query for the button *within the cloned content*
            m_editButton = this.mainContainer.Q<Button>("edit-task-button"); // Search within mainContainer
            if (m_editButton != null)
            {
                m_editButton.clicked += OnEditTaskClicked;
            }
            else
            {
                Debug.LogWarning($"ND_TrelloChild ({this.node?.id}): 'edit-task-button' not found in cloned UXML content.");
            }

            UpdateNodeName();
            RefreshExpandedState();
        }

        private void UpdateNodeName()
        {
            if (node is TrelloChildNode trelloChild)
            {
                m_taskNameLabel.text = trelloChild.task;
                isCompleteToggle.value = trelloChild.isComplete;
            }

        }

        private void OnEditTaskClicked()
        {
            TrelloChildNode childNodeData = this.node as TrelloChildNode;
            if (childNodeData != null)
            {
                Debug.Log($"Edit button clicked for task: '{childNodeData.task}' (ID: {childNodeData.id})");

                // Query within mainContainer where UXML was cloned
                var taskPropertyField = this.mainContainer.Q<PropertyField>("taskField");
                TextField taskTextField = taskPropertyField?.Q<TextField>();

                if (taskTextField != null)
                {
                    taskTextField.Focus();
                    taskTextField.SelectAll();
                }
                else
                {
                    Debug.LogWarning("ND_TrelloChild: Could not find TextField within taskField to focus.");
                }
            }

        }

        public void RequestDelete()
        {
            Debug.Log($"ND_TrelloChild ({this.node?.id}): Delete requested.");
            var parentTrelloNodeEditor = this.GetFirstAncestorOfType<ND_TrelloNodeEditor>();
            if (parentTrelloNodeEditor != null && this.node is TrelloChildNode childData)
            {
                parentTrelloNodeEditor.RemoveChildTaskData(childData);
                this.RemoveFromHierarchy();
            }
            else
            {
                this.parent?.Remove(this);
            }
        }



        public override void UpdateNode()
        {
            Debug.Log("UpdateFromTrello");

            UpdateNodeName();
            base.UpdateNode();
        }

        public override bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            return false;
        }
    }
}