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

        private void DrawEdge(MeshGenerationContext ctx)
        {
            if (OutputPort == null) return;
            var canvas = GetFirstAncestorOfType<ND_BTGraphCanvas>();
            if (canvas == null) return;

            Vector2 start = canvas.ContentContainer.WorldToLocal(OutputPort.GlobalCenter);
            Vector2 end = InputPort != null ? canvas.ContentContainer.WorldToLocal(InputPort.GlobalCenter) : m_TempEndPosition;

            var painter = ctx.painter2D;
            painter.strokeColor = m_Theme.edgeColor;
            painter.lineWidth = m_Theme.edgeThickness;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            float dist = Mathf.Abs(start.y - end.y) * 0.5f + 25f;
            Vector2 p1 = start + Vector2.up * dist;
            Vector2 p2 = end - Vector2.up * dist;

            painter.BeginPath();
            painter.MoveTo(start);
            painter.BezierCurveTo(p1, p2, end);
            painter.Stroke();

            Vector2 mid = 0.125f * start + 0.375f * p1 + 0.375f * p2 + 0.125f * end;
            Vector2 dir = (0.75f * (p1 - start) + 1.5f * (p2 - p1) + 0.75f * (end - p2)).normalized;
            if (dir == Vector2.zero) dir = Vector2.down;

            DrawArrow(painter, mid, dir);
            m_EdgeLabel.style.left = mid.x - (m_EdgeLabel.layout.width / 2f);
            m_EdgeLabel.style.top = mid.y - (m_EdgeLabel.layout.height / 2f) - (string.IsNullOrEmpty(Text) ? 0 : 12f);
        }

        private void DrawArrow(Painter2D painter, Vector2 center, Vector2 dir)
        {
            float size = 10f;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 tip = center + dir * (size * 0.6f);
            Vector2 bMid = center - dir * (size * 0.6f);
            
            painter.fillColor = m_Theme.edgeColor;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(bMid + perp * (size * 0.6f));
            painter.LineTo(bMid - perp * (size * 0.6f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}