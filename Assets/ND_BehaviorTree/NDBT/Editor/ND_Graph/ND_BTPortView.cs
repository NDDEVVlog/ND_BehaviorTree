using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public enum BTPortDirection { Input, Output }
    public enum BTPortCapacity { Single, Multi }

    /// <summary>
    /// Connection point. Shape comes from the port theme (Circle / Square / Diamond / Cat); look by state:
    ///   unplugged  — hollow (Cat: mouth open);
    ///   plugged    — filled (Cat: mouth closed, "ω");
    ///   multi      — an output that takes many children is wider;
    ///   candidate  — while dragging a connection, ports that can receive it glow;
    ///   hover      — grows under the mouse (strongest while dragging onto it).
    /// </summary>
    public class ND_BTPortView : VisualElement
    {
        public ND_BTNodeView ParentNodeView { get; private set; }
        public BTPortDirection Direction { get; private set; }
        public BTPortCapacity Capacity { get; private set; }
        public List<ND_BTEdgeView> Connections { get; private set; } = new List<ND_BTEdgeView>();
        public Vector2 GlobalCenter => parent.LocalToWorld(layout.center);
        public bool IsConnected => Connections.Count > 0;

        private static readonly Color HollowFill = new Color(0.11f, 0.12f, 0.15f, 1f);
        private static readonly Color PluggedRim = new Color(0.07f, 0.08f, 0.1f, 1f);
        private static readonly Color CatInk = new Color(0.13f, 0.1f, 0.15f, 1f);
        private static readonly Color CatTongue = new Color(1f, 0.45f, 0.6f, 1f);

        private readonly Color m_Color;
        private readonly float m_Size;
        private readonly PortShape m_Shape;
        private readonly Texture2D m_UnpluggedImage;
        private readonly Texture2D m_PluggedImage;
        private bool m_Hover;
        private bool m_Candidate;

        public ND_BTPortView(ND_BTNodeView parentNodeView, BTPortDirection direction, BTPortCapacity capacity)
        {
            ParentNodeView = parentNodeView;
            Direction = direction;
            Capacity = capacity;

            var theme = ND_BehaviorTreeSetting.Instance.portTheme;
            m_Color = direction == BTPortDirection.Input ? theme.inputPortColor : theme.outputPortColor;
            m_Size = theme.portSize;
            m_Shape = theme.shape;
            m_UnpluggedImage = theme.unpluggedImage;
            m_PluggedImage = theme.pluggedImage;
            if (m_Shape == PortShape.Image && (m_UnpluggedImage == null || m_PluggedImage == null))
            {
                Debug.LogWarning("[ND_BTPortView] Port shape is Image but the port theme has no unplugged/plugged image; drawing circles instead. Assign both in Project Settings → ND Behavior Tree → Port Theme.");
                m_Shape = PortShape.Circle;
            }
            bool picture = m_Shape == PortShape.Cat || m_Shape == PortShape.Image;
            float height = m_Shape == PortShape.Image ? m_Size * 1.5f : m_Size; // the picture has transparent margins

            style.alignSelf = Align.Center;
            style.height = height;
            style.width = m_Shape == PortShape.Cat ? m_Size * 1.25f
                        : m_Shape == PortShape.Image ? height
                        : capacity == BTPortCapacity.Multi && m_Shape != PortShape.Diamond ? m_Size * 1.8f
                        : m_Size;
            if (direction == BTPortDirection.Input) style.marginTop = -height / 2f;
            else style.marginBottom = -height / 2f;

            float radius = m_Shape == PortShape.Circle || m_Shape == PortShape.Image ? height / 2f : picture ? 0f : 3f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
            float border = picture ? 0f : 2f;
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

            RegisterCallback<PointerEnterEvent>(_ => { m_Hover = true; UpdateVisual(); });
            RegisterCallback<PointerLeaveEvent>(_ => { m_Hover = false; UpdateVisual(); });
            RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
                // Alt+click (as in Unreal): cut this port's connections. Otherwise drag a new connection.
                if (evt.altKey) canvas?.DisconnectPort(this);
                else canvas?.ConnectionManager.StartConnection(this, evt.position, evt.pointerId);
                evt.StopPropagation();
            });
            UpdateVisual();
        }

        public void Connect(ND_BTEdgeView edge) { if (!Connections.Contains(edge)) Connections.Add(edge); UpdateVisual(); }
        public void Disconnect(ND_BTEdgeView edge) { Connections.Remove(edge); UpdateVisual(); }

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

        private void UpdateVisual()
        {
            bool plugged = IsConnected;

            if (m_Shape == PortShape.Cat)
            {
                style.backgroundColor = Color.clear;
                MarkDirtyRepaint(); // the mouth follows the plugged state
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
            tooltip = (Direction == BTPortDirection.Input
                ? (plugged ? "Input — connected to a parent. Alt+click to disconnect from the parent." : "Input — not connected: drag from a parent's output to here.")
                : Capacity == BTPortCapacity.Multi
                    ? (plugged ? $"Output — {Connections.Count} child(ren). Drag to add more; order = left to right. Alt+click to disconnect all children." : "Output — takes many children: drag to a node's input.")
                    : (plugged ? "Output — connected (single child). Alt+click to disconnect." : "Output — single child: drag to a node's input.")) + catNote;
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
