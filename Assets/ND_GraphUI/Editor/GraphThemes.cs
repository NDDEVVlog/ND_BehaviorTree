using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>How an edge is routed from an output port to an input port.</summary>
    public enum EdgeCurve { Bezier, Straight, Step }

    [Serializable]
    public class EdgeTheme
    {
        public Color edgeColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        public float edgeThickness = 3.0f;
        public Color labelBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        public Color labelTextColor = Color.white;
        [Tooltip("Bezier: smooth S-curve · Straight: direct line · Step: right-angled (circuit style).")]
        public EdgeCurve curve = EdgeCurve.Bezier;
        [Tooltip("Draw the direction arrow in the middle of the edge.")]
        public bool showArrow = true;
        [Tooltip("Draw edges behind nodes and ports (the port sits on top of the line end), instead of over them.")]
        public bool drawBehindNodes;

        public EdgeTheme Copy() => (EdgeTheme)MemberwiseClone();
    }

    /// <summary>
    /// Port look. Cat: a drawn cat head that opens its mouth while unplugged and closes it once connected.
    /// Image: two pictures — <see cref="PortTheme.unpluggedImage"/> while unplugged, <see cref="PortTheme.pluggedImage"/> once connected.
    /// Custom: your own port — a <see cref="PortPainter"/> script and/or a UXML template (+ USS).
    /// </summary>
    public enum PortShape { Circle, Square, Diamond, Cat, Image, Custom }

    [Serializable]
    public class PortTheme
    {
        public Color inputPortColor = new Color(0.4f, 0.8f, 0.4f, 1.0f);
        public Color outputPortColor = new Color(0.8f, 0.6f, 0.1f, 1.0f);
        public float portSize = 14f;
        public PortShape shape = PortShape.Circle;
        [Tooltip("Shape = Image: picture while the port is NOT connected (transparent background recommended).")]
        public Texture2D unpluggedImage;
        [Tooltip("Shape = Image: picture once the port IS connected.")]
        public Texture2D pluggedImage;
        [Tooltip("Shape = Custom: a PortPainter script that draws the port.")]
        public UnityEditor.MonoScript customPainter;
        [Tooltip("Shape = Custom: UXML cloned into the port. The port carries nd-port--input/--output/--multi/--plugged/--hover/--candidate for your USS.")]
        public VisualTreeAsset customTemplate;
        [Tooltip("Shape = Custom: stylesheet added to the port (style the template by the nd-port--* state classes).")]
        public StyleSheet customStyle;

        public PortTheme Copy() => (PortTheme)MemberwiseClone();
    }
}
