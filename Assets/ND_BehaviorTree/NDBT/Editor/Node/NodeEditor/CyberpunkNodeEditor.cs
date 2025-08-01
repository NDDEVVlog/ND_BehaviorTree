using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor.Experimental.GraphView;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// A hyper-stylized editor for the Cyberpunk "NetRunner" theme.
    /// It applies glitch effects, neon colors, and scanlines.
    /// </summary>
    public class CyberpunkNodeEditor : ND_NodeEditor
    {
        public CyberpunkNodeEditor(Node node, UnityEditor.SerializedObject BTObject, GraphView graphView)
            : base(node, BTObject, graphView)
        {
            // 1. Clear any default Unity styles that might interfere.
            this.styleSheets.Clear();

            // 2. Load our custom Cyberpunk stylesheet.
            // (You should create a setting for this path, but for now we hardcode it).
            string themePath = ND_BehaviorTreeSetting.Instance.GetStyleSheetPath("CyberPunk");
            StyleSheet cyberpunkStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(themePath);
            
            if (cyberpunkStyleSheet != null)
            {
                this.styleSheets.Add(cyberpunkStyleSheet);
            }
            else
            {
                Debug.LogWarning($"Could not load Cyberpunk stylesheet at: {themePath}");
            }

            // 3. Add the core classes to this node to activate the styles.
            this.AddToClassList("cyberpunk-node");

            // Optional: Differentiate node types within the theme
            if (m_Node is GOAP.GOAPActionNode || m_Node is GOAP.GOAPPlannerNode)
            {
                this.AddToClassList("corp-node"); // "Corporate" node style
            }
            else
            {
                this.AddToClassList("rogue-node"); // "Rogue AI" node style
            }
        }
    }
}