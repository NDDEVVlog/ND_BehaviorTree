
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
        
        // This container is now ONLY for services. Decorators are their own nodes on the graph.
        internal VisualElement m_ServiceContainer;

        public Node node => m_Node;
        public List<Port> Ports => m_Ports;
        public ND_BehaviorTreeView m_GraphView;

        private Label titleLabel; // Renamed from 'title' to be more descriptive

        public ND_NodeEditor(Node node, SerializedObject BTObject, GraphView graphView)
            : base(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath())
        {
            m_GraphView = (ND_BehaviorTreeView)graphView;
            InitializeNodeView(node, BTObject, graphView);
        }

        // --- Initialization ---
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
            var typeLabel = this.Q<Label>("type-label");
            titleLabel = this.Q<Label>("title-textfield");
            var iconImage = this.Q<Image>("icon-image");

            // Query the container for services, which was previously the generic child container.
            m_ServiceContainer = this.Q<VisualElement>("child-node-container");

            if (string.IsNullOrEmpty(node.typeName))
            {
                node.typeName = info.title;
            }
            titleLabel.text = node.typeName;
            typeLabel.text = info.title ?? "Node";

            // --- Configure Icon ---
            if (iconImage != null && !string.IsNullOrEmpty(info.iconPath))
            {
                Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath);
                if (iconTexture != null) iconImage.image = iconTexture;
                else iconImage.style.display = DisplayStyle.None;
            }
            else
            {
                iconImage.style.display = DisplayStyle.None;
            }
            
            // --- Configure Services ---
            // Only Composite nodes can have services attached to them.
            if (m_Node is CompositeNode compositeNode)
            {
                this.AddToClassList("composite-node");
                DrawServices(compositeNode, graphView);
            }
            else
            {
                // If it's not a composite node, hide the service container entirely.
                if (m_ServiceContainer != null)
                {
                    m_ServiceContainer.style.display = DisplayStyle.None;
                }
            }

            // --- Create Ports (This now works for Decorators automatically) ---
            if (info.hasFlowInput)
            {
                m_InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(PortType.FlowPort));
                m_InputPort.portName = "";
                topPortContainer.Add(m_InputPort);
                m_Ports.Add(m_InputPort);
            }

            if (info.hasFlowOutput)
            {
                // Decorators should only have one child, so their output capacity is Single.
                var capacity = m_Node is DecoratorNode ? Port.Capacity.Single : Port.Capacity.Multi;
                m_OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, capacity, typeof(PortType.FlowPort));
                m_OutputPort.portName = "";
                bottomPortContainer.Add(m_OutputPort);
                m_Ports.Add(m_OutputPort);
            }

            this.AddManipulator(new DoubleClickNodeManipulator(this));
            RefreshExpandedState();
            RefreshPorts();
        }
        
        /// <summary>
        /// Draws the visual items for attached services inside the node's container.
        /// </summary>
        public void DrawServices(CompositeNode composite, GraphView graphView)
        {
            if (m_ServiceContainer == null) return;
            m_ServiceContainer.Clear();

            if (composite.services == null || composite.services.Count == 0)
            {
                m_ServiceContainer.style.display = DisplayStyle.None;
                return;
            }

            m_ServiceContainer.style.display = DisplayStyle.Flex;
            
            // Draw Services
            foreach (var serviceNode in composite.services)
            {
                if (serviceNode != null)
                {
                    // Assuming ND_AuxiliaryEditor is a VisualElement that can represent a service.
                    var serviceEditor = new ND_AuxiliaryEditor(serviceNode, m_SerializedObject, graphView);
                    m_ServiceContainer.Add(serviceEditor);
                }
            }
        }

        public void SavePosition()
        {
            m_Node.SetPosition(GetPosition());
        }

        #region IDropTarget Implementation (Handles Drag-and-Drop for SERVICES ONLY)
        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            // Cannot drop onto a node that isn't a Composite (and thus has no service container)
            if (!(m_Node is CompositeNode))
            {
                return false;
            }

            // Must be a single node being dragged
            if (selection.Count != 1 || !(selection.First() is ND_NodeEditor draggedNode))
            {
                return false;
            }

            // Can't drop a node onto itself
            if (draggedNode == this)
            {
                return false;
            }

            // *** CRITICAL CHANGE: Only accept nodes that are ServiceNodes ***
            return draggedNode.m_Node is ServiceNode;
        }

        public bool DragEnter(DragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource)
        {
            if (enteredTarget != this || !CanAcceptDrop(selection.ToList()))
            {
                return false;
            }
            m_ServiceContainer?.AddToClassList("drop-zone-highlight");
            return true;
        }

        public bool DragLeave(DragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource)
        {
            m_ServiceContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }

        public bool DragUpdated(DragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            return CanAcceptDrop(selection.ToList());
        }

        public bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            var compositeNode = m_Node as CompositeNode;
            if (compositeNode == null || !CanAcceptDrop(selection.ToList()))
            {
                return false;
            }

            ND_NodeEditor droppedNodeEditor = selection.First() as ND_NodeEditor;
            ServiceNode serviceNode = droppedNodeEditor.m_Node as ServiceNode;
            

            return false;
        }

        public virtual bool DragExited()
        {
            m_ServiceContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }
        #endregion

        public void UpdateNode()
        {
            titleLabel.text = node.typeName;
        }
        
        public void UpdateState()
        {
            RemoveFromClassList("running");
            RemoveFromClassList("success");
            RemoveFromClassList("failure");

            if (m_GraphView.EditorWindow.currentGraph == null || !Application.isPlaying) return;

            var runner = (m_GraphView.EditorWindow as ND_BehaviorTreeEditorWindow)?.m_targetRunner;
            if (runner == null || runner.RuntimeTree == null) return;

            Node runtimeNode = runner.RuntimeTree.FindNode(m_Node.id);
            if (runtimeNode != null)
            {
                switch (runtimeNode.status)
                {
                    case Node.Status.Running: AddToClassList("running"); break;
                    case Node.Status.Success: AddToClassList("success"); break;
                    case Node.Status.Failure: AddToClassList("failure"); break;
                }
            }
        }
        
        public void ClearState()
        {
            RemoveFromClassList("running");
            RemoveFromClassList("success");
            RemoveFromClassList("failure");
        }
    }
}