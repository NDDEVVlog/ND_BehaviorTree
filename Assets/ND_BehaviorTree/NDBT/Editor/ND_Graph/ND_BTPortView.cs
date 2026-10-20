using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public enum BTPortDirection { Input, Output }
    public enum BTPortCapacity { Single, Multi }

    /// <summary>
    /// Connection point. Look by state (colours from the port theme):
    ///   unplugged  — hollow ring;
    ///   plugged    — filled dot;
    ///   multi      — an output that takes many children is a wider pill (filled once it has at least one child);
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

        private readonly Color m_Color;
        private readonly float m_Size;
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

            style.alignSelf = Align.Center;
            style.height = m_Size;
            style.width = capacity == BTPortCapacity.Multi ? m_Size * 1.8f : m_Size;
            float radius = m_Size / 2f;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2f;
            if (direction == BTPortDirection.Input) style.marginTop = -m_Size / 2f;
            else style.marginBottom = -m_Size / 2f;
            style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale"), new StylePropertyName("background-color") };
            style.transitionDuration = new List<TimeValue> { new TimeValue(0.08f, TimeUnit.Second) };

            RegisterCallback<PointerEnterEvent>(_ => { m_Hover = true; UpdateVisual(); });
            RegisterCallback<PointerLeaveEvent>(_ => { m_Hover = false; UpdateVisual(); });
            RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button == 0) {
                    GetFirstAncestorOfType<ND_BTGraphCanvas>()?.ConnectionManager.StartConnection(this, evt.position, evt.pointerId);
                    evt.StopPropagation();
                }
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
            Color rim = m_Candidate ? Color.Lerp(m_Color, Color.white, 0.45f) : m_Color;

            style.backgroundColor = plugged ? m_Color : (m_Candidate ? new Color(m_Color.r, m_Color.g, m_Color.b, 0.35f) : HollowFill);
            Color border = plugged && !m_Candidate ? PluggedRim : rim;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = border;

            float scale = m_Hover ? (m_Candidate ? 1.55f : 1.3f) : (m_Candidate ? 1.2f : 1f);
            style.scale = new Scale(new Vector3(scale, scale, 1f));

            tooltip = Direction == BTPortDirection.Input
                ? (plugged ? "Input — connected to a parent. Right-click the node to disconnect." : "Input — not connected: drag from a parent's output to here.")
                : Capacity == BTPortCapacity.Multi
                    ? (plugged ? $"Output — {Connections.Count} child(ren). Drag to add more; order = left to right." : "Output — takes many children: drag to a node's input.")
                    : (plugged ? "Output — connected (single child)." : "Output — single child: drag to a node's input.");
        }
    }
}
