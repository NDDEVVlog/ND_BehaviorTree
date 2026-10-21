using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Builds the Standard Dialogue UI hierarchy (uGUI, legacy Text so it works without TextMeshPro resources):
    /// Canvas › Dialogue UI › Panel › (Subtitle: portrait, speaker, line, continue) + (Responses: vertical list + template).
    /// Every reference of <see cref="StandardDialogueUI"/> is wired. Restyle freely afterwards.
    /// </summary>
    public static class DialogueUIBuilder
    {
        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.3f, 1f);

        [MenuItem("GameObject/ND Dialogue/Dialogue UI", false, 10)]
        private static void CreateFromMenu(MenuCommand command)
        {
            var ui = CreateStandardUI();
            GameObjectUtility.SetParentAndAlign(ui.transform.root.gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(ui.transform.root.gameObject, "Create Dialogue UI");
            Selection.activeObject = ui.gameObject;
        }

        [MenuItem("GameObject/ND Dialogue/Dialogue Manager + UI", false, 11)]
        private static void CreateSetupFromMenu(MenuCommand command)
        {
            var manager = CreateSetup(null);
            Undo.RegisterCreatedObjectUndo(manager.gameObject, "Create Dialogue Manager");
            Selection.activeObject = manager.gameObject;
        }

        /// <summary>A manager wired to a new Standard Dialogue UI (and an EventSystem if the scene has none).</summary>
        public static DialogueManager CreateSetup(DialogueDatabase database, string startConversation = null)
        {
            var ui = CreateStandardUI();
            var go = new GameObject("Dialogue Manager");
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = database;
            so.FindProperty("m_View").objectReferenceValue = ui;
            so.FindProperty("m_StartConversation").stringValue = startConversation ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        public static StandardDialogueUI CreateStandardUI()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Dialogue Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();

            // Always-active host (Update/input live here); the Panel child is what shows and hides.
            var host = Rect("Dialogue UI", canvasGo.transform);
            Stretch(host, Vector2.zero, Vector2.one);
            var ui = host.gameObject.AddComponent<StandardDialogueUI>();

            var panel = Rect("Panel", host);
            panel.anchorMin = new Vector2(0.08f, 0.03f);
            panel.anchorMax = new Vector2(0.92f, 0.36f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            // Subtitle
            var subtitle = Rect("Subtitle", panel);
            Stretch(subtitle, Vector2.zero, Vector2.one);
            var portraitRect = Rect("Portrait", subtitle);
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(24f, 0f);
            portraitRect.sizeDelta = new Vector2(220f, 220f);
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;

            var speaker = Label("Speaker", subtitle, font, 34, FontStyle.Bold, TextAnchor.UpperLeft);
            speaker.rectTransform.anchorMin = new Vector2(0f, 1f);
            speaker.rectTransform.anchorMax = new Vector2(1f, 1f);
            speaker.rectTransform.pivot = new Vector2(0f, 1f);
            speaker.rectTransform.offsetMin = new Vector2(268f, -70f);
            speaker.rectTransform.offsetMax = new Vector2(-24f, -18f);

            var line = Label("Line", subtitle, font, 30, FontStyle.Normal, TextAnchor.UpperLeft);
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.offsetMin = new Vector2(268f, 24f);
            line.rectTransform.offsetMax = new Vector2(-150f, -80f);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;

            var continueButton = Button("Continue", subtitle, font, "Continue ▶", 26);
            var crt = (RectTransform)continueButton.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(1f, 0f);
            crt.anchoredPosition = new Vector2(-20f, 20f);
            crt.sizeDelta = new Vector2(200f, 56f);

            // Responses: a vertical list on the right half, above the line text
            var responses = Rect("Responses", panel);
            responses.anchorMin = new Vector2(0.45f, 0.06f);
            responses.anchorMax = new Vector2(0.98f, 0.94f);
            responses.offsetMin = responses.offsetMax = Vector2.zero;
            var layout = responses.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var template = Button("Response Template", responses, font, "Response", 26);
            ((RectTransform)template.transform).sizeDelta = new Vector2(0f, 54f);
            template.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            template.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(16f, 0f);
            template.gameObject.SetActive(false);
            responses.gameObject.SetActive(false);

            var so = new SerializedObject(ui);
            so.FindProperty("m_Root").objectReferenceValue = panel.gameObject;
            so.FindProperty("m_SubtitlePanel").objectReferenceValue = subtitle.gameObject;
            so.FindProperty("m_ResponsePanel").objectReferenceValue = responses.gameObject;
            so.FindProperty("m_Portrait").objectReferenceValue = portrait;
            so.FindProperty("m_SpeakerName").objectReferenceValue = speaker;
            so.FindProperty("m_LineText").objectReferenceValue = line;
            so.FindProperty("m_ContinueButton").objectReferenceValue = continueButton;
            so.FindProperty("m_ResponseTemplate").objectReferenceValue = template;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false); // hidden until a conversation starts
            return ui;
        }

        /// <summary>
        /// A "E — Talk to …" prompt at the bottom centre of <paramref name="canvas"/>, hidden until an interactor shows it.
        /// Returns the root (to show/hide) and its text.
        /// </summary>
        public static (GameObject root, Text text) CreatePrompt(Transform canvas)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = Rect("Interaction Prompt", canvas);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.42f);
            root.sizeDelta = new Vector2(520f, 60f);
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var text = Label("Text", root, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.text = "E — Talk";
            root.gameObject.SetActive(false);
            return (root.gameObject, text);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text Label(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor anchor)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, Font font, string caption, int size)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.85f, 1f);
            colors.selectedColor = new Color(0.75f, 0.85f, 1f);
            button.colors = colors;
            var label = Label("Text", rect, font, size, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.text = caption;
            return button;
        }
    }
}
