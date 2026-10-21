using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ND_BehaviorTree.Editor.Graph;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTNodeView : VisualElement
    {
        protected class ExposedPropertyUpdater
        {
            public FieldInfo fieldInfo;
            public Label valueLabel;
            public object lastValue; // last shown value; the label is rewritten only when it changes
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

        /// <summary>The port look of this node: its appearance pack's override, else the project's Port Theme.</summary>
        public PortTheme PortTheme => m_ThemeData.PortTheme ?? ND_BehaviorTreeSetting.Instance.portTheme;
        protected Label m_ExecutionIndexBadge;
        protected VisualElement m_DecoratorContainer;
        protected readonly List<ND_BTAttachmentView> m_AttachmentViews = new List<ND_BTAttachmentView>();
        protected ND_BTKeySlotView m_KeySlots;
        protected VisualElement m_ProgressTrack;
        protected VisualElement m_ProgressFill;
        private int m_ShownProgress = -1;

        // Live-debug caches (avoid per-tick allocations).
        private int m_LiveTick;
        private Label m_TypeLabel;
        private readonly List<Node> m_ChainScratch = new List<Node>();
        private readonly List<Node> m_ShownChain = new List<Node>();

        private static bool SameChain(List<Node> a, List<Node> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        protected readonly List<ExposedPropertyUpdater> m_ExposedPropertyUpdaters = new List<ExposedPropertyUpdater>();
        private bool m_IsDragging;
        private bool m_HasDragMoved;
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
            CreateExecutionIndexBadge();

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

            // Decorator/service/slot sections are styled the same in every theme (themes may override the classes).
            ND_BTStyles.Apply(this, ND_BTStyles.NodeAttachments);
            // The theme's decorator / service / key-slot colours (added after, so they win over the defaults).
            if (settings.attachmentStyle != null) ND_BTStyles.Apply(this, settings.attachmentStyle);
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
            RefreshSubtreeLabel();

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

            string summary = WeightGraph.DescriptionOf(NodeData.GetType())?.Summary;
            tooltip = summary != null ? $"{summary}\n\nDouble-click for details." : "Double-click to edit.";

            SetPosition(NodeData.position.position);
            InitializePorts(info);
            DrawExposedProperties();
            DrawProgressBar();
            DrawKeySlots();
            DrawDecorators();
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
                BTPortCapacity capacity = BehaviorTreeGraphService.AcceptsMultipleChildren(NodeData) ? BTPortCapacity.Multi : BTPortCapacity.Single;
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

        /// <summary>Subtree / Event nodes: shows the referenced asset or event name under the title (kept current every editor tick).</summary>
        public void RefreshSubtreeLabel()
        {
            string text;
            if (NodeData is SubtreeNode subtreeNode) text = subtreeNode.subtree != null ? $"Subtree: {subtreeNode.subtree.name}" : "Subtree: (none)";
            else if (NodeData is EventNode eventNode) text = $"Event: {(string.IsNullOrWhiteSpace(eventNode.eventName) ? "(unnamed)" : eventNode.eventName)}";
            else if (NodeData is InitNode initNode) text = initNode.stopTreeOnFailure ? "Runs once at start · stops tree on failure" : "Runs once at start";
            else return;

            var typeLabel = this.Q<Label>("type-label");
            if (typeLabel != null && typeLabel.text != text) typeLabel.text = text;
        }

        /// <summary>Current on-canvas position (may differ from data while a drag is in progress).</summary>
        public Vector2 ViewPosition => new Vector2(style.left.value.value, style.top.value.value);

        protected virtual void CreateExecutionIndexBadge()
        {
            m_ExecutionIndexBadge = new Label
            {
                name = "execution-index-badge",
                pickingMode = PickingMode.Ignore,
                tooltip = "Execution order inside the parent composite (left-to-right).",
                style =
                {
                    position = Position.Absolute,
                    left = -10, top = -10,
                    minWidth = 20, height = 20,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    backgroundColor = new Color(0.15f, 0.45f, 0.85f, 1f),
                    color = Color.white,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                    display = DisplayStyle.None
                }
            };
            hierarchy.Add(m_ExecutionIndexBadge);
        }

        private Label m_WeightBadge;
        private Label m_FocusBadge;

        /// <summary>Weight pill at the top-right ("w 2 ×Menace"); null hides it. Shown only under weighted parents.</summary>
        public virtual void SetWeightBadge(string text, string badgeTooltip)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (m_WeightBadge != null) m_WeightBadge.style.display = DisplayStyle.None;
                return;
            }
            if (m_WeightBadge == null)
            {
                m_WeightBadge = new Label { name = "weight-badge", pickingMode = PickingMode.Position };
                m_WeightBadge.AddToClassList("nd-weight-badge");
                hierarchy.Add(m_WeightBadge);
            }
            m_WeightBadge.style.display = DisplayStyle.Flex;
            if (m_WeightBadge.text != text) m_WeightBadge.text = text;
            m_WeightBadge.tooltip = badgeTooltip;
        }

        /// <summary>Live Debug: marks a node that frames itself (and pings the agent) whenever it starts running.</summary>
        public void SetFocusOnRunMarker(bool on)
        {
            EnableInClassList("nd-focus-on-run", on);
            if (!on)
            {
                if (m_FocusBadge != null) m_FocusBadge.style.display = DisplayStyle.None;
                return;
            }
            if (m_FocusBadge == null)
            {
                m_FocusBadge = new Label("◎") { name = "focus-badge", pickingMode = PickingMode.Position, tooltip = "Focus when running: the graph frames this node and pings the agent each time it starts running." };
                m_FocusBadge.AddToClassList("nd-focus-badge");
                hierarchy.Add(m_FocusBadge);
            }
            m_FocusBadge.style.display = DisplayStyle.Flex;
        }

        /// <summary>Shows the 1-based execution index; pass a negative value to hide it.</summary>
        public virtual void SetExecutionIndex(int zeroBasedIndex)
        {
            if (m_ExecutionIndexBadge == null) return;
            bool visible = zeroBasedIndex >= 0;
            m_ExecutionIndexBadge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) m_ExecutionIndexBadge.text = (zeroBasedIndex + 1).ToString();
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
                    m_HasDragMoved = false;
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
                float scale = parent.resolvedStyle.scale.value.x;
                if (scale <= 0.0001f) scale = 1f;
                Vector2 delta = (Vector2)evt.position - m_DragStartMousePos;

                foreach (var kvp in m_DragStartNodePositions)
                {
                    ND_BTNodeView nodeView = kvp.Key;
                    Vector2 startPos = kvp.Value;

                    // View only while dragging; data is committed once on pointer-up (one undo step).
                    Vector2 newPos = startPos + (delta / scale);
                    nodeView.SetPosition(newPos);
                }
                m_HasDragMoved = true;
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (m_IsDragging && this.HasPointerCapture(evt.pointerId))
            {
                m_IsDragging = false;
                this.ReleasePointer(evt.pointerId);
                if (m_HasDragMoved)
                {
                    GetFirstAncestorOfType<ND_BTGraphCanvas>()?.CommitNodeMove(new List<ND_BTNodeView>(m_DragStartNodePositions.Keys));
                }
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

        /// <summary>
        /// Draws attached decorators above the title as cards, top = evaluated first.
        /// Right-click a card to reorder/remove; double-click to edit its properties.
        /// </summary>
        public virtual void DrawDecorators()
        {
            if (m_DecoratorContainer == null)
            {
                var title = this.Q<VisualElement>("title");
                if (title?.parent == null) return;
                m_DecoratorContainer = new VisualElement { name = "decorator-container" };
                m_DecoratorContainer.AddToClassList("nd-section");
                m_DecoratorContainer.AddToClassList("nd-section--decorators");
                title.parent.Insert(title.parent.IndexOf(title), m_DecoratorContainer);
            }

            m_DecoratorContainer.Clear();
            m_AttachmentViews.RemoveAll(v => !v.IsService);
            var decorators = NodeData.decorators;
            m_DecoratorContainer.style.display = decorators.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < decorators.Count; i++)
            {
                var decorator = decorators[i];
                if (decorator == null) continue;
                int index = i;

                var item = CreateAttachmentCard(decorator, isService: false, index);
                item.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
                    if (canvas == null) return;
                    evt.menu.AppendAction("Edit Properties", a => NodePropertyEditorWindow.Open(decorator, this));
                    evt.menu.AppendAction("Move Up", a => canvas.MoveDecorator(NodeData, decorator, index - 1),
                        index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendAction("Move Down", a => canvas.MoveDecorator(NodeData, decorator, index + 1),
                        index < NodeData.decorators.Count - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Remove Decorator", a => canvas.RemoveDecorator(NodeData, decorator));
                    evt.StopPropagation();
                }));

                m_DecoratorContainer.Add(item);
            }
        }

        public virtual void DrawServices()
        {
            if (m_ServiceContainer == null) return;
            m_ServiceContainer.Clear();
            // The UXML's dashed "child-node-container" box is replaced by a flush section of the node.
            m_ServiceContainer.RemoveFromClassList("child-node-container");
            m_ServiceContainer.AddToClassList("nd-section");
            m_ServiceContainer.AddToClassList("nd-section--services");
            m_AttachmentViews.RemoveAll(v => v.IsService);

            var services = NodeData.services;
            services.RemoveAll(s => s == null);
            m_ServiceContainer.style.display = services.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < services.Count; i++)
            {
                var service = services[i];
                var item = CreateAttachmentCard(service, isService: true, i);
                item.AddManipulator(new ContextualMenuManipulator(e => {
                    e.menu.AppendAction("Edit Properties", a => NodePropertyEditorWindow.Open(service, this));
                    e.menu.AppendAction("Remove Service", a => GetFirstAncestorOfType<ND_BTGraphCanvas>()?.RemoveService(NodeData, service));
                    e.StopPropagation();
                }));
                m_ServiceContainer.Add(item);
            }
        }

        /// <summary>Card for an attached decorator/service; double-click opens its properties.</summary>
        protected virtual ND_BTAttachmentView CreateAttachmentCard(Node attached, bool isService, int index)
        {
            var card = new ND_BTAttachmentView(attached, isService, index);
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    NodePropertyEditorWindow.Open(attached, this);
                    evt.StopPropagation();
                }
            });
            m_AttachmentViews.Add(card);
            return card;
        }

        /// <summary>Key fields of the node itself, shown under the title as drop targets for blackboard keys.</summary>
        protected virtual void DrawKeySlots()
        {
            var title = this.Q<VisualElement>("title");
            if (title?.parent == null) return;
            m_KeySlots = new ND_BTKeySlotView(NodeData, compact: false);
            title.parent.Insert(title.parent.IndexOf(title) + 1, m_KeySlots);
        }

        /// <summary>Live progress bar for nodes that declare [NodeProgressBar(current, max)].</summary>
        protected virtual void DrawProgressBar()
        {
            if (!ProgressReader.HasProgress(NodeData.GetType()) || NodeData is ServiceNode) return;
            var title = this.Q<VisualElement>("title");
            if (title?.parent == null) return;
            m_ProgressTrack = new VisualElement { pickingMode = PickingMode.Ignore };
            m_ProgressTrack.AddToClassList("nd-node-progress");
            m_ProgressFill = new VisualElement { pickingMode = PickingMode.Ignore };
            m_ProgressFill.AddToClassList("nd-node-progress__fill");
            m_ProgressTrack.Add(m_ProgressFill);
            title.parent.Insert(title.parent.IndexOf(title) + 1, m_ProgressTrack);
        }

        /// <summary>Re-reads key slots and attachment summaries (called periodically and after key edits).</summary>
        public virtual void RefreshKeyInfo()
        {
            m_KeySlots?.Refresh();
            foreach (var card in m_AttachmentViews) card.RefreshInfo();
        }

        public void RebuildKeySlots()
        {
            m_KeySlots?.Rebuild();
            foreach (var card in m_AttachmentViews) card.RefreshInfo();
        }

        /// <summary>
        /// Live debug: shows this node's runtime state (see LiveDebugUtility). <paramref name="lookup"/> resolves runtime nodes by id.
        /// </summary>
        public virtual void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            Node runtimeNode = lookup(NodeData.id);
            var state = LiveDebugUtility.GetState(runtimeNode, frame);
            EnableInClassList("running", state == LiveState.Running);
            EnableInClassList("success", state == LiveState.Succeeded);
            EnableInClassList("failure", state == LiveState.Failed);

            foreach (var card in m_AttachmentViews)
            {
                var runtimeAttachment = lookup(card.Attached.id);
                var cardState = LiveDebugUtility.GetState(runtimeAttachment, frame);
                // A service has no meaningful result of its own: it is "running" while its node runs.
                if (card.IsService && cardState != LiveState.Inactive) cardState = state == LiveState.Running ? LiveState.Running : LiveState.Inactive;
                card.SetLive(cardState, runtimeAttachment);
            }

            if (m_ProgressTrack != null)
            {
                int percent = state == LiveState.Running && ProgressReader.TryGetProgress(runtimeNode, out float progress) ? Mathf.RoundToInt(progress * 100f) : -1;
                if (percent != m_ShownProgress)
                {
                    m_ShownProgress = percent;
                    m_ProgressTrack.EnableInClassList("nd-node-progress--visible", percent >= 0);
                    if (percent >= 0) m_ProgressFill.style.width = Length.Percent(percent);
                }
            }

            if (runtimeNode == null) return;

            // Exposed values: ~5 Hz (every 6th live tick at 30 Hz), text written only when it changed.
            if (m_ExposedPropertyUpdaters.Count > 0 && ++m_LiveTick % 6 == 0)
            {
                foreach (var updater in m_ExposedPropertyUpdaters)
                {
                    var val = updater.fieldInfo.GetValue(runtimeNode);
                    if (Equals(val, updater.lastValue)) continue;
                    updater.lastValue = val;
                    updater.valueLabel.text = val != null ? (val is float f ? f.ToString("F2") : val.ToString()) : "null";
                }
            }

            // Subtree: show where execution is inside the subtree's runtime clone. The chain of running nodes is
            // compared by reference each tick; text is built only when the chain changes.
            if (runtimeNode is SubtreeNode runtimeSubtree)
            {
                m_ChainScratch.Clear();
                if (runtimeSubtree.RuntimeSubtree != null) LiveDebugUtility.GetActiveChain(runtimeSubtree.RuntimeSubtree, frame, m_ChainScratch);
                if (!SameChain(m_ChainScratch, m_ShownChain))
                {
                    m_ShownChain.Clear();
                    m_ShownChain.AddRange(m_ChainScratch);
                    if (m_TypeLabel == null) m_TypeLabel = this.Q<Label>("type-label");
                    if (m_TypeLabel != null)
                    {
                        if (m_ShownChain.Count > 0) m_TypeLabel.text = "▶ " + LiveDebugUtility.DescribeChain(m_ShownChain);
                        else RefreshSubtreeLabel();
                    }
                }
            }
        }

        public virtual void ClearState()
        {
            RemoveFromClassList("running"); RemoveFromClassList("success"); RemoveFromClassList("failure");
            foreach (var u in m_ExposedPropertyUpdaters) { u.valueLabel.text = "---"; u.lastValue = null; }
            m_ShownChain.Clear();
            foreach (var card in m_AttachmentViews) card.ClearLive();
            if (m_ProgressTrack != null)
            {
                m_ProgressTrack.RemoveFromClassList("nd-node-progress--visible");
                m_ShownProgress = -1;
            }
            RefreshSubtreeLabel();
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return;
            Vector2 screenPos = GUIUtility.GUIToScreenPoint(evt.mousePosition);

            // Live Debug: can be armed before Play; the agent actions need a watched agent.
            var liveStatus = canvas.IsLiveDebugging ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            evt.menu.AppendAction("Live Debug/Focus When Running", a => canvas.ToggleFocusOnRun(NodeData),
                canvas.IsFocusOnRun(NodeData) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Live Debug/Focus This Node", a => canvas.FocusNode(this));
            evt.menu.AppendAction("Live Debug/Ping Agent", a => canvas.PingWatchedAgent(), liveStatus);
            evt.menu.AppendAction("Live Debug/Clear All Focus Markers", a => canvas.ClearFocusOnRun(),
                canvas.HasFocusOnRun ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();

            if (canvas.Graph.CanAttachDecorator(NodeData, out _))
            {
                evt.menu.AppendAction("Add Decorator", a => canvas.OpenDecoratorSearch(this, screenPos));
            }

            if (NodeData is SubtreeNode subtreeNode)
            {
                evt.menu.AppendAction("Open Subtree", a => canvas.OpenSubtree(subtreeNode),
                    subtreeNode.subtree != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Step Into (Live)", a => canvas.StepIntoSubtreeLive(subtreeNode),
                    canvas.IsLiveDebugging ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Create New Subtree Asset", a => canvas.CreateSubtreeAsset(subtreeNode));
                evt.menu.AppendSeparator();
            }

            if (canvas.Graph.CanAttachService(NodeData, out _))
            {
                evt.menu.AppendAction("Add Service", a => canvas.OpenServiceSearch(this, screenPos));
            }
            evt.menu.AppendSeparator();

            if (NodeData is CompositeNode composite)
            {
                evt.menu.AppendAction("Sort Children Left-to-Right", a => canvas.SortChildren(composite));
                evt.menu.AppendSeparator();
            }

            if (InputPort != null && InputPort.Connections.Count > 0)
            {
                evt.menu.AppendAction("Disconnect From Parent   (Alt+click input port)", a => canvas.DisconnectInput(InputPort));
            }
            if (OutputPort != null && OutputPort.Connections.Count > 0)
            {
                evt.menu.AppendAction("Disconnect All Children   (Alt+click output port)", a => canvas.DisconnectPort(OutputPort));
            }
            if ((InputPort != null && InputPort.Connections.Count > 0) || (OutputPort != null && OutputPort.Connections.Count > 0))
            {
                evt.menu.AppendSeparator();
            }

            // Đồng bộ Copy / Delete khi click thẳng vào Node
            if (!canvas.SelectedNodes.Contains(this))
            {
                canvas.ClearSelection();
                canvas.AddToSelection(this);
            }

            evt.menu.AppendAction("Copy   (Ctrl+C)", a => canvas.CopySelection());
            evt.menu.AppendAction("Duplicate   (Ctrl+D)", a => canvas.DuplicateSelection());
            if (Bridge.AiAccess.UiUnlocked)
            {
                evt.menu.AppendAction("Copy Node IDs for AI", a =>
                    EditorGUIUtility.systemCopyBuffer = Bridge.CopyForAi.NodesText(canvas.TreeData, canvas.SelectedNodes.Select(v => v.NodeData)));
            }
            evt.menu.AppendAction("Group Selected Nodes   (Ctrl+G)", a => canvas.CreateGroup(ViewPosition));
            evt.menu.AppendAction("Convert Selection to Subtree...   (Ctrl+Shift+E)", a => canvas.ConvertSelectionToSubtree());

            Plugins.ND_BTPluginRegistry.AppendTo(evt.menu, Plugins.ND_BTPluginRegistry.NodeMenuItems(NodeData), () => Plugins.ND_BTPluginRegistry.ArgsFor(canvas, NodeData));

            evt.menu.AppendSeparator();

            if (canvas.SelectedNodes.Count > 1)
            {
                evt.menu.AppendAction("Delete Selected Nodes", a => canvas.DeleteSelectedNodes());
            }
            else if (!(NodeData is RootNode))
            {
                evt.menu.AppendAction("Delete Node", a => canvas.DeleteNode(this));
            }
        }
    }
}