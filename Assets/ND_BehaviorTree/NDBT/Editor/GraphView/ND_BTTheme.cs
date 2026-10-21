using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    [Serializable]
    public class NodeThemeEntry
    {
        public string nodeTypeName;
        public VisualTreeAsset customUXML;
        public StyleSheet customUSS;
        public Color nodeColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    }

    /// <summary>How an edge is routed from the parent's output to the child's input.</summary>
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
    /// Image: two pictures (e.g. Pop Cat) — <see cref="PortTheme.unpluggedImage"/> while unplugged, <see cref="PortTheme.pluggedImage"/> once connected.
    /// Custom: your own port — a <see cref="CustomGraph.ND_BTPortPainter"/> script and/or a UXML template (+ USS).
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
        [Tooltip("Shape = Custom: an ND_BTPortPainter script that draws the port (Create > ND_BehaviorTree > Port Painter).")]
        public UnityEditor.MonoScript customPainter;
        [Tooltip("Shape = Custom: UXML cloned into the port. The port carries nd-port--input/--output/--multi/--plugged/--hover/--candidate for your USS.")]
        public VisualTreeAsset customTemplate;
        [Tooltip("Shape = Custom: stylesheet added to the port (style the template by the nd-port--* state classes).")]
        public StyleSheet customStyle;

        public PortTheme Copy() => (PortTheme)MemberwiseClone();
    }

    /// <summary>
    /// Everything a theme changes: node stylesheet, canvas colours, edges and ports. Built-in themes live in
    /// <see cref="Theme.ND_BTThemeCatalog"/>; custom ones are <see cref="Theme.ND_BTThemePreset"/> assets.
    /// </summary>
    [Serializable]
    public class ThemeData
    {
        public string displayName = "Custom";
        [Tooltip("Node stylesheet (same classes as NodeElementUss.uss). Empty = keep the current one.")]
        public StyleSheet nodeStyle;
        [Tooltip("Decorator / service cards and key slots: overrides for ND_NodeAttachments.uss. Empty = the default look.")]
        public StyleSheet attachmentStyle;
        [Tooltip("Blackboard panel: overrides for ND_Blackboard.uss. Empty = the default look.")]
        public StyleSheet blackboardStyle;
        public Color graphBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        public Color gridColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public EdgeTheme edge = new EdgeTheme();
        public PortTheme port = new PortTheme();
    }

    [Serializable]
    public class GraphTheme
    {
        public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
        public Color gridColor = new Color(0.18f, 0.18f, 0.18f, 1.0f);
        public float gridSpacing = 25f;
    }

    [Serializable]
    public class BlackboardTheme
    {
        public VisualTreeAsset customUXML;
        public StyleSheet customUSS;
        public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    }
        [Serializable]
    public class ViewThemeData
    {
        public StyleSheet CustomStyle { get; set; }
        public VisualTreeAsset CustomUXML { get; set; }
        /// <summary>Port look for this node type (from its appearance pack mapping); null = the project's port theme.</summary>
        public PortTheme PortTheme { get; set; }
    }
}