using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTNodeView : VisualElement
    {
        protected class ExposedPropertyUpdater
        {
            public FieldInfo fieldInfo;
            public Label valueLabel;
        }

        public Node NodeData { get; private set; }
        public ND_BTPortView InputPort { get; private set; }
        public ND_BTPortView OutputPort { get; private set; }
        public event Action OnNodeMoved;

        protected VisualElement m_TopPortContainer;
        protected VisualElement m_BottomPortContainer;
        protected VisualElement m_DetailsContainer;
        protected VisualElement m_ServiceContainer;
        protected ViewThemeData m_ThemeData;

        protected readonly List<ExposedPropertyUpdater> m_ExposedPropertyUpdaters = new List<ExposedPropertyUpdater>();
        private bool m_IsDragging;
        private Vector2 m_DragStartMousePos;
        private Dictionary<ND_BTNodeView, Vector2> m_DragStartNodePositions = new Dictionary<ND_BTNodeView, Vector2>();

        public ND_BTNodeView(Node node, ViewThemeData themeData)
        {
            NodeData = node;
            m_ThemeData = themeData ?? new ViewThemeData();
            viewDataKey = node.id;
            style.position = Position.Absolute;

            ApplyThemeAndLayout();
            SetupContainers();
            PopulateData();

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => m_IsDragging = false);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        protected virtual void ApplyThemeAndLayout()
        {
            var settings = ND_BehaviorTreeSetting.Instance;
            VisualTreeAsset uxmlToApply = m_ThemeData.CustomUXML != null ? m_ThemeData.CustomUXML : settings.defaultNodeUXML;
            if (uxmlToApply != null) uxmlToApply.CloneTree(this);

            StyleSheet styleToApply = m_ThemeData.CustomStyle != null ? m_ThemeData.CustomStyle : settings.defaultNodeStyle;
            if (styleToApply != null) this.styleSheets.Add(styleToApply);
        }

        protected virtual void SetupContainers()
        {
            m_TopPortContainer = this.Q<VisualElement>("top-port");
            m_BottomPortContainer = this.Q<VisualElement>("bottom-port");
            m_DetailsContainer = this.Q<VisualElement>("details-container");
            m_ServiceContainer = this.Q<VisualElement>("child-node-container");
        }

        protected virtual void PopulateData()
        {
            NodeInfoAttribute info = NodeData.GetType().GetCustomAttribute<NodeInfoAttribute>();
            var titleLabel = this.Q<Label>("title-textfield");
            var typeLabel = this.Q<Label>("type-label");
            var iconImage = this.Q<Image>("icon-image");

            if (titleLabel != null) titleLabel.text = string.IsNullOrEmpty(NodeData.typeName) ? NodeData.GetType().Name : NodeData.typeName;
            if (typeLabel != null && info != null) typeLabel.text = info.title ?? "Node";

            if (iconImage != null)
            {
                if (info != null && !string.IsNullOrEmpty(info.iconPath))
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(info.iconPath);
                    if (tex != null) iconImage.image = tex;
                    else iconImage.style.display = DisplayStyle.None;
                }
                else iconImage.style.display = DisplayStyle.None;
            }

            SetPosition(NodeData.position.position);
            InitializePorts(info);
            DrawExposedProperties();
            DrawServices();
        }

        protected virtual void InitializePorts(NodeInfoAttribute info)
        {
            if (info == null) return;
            if (info.hasFlowInput && m_TopPortContainer != null)
            {
                InputPort = new ND_BTPortView(this, BTPortDirection.Input, BTPortCapacity.Single);
                m_TopPortContainer.Add(InputPort);
            }
            if (info.hasFlowOutput && m_BottomPortContainer != null)
            {
                BTPortCapacity capacity = (NodeData is DecoratorNode) ? BTPortCapacity.Single : BTPortCapacity.Multi;
                OutputPort = new ND_BTPortView(this, BTPortDirection.Output, capacity);
                m_BottomPortContainer.Add(OutputPort);
            }
        }

        public void SetPosition(Vector2 position)
        {
            style.left = position.x;
            style.top = position.y;
            OnNodeMoved?.Invoke();
        }

        protected virtual void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
            {
                var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
                if (canvas != null)
                {
                    canvas.Focus(); // Bắt buộc nhận Event bàn phím

                    if (evt.actionKey || evt.shiftKey)
                    {
                        canvas.ToggleSelection(this);
                    }
                    else
                    {
                        if (!canvas.SelectedNodes.Contains(this))
                        {
                            canvas.ClearSelection();
                            canvas.AddToSelection(this);
                        }
                    }

                    m_IsDragging = true;
                    m_DragStartMousePos = evt.position;
                    m_DragStartNodePositions.Clear();
                    
                    foreach (var nodeView in canvas.SelectedNodes)
                    {
                        m_DragStartNodePositions[nodeView] = new Vector2(nodeView.style.left.value.value, nodeView.style.top.value.value);
                        nodeView.BringToFront();
                    }
                }

                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();

                if (evt.clickCount == 2) 
                {
                    NodePropertyEditorWindow.Open(NodeData, this);
                }
            }
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_IsDragging && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 parentScale = parent.style.scale.value.value;
                Vector2 delta = (Vector2)evt.position - m_DragStartMousePos;
                
                foreach (var kvp in m_DragStartNodePositions)
                {
                    ND_BTNodeView nodeView = kvp.Key;
                    Vector2 startPos = kvp.Value;
                    
                    Vector2 newPos = startPos + (delta / parentScale.x);
                    nodeView.SetPosition(newPos);
                    nodeView.NodeData.SetPosition(new Rect(newPos, Vector2.zero));
                    EditorUtility.SetDirty(nodeView.NodeData);
                }
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (m_IsDragging && this.HasPointerCapture(evt.pointerId))
            {
                m_IsDragging = false;
                this.ReleasePointer(evt.pointerId);
                m_DragStartNodePositions.Clear();
                evt.StopPropagation();
            }
        }

        protected virtual void DrawExposedProperties()
        {
            if (m_DetailsContainer == null) return;
            bool hasProps = false;
            foreach (var field in NodeData.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<ExposePropertyAttribute>() != null)
                {
                    hasProps = true;
                    var container = new VisualElement(); container.AddToClassList("exposed-property");
                    var lbl = new Label(ObjectNames.NicifyVariableName(field.Name)); lbl.AddToClassList("exposed-property-label");
                    var valLbl = new Label("---"); valLbl.AddToClassList("exposed-property-value");
                    container.Add(lbl); container.Add(valLbl);
                    m_DetailsContainer.Add(container);
                    m_ExposedPropertyUpdaters.Add(new ExposedPropertyUpdater { fieldInfo = field, valueLabel = valLbl });
                }
            }
            if (hasProps) m_DetailsContainer.style.display = DisplayStyle.Flex;
        }

        public virtual void DrawServices()
        {
            if (m_ServiceContainer == null) return;

            if (!(NodeData is CompositeNode composite) || composite.services == null || composite.services.Count == 0)
            {
                m_ServiceContainer.style.display = DisplayStyle.None;
                return;
            }

            composite.services.RemoveAll(s => s == null);

            m_ServiceContainer.style.display = DisplayStyle.Flex;
            foreach (var service in composite.services)
            {
                var item = new VisualElement(); 
                item.AddToClassList("child-node-item"); 
                item.AddToClassList("service-child");
                item.Add(new Label(service.name) { name = "title-label" });
                item.AddManipulator(new ContextualMenuManipulator(e => {
                    e.menu.AppendAction("Remove Service", a => GetFirstAncestorOfType<ND_BTGraphCanvas>()?.RemoveService(composite, service));
                }));
                m_ServiceContainer.Add(item);
            }
        }

        public virtual void UpdateState(BehaviorTree runnerTree)
        {
            RemoveFromClassList("running"); RemoveFromClassList("success"); RemoveFromClassList("failure");
            Node runtimeNode = runnerTree?.FindNode(NodeData.id);
            if (runtimeNode == null) return;
            if (runtimeNode.status == Node.Status.Running) AddToClassList("running");
            else if (runtimeNode.status == Node.Status.Success) AddToClassList("success");
            else if (runtimeNode.status == Node.Status.Failure) AddToClassList("failure");
            foreach (var updater in m_ExposedPropertyUpdaters)
            {
                var val = updater.fieldInfo.GetValue(runtimeNode);
                updater.valueLabel.text = val != null ? (val is float f ? f.ToString("F2") : val.ToString()) : "null";
            }
        }

        public virtual void ClearState()
        {
            RemoveFromClassList("running"); RemoveFromClassList("success"); RemoveFromClassList("failure");
            foreach (var u in m_ExposedPropertyUpdaters) u.valueLabel.text = "---";
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return;
            Vector2 screenPos = GUIUtility.GUIToScreenPoint(evt.mousePosition);

            if (NodeData is CompositeNode)
            {
                evt.menu.AppendAction("Add Service", a => canvas.OpenServiceSearch(this, screenPos));
                evt.menu.AppendSeparator();
            }

            // Đồng bộ Copy / Delete khi click thẳng vào Node
            if (!canvas.SelectedNodes.Contains(this))
            {
                canvas.ClearSelection();
                canvas.AddToSelection(this);
            }

            evt.menu.AppendAction("Copy", a => canvas.CopySelection());
            evt.menu.AppendAction("Duplicate", a => canvas.DuplicateSelection());

            evt.menu.AppendSeparator();

            if (canvas.SelectedNodes.Count > 1)
            {
                evt.menu.AppendAction("Delete Selected Nodes", a => canvas.DeleteSelectedNodes());
            }
            else
            {
                evt.menu.AppendAction("Delete Node", a => canvas.DeleteNode(this));
            }
        }
    }
}