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

    [Serializable]
    public class EdgeTheme
    {
        public Color edgeColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        public float edgeThickness = 3.0f;
        public Color labelBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        public Color labelTextColor = Color.white;
    }

    [Serializable]
    public class PortTheme
    {
        public Color inputPortColor = new Color(0.4f, 0.8f, 0.4f, 1.0f);
        public Color outputPortColor = new Color(0.8f, 0.6f, 0.1f, 1.0f);
        public float portSize = 14f;
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
    }
}