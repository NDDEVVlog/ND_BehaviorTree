using ND.GraphUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Editor
{
    /// <summary>
    /// Port of the GOAP Planner "Card" (GoapAppearancePack): a target reticle. Hollow centre while unplugged, a filled
    /// "goal locked" centre once connected, crosshair ticks while hovered, lighter with a white rim while a dragged
    /// connection can drop on it.
    /// </summary>
    public class GoapPlannerPortPainter : PortPainter
    {
        private static readonly Color Hollow = new Color(0.11f, 0.12f, 0.15f, 1f);

        public override void Draw(Painter2D p, Rect rect, in PortState state)
        {
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.42f;
            Color color = state.IsCandidate ? Color.Lerp(state.Color, Color.white, 0.4f) : state.Color;

            // Outer ring
            p.fillColor = Hollow;
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Fill();
            p.strokeColor = state.IsCandidate ? Color.white : color;
            p.lineWidth = Mathf.Max(1.5f, r * 0.22f);
            p.Stroke();

            // Centre: hollow dot while waiting, filled once a child / parent is connected
            p.BeginPath();
            p.Arc(c, r * 0.38f, 0f, 360f);
            if (state.IsPlugged)
            {
                p.fillColor = color;
                p.Fill();
            }
            else
            {
                p.strokeColor = color;
                p.lineWidth = Mathf.Max(1f, r * 0.14f);
                p.Stroke();
            }

            // Crosshair ticks while hovered
            if (!state.IsHover) return;
            p.strokeColor = color;
            p.lineWidth = Mathf.Max(1f, r * 0.14f);
            p.lineCap = LineCap.Round;
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = i == 0 ? Vector2.up : i == 1 ? Vector2.down : i == 2 ? Vector2.left : Vector2.right;
                p.BeginPath();
                p.MoveTo(c + dir * r * 1.05f);
                p.LineTo(c + dir * r * 1.4f);
                p.Stroke();
            }
        }
    }
}
