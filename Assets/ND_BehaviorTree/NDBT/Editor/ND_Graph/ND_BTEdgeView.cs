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

        /// <summary>Bezier control points in canvas-content space (same curve as drawn).</summary>
        private bool TryGetCurve(out Vector2 start, out Vector2 p1, out Vector2 p2, out Vector2 end)
        {
            start = p1 = p2 = end = default;
            if (OutputPort == null) return false;
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return false;

            start = canvas.ContentContainer.WorldToLocal(OutputPort.GlobalCenter);
            end = InputPort != null ? canvas.ContentContainer.WorldToLocal(InputPort.GlobalCenter) : m_TempEndPosition;
            float dist = Mathf.Abs(start.y - end.y) * 0.5f + 25f;
            p1 = start + Vector2.up * dist;
            p2 = end - Vector2.up * dist;
            return true;
        }

        /// <summary>Approximate distance from <paramref name="contentPoint"/> (canvas-content space) to the drawn curve.</summary>
        public float DistanceTo(Vector2 contentPoint)
        {
            if (InputPort == null || !TryGetCurve(out var start, out var p1, out var p2, out var end)) return float.MaxValue;
            const int Segments = 24;
            float best = float.MaxValue;
            Vector2 previous = start;
            for (int i = 1; i <= Segments; i++)
            {
                float t = i / (float)Segments, u = 1f - t;
                Vector2 point = u * u * u * start + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * end;
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
            if (!TryGetCurve(out var start, out var p1, out var p2, out var end)) return;

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
            painter.BezierCurveTo(p1, p2, end);
            painter.Stroke();

            Vector2 mid = 0.125f * start + 0.375f * p1 + 0.375f * p2 + 0.125f * end;
            Vector2 dir = (0.75f * (p1 - start) + 1.5f * (p2 - p1) + 0.75f * (end - p2)).normalized;
            if (dir == Vector2.zero) dir = Vector2.down;

            DrawArrow(painter, mid, dir, color);

            if (m_LiveState == Graph.LiveState.Running)
            {
                // Three dots flowing from parent to child.
                float phase = (float)(EditorApplication.timeSinceStartup * 0.8 % 1.0);
                painter.fillColor = Color.white;
                for (int i = 0; i < 3; i++)
                {
                    float t = (phase + i / 3f) % 1f;
                    float u = 1f - t;
                    Vector2 p = u * u * u * start + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * end;
                    painter.BeginPath();
                    painter.Arc(p, 3.5f, 0f, 360f);
                    painter.Fill();
                }
            }
            if (m_WeightLabel != null && m_WeightLabel.resolvedStyle.display != DisplayStyle.None)
            {
                const float t = 0.72f;
                float u = 1f - t;
                Vector2 at = u * u * u * start + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * end;
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