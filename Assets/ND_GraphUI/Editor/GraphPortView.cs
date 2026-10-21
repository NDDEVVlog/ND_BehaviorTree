using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>
    /// Connection point. Shape comes from the owner node's <see cref="PortTheme"/> (Circle / Square / Diamond / Cat / Image / Custom);
    /// look by state:
    ///   unplugged  — hollow (Cat: mouth open);
    ///   plugged    — filled (Cat: mouth closed, "ω");
    ///   multi      — a port that takes many edges is longer along the node edge;
    ///   candidate  — while dragging a connection, ports that can receive it glow;
    ///   hover      — grows under the mouse (strongest while dragging onto it).
    /// </summary>
    public class GraphPortView : VisualElement
    {
        public GraphNodeView ParentNodeView { get; private set; }
        public PortDirection Direction { get; private set; }
        public PortCapacity Capacity { get; private set; }
        public PortOrientation Orientation { get; private set; }
        /// <summary>Identifies the port among its node's ports (empty for a node's only input/output).</summary>
        public string PortName { get; private set; }
        public List<GraphEdgeView> Connections { get; private set; } = new List<GraphEdgeView>();
        public Vector2 GlobalCenter => parent.LocalToWorld(layout.center);
        public bool IsConnected => Connections.Count > 0;

        /// <summary>Unit vector pointing away from the node: the way an edge leaves (output) or arrives (input).</summary>
        public Vector2 Outward => Orientation == PortOrientation.Vertical
            ? (Direction == PortDirection.Output ? Vector2.up : Vector2.down)       // UI space: +y is down
            : (Direction == PortDirection.Output ? Vector2.right : Vector2.left);

        private static readonly Color HollowFill = new Color(0.11f, 0.12f, 0.15f, 1f);
        private static readonly Color PluggedRim = new Color(0.07f, 0.08f, 0.1f, 1f);
        private static readonly Color CatInk = new Color(0.13f, 0.1f, 0.15f, 1f);
        private static readonly Color CatTongue = new Color(1f, 0.45f, 0.6f, 1f);

        private readonly Color m_Color;
        private readonly float m_Size;
        private readonly PortShape m_Shape;
        private readonly Texture2D m_UnpluggedImage;
        private readonly Texture2D m_PluggedImage;
        private PortPainter m_Painter;
        private bool m_Hover;
        private bool m_Candidate;

        public GraphPortView(GraphNodeView parentNodeView, PortDirection direction, PortCapacity capacity,
            PortOrientation orientation = PortOrientation.Vertical, string portName = "")
        {
            ParentNodeView = parentNodeView;
            Direction = direction;
            Capacity = capacity;
            Orientation = orientation;
            PortName = portName ?? string.Empty;

            var theme = parentNodeView.PortTheme;
            m_Color = direction == PortDirection.Input ? theme.inputPortColor : theme.outputPortColor;
            m_Size = theme.portSize;
            m_Shape = theme.shape;
            m_UnpluggedImage = theme.unpluggedImage;
            m_PluggedImage = theme.pluggedImage;
            if (m_Shape == PortShape.Image && (m_UnpluggedImage == null || m_PluggedImage == null))
            {
                Debug.LogWarning("[GraphUI] Port shape is Image but the port theme has no unplugged/plugged image; drawing circles instead. Assign both images in the port theme.");
                m_Shape = PortShape.Circle;
            }
            if (m_Shape == PortShape.Custom)
            {
                if (theme.customPainter != null) m_Painter = PortPainter.Create(theme.customPainter.GetClass(), theme.customPainter.name);
                if (m_Painter == null && theme.customTemplate == null)
                {
                    Debug.LogWarning("[GraphUI] Port shape is Custom but the port theme has no usable Custom Painter or Custom Template; drawing circles instead.");
                    m_Shape = PortShape.Circle;
                }
            }

            ApplySize();
            float border = IsPicture ? 0f : 2f;
            if (m_Shape == PortShape.Image)
            {
                style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = border;
            style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale"), new StylePropertyName("background-color") };
            style.transitionDuration = new List<TimeValue> { new TimeValue(0.08f, TimeUnit.Second) };
            if (m_Shape == PortShape.Cat) generateVisualContent += DrawCat;
            if (m_Shape == PortShape.Custom) SetupCustom(theme);

            RegisterCallback<PointerEnterEvent>(_ => { m_Hover = true; UpdateVisual(); });
            RegisterCallback<PointerLeaveEvent>(_ => { m_Hover = false; UpdateVisual(); });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                GetFirstAncestorOfType<GraphCanvas>()?.OnPortPointerDown(this, evt);
                evt.StopPropagation();
            });
            UpdateVisual();
        }

        private bool IsPicture => m_Shape == PortShape.Cat || m_Shape == PortShape.Image || m_Shape == PortShape.Custom;

        /// <summary>Sizes the port; lengths are computed for a vertical port and transposed for a horizontal one.</summary>
        private void ApplySize()
        {
            bool custom = m_Shape == PortShape.Custom;
            Vector2 customSize = m_Painter?.GetSize(m_Size, Direction, Capacity) ?? new Vector2(m_Size, m_Size);
            float across = m_Shape == PortShape.Image ? m_Size * 1.5f : custom ? customSize.y : m_Size; // the picture has transparent margins
            float along = custom ? customSize.x
                        : m_Shape == PortShape.Cat ? m_Size * 1.25f
                        : m_Shape == PortShape.Image ? across
                        : Capacity == PortCapacity.Multi && m_Shape != PortShape.Diamond ? m_Size * 1.8f
                        : m_Size;

            bool vertical = Orientation == PortOrientation.Vertical;
            float width = vertical ? along : across;
            float height = vertical ? across : along;
            style.alignSelf = Align.Center;
            style.width = width;
            style.height = height;
            // Half of the port overhangs the node edge, so the edge meets the node border.
            if (vertical)
            {
                if (Direction == PortDirection.Input) style.marginTop = -height / 2f;
                else style.marginBottom = -height / 2f;
            }
            else
            {
                if (Direction == PortDirection.Input) style.marginLeft = -width / 2f;
                else style.marginRight = -width / 2f;
            }

            float radius = m_Shape == PortShape.Circle || m_Shape == PortShape.Image ? Mathf.Min(width, height) / 2f : IsPicture ? 0f : 3f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
        }

        public void Connect(GraphEdgeView edge) { if (!Connections.Contains(edge)) Connections.Add(edge); UpdateVisual(); }
        public void Disconnect(GraphEdgeView edge) { Connections.Remove(edge); UpdateVisual(); }

        /// <summary>While a connection is being dragged: can this port receive it?</summary>
        public void SetCandidate(bool candidate)
        {
            if (m_Candidate == candidate) return;
            m_Candidate = candidate;
            UpdateVisual();
        }

        /// <summary>While dragging a connection the canvas holds the pointer, so the manager reports the port under it.</summary>
        public void SetDragHover(bool hover)
        {
            if (m_Hover == hover) return;
            m_Hover = hover;
            UpdateVisual();
        }

        /// <summary>Hover text; override to describe what the connection means in your graph.</summary>
        protected virtual string BuildTooltip()
        {
            if (Direction == PortDirection.Input)
                return IsConnected ? "Input — connected. Alt+click to disconnect." : "Input — drag from an output to here.";
            return Capacity == PortCapacity.Multi
                ? (IsConnected ? $"Output — {Connections.Count} connection(s). Drag to add more. Alt+click to disconnect all." : "Output — takes many connections: drag to an input.")
                : (IsConnected ? "Output — connected. Alt+click to disconnect." : "Output — drag to an input.");
        }

        protected void UpdateVisual()
        {
            bool plugged = IsConnected;

            if (m_Shape == PortShape.Cat)
            {
                style.backgroundColor = Color.clear;
                MarkDirtyRepaint(); // the mouth follows the plugged state
            }
            else if (m_Shape == PortShape.Custom)
            {
                style.backgroundColor = Color.clear;
                EnableInClassList("nd-port--plugged", plugged);
                EnableInClassList("nd-port--hover", m_Hover);
                EnableInClassList("nd-port--candidate", m_Candidate);
                if (m_Painter != null) MarkDirtyRepaint();
            }
            else if (m_Shape == PortShape.Image)
            {
                // Mouth open while waiting for a connection, closed once connected; a soft halo when it can receive one.
                style.backgroundImage = new StyleBackground(plugged ? m_PluggedImage : m_UnpluggedImage);
                style.backgroundColor = m_Candidate ? new Color(m_Color.r, m_Color.g, m_Color.b, 0.45f) : Color.clear;
            }
            else
            {
                Color rim = m_Candidate ? Color.Lerp(m_Color, Color.white, 0.45f) : m_Color;
                style.backgroundColor = plugged ? m_Color : (m_Candidate ? new Color(m_Color.r, m_Color.g, m_Color.b, 0.35f) : HollowFill);
                Color border = plugged && !m_Candidate ? PluggedRim : rim;
                style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = border;
            }

            float scale = m_Hover ? (m_Candidate ? 1.55f : 1.3f) : (m_Candidate ? 1.2f : 1f);
            style.scale = new Scale(new Vector3(scale, scale, 1f));
            style.rotate = m_Shape == PortShape.Diamond ? new Rotate(45f) : new Rotate(0f);

            string catNote = m_Shape == PortShape.Cat || m_Shape == PortShape.Image ? (plugged ? " (fed: mouth closed)" : " (hungry: mouth open)") : string.Empty;
            tooltip = BuildTooltip() + catNote;
        }

        /// <summary>The painter drawing this port (Shape = Custom with a Custom Painter), else null.</summary>
        public PortPainter Painter => m_Painter;

        /// <summary>
        /// Shape = Custom: clones the template (its elements let clicks through to the port), adds the stylesheet and
        /// the fixed nd-port classes; the state classes follow in <see cref="UpdateVisual"/>.
        /// </summary>
        private void SetupCustom(PortTheme theme)
        {
            AddToClassList("nd-port");
            AddToClassList(Direction == PortDirection.Input ? "nd-port--input" : "nd-port--output");
            EnableInClassList("nd-port--multi", Capacity == PortCapacity.Multi);
            EnableInClassList("nd-port--horizontal", Orientation == PortOrientation.Horizontal);
            if (theme.customStyle != null) styleSheets.Add(theme.customStyle);
            if (theme.customTemplate != null)
            {
                theme.customTemplate.CloneTree(this);
                this.Query<VisualElement>().ForEach(e => { if (e != this) e.pickingMode = PickingMode.Ignore; });
            }
            if (m_Painter != null) generateVisualContent += DrawCustom;
        }

        private void DrawCustom(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (m_Painter == null || rect.width < 1f || rect.height < 1f) return;
            try
            {
                m_Painter.Draw(ctx.painter2D, rect, new PortState(Direction, Capacity, Orientation, m_Color, Connections.Count, m_Hover, m_Candidate));
            }
            catch (System.Exception e)
            {
                // Stop drawing it: a throwing painter would otherwise log on every repaint.
                Debug.LogError($"[GraphUI] Custom Painter '{m_Painter.GetType().FullName}' threw and was turned off for this port: {e}");
                m_Painter = null;
            }
        }

        /// <summary>Cat head: ears, face, eyes; mouth open while unplugged, a closed "ω" once connected.</summary>
        private void DrawCat(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width < 2f || rect.height < 2f) return;
            var p = ctx.painter2D;
            bool plugged = IsConnected;
            float r = Mathf.Min(rect.width, rect.height) * 0.4f;
            Vector2 c = new Vector2(rect.width * 0.5f, rect.height * 0.56f);
            Color face = m_Candidate ? Color.Lerp(m_Color, Color.white, 0.35f) : m_Color;

            // Ears
            p.fillColor = face;
            Ear(p, c, r, -1f);
            Ear(p, c, r, 1f);

            // Head (+ a light rim when it can receive a connection)
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Fill();
            if (m_Candidate)
            {
                p.strokeColor = Color.white;
                p.lineWidth = Mathf.Max(1f, r * 0.12f);
                p.Stroke();
            }

            // Eyes: open dots while waiting, happy "^ ^" once fed
            if (plugged)
            {
                p.strokeColor = CatInk;
                p.lineWidth = Mathf.Max(1f, r * 0.13f);
                p.lineCap = LineCap.Round;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 e = c + new Vector2(side * r * 0.4f, -r * 0.12f);
                    p.BeginPath();
                    p.MoveTo(e + new Vector2(-r * 0.15f, r * 0.06f));
                    p.LineTo(e + new Vector2(0f, -r * 0.1f));
                    p.LineTo(e + new Vector2(r * 0.15f, r * 0.06f));
                    p.Stroke();
                }
            }
            else
            {
                p.fillColor = CatInk;
                for (int side = -1; side <= 1; side += 2)
                {
                    p.BeginPath();
                    p.Arc(c + new Vector2(side * r * 0.4f, -r * 0.12f), r * 0.13f, 0f, 360f);
                    p.Fill();
                }
            }

            if (plugged)
            {
                // Closed mouth: ω
                p.strokeColor = CatInk;
                p.lineWidth = Mathf.Max(1f, r * 0.12f);
                p.lineCap = LineCap.Round;
                Vector2 m = c + new Vector2(0f, r * 0.3f);
                p.BeginPath();
                p.MoveTo(m + new Vector2(-r * 0.34f, 0f));
                p.QuadraticCurveTo(m + new Vector2(-r * 0.17f, r * 0.28f), m);
                p.QuadraticCurveTo(m + new Vector2(r * 0.17f, r * 0.28f), m + new Vector2(r * 0.34f, 0f));
                p.Stroke();
            }
            else
            {
                // Open mouth (with a little tongue)
                Vector2 m = c + new Vector2(0f, r * 0.42f);
                p.fillColor = CatInk;
                p.BeginPath();
                p.Arc(m, r * 0.3f, 0f, 360f);
                p.Fill();
                p.fillColor = CatTongue;
                p.BeginPath();
                p.Arc(m + new Vector2(0f, r * 0.13f), r * 0.15f, 0f, 360f);
                p.Fill();
            }
        }

        private static void Ear(Painter2D p, Vector2 c, float r, float side)
        {
            p.BeginPath();
            p.MoveTo(c + new Vector2(side * r * 0.95f, -r * 0.1f));
            p.LineTo(c + new Vector2(side * r * 0.85f, -r * 1.25f));
            p.LineTo(c + new Vector2(side * r * 0.15f, -r * 0.85f));
            p.ClosePath();
            p.Fill();
        }
    }
}
