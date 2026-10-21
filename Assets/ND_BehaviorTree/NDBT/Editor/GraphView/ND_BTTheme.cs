using System;
using ND.GraphUI;
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