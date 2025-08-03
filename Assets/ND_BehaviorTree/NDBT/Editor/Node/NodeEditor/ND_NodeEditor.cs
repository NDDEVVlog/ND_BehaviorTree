using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Codice.CM.Common;
using ND_BehaviorTree.GOAP;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using NodeElements = UnityEditor.Experimental.GraphView.Node; // Alias for GraphView.Node

namespace ND_BehaviorTree.Editor
{
    public class ND_NodeEditor : NodeElements, IDropTarget
    {
        // --- Helper classes for runtime UI updates ---
        private class ExposedPropertyUpdater
        {
            public FieldInfo fieldInfo;
            public Label valueLabel;
        }

        private class ProgressBarUpdater
        {
            public NodeProgressBar attribute;
            public VisualElement fillElement;
        }

        // --- Fields ---
        internal Node m_Node;
        internal Port m_InputPort;
        internal Port m_OutputPort;
        internal List<Port> m_Ports = new List<Port>();
        public SerializedObject m_SerializedObject;

        internal VisualElement m_ServiceContainer;
        internal VisualElement m_DetailsContainer; // Container for exposed vars/progress bars

        public Node node => m_Node;
        public List<Port> Ports => m_Ports;
        public ND_BehaviorTreeView m_GraphView;

        public Label titleLabel;
        public string StylePath = "Default";

        // --- Caches for runtime updates ---
        private readonly List<ExposedPropertyUpdater> m_ExposedPropertyUpdaters = new List<ExposedPropertyUpdater>();
        private readonly List<ProgressBarUpdater> m_ProgressBarUpdaters = new List<ProgressBarUpdater>();

        string styleSheetPath;
        string styleDefaultPath;

        public ND_NodeEditor(Node node, SerializedObject btObject, GraphView graphView, string styleSheetPath)
           : base(ND_BehaviorTreeSetting.Instance.GetNodeDefaultUXMLPath())
        {
            this.styleSheetPath = styleSheetPath;
            this.styleDefaultPath = ND_BehaviorTreeSetting.Instance.GetStyleSheetPath("Default");

            if (!string.IsNullOrEmpty(styleDefaultPath))
            {
                StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleDefaultPath);
                if (styleSheet != null)
                {
                    this.styleSheets.Add(styleSheet);
                }
                else
                {
                    Debug.LogWarning($"[ND_NodeEditor] Không thể tải stylesheet tại đường dẫn: {styleDefaultPath}");
                }
            }
            m_GraphView = (ND_BehaviorTreeView)graphView;
            InitializeNodeView(node, btObject, graphView);
        }




        // --- Initialization ---
        public void InitializeNodeView(Node node, SerializedObject BTObject, GraphView graphView)
        {
            this.m_Node = node;
            this.m_SerializedObject = BTObject;
            this.viewDataKey = node.id;

            Type typeInfo = node.GetType();
            NodeInfoAttribute info = typeInfo.GetCustomAttribute<NodeInfoAttribute>();


            // --- Query UXML Elements ---
            var topPortContainer = this.Q<VisualElement>("top-port");
            var bottomPortContainer = this.Q<VisualElement>("bottom-port");
            var typeLabel = this.Q<Label>("type-label");
            titleLabel = this.Q<Label>("title-textfield");
            var iconImage = this.Q<Image>("icon-image");
            m_ServiceContainer = this.Q<VisualElement>("child-node-container");
            m_DetailsContainer = this.Q<VisualElement>("details-container");

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

            // --- Draw Exposed Properties and Progress Bars ---
            DrawExposedProperties();
            DrawProgressBars();
            if (m_DetailsContainer.childCount > 0)
            {
                m_DetailsContainer.style.display = DisplayStyle.Flex;
            }

            // --- Configure Services ---
            if (m_Node is CompositeNode compositeNode)
            {
                this.AddToClassList("composite-node");
                DrawServices(compositeNode, graphView);
            }
            else
            {
                if (m_ServiceContainer != null)
                {
                    m_ServiceContainer.style.display = DisplayStyle.None;
                }
            }

            // --- Create Ports ---
            DrawPort(info, topPortContainer, bottomPortContainer);

            this.AddManipulator(new DoubleClickNodeManipulator(this));
            RefreshExpandedState();
            RefreshPorts();
        }

