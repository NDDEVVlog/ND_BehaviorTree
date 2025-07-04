// --- MODIFIED FILE: ND_NodeEditor.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NodeElements = UnityEditor.Experimental.GraphView.Node; // Alias for GraphView.Node

namespace ND_BehaviorTree.Editor
{
    public class ND_NodeEditor : NodeElements, IDropTarget
    {
        // --- Fields ---
        internal Node m_Node;
        internal Port m_InputPort;
        internal Port m_OutputPort;
        private List<Port> m_Ports = new List<Port>();
        public SerializedObject m_SerializedObject;
        internal VisualElement m_ChildNodeContainer;

        public Node node => m_Node;
        public List<Port> Ports => m_Ports;

        public ND_BehaviorTreeView m_GraphView;

        // We can REMOVE the m_GraphView field, it's no longer needed.
        // public ND_BehaviorTreeView m_GraphView; 

        // --- Constructor ---
        // The graphView parameter is still useful for the base ND_AuxiliaryEditor constructor call.
        public ND_NodeEditor(Node node, SerializedObject BTObject, GraphView graphView)
            : base(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath())
        {
            m_GraphView = (ND_BehaviorTreeView)graphView;
            InitializeNodeView(node, BTObject, graphView); // Pass graphView along
        }

        // --- Initialization ---
        // Accept graphView as a parameter to pass down
        private void InitializeNodeView(Node node, SerializedObject BTObject, GraphView graphView)
        {
            this.m_Node = node;
            this.m_SerializedObject = BTObject;
            this.viewDataKey = node.id;

            Type typeInfo = node.GetType();
            NodeInfoAttribute info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUSSPath());
            if (styleSheet != null)
            {
                this.styleSheets.Add(styleSheet);
            }

            // --- Query UXML Elements ---
            var topPortContainer = this.Q<VisualElement>("top-port");
            var bottomPortContainer = this.Q<VisualElement>("bottom-port");
            var titleLabel = this.Q<Label>("title-label");
            var taskNodeContent = this.Q<VisualElement>("task-node-content");
            var iconContainer = this.Q<VisualElement>("icon");
            var iconImage = this.Q<Image>("icon-image");

            m_ChildNodeContainer = this.Q<VisualElement>("child-node-container");

            if (info.title != null)
                titleLabel.text = info.title;

            if (iconImage != null && !string.IsNullOrEmpty(info.iconPath))
            {
                Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath);
                if (iconTexture != null)
                {
                    iconImage.image = iconTexture;
                }
                else
                {
                    Debug.LogWarning($"Behavior Tree: Icon not found at path '{info.iconPath}' for node '{info.title}'.");
                }
            }
            else
            {
                iconImage.style.display = DisplayStyle.None;
            }

            if (m_Node is CompositeNode compositeNode)
            {
                this.AddToClassList("composite-node");
                DrawChildren(compositeNode, graphView); // Pass graphView to DrawChildren
            }
            else
            {
                if (m_ChildNodeContainer != null)
                {
                    m_ChildNodeContainer.style.display = DisplayStyle.None;
                }
            }

