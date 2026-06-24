using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    [CreateAssetMenu(fileName = "NodeEditorConfig", menuName = "ND_BehaviorTree/Node Editor Config")]
    public class NodeEditorConfig : ScriptableObject
    {
        public List<NodeEditorMapping> mappings = new List<NodeEditorMapping>();

        [SerializeField]
        private List<StyleSheetEntry> _styleSheets = new List<StyleSheetEntry>();

        [SerializeField]
        private List<UxmlEntry> _uxmlAssets = new List<UxmlEntry>();

        public StyleSheet GetStyleSheet(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var entry in _styleSheets)
            {
                if (entry.styleKey == key && entry.styleSheet != null) return entry.styleSheet;
            }
            return null;
        }

        public VisualTreeAsset GetUXML(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var entry in _uxmlAssets)
            {
                if (entry.uxmlKey == key && entry.uxmlAsset != null) return entry.uxmlAsset;
            }
            return null;
        }
    }
}