       public virtual void AddBottomPortStyleSheet(string portStylePath)
        {
            if (m_InputPort == null)
            {
                return;
            }

            m_InputPort.AddToClassList("btport");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(portStylePath);

            // This check is also good practice, in case the path is invalid.
            if (styleSheet != null)
            {
                m_InputPort.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"Could not load stylesheet for port at path: {portStylePath}");
            }
        }

    public virtual void AddTopPortStyleSheet(string portStylePath)
    {
        // --- FIX: Add a null check here ---
        // If this node type doesn't have an output port, just exit silently.
        if (m_OutputPort == null)
        {
            return;
        }
        
        // The original check for the path string is also good, let's keep it.
        if (!string.IsNullOrEmpty(portStylePath))
        {
            m_OutputPort.AddToClassList("btport");

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(portStylePath);

            if (styleSheet != null)
            {
                m_OutputPort.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"Could not load stylesheet for port at path: {portStylePath}");
            }
        }
    }

        #region DrawSection

        public virtual void DrawPort(NodeInfoAttribute info, VisualElement topPortContainer, VisualElement bottomPortContainer)
        {
            if (info.hasFlowInput)
            {
                m_InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(PortType.FlowPort));
                m_InputPort.portName = "";


                topPortContainer.Add(m_InputPort);
                m_Ports.Add(m_InputPort);
                    
            }

            if (info.hasFlowOutput)
            {

                var capacity = (m_Node is DecoratorNode or GOAPActionNode) ? Port.Capacity.Single : Port.Capacity.Multi;
                m_OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, capacity, typeof(PortType.FlowPort));
                m_OutputPort.portName = "";
                bottomPortContainer.Add(m_OutputPort);
                m_Ports.Add(m_OutputPort);
            }
        }

        protected void DrawExposedProperties()
        {
            if (m_DetailsContainer == null) return;

            var fields = m_Node.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<ExposePropertyAttribute>() != null)
                {
                    var propertyContainer = new VisualElement();
                    propertyContainer.AddToClassList("exposed-property");

                    var label = new Label(ObjectNames.NicifyVariableName(field.Name));
                    label.AddToClassList("exposed-property-label");

                    var valueLabel = new Label("---"); // Default value
                    valueLabel.AddToClassList("exposed-property-value");

                    propertyContainer.Add(label);
                    propertyContainer.Add(valueLabel);
                    m_DetailsContainer.Add(propertyContainer);

                    m_ExposedPropertyUpdaters.Add(new ExposedPropertyUpdater { fieldInfo = field, valueLabel = valueLabel });
                }
            }
        }

        protected void DrawProgressBars()
        {
            if (m_DetailsContainer == null) return;

            // Use AllowMultiple:true on the attribute to support multiple progress bars on one node
            var fields = m_Node.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var attributes = field.GetCustomAttributes<NodeProgressBar>();
                foreach (var attribute in attributes)
                {
                    var progressBarContainer = new VisualElement();
                    progressBarContainer.AddToClassList("progress-bar");

                    var fillElement = new VisualElement();
                    fillElement.AddToClassList("progress-bar-fill");

                    progressBarContainer.Add(fillElement);
                    m_DetailsContainer.Add(progressBarContainer);

                    m_ProgressBarUpdaters.Add(new ProgressBarUpdater { attribute = attribute, fillElement = fillElement });
                }
            }
        }

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

            foreach (var serviceNode in composite.services)
            {
                if (serviceNode != null)
                {
                    var serviceEditor = new ND_AuxiliaryEditor(serviceNode, m_SerializedObject, graphView, styleSheetPath);
                    m_ServiceContainer.Add(serviceEditor);
                }
            }
        }

        #endregion

        #region IDropTarget Implementation
        public bool CanAcceptDrop(List<ISelectable> selection)
        {
            if (!(m_Node is CompositeNode)) return false;
            if (selection.Count != 1 || !(selection.First() is ND_NodeEditor draggedNode)) return false;
            if (draggedNode == this) return false;
            return draggedNode.m_Node is ServiceNode;
        }

        public bool DragEnter(DragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource)
        {
            if (enteredTarget != this || !CanAcceptDrop(selection.ToList())) return false;
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
            if (!(m_Node is CompositeNode) || !CanAcceptDrop(selection.ToList())) return false;

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


        #region UpdateNodeEditor
                // --- Selection Handling ---
        public override void OnSelected()
        {
            base.OnSelected();
            this.AddToClassList("selected");
            Debug.Log($"Node '{node.typeName}' was selected.");
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            this.RemoveFromClassList("selected");

            // Debug.Log($"Node '{node.typeName}' was unselected.");
        }

        public void SavePosition()
        {
            m_Node.SetPosition(GetPosition());
        }
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
                // Update status visualization
                switch (runtimeNode.status)
                {
                    case Node.Status.Running: AddToClassList("running"); break;
                    case Node.Status.Success: AddToClassList("success"); break;
                    case Node.Status.Failure: AddToClassList("failure"); break;
                }

                // --- THIS IS THE FIX FOR THE TEXT LABEL ---
                // Update exposed properties
                foreach (var updater in m_ExposedPropertyUpdaters)
                {
                    var value = updater.fieldInfo.GetValue(runtimeNode);
                    string displayText = "null";
                    if (value != null)
                    {
                        // Check if the value is a float or double, and format it to 2 decimal places.
                        if (value is float f)
                        {
                            displayText = f.ToString("F2");
                        }
                        else if (value is double d)
                        {
                            displayText = d.ToString("F2");
                        }
                        else // For all other types, use the default string conversion.
                        {
                            displayText = value.ToString();
                        }
                    }
                    // Update the text of the value label.
                    updater.valueLabel.text = displayText;
                }

                // Update progress bars
                foreach (var updater in m_ProgressBarUpdaters)
                {
                    try
                    {
                        float currentValue = 0, start = 0, end = 1;

                        // Case 1: The attribute is observing fields directly on the node.
                        if (!string.IsNullOrEmpty(updater.attribute.currentValueField))
                        {
                            var currentField = runtimeNode.GetType().GetField(updater.attribute.currentValueField, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var maxField = runtimeNode.GetType().GetField(updater.attribute.maxValueField, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (currentField != null && maxField != null)
                            {
                                currentValue = Convert.ToSingle(currentField.GetValue(runtimeNode));
                                end = Convert.ToSingle(maxField.GetValue(runtimeNode));
                                start = updater.attribute.fieldStartValue;
                            }
                        }
                        // Case 2 & 3: The attribute is observing the blackboard.
                        else
                        {
                            var blackboard = runtimeNode.blackboard;
                            if (blackboard == null) continue;

                            currentValue = Convert.ToSingle(blackboard.GetValue<object>(updater.attribute.variableObserver));

                            if (updater.attribute.startVariable != null && updater.attribute.endVariable != null)
                            {
                                start = Convert.ToSingle(blackboard.GetValue<object>(updater.attribute.startVariable));
                                end = Convert.ToSingle(blackboard.GetValue<object>(updater.attribute.endVariable));
                            }
                            else
                            {
                                start = updater.attribute.startValue;
                                end = updater.attribute.endValue;
                            }
                        }

                        // Calculate and apply the percentage for the progress bar
                        float range = end - start;
                        float percentage = 0;
                        if (range > 0)
                        {
                            percentage = Mathf.Clamp01((currentValue - start) / range);
                        }

                        // You can optionally round the progress bar width as well
                        float roundedPercentage = (float)Math.Round(percentage * 100, 2);
                        updater.fillElement.style.width = Length.Percent(roundedPercentage);
                    }
                    catch (Exception)
                    {
                        updater.fillElement.style.width = Length.Percent(0);
                    }
                }
            }
        }

        public void ClearState()
        {
            RemoveFromClassList("running");
            RemoveFromClassList("success");
            RemoveFromClassList("failure");

            foreach (var updater in m_ExposedPropertyUpdaters)
            {
                updater.valueLabel.text = "---";
            }
            foreach (var updater in m_ProgressBarUpdaters)
            {
                updater.fillElement.style.width = Length.Percent(0);
            }
        }
        #endregion
    }
}