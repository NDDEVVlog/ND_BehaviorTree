using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Base for rectangular canvas annotations (groups, notes): drag by the header, resize from 4 edges + 4 corners.
    /// The view previews changes live and reports ONE commit when the pointer is released.
    /// </summary>
    public abstract class ND_BTBoxView : VisualElement
    {
        protected const float MinWidth = 120f;
        protected const float MinHeight = 60f;
        private const float HandleSize = 8f;

        public VisualElement Header { get; }
        /// <summary>Accent colour used for hovered resize handles and the corner grip.</summary>
        protected Color AccentColor { get; set; } = new Color(1f, 1f, 1f, 0.6f);
        private readonly Label m_Grip;
        public Rect ViewRect => new Rect(style.left.value.value, style.top.value.value, style.width.value.value, style.height.value.value);

        private Rect m_StartRect;
        private Vector2 m_StartMouse;
        private bool m_Dragging;

        protected ND_BTBoxView()
        {
            style.position = Position.Absolute;
            Header = new VisualElement { name = "box-header" };
            Header.style.flexDirection = FlexDirection.Row;
            Header.style.alignItems = Align.Center;
            Header.style.minHeight = 24;
            Header.style.paddingLeft = Header.style.paddingRight = 6;
            Add(Header);

            Header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            Header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            Header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);

            //            left,  top,   right, bottom   (which edges a handle moves)
            AddHandle(true,  false, false, false, "w-resize");
            AddHandle(false, false, true,  false, "e-resize");
            AddHandle(false, true,  false, false, "n-resize");
            AddHandle(false, false, false, true,  "s-resize");
            AddHandle(true,  true,  false, false, "nw-resize");
            AddHandle(false, true,  true,  false, "ne-resize");
            AddHandle(true,  false, false, true,  "sw-resize");
            AddHandle(false, false, true,  true,  "se-resize");

            // Visible bottom-right grip so users know the box can be resized (the se-resize handle sits on top of it).
            m_Grip = new Label("◢")
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, right = 2, bottom = 0, fontSize = 12, color = new Color(1f, 1f, 1f, 0.45f) }
            };
            hierarchy.Insert(1, m_Grip);
        }

        protected void SetGripColor(Color color) => m_Grip.style.color = color;

        public void SetViewRect(Rect rect)
        {
            style.left = rect.x;
            style.top = rect.y;
            style.width = rect.width;
            style.height = rect.height;
        }

        /// <summary>Live preview while dragging the header.</summary>
        protected virtual void OnMovePreview(Vector2 delta) { }
        protected abstract void OnMoveCommitted(Rect startRect, Rect finalRect);
        protected abstract void OnResizeCommitted(Rect finalRect);

        private float CanvasScale
        {
            get
            {
                float s = parent != null ? parent.resolvedStyle.scale.value.x : 1f;
                return s > 0.0001f ? s : 1f;
            }
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.clickCount > 1) return;
            if ((evt.target as VisualElement)?.GetFirstOfType<TextField>() != null) return; // let text fields take the click
            m_Dragging = true;
            m_StartRect = ViewRect;
            m_StartMouse = evt.position;
            Header.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (!m_Dragging || !Header.HasPointerCapture(evt.pointerId)) return;
            Vector2 delta = ((Vector2)evt.position - m_StartMouse) / CanvasScale;
            SetViewRect(new Rect(m_StartRect.position + delta, m_StartRect.size));
            OnMovePreview(delta);
            evt.StopPropagation();
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (!m_Dragging || !Header.HasPointerCapture(evt.pointerId)) return;
            m_Dragging = false;
            Header.ReleasePointer(evt.pointerId);
            if (ViewRect.position != m_StartRect.position) OnMoveCommitted(m_StartRect, ViewRect);
            evt.StopPropagation();
        }

        private void AddHandle(bool left, bool top, bool right, bool bottom, string name)
        {
            var handle = new VisualElement { name = name };
            handle.style.position = Position.Absolute;
            if (left) handle.style.left = -HandleSize / 2;
            if (right) handle.style.right = -HandleSize / 2;
            if (top) handle.style.top = -HandleSize / 2;
            if (bottom) handle.style.bottom = -HandleSize / 2;
            bool horizontalEdge = (top || bottom) && !(left || right);
            bool verticalEdge = (left || right) && !(top || bottom);
            if (horizontalEdge) { handle.style.left = HandleSize; handle.style.right = HandleSize; handle.style.height = HandleSize; }
            else if (verticalEdge) { handle.style.top = HandleSize; handle.style.bottom = HandleSize; handle.style.width = HandleSize; }
            else { handle.style.width = HandleSize * 1.5f; handle.style.height = HandleSize * 1.5f; }

            Rect startRect = default;
            Vector2 startMouse = default;
            bool resizing = false;

            handle.tooltip = "Drag to resize";
            handle.RegisterCallback<PointerEnterEvent>(e => handle.style.backgroundColor = AccentColor);
            handle.RegisterCallback<PointerLeaveEvent>(e => { if (!resizing) handle.style.backgroundColor = StyleKeyword.Null; });

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                resizing = true;
                startRect = ViewRect;
                startMouse = evt.position;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!resizing || !handle.HasPointerCapture(evt.pointerId)) return;
                Vector2 d = ((Vector2)evt.position - startMouse) / CanvasScale;
                float xMin = startRect.xMin, yMin = startRect.yMin, xMax = startRect.xMax, yMax = startRect.yMax;
                if (left) xMin = Mathf.Min(xMin + d.x, xMax - MinWidth);
                if (right) xMax = Mathf.Max(xMax + d.x, xMin + MinWidth);
                if (top) yMin = Mathf.Min(yMin + d.y, yMax - MinHeight);
                if (bottom) yMax = Mathf.Max(yMax + d.y, yMin + MinHeight);
                SetViewRect(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!resizing || !handle.HasPointerCapture(evt.pointerId)) return;
                resizing = false;
                handle.ReleasePointer(evt.pointerId);
                handle.style.backgroundColor = StyleKeyword.Null;
                if (ViewRect != startRect) OnResizeCommitted(ViewRect);
                evt.StopPropagation();
            });

            hierarchy.Add(handle);
        }

        protected static void SetBorder(VisualElement e, Color color, float width, float radius)
        {
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        /// <summary>Shared colour presets for groups and notes.</summary>
        protected static readonly (string name, Color color)[] ColorPresets =
        {
            ("Blue",   new Color(0.25f, 0.45f, 0.75f, 1f)),
            ("Green",  new Color(0.30f, 0.60f, 0.35f, 1f)),
            ("Red",    new Color(0.75f, 0.30f, 0.30f, 1f)),
            ("Purple", new Color(0.55f, 0.35f, 0.75f, 1f)),
            ("Yellow", new Color(0.95f, 0.85f, 0.35f, 1f)),
            ("Gray",   new Color(0.45f, 0.45f, 0.45f, 1f)),
        };
    }
}
