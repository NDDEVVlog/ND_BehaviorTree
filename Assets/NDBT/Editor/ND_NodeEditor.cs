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
        private Port m_OutputPort;
        private List<Port> m_Ports = new List<Port>();
        public SerializedObject m_SerializedObject;

        // The specific container in the UXML where child nodes can be dropped.
        public VisualElement m_DecoratorContainer;
        private VisualElement m_ChildNodeContainer;


        public Node node => m_Node;
        public List<Port> Ports => m_Ports;


        // --- Constructor ---
        // This is the entry point for creating a new node editor instance.
        public ND_NodeEditor(Node node, SerializedObject BTObject, GraphView graphView)
            // It calls the base Node constructor, providing the path to our custom UXML file.
            : base(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath())
        {
            // All initialization logic is handled in this central method.
            InitializeNodeView(node, BTObject);
        }

        // --- Initialization ---
        private void InitializeNodeView(Node node, SerializedObject BTObject)
        {
            this.m_Node = node;
            this.m_SerializedObject = BTObject;
            this.viewDataKey = node.id; // Crucial for saving the node's position.

            Type typeInfo = node.GetType();
            NodeInfoAttribute info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();

            // Load and apply the stylesheet for the node's appearance.
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUSSPath());
            if (styleSheet != null)
            {
                this.styleSheets.Add(styleSheet);
            }

            // --- Query UXML Elements ---
            // Find the specific containers we defined in the UXML file.
            m_DecoratorContainer = this.Q<VisualElement>("decorator-container");
            var topPortContainer = this.Q<VisualElement>("top-port");
            var bottomPortContainer = this.Q<VisualElement>("bottom-port");
             
            var titleLabel = this.Q<Label>("title-label");
            var taskNodeContent = this.Q<VisualElement>("task-node-content");

            var iconContainer = this.Q<VisualElement>("icon"); // Get the icon's container
            var iconImage = this.Q<Image>("icon-image");       // Get the new Image element 

            m_ChildNodeContainer = this.Q<VisualElement>("child-node-container");
            
            titleLabel.text = info.title;

            if (iconImage != null && !string.IsNullOrEmpty(info.iconPath))
            {
                // Load the texture from the path specified in the attribute.
                Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath);
                if (iconTexture != null)
                {
                    // Assign the loaded texture to the Image element.
                    iconImage.image = iconTexture;
                }
                else
                {
                    // If the icon isn't found, log a warning for easy debugging.
                    Debug.LogWarning($"Behavior Tree: Icon not found at path '{info.iconPath}' for node '{info.title}'.");
                }
            }
            else
            {
                // If no icon path is provided, hide the icon container entirely.
                iconContainer.style.display = DisplayStyle.None;
            }



            // If this is a composite node, draw its children.
            if (m_Node is CompositeNode compositeNode)
            {
                this.AddToClassList("composite-node");
                DrawChildren(compositeNode);
            }
            else
            {
                // Hide the container if it's not a composite node.
                m_ChildNodeContainer.style.display = DisplayStyle.None;
            }

            // --- Create Ports based on NodeInfo attribute ---
            if (info.hasFlowInput)
            {
                Port inputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(PortType.FlowPort));
                inputPort.portName = "";
                topPortContainer.Add(inputPort);
                m_Ports.Add(inputPort);
            }

            if (info.hasFlowOutput)
            {
                m_OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(PortType.FlowPort));
                m_OutputPort.portName = "";
                bottomPortContainer.Add(m_OutputPort);
                m_Ports.Add(m_OutputPort);
            }

            // These methods from the base class must be called to finalize the node.
            RefreshExpandedState();
            RefreshPorts();
        }

        // Draws the visual items for the children inside the container
        public void DrawChildren(CompositeNode composite)
        {
            m_ChildNodeContainer.Clear();
            if (composite.children == null) return;
            
            foreach (var childNode in composite.children)
            {
                var childView = CreateChildNodeView(childNode);
                m_ChildNodeContainer.Add(childView);
            }
        }

        // Creates a single visual element for a child node (Decorator or Service)
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

        // --- Data Persistence ---
        public void SavePosition()
        {
            // Updates the node's data model with its current GUI position.
            m_Node.SetPosition(GetPosition());
        }

        #region IDropTarget Implementation (Handles Drag-and-Drop)

        // Checks if a dragged item can be dropped onto this node.
        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            // Condition 1: The decorator container must exist and be visible.
            if (m_DecoratorContainer == null || m_DecoratorContainer.style.display == DisplayStyle.None)
            {
                return false;
            }

            // Condition 2: The dragged item must be a single node.
            if (selection.Count != 1 || !(selection.First() is ND_NodeEditor draggedNode))
            {
                return false;
            }
            
            // Condition 3: Cannot drop a node onto itself.
            if(draggedNode == this)
            {
                return false;
            }

            // Condition 4 (Business Logic): Only allow "Decorator" nodes to be dropped.
            return draggedNode.m_Node.GetType().Name == "Decorator";
        }

        // Called when a valid drag operation enters the bounds of this node.
        public bool DragEnter(DragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource)
        {
            if (enteredTarget != this || !CanAcceptDrop(selection.ToList()))
            {
                return false;
            }
            // Apply a visual highlight to the drop zone.
            m_DecoratorContainer?.AddToClassList("drop-zone-highlight");
            return true;
        }

        // Called when a drag operation leaves the bounds of this node.
        public bool DragLeave(DragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource)
        {
            // Always remove the visual highlight.
            m_DecoratorContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }

        // Called continuously while an item is dragged over this node.
        public bool DragUpdated(DragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            // Continuously check if the drop is valid.
            return CanAcceptDrop(selection.ToList());
        }

        // Called when the user releases the mouse to perform the drop.
        public bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            // Final check to ensure the drop is valid.
            if (!CanAcceptDrop(selection.ToList()))
            {
                return false;
            }

            ND_NodeEditor droppedNodeEditor = selection.First() as ND_NodeEditor;
            if (droppedNodeEditor != null)
            {
                // 1. Remove the dropped node from its current visual parent (the main graph view).
                droppedNodeEditor.RemoveFromHierarchy();

                // 2. Add the node to our decorator container, making it a visual child.
                m_DecoratorContainer.Add(droppedNodeEditor);

                // --- IMPORTANT: UPDATE THE ACTUAL DATA MODEL HERE ---
                // For example:
                // if (this.m_Node is ICompositeNode parent && droppedNodeEditor.m_Node is IChildNode child)
                // {
                //    parent.AddChild(child);
                // }
                // This ensures the parent-child relationship is saved.

                evt.StopPropagation(); // Mark the event as handled.
                return true;
            }

            return false;
        }

        public virtual bool DragExited()
        {
             m_DecoratorContainer?.RemoveFromClassList("drop-zone-highlight");
             return true;
        }

        #endregion
    }
}