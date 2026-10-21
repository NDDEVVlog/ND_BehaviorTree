using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>
    /// A node on a <see cref="GraphCanvas"/>: absolute position, click-to-select, and dragging of the whole selection.
    /// Dragging only moves the views; the canvas gets ONE <see cref="GraphCanvas.CommitNodeMove"/> on release,
    /// so the data change is a single undo step. Subclasses build the content and create their ports with
    /// <see cref="AddPort"/>.
    /// </summary>
    public abstract class GraphNodeView : VisualElement
    {
        /// <summary>Stable id of the node this view shows (unique within its canvas).</summary>
        public abstract string Id { get; }

        /// <summary>Look of this node's ports.</summary>
        public abstract PortTheme PortTheme { get; }

        public event Action OnNodeMoved;

        private readonly List<GraphPortView> m_Ports = new List<GraphPortView>();
        public IReadOnlyList<GraphPortView> Ports => m_Ports;

        private bool m_IsDragging;
        private bool m_HasDragMoved;
        private Vector2 m_DragStartMousePos;
        private readonly Dictionary<GraphNodeView, Vector2> m_DragStartNodePositions = new Dictionary<GraphNodeView, Vector2>();

        private VisualElement m_LinkHandle;

        /// <summary>
        /// Port-less linking: on hover the node shows a handle; dragging it onto another node asks the canvas to link
        /// them (<see cref="GraphCanvas.TryConnectNodes"/>), drawn as a node-anchored edge.
        /// </summary>
        protected virtual bool AllowsNodeLinks => false;

        /// <summary>The hover handle that starts a node link (null until the node is on a panel, or when links are off).</summary>
        public VisualElement LinkHandle => m_LinkHandle;

        protected GraphNodeView()
        {
            style.position = Position.Absolute;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => m_IsDragging = false);
            // Built after the subclass constructor ran, so AllowsNodeLinks can depend on its fields.
            RegisterCallback<AttachToPanelEvent>(_ => EnsureLinkHandle());
            RegisterCallback<PointerEnterEvent>(_ => SetLinkHandleVisible(true));
            RegisterCallback<PointerLeaveEvent>(_ => SetLinkHandleVisible(false));
        }

        /// <summary>Creates the link handle if this node allows links (also callable directly, e.g. in tests).</summary>
        public void EnsureLinkHandle()
        {
            if (m_LinkHandle != null || !AllowsNodeLinks) return;
            m_LinkHandle = new VisualElement { name = "link-handle", tooltip = "Drag onto another node to link to it." };
            m_LinkHandle.AddToClassList("nd-link-handle");
            var s = m_LinkHandle.style;
            s.position = Position.Absolute;
            s.right = -7; s.top = -7;
            s.width = s.height = 14;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 7;
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 2;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = Color.white;
            s.backgroundColor = new Color(0.25f, 0.6f, 1f, 1f);
            s.display = DisplayStyle.None;
            m_LinkHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                GetFirstAncestorOfType<GraphCanvas>()?.ConnectionManager.StartNodeConnection(this, evt.position, evt.pointerId);
                evt.StopPropagation(); // not a node drag
            });
            hierarchy.Add(m_LinkHandle);
        }

        private void SetLinkHandleVisible(bool visible)
        {
            if (m_LinkHandle != null) m_LinkHandle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Registers a port (the caller adds it to whichever container it belongs in).</summary>
        protected T AddPort<T>(T port) where T : GraphPortView
        {
            m_Ports.Add(port);
            return port;
        }

        public GraphPortView FindPort(PortDirection direction, string portName = "")
        {
            foreach (var port in m_Ports)
                if (port.Direction == direction && port.PortName == (portName ?? string.Empty))
                    return port;
            return null;
        }

        public void SetPosition(Vector2 position)
        {
            style.left = position.x;
            style.top = position.y;
            OnNodeMoved?.Invoke();
        }

        /// <summary>Current on-canvas position (may differ from data while a drag is in progress).</summary>
        public Vector2 ViewPosition => new Vector2(style.left.value.value, style.top.value.value);

        /// <summary>Left double-click on the node body.</summary>
        protected virtual void OnDoubleClick() { }

        protected virtual void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            var canvas = GetFirstAncestorOfType<GraphCanvas>();
            if (canvas != null)
            {
                canvas.Focus(); // keyboard shortcuts need the canvas focused

                if (evt.actionKey || evt.shiftKey) canvas.ToggleSelection(this);
                else if (!canvas.IsSelected(this))
                {
                    canvas.ClearSelection();
                    canvas.AddToSelection(this);
                }

                m_IsDragging = true;
                m_HasDragMoved = false;
                m_DragStartMousePos = evt.position;
                m_DragStartNodePositions.Clear();
                foreach (var nodeView in canvas.SelectedViews)
                {
                    m_DragStartNodePositions[nodeView] = nodeView.ViewPosition;
                    nodeView.BringToFront();
                }
            }

            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();

            if (evt.clickCount == 2) OnDoubleClick();
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_IsDragging || !this.HasPointerCapture(evt.pointerId)) return;
            float scale = parent.resolvedStyle.scale.value.x;
            if (scale <= 0.0001f) scale = 1f;
            Vector2 delta = (Vector2)evt.position - m_DragStartMousePos;

            // View only while dragging; data is committed once on pointer-up (one undo step).
            foreach (var kvp in m_DragStartNodePositions)
                kvp.Key.SetPosition(kvp.Value + delta / scale);
            m_HasDragMoved = true;
            evt.StopPropagation();
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (!m_IsDragging || !this.HasPointerCapture(evt.pointerId)) return;
            m_IsDragging = false;
            this.ReleasePointer(evt.pointerId);
            if (m_HasDragMoved)
                GetFirstAncestorOfType<GraphCanvas>()?.CommitNodeMove(new List<GraphNodeView>(m_DragStartNodePositions.Keys));
            m_DragStartNodePositions.Clear();
            evt.StopPropagation();
        }
    }
}
