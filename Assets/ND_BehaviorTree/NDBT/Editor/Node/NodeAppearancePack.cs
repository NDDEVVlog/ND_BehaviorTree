using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A self-contained set of node looks: mappings (node script → UXML/USS key or custom view) plus the stylesheets
    /// and UXML those keys name (keys are local to the pack). Every pack in the project is picked up automatically
    /// (see <see cref="NodeAppearanceRegistry"/>); <see cref="NodeEditorConfig"/> turns packs on or off.
    /// </summary>
    [CreateAssetMenu(fileName = "NodeAppearancePack", menuName = "ND_BehaviorTree/Node Appearance Pack")]
    public class NodeAppearancePack : ScriptableObject
    {
        [Tooltip("When packs map the same node type, the higher priority wins (a more specific node type always wins first).")]
        public int priority;

        [Tooltip("Optional: the ND_BTPlugin script this pack belongs to. The pack is used only while that plugin is installed.")]
        public MonoScript ownerPlugin;

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
