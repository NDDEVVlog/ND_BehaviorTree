using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>
    /// Data-agnostic graph surface: pan (middle-drag / Alt+drag), zoom (wheel), grid, box selection, edge clicks,
    /// framing and keyboard routing. It never touches graph data: every change is a virtual hook the concrete canvas
    /// implements (usually by calling its graph service and then updating the views).
    /// Derive from <see cref="GraphCanvas{TNode,TEdge}"/>, which adds typed node/edge bookkeeping.
    /// </summary>
    public abstract class GraphCanvas : VisualElement
    {
        public VisualElement ContentContainer { get; }
        public GraphConnectionManager ConnectionManager { get; }

        /// <summary>Window that shows notifications for this canvas (optional).</summary>
        public EditorWindow HostWindow { get; set; }

        protected Vector2 m_PanOffset = Vector2.zero;
        protected float m_ZoomFactor = 1.0f;
        protected bool m_IsPanning;
        protected Vector2 m_LastMousePosition;
        protected Vector2 m_CurrentMousePosition;

        private readonly VisualElement m_SelectionBox;
        private bool m_IsBoxSelecting;
        private Vector2 m_BoxSelectStartPos;

        public float Zoom => m_ZoomFactor;
        public Vector2 PanOffset => m_PanOffset;
        /// <summary>Last known mouse position in content space (where a pasted / created node should go).</summary>
        public Vector2 MouseContentPosition => ContentContainer.WorldToLocal(m_CurrentMousePosition);
        /// <summary>Last known mouse position in panel (world) space.</summary>
        public Vector2 MousePanelPosition => m_CurrentMousePosition;

        public const float MinZoom = 0.2f;
        public const float MaxZoom = 3.0f;
        private const float EdgeHitTolerance = 8f;

        protected GraphCanvas()
        {
            focusable = true;
            tabIndex = 0;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = BackgroundColor;

            ContentContainer = new VisualElement { name = "graph-content", style = { transformOrigin = new TransformOrigin(0, 0) } };
            hierarchy.Add(ContentContainer);

            m_SelectionBox = new VisualElement { name = "selection-box", pickingMode = PickingMode.Ignore };
            m_SelectionBox.style.position = Position.Absolute;
            m_SelectionBox.style.backgroundColor = new Color(0.1f, 0.5f, 1f, 0.2f);
            m_SelectionBox.style.borderLeftColor = m_SelectionBox.style.borderRightColor =
            m_SelectionBox.style.borderTopColor = m_SelectionBox.style.borderBottomColor = new Color(0.1f, 0.5f, 1f, 1f);
            m_SelectionBox.style.borderLeftWidth = m_SelectionBox.style.borderRightWidth =
            m_SelectionBox.style.borderTopWidth = m_SelectionBox.style.borderBottomWidth = 1f;
            m_SelectionBox.style.display = DisplayStyle.None;
            ContentContainer.Add(m_SelectionBox);

            ConnectionManager = new GraphConnectionManager(this);

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerDownEvent>(OnEdgePointerDown, TrickleDown.TrickleDown); // before groups/notes under the edge
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => { m_IsPanning = false; m_IsBoxSelecting = false; });
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            generateVisualContent += DrawGrid;
        }

        #region Hooks: look

        protected virtual Color BackgroundColor => new Color(0.12f, 0.12f, 0.12f, 1f);
        protected virtual Color GridColor => new Color(0.18f, 0.18f, 0.18f, 1f);
        protected virtual float GridSpacing => 25f;

        /// <summary>Re-reads <see cref="BackgroundColor"/> / <see cref="GridColor"/> and repaints.</summary>
        public void RefreshCanvasLook()
        {
            style.backgroundColor = BackgroundColor;
            MarkDirtyRepaint();
        }

        #endregion

        #region Hooks: views and selection (implemented by GraphCanvas<TNode, TEdge>)

        public abstract IEnumerable<GraphNodeView> NodeViews { get; }
        public abstract IEnumerable<GraphEdgeView> EdgeViews { get; }

        public abstract IEnumerable<GraphNodeView> SelectedViews { get; }
        public abstract int SelectionCount { get; }
        public abstract bool IsSelected(GraphNodeView view);
        public abstract void AddToSelection(GraphNodeView view);
        public abstract void ToggleSelection(GraphNodeView view);
        public abstract void ClearSelection();

        public abstract GraphEdgeView CreateEdgeView(GraphPortView output);

        #endregion

        #region Hooks: data

        /// <summary>A drag of these node views ended. Write their <see cref="GraphNodeView.ViewPosition"/> to the data (one undo step).</summary>
        public abstract void CommitNodeMove(IReadOnlyList<GraphNodeView> movedViews);

        /// <summary>The user dropped a connection. Create it in the data and draw it; false if refused.</summary>
        public abstract bool TryConnect(GraphPortView output, GraphPortView input);

        /// <summary>Would <see cref="TryConnect"/> accept this pair? Drives the candidate glow while dragging. Default: any two nodes.</summary>
        public virtual bool CanConnect(GraphPortView output, GraphPortView input) => output.ParentNodeView != input.ParentNodeView;

        /// <summary>Alt+click on a port. Default: nothing.</summary>
        public virtual void DisconnectPort(GraphPortView port) { }

        /// <summary>A node-anchored edge view starting at <paramref name="from"/> (also the drag preview); null if this canvas has no node links.</summary>
        public abstract GraphEdgeView CreateNodeEdgeView(GraphNodeView from);

        /// <summary>Would <see cref="TryConnectNodes"/> accept this pair? Drives the nd-link-candidate class while dragging. Default: any two nodes.</summary>
        public virtual bool CanConnectNodes(GraphNodeView from, GraphNodeView to) => from != null && to != null && from != to;

        /// <summary>The user dropped a node link (handle of <paramref name="from"/> onto <paramref name="to"/>). Create it in the data and draw it.</summary>
        public virtual bool TryConnectNodes(GraphNodeView from, GraphNodeView to) => false;

        /// <summary>While a node link is dragged, nodes that could receive it get the nd-link-candidate class. Null clears it.</summary>
        public virtual void SetNodeLinkCandidates(GraphNodeView from)
        {
            foreach (var view in NodeViews)
                view.EnableInClassList("nd-link-candidate", from != null && view != from && CanConnectNodes(from, view));
        }

        /// <summary>
        /// A click on an edge (not on a node or port), only reported for Alt+click (<paramref name="cut"/>) or a double-click.
        /// Return true when handled.
        /// </summary>
        protected virtual bool OnEdgeClicked(GraphEdgeView edge, Vector2 contentPosition, bool cut) => false;

        /// <summary>F with nothing selected. Default: frame every node.</summary>
        public virtual void FrameDefault() => FrameViews(NodeViews, 1f);

        public virtual void ShowNotification(string message)
        {
            if (HostWindow != null) HostWindow.ShowNotification(new GUIContent(message), 2.5);
            else Debug.Log($"[GraphUI] {message}");
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt) { }

        #endregion

        #region Elements

        public void AddElement(VisualElement element) => ContentContainer.Add(element);
        public void RemoveElement(VisualElement element) { if (ContentContainer.Contains(element)) ContentContainer.Remove(element); }

        /// <summary>Places an element just before the first node view, so nodes and ports draw over it.</summary>
        public void InsertBehindNodes(VisualElement element)
        {
            int index = 0;
            for (; index < ContentContainer.childCount; index++)
                if (ContentContainer[index] is GraphNodeView) break;
            ContentContainer.Insert(index, element);
        }

        #endregion

        #region Connections

        /// <summary>Default port press: Alt+click disconnects, otherwise a connection drag starts.</summary>
        public virtual void OnPortPointerDown(GraphPortView port, PointerDownEvent evt)
        {
            // Alt+click (as in Unreal): cut this port's connections. Otherwise drag a new connection.
            if (evt.altKey) DisconnectPort(port);
            else ConnectionManager.StartConnection(port, evt.position, evt.pointerId);
        }

        /// <summary>
        /// While a connection is dragged from <paramref name="start"/>, every port that could receive it glows
        /// (<see cref="CanConnect"/>). Null clears the highlight.
        /// </summary>
        public virtual void SetConnectionCandidates(GraphPortView start)
        {
            foreach (var view in NodeViews)
            {
                foreach (var port in view.Ports)
                {
                    bool candidate = start != null && view != start.ParentNodeView && port.Direction != start.Direction
                        && GraphConnectionManager.TryGetOrientedPorts(start, port, out var output, out var input)
                        && CanConnect(output, input);
                    port.SetCandidate(candidate);
                }
            }
        }

        private void OnEdgePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            bool cut = evt.altKey && evt.clickCount == 1;
            if (!cut && evt.clickCount != 2) return;
            if (evt.target is VisualElement target && (target is GraphNodeView || target is GraphPortView
                || target.GetFirstAncestorOfType<GraphNodeView>() != null)) return;

            Vector2 local = ContentContainer.WorldToLocal(evt.position);
            var edge = EdgeAt(local);
            if (edge == null) return;
            if (OnEdgeClicked(edge, local, cut)) evt.StopImmediatePropagation();
        }

        /// <summary>The edge whose path passes closest to <paramref name="contentPoint"/>, within a few screen pixels; or null.</summary>
        public GraphEdgeView EdgeAt(Vector2 contentPoint)
        {
            GraphEdgeView best = null;
            float bestDistance = EdgeHitTolerance / Mathf.Max(0.1f, m_ZoomFactor);
            foreach (var edge in EdgeViews)
            {
                float distance = edge.DistanceTo(contentPoint);
                if (distance < bestDistance) { bestDistance = distance; best = edge; }
            }
            return best;
        }

        #endregion

        #region Framing

        public static Rect NodeContentRect(GraphNodeView view)
        {
            var r = view.layout;
            if (float.IsNaN(r.width) || r.width <= 0) r = new Rect(view.ViewPosition, new Vector2(200, 80));
            return r;
        }

        public void FrameRect(Rect contentRect, float zoom)
        {
            m_ZoomFactor = zoom;
            Vector2 viewCenter = new Vector2(layout.width, layout.height) * 0.5f;
            m_PanOffset = viewCenter - contentRect.center * m_ZoomFactor;
            ApplyTransform();
        }

        /// <summary>Zooms to fit the views (never above <paramref name="maxZoom"/>). No-op for an empty set.</summary>
        public void FrameViews(IEnumerable<GraphNodeView> views, float maxZoom)
        {
            bool any = false;
            Rect bounds = default;
            foreach (var view in views)
            {
                var r = NodeContentRect(view);
                bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                                               Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax)) : r;
                any = true;
            }
            if (!any) return;
            const float padding = 80f;
            float zoom = Mathf.Min(maxZoom, layout.width / (bounds.width + padding), layout.height / (bounds.height + padding));
            FrameRect(bounds, Mathf.Clamp(zoom, MinZoom, maxZoom));
        }

        /// <summary>F key: frame the selected nodes (zoom to fit, max 100%), or <see cref="FrameDefault"/> when nothing is selected.</summary>
        public void FrameSelection()
        {
            if (SelectionCount == 0) FrameDefault();
            else FrameViews(SelectedViews, 1f);
        }

        /// <summary>Centres the graph on a node (keeps a readable zoom) and flashes it.</summary>
        public void FocusNode(GraphNodeView view)
        {
            if (view == null) return;
            FrameRect(NodeContentRect(view), Mathf.Clamp(m_ZoomFactor, 0.6f, 1f));
            view.AddToClassList("nd-flash");
            view.schedule.Execute(() => view.RemoveFromClassList("nd-flash")).StartingIn(700);
        }

        #endregion

        #region Input

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Typing in a text field (note, rename, inline edit) must not trigger graph shortcuts.
            if ((evt.target as VisualElement)?.GetFirstOfType<TextField>() != null) return;
            if (HandleShortcut(evt)) evt.StopPropagation();
        }

        /// <summary>Keyboard shortcuts. Base: Ctrl+A select all, Esc clear selection, F frame. Ctrl = Cmd on macOS.</summary>
        protected virtual bool HandleShortcut(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.A when evt.actionKey:
                    foreach (var view in NodeViews) AddToSelection(view);
                    return true;
                case KeyCode.Escape:
                    ClearSelection();
                    return true;
                case KeyCode.F when !evt.actionKey:
                    FrameSelection();
                    return true;
                default:
                    return false;
            }
        }

        protected virtual void OnWheel(WheelEvent evt)
        {
            float zoomDelta = evt.delta.y > 0 ? -0.05f : 0.05f;
            float newZoom = Mathf.Clamp(m_ZoomFactor + zoomDelta, MinZoom, MaxZoom);
            m_PanOffset = evt.localMousePosition - (evt.localMousePosition - m_PanOffset) * (newZoom / m_ZoomFactor);
            m_ZoomFactor = newZoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        protected virtual void OnPointerDown(PointerDownEvent evt)
        {
            Focus();

            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                m_IsPanning = true;
                m_LastMousePosition = evt.position;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
            else if (evt.button == 0 && evt.target == this)
            {
                if (!evt.actionKey && !evt.shiftKey) ClearSelection();

                m_IsBoxSelecting = true;
                m_BoxSelectStartPos = ContentContainer.WorldToLocal(evt.position);

                m_SelectionBox.style.display = DisplayStyle.Flex;
                m_SelectionBox.style.left = m_BoxSelectStartPos.x;
                m_SelectionBox.style.top = m_BoxSelectStartPos.y;
                m_SelectionBox.style.width = 0;
                m_SelectionBox.style.height = 0;

                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            m_CurrentMousePosition = evt.position;

            if (m_IsPanning && this.HasPointerCapture(evt.pointerId))
            {
                m_PanOffset += (Vector2)evt.position - m_LastMousePosition;
                m_LastMousePosition = evt.position;
                ApplyTransform();
                evt.StopPropagation();
            }
            else if (m_IsBoxSelecting && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 currentPos = ContentContainer.WorldToLocal(evt.position);
                Vector2 min = Vector2.Min(m_BoxSelectStartPos, currentPos);
                Vector2 max = Vector2.Max(m_BoxSelectStartPos, currentPos);

                m_SelectionBox.style.left = min.x;
                m_SelectionBox.style.top = min.y;
                m_SelectionBox.style.width = max.x - min.x;
                m_SelectionBox.style.height = max.y - min.y;
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (m_IsPanning && this.HasPointerCapture(evt.pointerId))
            {
                m_IsPanning = false;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
            else if (m_IsBoxSelecting && this.HasPointerCapture(evt.pointerId))
            {
                m_IsBoxSelecting = false;
                m_SelectionBox.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);

                var selectionRect = new Rect(
                    m_SelectionBox.style.left.value.value,
                    m_SelectionBox.style.top.value.value,
                    m_SelectionBox.style.width.value.value,
                    m_SelectionBox.style.height.value.value);

                foreach (var view in NodeViews)
                    if (selectionRect.Overlaps(view.layout)) AddToSelection(view);
                evt.StopPropagation();
            }
        }

        protected void ApplyTransform()
        {
            ContentContainer.style.translate = new Translate(m_PanOffset.x, m_PanOffset.y, 0);
            ContentContainer.style.scale = new Scale(new Vector3(m_ZoomFactor, m_ZoomFactor, 1));
            MarkDirtyRepaint();
        }

        protected virtual void DrawGrid(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            painter.strokeColor = GridColor;
            painter.lineWidth = 1.0f;
            float gs = GridSpacing * m_ZoomFactor;
            if (gs < 2f) return;
            float ox = m_PanOffset.x % gs;
            float oy = m_PanOffset.y % gs;

            painter.BeginPath();
            for (float x = ox; x < layout.width; x += gs) { painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, layout.height)); }
            for (float y = oy; y < layout.height; y += gs) { painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(layout.width, y)); }
            painter.Stroke();
        }

        #endregion
    }
}
