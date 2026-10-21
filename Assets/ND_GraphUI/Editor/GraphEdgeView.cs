using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>
    /// A connection drawn to its target (or to the mouse while it is being dragged), in one of two modes:
    /// <list type="bullet">
    /// <item><b>Port-anchored</b> — from an output port to an input port. The path leaves and enters each port along
    /// <see cref="GraphPortView.Outward"/>, so it works for top-to-bottom trees and left-to-right flows alike.</item>
    /// <item><b>Node-anchored</b> — from one node to another with no ports: a straight line aimed from centre to
    /// centre, cut at both node borders, arrow at the target's border. Node positions alone decide the direction.</item>
    /// </list>
    /// Shape (port mode), colour and arrow come from the <see cref="EdgeTheme"/>.
    /// </summary>
    public class GraphEdgeView : VisualElement
    {
        public GraphPortView OutputPort { get; private set; }
        public GraphPortView InputPort { get; private set; }
        public EdgeTheme Theme { get; }

        private GraphNodeView m_FromNode;
        private GraphNodeView m_ToNode;

        /// <summary>Source node (the output port's node in port mode).</summary>
        public GraphNodeView FromNode => OutputPort != null ? OutputPort.ParentNodeView : m_FromNode;
        /// <summary>Target node (the input port's node in port mode); null while dragging.</summary>
        public GraphNodeView ToNode => InputPort != null ? InputPort.ParentNodeView : m_ToNode;
        public bool IsNodeAnchored => OutputPort == null;
        public bool IsComplete => InputPort != null || m_ToNode != null;

        /// <summary>Sideways shift (pixels) so A→B and B→A do not draw on top of each other. Set by the canvas.</summary>
        public float LaneOffset { get; private set; }

        private Vector2 m_TempEndPosition;
        private readonly Label m_EdgeLabel;

        private Color? m_HighlightColor;
        private bool m_Emphasized;
        private bool m_Flowing;

        /// <summary>Node-anchored edge leaving <paramref name="fromNode"/>; give it a target with <see cref="SetTargetNode"/>.</summary>
        public GraphEdgeView(GraphNodeView fromNode, EdgeTheme theme) : this((GraphPortView)null, theme)
        {
            m_FromNode = fromNode;
            Follow(fromNode);
        }

        public void SetTargetNode(GraphNodeView toNode)
        {
            m_ToNode = toNode;
            Follow(toNode);
            MarkDirtyRepaint();
        }

        public void SetLaneOffset(float offset)
        {
            if (Mathf.Approximately(offset, LaneOffset)) return;
            LaneOffset = offset;
            MarkDirtyRepaint();
        }

        /// <summary>Redraw when the node moves or gets its size (node-anchored ends depend on the node's rect).</summary>
        private void Follow(GraphNodeView node)
        {
            if (node == null) return;
            node.OnNodeMoved += MarkDirtyRepaint;
            node.RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public GraphEdgeView(GraphPortView outputPort, EdgeTheme theme)
        {
            OutputPort = outputPort;
            Theme = theme ?? new EdgeTheme();

            style.position = Position.Absolute;
            style.top = style.left = style.right = style.bottom = 0;
            pickingMode = PickingMode.Ignore;

            m_EdgeLabel = new Label("")
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    backgroundColor = Theme.labelBackgroundColor,
                    color = Theme.labelTextColor,
                    paddingLeft = 5, paddingRight = 5,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    fontSize = 11,
                    display = DisplayStyle.None
                }
            };
            Add(m_EdgeLabel);
            generateVisualContent += DrawEdge;

            if (OutputPort?.ParentNodeView != null) OutputPort.ParentNodeView.OnNodeMoved += MarkDirtyRepaint;
        }

        /// <summary>Text pill in the middle of the edge; empty hides it.</summary>
        public string Text
        {
            get => m_EdgeLabel.text;
            set
            {
                m_EdgeLabel.text = value;
                m_EdgeLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>The middle label element, for adding menus or classes.</summary>
        protected Label EdgeLabel => m_EdgeLabel;

        /// <summary>
        /// Overrides the theme colour (null = theme colour). <paramref name="emphasized"/> draws a thicker line;
        /// <paramref name="flowing"/> animates dots from output to input (the owner keeps calling MarkDirtyRepaint while it runs).
        /// </summary>
        public void SetHighlight(Color? color, bool emphasized, bool flowing)
        {
            bool changed = color != m_HighlightColor || emphasized != m_Emphasized || flowing != m_Flowing;
            m_HighlightColor = color;
            m_Emphasized = emphasized;
            m_Flowing = flowing;
            if (changed || flowing) MarkDirtyRepaint();
        }

        public void SetInputPort(GraphPortView inputPort)
        {
            InputPort = inputPort;
            if (InputPort?.ParentNodeView != null) InputPort.ParentNodeView.OnNodeMoved += MarkDirtyRepaint;
            MarkDirtyRepaint();
        }

        public void UpdateTempPosition(Vector2 mousePosition)
        {
            var canvas = GetFirstAncestorOfType<GraphCanvas>();
            if (canvas == null) return;
            m_TempEndPosition = canvas.ContentContainer.WorldToLocal(mousePosition);
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ path (shape from EdgeTheme.curve)

        /// <summary>Edge end points in canvas-content space.</summary>
        protected bool TryGetEnds(out Vector2 start, out Vector2 end)
        {
            start = end = default;
            if (IsNodeAnchored) return TryGetNodeEnds(out start, out end);
            var canvas = GetFirstAncestorOfType<GraphCanvas>();
            if (canvas == null) return false;
            start = canvas.ContentContainer.WorldToLocal(OutputPort.GlobalCenter);
            end = InputPort != null ? canvas.ContentContainer.WorldToLocal(InputPort.GlobalCenter) : m_TempEndPosition;
            return true;
        }

        /// <summary>Centre-to-centre line, shifted by the lane offset, cut at both node borders (node rects are content space).</summary>
        private bool TryGetNodeEnds(out Vector2 start, out Vector2 end)
        {
            start = end = default;
            if (m_FromNode == null || !IsValidRect(m_FromNode.layout)) return false;
            Rect from = m_FromNode.layout;
            bool hasTarget = m_ToNode != null && IsValidRect(m_ToNode.layout);
            Vector2 target = hasTarget ? m_ToNode.layout.center : m_TempEndPosition;

            Vector2 dir = target - from.center;
            if (dir.sqrMagnitude < 0.01f) return false; // overlapping centres: nothing sensible to draw
            dir.Normalize();
            Vector2 lane = new Vector2(-dir.y, dir.x) * LaneOffset;

            start = ExitPoint(from, from.center + lane, dir);
            end = hasTarget ? ExitPoint(m_ToNode.layout, target + lane, -dir) : target;
            return true;
        }

        private static bool IsValidRect(Rect r) => !float.IsNaN(r.width) && r.width > 0f && r.height > 0f;

        /// <summary>Where a ray from <paramref name="origin"/> (inside <paramref name="rect"/>) along <paramref name="dir"/> leaves the rect.</summary>
        public static Vector2 ExitPoint(Rect rect, Vector2 origin, Vector2 dir)
        {
            float tx = dir.x > 1e-5f ? (rect.xMax - origin.x) / dir.x : dir.x < -1e-5f ? (rect.xMin - origin.x) / dir.x : float.PositiveInfinity;
            float ty = dir.y > 1e-5f ? (rect.yMax - origin.y) / dir.y : dir.y < -1e-5f ? (rect.yMin - origin.y) / dir.y : float.PositiveInfinity;
            float t = Mathf.Max(0f, Mathf.Min(tx, ty));
            return float.IsInfinity(t) ? origin : origin + dir * t;
        }

        private Vector2 StartOutward => OutputPort != null ? OutputPort.Outward : Vector2.right;
        // While dragging there is no input port yet: arrive head-on.
        private Vector2 EndOutward => InputPort != null ? InputPort.Outward : -StartOutward;
        private bool IsHorizontal => OutputPort != null && OutputPort.Orientation == PortOrientation.Horizontal;
        /// <summary>Node-anchored edges are always straight; port edges follow the theme.</summary>
        private EdgeCurve Curve => IsNodeAnchored ? EdgeCurve.Straight : Theme.curve;

        private void BezierControls(Vector2 start, Vector2 end, out Vector2 p1, out Vector2 p2)
        {
            float span = IsHorizontal ? Mathf.Abs(start.x - end.x) : Mathf.Abs(start.y - end.y);
            float dist = span * 0.5f + 25f;
            p1 = start + StartOutward * dist;
            p2 = end + EndOutward * dist;
        }

        /// <summary>Corner points of the Step route: out along the flow axis to the midline, across, then in.</summary>
        private void StepCorners(Vector2 start, Vector2 end, out Vector2 a, out Vector2 b)
        {
            if (IsHorizontal)
            {
                float midX = (start.x + end.x) * 0.5f;
                a = new Vector2(midX, start.y);
                b = new Vector2(midX, end.y);
            }
            else
            {
                float midY = (start.y + end.y) * 0.5f;
                a = new Vector2(start.x, midY);
                b = new Vector2(end.x, midY);
            }
        }

        /// <summary>Point at <paramref name="t"/> (0 = output, 1 = input) along the drawn path.</summary>
        protected Vector2 PointAt(float t, Vector2 start, Vector2 end)
        {
            switch (Curve)
            {
                case EdgeCurve.Straight:
                    return Vector2.Lerp(start, end, t);
                case EdgeCurve.Step:
                {
                    StepCorners(start, end, out var a, out var b);
                    float l1 = Vector2.Distance(start, a), l2 = Vector2.Distance(a, b), l3 = Vector2.Distance(b, end);
                    float total = l1 + l2 + l3;
                    if (total < 0.001f) return start;
                    float d = t * total;
                    if (d <= l1) return Vector2.Lerp(start, a, l1 < 0.001f ? 1f : d / l1);
                    d -= l1;
                    if (d <= l2) return Vector2.Lerp(a, b, l2 < 0.001f ? 1f : d / l2);
                    d -= l2;
                    return Vector2.Lerp(b, end, l3 < 0.001f ? 1f : d / l3);
                }
                default:
                {
                    BezierControls(start, end, out var p1, out var p2);
                    float u = 1f - t;
                    return u * u * u * start + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * end;
                }
            }
        }

        protected Vector2 DirectionAt(float t, Vector2 start, Vector2 end)
        {
            Vector2 dir = (PointAt(Mathf.Min(1f, t + 0.02f), start, end) - PointAt(Mathf.Max(0f, t - 0.02f), start, end)).normalized;
            return dir == Vector2.zero ? StartOutward : dir;
        }

        /// <summary>Approximate distance from <paramref name="contentPoint"/> (canvas-content space) to the drawn path.</summary>
        public float DistanceTo(Vector2 contentPoint)
        {
            if (!IsComplete || !TryGetEnds(out var start, out var end)) return float.MaxValue;
            const int Segments = 32;
            float best = float.MaxValue;
            Vector2 previous = start;
            for (int i = 1; i <= Segments; i++)
            {
                Vector2 point = PointAt(i / (float)Segments, start, end);
                best = Mathf.Min(best, DistanceToSegment(contentPoint, previous, point));
                previous = point;
            }
            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr);
            return Vector2.Distance(p, a + ab * t);
        }

        private void DrawEdge(MeshGenerationContext ctx)
        {
            if (!TryGetEnds(out var start, out var end)) return;

            var painter = ctx.painter2D;
            Color color = m_HighlightColor ?? Theme.edgeColor;
            painter.strokeColor = color;
            painter.lineWidth = m_Emphasized ? Theme.edgeThickness * 1.7f : Theme.edgeThickness;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(start);
            switch (Curve)
            {
                case EdgeCurve.Straight:
                    painter.LineTo(end);
                    break;
                case EdgeCurve.Step:
                {
                    StepCorners(start, end, out var a, out var b);
                    painter.LineTo(a);
                    painter.LineTo(b);
                    painter.LineTo(end);
                    break;
                }
                default:
                {
                    BezierControls(start, end, out var p1, out var p2);
                    painter.BezierCurveTo(p1, p2, end);
                    break;
                }
            }
            painter.Stroke();

            Vector2 mid = PointAt(0.5f, start, end);
            if (Theme.showArrow || IsNodeAnchored)
            {
                Vector2 dir = DirectionAt(0.5f, start, end);
                // Node mode: the arrow sits at the target's border (no port shows where the edge ends).
                Vector2 at = IsNodeAnchored ? end - dir * ArrowSize * 0.6f : mid;
                DrawArrow(painter, at, dir, color);
            }

            if (m_Flowing)
            {
                // Three dots flowing from output to input.
                float phase = (float)(EditorApplication.timeSinceStartup * 0.8 % 1.0);
                painter.fillColor = Color.white;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = PointAt((phase + i / 3f) % 1f, start, end);
                    painter.BeginPath();
                    painter.Arc(p, 3.5f, 0f, 360f);
                    painter.Fill();
                }
            }

            m_EdgeLabel.style.left = mid.x - (m_EdgeLabel.layout.width / 2f);
            m_EdgeLabel.style.top = mid.y - (m_EdgeLabel.layout.height / 2f) - (string.IsNullOrEmpty(Text) ? 0 : 12f);
            OnPathDrawn(start, end);
        }

        /// <summary>Called after each repaint of the path: place extra labels with <see cref="PointAt"/>.</summary>
        protected virtual void OnPathDrawn(Vector2 start, Vector2 end) { }

        private const float ArrowSize = 10f;

        private static void DrawArrow(Painter2D painter, Vector2 center, Vector2 dir, Color color)
        {
            float size = ArrowSize;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 tip = center + dir * (size * 0.6f);
            Vector2 bMid = center - dir * (size * 0.6f);

            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(bMid + perp * (size * 0.6f));
            painter.LineTo(bMid - perp * (size * 0.6f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
