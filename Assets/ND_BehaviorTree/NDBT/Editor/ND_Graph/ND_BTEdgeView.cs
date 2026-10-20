using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTEdgeView : VisualElement
    {
        public ND_BTPortView OutputPort { get; private set; }
        public ND_BTPortView InputPort { get; private set; }
        
        private Vector2 m_TempEndPosition;
        private Label m_EdgeLabel;
        private EdgeTheme m_Theme;
        private Graph.LiveState m_LiveState = Graph.LiveState.Inactive;

        private static readonly Color RunningColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color SuccessColor = new Color(0.35f, 0.9f, 0.35f, 1f);
        private static readonly Color FailureColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        /// <summary>Live debug: colours the edge by the child's state; running edges animate flowing dots.</summary>
        public void SetLiveState(Graph.LiveState state)
        {
            bool changed = state != m_LiveState;
            m_LiveState = state;
            if (changed || state == Graph.LiveState.Running) MarkDirtyRepaint();
        }

        public string Text
        {
            get => m_EdgeLabel.text;
            set
            {
                m_EdgeLabel.text = value;
                m_EdgeLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public ND_BTEdgeView(ND_BTPortView outputPort)
        {
            OutputPort = outputPort;
            m_Theme = ND_BehaviorTreeSetting.Instance.edgeTheme;
            
            style.position = Position.Absolute;
            style.top = style.left = style.right = style.bottom = 0;
            pickingMode = PickingMode.Ignore; 

            m_EdgeLabel = new Label("")
            {
                pickingMode = PickingMode.Position,
                style = {
                    position = Position.Absolute,
                    backgroundColor = m_Theme.labelBackgroundColor,
                    color = m_Theme.labelTextColor,
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
            m_EdgeLabel.AddManipulator(new ContextualMenuManipulator(evt => {
                evt.menu.AppendAction("Edit Text", a => Text = EditorInputDialog.Show("Edit Label", "Enter text:", Text));
            }));
        }

        private Label m_WeightLabel;
        private string m_StaticWeightText;
        private int m_LiveScoreKey = int.MinValue;

        /// <summary>Weight/chance pill near the child end (null hides it). Set by the canvas for weighted parents.</summary>
        public void SetWeightLabel(string text, string labelTooltip)
        {
            m_StaticWeightText = text;
            m_LiveScoreKey = int.MinValue;
            if (string.IsNullOrEmpty(text))
            {
                if (m_WeightLabel != null) m_WeightLabel.style.display = DisplayStyle.None;
                return;
            }
            EnsureWeightLabel();
            m_WeightLabel.style.display = DisplayStyle.Flex;
            m_WeightLabel.text = text;
            m_WeightLabel.tooltip = labelTooltip;
            m_WeightLabel.RemoveFromClassList("nd-edge-weight--chosen");
            MarkDirtyRepaint();
        }

        /// <summary>Live Utility score; the text is rebuilt only when the rounded score or the chosen state changes.</summary>
        public void SetLiveScore(float score, bool chosen)
        {
            if (m_WeightLabel == null) return;
            int key = float.IsNaN(score) ? int.MaxValue : Mathf.RoundToInt(score * 100f) * 2 + (chosen ? 1 : 0);
            if (key == m_LiveScoreKey) return;
            m_LiveScoreKey = key;
            m_WeightLabel.text = float.IsNaN(score) ? m_StaticWeightText : (chosen ? "▶ " : string.Empty) + score.ToString("0.##");
            m_WeightLabel.EnableInClassList("nd-edge-weight--chosen", chosen);
        }

        private void EnsureWeightLabel()
        {
            if (m_WeightLabel != null) return;
            m_WeightLabel = new Label { pickingMode = PickingMode.Position };
            m_WeightLabel.AddToClassList("nd-edge-weight");
            m_WeightLabel.style.position = Position.Absolute;
            ND_BTStyles.Apply(m_WeightLabel, ND_BTStyles.NodeAttachments);
            Add(m_WeightLabel);
        }

        public void SetInputPort(ND_BTPortView inputPort)
        {
            InputPort = inputPort;
            if (InputPort?.ParentNodeView != null) InputPort.ParentNodeView.OnNodeMoved += MarkDirtyRepaint;
            MarkDirtyRepaint();
        }

        public void UpdateTempPosition(Vector2 mousePosition)
        {
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas != null)
            {
                m_TempEndPosition = canvas.ContentContainer.WorldToLocal(mousePosition);
                MarkDirtyRepaint();
            }
        }


        // ------------------------------------------------------------------ path (shape from EdgeTheme.curve)

        /// <summary>Edge end points in canvas-content space.</summary>
        private bool TryGetEnds(out Vector2 start, out Vector2 end)
        {
            start = end = default;
            if (OutputPort == null) return false;
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return false;
            start = canvas.ContentContainer.WorldToLocal(OutputPort.GlobalCenter);
            end = InputPort != null ? canvas.ContentContainer.WorldToLocal(InputPort.GlobalCenter) : m_TempEndPosition;
            return true;
        }

        private static void BezierControls(Vector2 start, Vector2 end, out Vector2 p1, out Vector2 p2)
        {
            float dist = Mathf.Abs(start.y - end.y) * 0.5f + 25f;
            p1 = start + Vector2.up * dist;
            p2 = end - Vector2.up * dist;
        }

        /// <summary>Point at <paramref name="t"/> (0 = parent, 1 = child) along the drawn path.</summary>
        private Vector2 PointAt(float t, Vector2 start, Vector2 end)
        {
            switch (m_Theme.curve)
            {
                case EdgeCurve.Straight:
                    return Vector2.Lerp(start, end, t);
                case EdgeCurve.Step:
                {
                    float midY = (start.y + end.y) * 0.5f;
                    Vector2 a = new Vector2(start.x, midY), b = new Vector2(end.x, midY);
                    float l1 = Mathf.Abs(midY - start.y), l2 = Mathf.Abs(end.x - start.x), l3 = Mathf.Abs(end.y - midY);
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

        private Vector2 DirectionAt(float t, Vector2 start, Vector2 end)
        {
            Vector2 dir = (PointAt(Mathf.Min(1f, t + 0.02f), start, end) - PointAt(Mathf.Max(0f, t - 0.02f), start, end)).normalized;
            return dir == Vector2.zero ? Vector2.down : dir;
        }

        /// <summary>Approximate distance from <paramref name="contentPoint"/> (canvas-content space) to the drawn path.</summary>
        public float DistanceTo(Vector2 contentPoint)
        {
            if (InputPort == null || !TryGetEnds(out var start, out var end)) return float.MaxValue;
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
            Color color = m_LiveState switch
            {
                Graph.LiveState.Running => RunningColor,
                Graph.LiveState.Succeeded => SuccessColor,
                Graph.LiveState.Failed => FailureColor,
                _ => m_Theme.edgeColor
            };
            painter.strokeColor = color;
            painter.lineWidth = m_LiveState == Graph.LiveState.Inactive ? m_Theme.edgeThickness : m_Theme.edgeThickness * 1.7f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(start);
            switch (m_Theme.curve)
            {
                case EdgeCurve.Straight:
                    painter.LineTo(end);
                    break;
                case EdgeCurve.Step:
                {
                    float midY = (start.y + end.y) * 0.5f;
                    painter.LineTo(new Vector2(start.x, midY));
                    painter.LineTo(new Vector2(end.x, midY));
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
            if (m_Theme.showArrow) DrawArrow(painter, mid, DirectionAt(0.5f, start, end), color);

            if (m_LiveState == Graph.LiveState.Running)
            {
                // Three dots flowing from parent to child.
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
            if (m_WeightLabel != null && m_WeightLabel.resolvedStyle.display != DisplayStyle.None)
            {
                Vector2 at = PointAt(0.72f, start, end);
                m_WeightLabel.style.left = at.x - m_WeightLabel.layout.width / 2f;
                m_WeightLabel.style.top = at.y - m_WeightLabel.layout.height / 2f;
            }

            m_EdgeLabel.style.left = mid.x - (m_EdgeLabel.layout.width / 2f);
            m_EdgeLabel.style.top = mid.y - (m_EdgeLabel.layout.height / 2f) - (string.IsNullOrEmpty(Text) ? 0 : 12f);
        }

        private void DrawArrow(Painter2D painter, Vector2 center, Vector2 dir, Color color)
        {
            float size = 10f;
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
