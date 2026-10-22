using System;
using System.Collections.Generic;
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

        // Bend points (canvas-content space): the path runs through them in order.
        private readonly List<Vector2> m_Bends = new List<Vector2>();
        private readonly List<VisualElement> m_BendHandles = new List<VisualElement>();
        private int m_DraggedBend = -1;

        public const string BendHandleClass = "nd-edge-bend";
        private const float BendHandleSize = 10f;
        private const float CornerRadius = 14f;
        private const float PortStub = 16f;

        /// <summary>Points the edge bends through (canvas-content space), in order from output to input.</summary>
        public IReadOnlyList<Vector2> Bends => m_Bends;

        /// <summary>
        /// The user added (double-click on the edge, via the canvas), moved (drag a dot) or removed (double-click or
        /// Alt+click a dot) a bend point. The owner saves <see cref="Bends"/>.
        /// </summary>
        public event Action<GraphEdgeView> BendsChanged;

        /// <summary>Whether <paramref name="element"/> is one of the draggable bend dots.</summary>
        public static bool IsBendHandle(VisualElement element) => element != null && element.ClassListContains(BendHandleClass);

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

        #region Bend points

        /// <summary>Replaces the bend points (e.g. from saved data). Raises nothing.</summary>
        public void SetBends(IEnumerable<Vector2> points)
        {
            m_Bends.Clear();
            if (points != null) m_Bends.AddRange(points);
            RebuildBendHandles();
            MarkDirtyRepaint();
        }

        /// <summary>Adds a bend point at <paramref name="contentPoint"/>, in the segment of the path nearest to it. Raises <see cref="BendsChanged"/>.</summary>
        public void InsertBend(Vector2 contentPoint)
        {
            if (!IsComplete || !TryGetEnds(out var start, out var end)) return;
            var path = PathPoints(start, end);
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float d = DistanceToSegment(contentPoint, path[i], path[i + 1]);
                if (d < best) { best = d; nearest = i; }
            }
            // Path = start, [port stub], bends…, [port stub], end: segment i ends just before bend (i - leading points + 1).
            int leading = IsNodeAnchored ? 1 : 2;
            int index = Mathf.Clamp(nearest + 1 - (leading - 1), 0, m_Bends.Count);
            m_Bends.Insert(index, contentPoint);
            RebuildBendHandles();
            MarkDirtyRepaint();
            BendsChanged?.Invoke(this);
        }

        public void RemoveBend(int index)
        {
            if (index < 0 || index >= m_Bends.Count) return;
            m_Bends.RemoveAt(index);
            RebuildBendHandles();
            MarkDirtyRepaint();
            BendsChanged?.Invoke(this);
        }

        private void RebuildBendHandles()
        {
            foreach (var handle in m_BendHandles) handle.RemoveFromHierarchy();
            m_BendHandles.Clear();
            for (int i = 0; i < m_Bends.Count; i++)
            {
                int index = i;
                var handle = new VisualElement { tooltip = "Drag to bend the edge · double-click or Alt+click to remove" };
                handle.AddToClassList(BendHandleClass);
                var s = handle.style;
                s.position = Position.Absolute;
                s.width = s.height = BendHandleSize;
                s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = BendHandleSize / 2f;
                s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1.5f;
                s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
                handle.RegisterCallback<PointerDownEvent>(evt => OnBendPointerDown(evt, handle, index));
                handle.RegisterCallback<PointerMoveEvent>(evt => OnBendPointerMove(evt, handle));
                handle.RegisterCallback<PointerUpEvent>(evt => OnBendPointerUp(evt, handle));
                handle.RegisterCallback<PointerCaptureOutEvent>(_ => m_DraggedBend = -1);
                m_BendHandles.Add(handle);
                Add(handle);
            }
        }

        private void OnBendPointerDown(PointerDownEvent evt, VisualElement handle, int index)
        {
            if (evt.button != 0) return;
            evt.StopPropagation();
            if (evt.altKey || evt.clickCount == 2)
            {
                RemoveBend(index);
                return;
            }
            m_DraggedBend = index;
            handle.CapturePointer(evt.pointerId);
        }

        private void OnBendPointerMove(PointerMoveEvent evt, VisualElement handle)
        {
            if (m_DraggedBend < 0 || !handle.HasPointerCapture(evt.pointerId)) return;
            m_Bends[m_DraggedBend] = this.WorldToLocal(evt.position);
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnBendPointerUp(PointerUpEvent evt, VisualElement handle)
        {
            if (!handle.HasPointerCapture(evt.pointerId)) return;
            handle.ReleasePointer(evt.pointerId);
            bool moved = m_DraggedBend >= 0;
            m_DraggedBend = -1;
            evt.StopPropagation();
            if (moved) BendsChanged?.Invoke(this);
        }

        private bool HasBends => m_Bends.Count > 0 && IsComplete;

        /// <summary>The path's corner points: start, a short stub out of a port, the bends, a stub into the port, end.</summary>
        private List<Vector2> PathPoints(Vector2 start, Vector2 end)
        {
            var points = new List<Vector2>(m_Bends.Count + 4) { start };
            if (!IsNodeAnchored) points.Add(start + StartOutward * PortStub);
            points.AddRange(m_Bends);
            if (!IsNodeAnchored) points.Add(end + EndOutward * PortStub);
            points.Add(end);
            return points;
        }

        private static Vector2 PointOnPolyline(List<Vector2> points, float t)
        {
            float total = 0f;
            for (int i = 0; i + 1 < points.Count; i++) total += Vector2.Distance(points[i], points[i + 1]);
            if (total < 0.001f) return points[0];
            float d = Mathf.Clamp01(t) * total;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                float length = Vector2.Distance(points[i], points[i + 1]);
                if (d <= length) return Vector2.Lerp(points[i], points[i + 1], length < 0.001f ? 1f : d / length);
                d -= length;
            }
            return points[points.Count - 1];
        }

        #endregion

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
            if (hasTarget && m_Bends.Count > 0)
            {
                // Leave towards the first bend, arrive from the last one.
                Vector2 outDir = m_Bends[0] - from.center;
                Vector2 inDir = target - m_Bends[m_Bends.Count - 1];
                if (outDir.sqrMagnitude < 0.01f || inDir.sqrMagnitude < 0.01f) return false;
                start = ExitPoint(from, from.center, outDir.normalized);
                end = ExitPoint(m_ToNode.layout, target, -inDir.normalized);
                return true;
            }

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
            if (HasBends) return PointOnPolyline(PathPoints(start, end), t);
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
            if (HasBends) DrawThroughBends(painter, PathPoints(start, end));
            else switch (Curve)
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

            for (int i = 0; i < m_BendHandles.Count && i < m_Bends.Count; i++)
            {
                var hs = m_BendHandles[i].style;
                hs.left = m_Bends[i].x - BendHandleSize / 2f;
                hs.top = m_Bends[i].y - BendHandleSize / 2f;
                hs.backgroundColor = color;
            }

            m_EdgeLabel.style.left = mid.x - (m_EdgeLabel.layout.width / 2f);
            m_EdgeLabel.style.top = mid.y - (m_EdgeLabel.layout.height / 2f) - (string.IsNullOrEmpty(Text) ? 0 : 12f);
            OnPathDrawn(start, end);
        }

        /// <summary>Straight runs between the points, with rounded corners at each inner point (the path is already at points[0]).</summary>
        private static void DrawThroughBends(Painter2D painter, List<Vector2> points)
        {
            for (int i = 1; i + 1 < points.Count; i++)
            {
                float room = Mathf.Min(Vector2.Distance(points[i - 1], points[i]), Vector2.Distance(points[i], points[i + 1])) * 0.5f;
                float radius = Mathf.Min(CornerRadius, room);
                if (radius < 0.5f) painter.LineTo(points[i]);
                else painter.ArcTo(points[i], points[i + 1], radius);
            }
            painter.LineTo(points[points.Count - 1]);
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
