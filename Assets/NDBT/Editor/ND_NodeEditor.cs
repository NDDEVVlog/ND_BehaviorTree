using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using NodeElements = UnityEditor.Experimental.GraphView.Node; // Your alias
using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace ND_BehaviorTree.Editor
{
    public class ND_NodeEditor : NodeElements, IDropTarget
    {
        internal Node m_Node;
        private Port m_OutputPort;

        private List<Port> m_Ports;
        public SerializedProperty m_serializedProperty;
        public SerializedObject m_SerializedObject;

        public GraphView drawTrelloView;

        public Node node => m_Node;

        public List<Port> Ports => m_Ports;

        private VisualElement m_TopPortContainer;
        private VisualElement m_BottomPortContainer;
        private VisualElement m_LeftPortContainer;
        private VisualElement m_RightPortContainer;
        public VisualElement m_DragableNodeContainer; // This will be the target for dropped nodes

        protected ND_NodeEditor(Node node, SerializedObject BTObject, GraphView graphView, string uxmlPath)
            : base(uxmlPath) // Pass the UXML path to the base GraphView.Node constructor
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree != null)
            {
                var root = visualTree.CloneTree();
                this.mainContainer.Clear();
                this.mainContainer.Add(root); // mainContainer is a standard container in GraphView.Node
            }
            else
            {
                Debug.LogError($"ND_NodeEditor: VisualTreeAsset not found at {uxmlPath}");
            }
            // All initialization now happens here, regardless of which constructor was initially called.
            InitializeNodeView(node, BTObject);
        }

        // Public constructor for using the default UXML path.
        // It now calls the protected constructor using 'this(...)'.
        public ND_NodeEditor(Node node, SerializedObject BTObject, GraphView graphView)
            : this(node, BTObject, graphView, ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath())
        {
            m_Node = node;
            Type typeInfo = node.GetType();
            NodeInfoAttribute info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();
            title = info.title;
        }

        private void InitializeNodeView(Node node, SerializedObject BTObject)
        {

            Debug.Log("CreatingNode");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUSSPath());
            if (styleSheet != null)
            {
                this.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning("ND_NodeEditor: StyleSheet not found at Assets/NDBT/Editor/Resources/Styles/VisualElement/NodeElementUss.uss");
            }

            // Debug.Log(ND_BTSetting.Instance.GetNodeDefaultUXMLPath()); // UXML path debug

            this.AddToClassList("nd-node");

            m_SerializedObject = BTObject;
            m_Node = node;

            m_TopPortContainer = this.Q<VisualElement>("top-port");
            m_BottomPortContainer = this.Q<VisualElement>("bottom-port");


            // Query for the specific container where nodes can be dropped
            // This targets the *first* element named "draggable-nodes-container" in your UXML.
            m_DragableNodeContainer = this.Q<VisualElement>("draggable-nodes-container");


            Type typeInfo = node.GetType();
            NodeInfoAttribute info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();
            title = info.title;

            m_Ports = new List<Port>();

            string[] depthhs = info.menuItem.Split('/');
            foreach (string depth in depthhs)
            {
                this.AddToClassList(depth.ToLower().Replace(' ', '-'));
            }

            this.name = typeInfo.Name;

            if (info.HasFlowOutput && m_BottomPortContainer != null)
            {
                CreateOutputPort();
            }

            if (info.HasFlowInput && m_TopPortContainer != null)
            {
                CreateInputPort();
            }

            foreach (FieldInfo property in typeInfo.GetFields())
            {
                if (property.GetCustomAttribute<ExposePropertyAttribute>() is ExposePropertyAttribute exposeProperty)
                {
                    PropertyField field = DrawProperty(property.Name);
                    //field.RegisterValueChangeCallback(OnFieldChangeCallBack);
                }
            }
            this.AddManipulator(new DoubleClickNodeManipulator(this));
            this.AddManipulator(new CreateDecorator());
            RefreshExpandedState();
            RefreshPorts();
        }

        private void OnFieldChangeCallBack(SerializedPropertyChangeEvent evt)
        {
            throw new NotImplementedException();
        }

        private void FetchSerializedProperty()
        {
            SerializedProperty nodes = m_SerializedObject.FindProperty("m_nodes");
            if (nodes != null && nodes.isArray)
            {
                for (int i = 0; i < nodes.arraySize; i++)
                {
                    var element = nodes.GetArrayElementAtIndex(i);
                    if (element == null) continue;
                    var elementId = element.FindPropertyRelative("m_guid");
                    if (elementId != null && elementId.stringValue == node.id)
                    {
                        m_serializedProperty = element;
                        return; // Found
                    }
                }
            }
            // Debug.LogWarning($"ND_NodeEditor: Could not find serialized property for node ID {node.id} ({node.typeName})");
        }

        private PropertyField DrawProperty(string name)
        {
            if (m_serializedProperty == null)
            {
                FetchSerializedProperty();
            }

            if (m_serializedProperty == null)
            {
                Debug.LogError($"ND_NodeEditor ({this.title}): m_serializedProperty is null. Cannot draw property '{name}'. Ensure FetchSerializedProperty works correctly.");
                return null;
            }

            SerializedProperty prop = m_serializedProperty.FindPropertyRelative(name);
            if (prop == null)
            {
                Debug.LogError($"ND_NodeEditor ({this.title}): Property '{name}' not found in serialized property '{m_serializedProperty.propertyPath}'.");
                return null;
            }

            PropertyField field = new PropertyField(prop);
            field.bindingPath = prop.propertyPath; // Not strictly necessary if prop is passed to constructor, but good for clarity.

            // Ensure extensionContainer (from base Node class) is valid
            if (extensionContainer == null)
            {
                Debug.LogError($"ND_NodeEditor ({this.title}): extensionContainer is null. Cannot add property field. Check UXML for an element named 'extension'.");
                return field; // Return field even if not added, to prevent further nullrefs
            }

            extensionContainer.Add(field);
            // Debug.Log($"ND_NodeEditor ({this.title}): Added property '{name}'. ExtensionContainer children: {extensionContainer.childCount}");
            return field;
        }

        private void CreateOutputPort()
        {
            m_OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(PortType.FlowPort));
            m_OutputPort.portName = "";
            m_OutputPort.tooltip = "The flow OutPut";
            m_Ports.Add(m_OutputPort);
            if (m_BottomPortContainer != null)
                m_BottomPortContainer.Add(m_OutputPort);
            else
                outputContainer.Add(m_OutputPort); // Fallback to default output container
        }

        private void CreateInputPort()
        {
            Port m_InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(PortType.FlowPort));
            m_InputPort.portName = "";
            m_InputPort.tooltip = "The flow input";
            m_Ports.Add(m_InputPort);
            if (m_TopPortContainer != null)
                m_TopPortContainer.Add(m_InputPort);
            else
                inputContainer.Add(m_InputPort); // Fallback to default input container
        }

        public void SavePosition()
        {
            m_Node.SetPosition(GetPosition());
        }

        #region IDropTarget Implementation
        public string GetDraggedItemTitle(IEnumerable<ISelectable> selection)
        {
            if (selection != null && selection.Any())
            {
                var firstSelectable = selection.First();
                if (firstSelectable is ND_NodeEditor draggedNode)
                {
                    return $"'{draggedNode.title}' (Type: {draggedNode.GetType().Name})";
                }
                else if (firstSelectable is GraphElement ge)
                {
                    return $"'{ge.name ?? "Unnamed GE"}' (Type: {ge.GetType().Name})";
                }
                return $"(Type: {firstSelectable.GetType().Name})";
            }
            return "NOTHING";
        }

        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            if (m_DragableNodeContainer == null) // Ensure the drop zone exists
            {
                return false;
            }

            if (selection == null || !selection.Any())
            {
                return false;
            }

            if (selection.Count == 1)
            {
                if (selection.First() is ND_NodeEditor draggedNodeEditor && draggedNodeEditor != this)
                {
                    // Prevent dropping if it's already a child of this specific container
                    if (m_DragableNodeContainer.Children().Contains(draggedNodeEditor))
                    {
                        return false;
                    }
                    return true; // It's a different ND_NodeEditor and not already a child here
                }
            }
            return false;
        }

        public virtual bool DragUpdated(DragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            string nodeTitle = (m_Node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "UNKNOWN_NODE";
            string draggedItemInfo = GetDraggedItemTitle(selection);

            if (dropTarget == this)
            {
                Debug.Log($"<color=blue>DragUpdated</color>: Dragging <b>{draggedItemInfo}</b> over Node: <b>'{nodeTitle}'</b>. CanAcceptDrop: {CanAcceptDrop(selection.ToList())}");
            }
            return CanAcceptDrop(selection.ToList());
        }

        public virtual bool DragPerform(DragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource)
        {
            if (m_DragableNodeContainer == null) return false;


            if (CanAcceptDrop(selection.ToList()))
            {
                ND_NodeEditor droppedNodeEditor = selection.First() as ND_NodeEditor;
                if (droppedNodeEditor != null)
                {
                    Debug.Log($"Node '{droppedNodeEditor.title}' is being dropped onto Node '{this.title}' in container '{m_DragableNodeContainer.name}'.");

                    // 1. Remove from its current visual parent
                    droppedNodeEditor.RemoveFromHierarchy();

                    // 2. Add to the designated container in this node
                    m_DragableNodeContainer.Add(droppedNodeEditor);

                    // 3. Adjust styling for being a child element instead of a free-floating graph node
                    droppedNodeEditor.style.position = Position.Relative;
                    droppedNodeEditor.style.left = StyleKeyword.Auto; // Or StyleKeyword.Null / float.NaN
                    droppedNodeEditor.style.top = StyleKeyword.Auto;
                    droppedNodeEditor.style.right = StyleKeyword.Auto;
                    droppedNodeEditor.style.bottom = StyleKeyword.Auto;
                    droppedNodeEditor.style.width = new StyleLength(new Length(80, LengthUnit.Percent));
                    // droppedNodeEditor.style.marginBottom = 5; // Optional: Add some spacing

                    // Optional: Notify the GraphView that an element's hierarchy changed if needed,
                    // though simple visual reparenting might not require explicit GraphView notification
                    // unless it affects GraphView's internal model of elements.

                    // --- Implement your actual data model update logic here ---
                    // For example, if m_treeNode can have children:
                    // if (this.m_treeNode is ICompositeNode compositeParent && droppedNodeEditor.m_treeNode is IChildNode childNodeData)
                    // {
                    //    // You might need to remove childNodeData from its old parent in the data model first
                    //    compositeParent.AddChild(childNodeData);
                    //    Debug.Log($"Data: Node '{droppedNodeEditor.m_treeNode.typeName}' added as child to '{this.m_treeNode.typeName}'.");
                    // }

                    // Mark the event as handled
                    evt.StopPropagation();
                    return true;
                }
            }
            return false;
        }

        public virtual bool DragEnter(DragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource)
        {
            string nodeTitle = (m_Node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "UNKNOWN_NODE";
            if (enteredTarget == this) // Only log if this node is the one being entered
            {
                Debug.Log($"<color=green>DragEnter</color> Node: <b>'{nodeTitle}'</b>. CanAcceptDrop: {CanAcceptDrop(selection.ToList())}");
                if (CanAcceptDrop(selection.ToList()))
                {
                    this.AddToClassList("drag-over-target");
                    m_DragableNodeContainer?.AddToClassList("drop-zone-highlight"); // Highlight specific drop zone

                    return true;
                }
            }
            return false;
        }

        public virtual bool DragLeave(DragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource)
        {
            string nodeTitle = (m_Node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "UNKNOWN_NODE";
            Debug.Log($"<color=orange>DragLeave</color> Node: <b>'{nodeTitle}'</b>.");
            // leftTarget is the element the drag pointer is leaving.
            if (leftTarget == this || this.Contains(leftTarget as VisualElement)) // Check if leaving this node or one of its children
            {

                this.RemoveFromClassList("drag-over-target");
                m_DragableNodeContainer?.RemoveFromClassList("drop-zone-highlight");
                ND_NodeEditor droppedNodeEditor = selection.First() as ND_NodeEditor;
                
                m_DragableNodeContainer.Remove(droppedNodeEditor);
                this.GetFirstAncestorOfType<ND_BehaviorTreeView>().AddElement(droppedNodeEditor);


            }
            return true; // Usually true to allow event to propagate if needed
        }

        public virtual bool DragExited()
        {
            string nodeTitle = (m_Node != null && !string.IsNullOrEmpty(this.title)) ? this.title : "UNKNOWN_NODE";
            Debug.Log($"<color=red>DragExited</color> (Custom) called on Node: <b>'{nodeTitle}'</b>. This is not a standard IDropTarget event for Nodes.");
            this.RemoveFromClassList("drag-over-target");
            // this.AddToClassList("appeared"); // Re-adding "appeared" might not be desired here, depends on your USS.
            m_DragableNodeContainer?.RemoveFromClassList("drop-zone-highlight");
            return true;
        }

        #endregion
        

        public virtual void UpdateNode(){}
    }
}