            // --- Create Ports ---
            if (info.hasFlowInput)
            {
                m_InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(PortType.FlowPort));
                m_InputPort.portName = "";
                topPortContainer.Add(m_InputPort);
                m_Ports.Add(m_InputPort);
            }

            if (info.hasFlowOutput)
            {
                m_OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(PortType.FlowPort));
                m_OutputPort.portName = "";
                bottomPortContainer.Add(m_OutputPort);
                m_Ports.Add(m_OutputPort);
            }

            this.AddManipulator(new DoubleClickNodeManipulator(this));
            RefreshExpandedState();
            RefreshPorts();
        }

        // Draws the visual items for the children inside the container
        // It now accepts the graphView as a parameter
        public void DrawChildren(CompositeNode composite, GraphView graphView)
        {
            if (m_ChildNodeContainer == null) return;
            m_ChildNodeContainer.Clear();

            if (composite.decorators == null && composite.services == null)
            {
                m_ChildNodeContainer.style.display = DisplayStyle.None;
                
                return;
            }



            // We no longer need to check for a null graphView, because it's passed in.

            // Draw Decorators
            if (composite.decorators != null)
            {
                foreach (var decoratorNode in composite.decorators)
                {
                    if (decoratorNode != null)
                    {
                        var decoratorEditor = new ND_AuxiliaryEditor(decoratorNode, m_SerializedObject, graphView);
                        m_ChildNodeContainer.Add(decoratorEditor);
                    }
                }
            }

            // Draw Services
            if (composite.services != null)
            {
                foreach (var serviceNode in composite.services)
                {
                    if (serviceNode != null)
                    {
                        var serviceEditor = new ND_AuxiliaryEditor(serviceNode, m_SerializedObject, graphView);
                        m_ChildNodeContainer.Add(serviceEditor);
                    }
                }
            }
        }

        // ... THE REST OF THE FILE IS UNCHANGED ...
        private VisualElement CreateChildNodeView(Node childNode)
        {
            var item = new VisualElement();
            item.AddToClassList("child-node-item");

            NodeInfoAttribute info = childNode.GetType().GetCustomAttribute<NodeInfoAttribute>();

            // Apply specific styling based on type
            if (childNode is DecoratorNode) item.AddToClassList("decorator-child");
            if (childNode is ServiceNode) item.AddToClassList("service-child");

            if (!string.IsNullOrEmpty(info.iconPath))
            {
                var icon = new Image { image = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath) };
                icon.AddToClassList("icon-image");
                item.Add(icon);
            }

            var label = new Label(info.title);
            label.AddToClassList("title-label");
            item.Add(label);

            return item;
        }

        public void SavePosition()
        {
            m_Node.SetPosition(GetPosition());
        }

        #region IDropTarget Implementation (Handles Drag-and-Drop)
        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            if (m_ChildNodeContainer == null || m_ChildNodeContainer.style.display == DisplayStyle.None)
            {
                return false;
            }

            if (selection.Count != 1 || !(selection.First() is ND_NodeEditor draggedNode))
            {
                return false;
            }

            if (draggedNode == this)
            {
                return false;
            }

            return draggedNode.m_Node is AuxiliaryNode;
        }

        public bool DragEnter(DragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource)
        {
            if (enteredTarget != this || !CanAcceptDrop(selection.ToList()))
            {
                return false;
            }

            m_ChildNodeContainer?.AddToClassList("drop-zone-highlight");
            return true;
        }

        public bool DragLeave(DragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource)
        {
            m_ChildNodeContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }

        public bool DragUpdated(DragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            return CanAcceptDrop(selection.ToList());
        }

        public bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            if (!CanAcceptDrop(selection.ToList()))
            {
                return false;
            }

            ND_NodeEditor droppedNodeEditor = selection.First() as ND_NodeEditor;
            if (droppedNodeEditor != null)
            {
                droppedNodeEditor.RemoveFromHierarchy();

                m_ChildNodeContainer.Add(droppedNodeEditor);

                evt.StopPropagation();
                return true;
            }

            return false;
        }

        public virtual bool DragExited()
        {
            m_ChildNodeContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }
        #endregion




        // --- NEW METHODS FOR STATE VISUALIZATION ---

        /// <summary>
        /// Updates the visual state of the node based on its runtime status.
        /// </summary>
        public void UpdateState()
        {
            // Clear previous state classes first
            RemoveFromClassList("running");
            RemoveFromClassList("success");
            RemoveFromClassList("failure");

            // This method is only called from the editor window when in play mode with a target runner
            if (m_GraphView.EditorWindow.currentGraph == null || !Application.isPlaying) return;

            var runner = (m_GraphView.EditorWindow as ND_BehaviorTreeEditorWindow)?.m_targetRunner;
            if (runner == null || runner.RuntimeTree == null) return;

            // Find the corresponding runtime node by its GUID
            Node runtimeNode = runner.RuntimeTree.FindNode(m_Node.id);
            if (runtimeNode != null)
            {
                switch (runtimeNode.status)
                {
                    case Node.Status.Running:
                        AddToClassList("running");
                        break;
                    case Node.Status.Success:
                        AddToClassList("success");
                        break;
                    case Node.Status.Failure:
                        AddToClassList("failure");
                        break;
                }
            }
        }

        /// <summary>
        /// Clears any runtime visual state from the node.
        /// </summary>
        public void ClearState()
        {
            RemoveFromClassList("running");
            RemoveFromClassList("success");
            RemoveFromClassList("failure");
        }


        // ... THE REST OF THE FILE IS UNCHANGED ...



    }
}