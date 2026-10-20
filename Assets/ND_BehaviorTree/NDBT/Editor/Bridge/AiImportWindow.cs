using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Paste what an AI generated (ndbt-tree text, a tree JSON, or a tree_apply operation list) and import it into a
    /// tree. "Check" is a dry run with a preview; "Import" applies it as one undo step (Ctrl+Z in the graph window).
    /// Shown only while AI features are enabled and unlocked.
    /// </summary>
    public class AiImportWindow : EditorWindow
    {
        private BehaviorTree m_Tree;
        private TextField m_Input;
        private Label m_Status;
        private TextField m_Preview;

        public static void Open(BehaviorTree tree)
        {
            if (!AiAccess.UiUnlocked)
            {
                Debug.LogWarning("[AiImportWindow] Unlock AI Access first (Project Settings → ND Behavior Tree → AI Access).");
                return;
            }
            var window = GetWindow<AiImportWindow>(true, "Import from AI", true);
            window.m_Tree = tree;
            window.minSize = new Vector2(560, 480);
            window.Refresh();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 8;

            root.Add(new Label("Paste the AI's answer: ndbt-tree text, a tree JSON (as copied), or a list of tree_apply operations.")
                { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 4 } });
            root.Add(new Label("Text / tree JSON REPLACE the whole tree. An operation list is applied on top of it. Missing blackboard keys declared with 'key' lines are created.")
                { style = { whiteSpace = WhiteSpace.Normal, color = new Color(1f, 0.75f, 0.35f), marginBottom = 6 } });

            m_Input = new TextField { multiline = true, style = { flexGrow = 1, minHeight = 180 } };
            m_Input.Q("unity-text-input").style.unityTextAlign = TextAnchor.UpperLeft;
            m_Input.Q("unity-text-input").style.whiteSpace = WhiteSpace.Pre;
            root.Add(new ScrollView { style = { flexGrow = 2 } }.WithChild(m_Input));

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            buttons.Add(new Button(() => { m_Input.value = EditorGUIUtility.systemCopyBuffer; Check(); }) { text = "Paste Clipboard & Check" });
            buttons.Add(new Button(Check) { text = "Check (dry run)" });
            buttons.Add(new VisualElement { style = { flexGrow = 1 } });
            buttons.Add(new Button(DoImport) { text = "Import", style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(buttons);

            m_Status = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 6 } };
            root.Add(m_Status);
            m_Preview = new TextField { multiline = true, isReadOnly = true, style = { flexGrow = 1, minHeight = 100 } };
            m_Preview.Q("unity-text-input").style.whiteSpace = WhiteSpace.Pre;
            root.Add(new ScrollView { style = { flexGrow = 1 } }.WithChild(m_Preview));
            Refresh();
        }

        private void Refresh()
        {
            if (m_Status == null) return;
            titleContent = new GUIContent(m_Tree != null ? $"Import from AI → {m_Tree.name}" : "Import from AI");
            if (m_Tree == null) SetStatus("Open a Behavior Tree in the graph window first.", true);
        }

        private void SetStatus(string text, bool error)
        {
            m_Status.text = text;
            m_Status.style.color = error ? new Color(1f, 0.45f, 0.4f) : new Color(0.55f, 0.9f, 0.55f);
        }

        private bool Ready()
        {
            if (!AiAccess.UiUnlocked) { SetStatus("AI Access is locked. Unlock it in Project Settings → ND Behavior Tree → AI Access.", true); return false; }
            if (m_Tree == null) { SetStatus("No target tree.", true); return false; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SetStatus("Exit Play mode before importing.", true); return false; }
            return true;
        }

        private void Check()
        {
            if (!Ready()) return;
            var result = AiTreeText.Import(m_Tree, m_Input.value, dryRun: true);
            Show(result, dryRun: true);
        }

        private void DoImport()
        {
            if (!Ready()) return;
            if (!EditorUtility.DisplayDialog("Import from AI", $"Import into '{m_Tree.name}'?\nText or tree JSON replaces the whole tree (Ctrl+Z in the graph window reverts it).", "Import", "Cancel")) return;
            var result = AiTreeText.Import(m_Tree, m_Input.value, dryRun: false);
            Show(result, dryRun: false);
        }

        private void Show(Dictionary<string, object> result, bool dryRun)
        {
            if (result.TryGetValue("error", out var error))
            {
                SetStatus($"✗ {error}", true);
                m_Preview.value = string.Empty;
                return;
            }
            var issues = result.TryGetValue("issues", out var i) && i is List<object> list ? list.Cast<string>().ToList() : new List<string>();
            string keys = result.TryGetValue(dryRun ? "keysToCreate" : "keysCreated", out var k) && k is List<object> kl && kl.Count > 0
                ? $" Keys {(dryRun ? "to create" : "created")}: {string.Join(", ", kl)}." : string.Empty;
            int ops = result.TryGetValue("operations", out var o) && o is List<object> ol ? ol.Count : 0;
            SetStatus(dryRun ? $"✓ Valid: {ops} operation(s) would be applied.{keys} Nothing was changed." : $"✓ Imported ({ops} operation(s)).{keys}", false);
            string preview = result.TryGetValue("preview", out var p) ? p as string : null;
            m_Preview.value = (issues.Count > 0 ? "Validation:\n  " + string.Join("\n  ", issues) + "\n\n" : string.Empty) + (preview ?? string.Empty);
        }
    }

    internal static class VisualElementExtensions
    {
        public static VisualElement WithChild(this VisualElement parent, VisualElement child)
        {
            parent.Add(child);
            return parent;
        }
    }
}
