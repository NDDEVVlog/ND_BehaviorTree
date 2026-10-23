using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue.Editor
{
    public static partial class DialogueUIBuilder
    {
        [MenuItem("GameObject/ND Dialogue/Dialogue Manager + UI (TextMeshPro)", false, 11)]
        private static void CreateTypewriterSetupFromMenu(MenuCommand command)
        {
            var manager = CreateTypewriterSetup(null);
            if (manager == null) return;
            Undo.RegisterCreatedObjectUndo(manager.gameObject, "Create Dialogue Manager (TextMeshPro)");
            Selection.activeObject = manager.gameObject;
        }

        /// <summary>A manager wired to a new Typewriter Dialogue UI; null (with an error) when TextMeshPro has no default font.</summary>
        public static DialogueManager CreateTypewriterSetup(DialogueDatabase database, string startConversation = null)
        {
            var ui = CreateTypewriterUI();
            if (ui == null) return null;
            var go = new GameObject("Dialogue Manager");
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = database;
            so.FindProperty("m_View").objectReferenceValue = ui;
            so.FindProperty("m_StartConversation").stringValue = startConversation ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        /// <summary>
        /// Canvas › Dialogue UI (TMP) › Panel › (Subtitle: portrait, speaker, typewriter line, continue) + (Responses),
        /// plus a Voice audio source. Needs TextMeshPro's default font (Window › TextMeshPro › Import TMP Essential Resources).
        /// </summary>
        public static TypewriterDialogueUI CreateTypewriterUI()
        {
            var font = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (font == null)
            {
                Debug.LogError("[DialogueUIBuilder] TextMeshPro has no default font asset: import it with Window › TextMeshPro › Import TMP Essential Resources, then build the UI again.");
                return null;
            }

            var canvasGo = new GameObject("Dialogue Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();

            var host = Rect("Dialogue UI (TMP)", canvasGo.transform);
            Stretch(host, Vector2.zero, Vector2.one);
            var ui = host.gameObject.AddComponent<TypewriterDialogueUI>();
            var voice = host.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            // Middleware players (e.g. FMOD, when its integration is on) first, the AudioClip player last.
            var voicePlayers = new System.Collections.Generic.List<DialogueVoicePlayer>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<DialogueVoicePlayer>())
                if (!type.IsAbstract && type != typeof(AudioSourceVoicePlayer) && !IsTestType(type)) voicePlayers.Add((DialogueVoicePlayer)host.gameObject.AddComponent(type));
            var clipPlayer = host.gameObject.AddComponent<AudioSourceVoicePlayer>();
            clipPlayer.Source = voice;
            voicePlayers.Add(clipPlayer);

            var panel = Rect("Panel", host);
            panel.anchorMin = new Vector2(0.08f, 0.03f);
            panel.anchorMax = new Vector2(0.92f, 0.36f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            var subtitle = Rect("Subtitle", panel);
            Stretch(subtitle, Vector2.zero, Vector2.one);
            var portraitRect = Rect("Portrait", subtitle);
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(24f, 0f);
            portraitRect.sizeDelta = new Vector2(220f, 220f);
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;

            var speaker = TmpLabel("Speaker", subtitle, font, 34, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            speaker.rectTransform.anchorMin = new Vector2(0f, 1f);
            speaker.rectTransform.anchorMax = new Vector2(1f, 1f);
            speaker.rectTransform.pivot = new Vector2(0f, 1f);
            speaker.rectTransform.offsetMin = new Vector2(268f, -70f);
            speaker.rectTransform.offsetMax = new Vector2(-24f, -18f);

            var line = TmpLabel("Line", subtitle, font, 30, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.offsetMin = new Vector2(268f, 24f);
            line.rectTransform.offsetMax = new Vector2(-150f, -80f);
            line.textWrappingMode = TextWrappingModes.Normal;
            var typewriter = line.gameObject.AddComponent<TypewriterText>();
            var typewriterSo = new SerializedObject(typewriter);
            typewriterSo.FindProperty("m_Text").objectReferenceValue = line;
            typewriterSo.ApplyModifiedPropertiesWithoutUndo();

            var continueButton = TmpButton("Continue", subtitle, font, "Continue ▶", 26);
            var crt = (RectTransform)continueButton.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(1f, 0f);
            crt.anchoredPosition = new Vector2(-20f, 20f);
            crt.sizeDelta = new Vector2(200f, 56f);

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
            var template = TmpButton("Response Template", responses, font, "Response", 26);
            ((RectTransform)template.transform).sizeDelta = new Vector2(0f, 54f);
            var templateLabel = template.GetComponentInChildren<TMP_Text>();
            templateLabel.alignment = TextAlignmentOptions.MidlineLeft;
            templateLabel.rectTransform.offsetMin = new Vector2(16f, 0f);
            template.gameObject.SetActive(false);
            responses.gameObject.SetActive(false);

            var so = new SerializedObject(ui);
            so.FindProperty("m_Root").objectReferenceValue = panel.gameObject;
            so.FindProperty("m_SubtitlePanel").objectReferenceValue = subtitle.gameObject;
            so.FindProperty("m_ResponsePanel").objectReferenceValue = responses.gameObject;
            so.FindProperty("m_Portrait").objectReferenceValue = portrait;
            so.FindProperty("m_SpeakerName").objectReferenceValue = speaker;
            so.FindProperty("m_Line").objectReferenceValue = typewriter;
            so.FindProperty("m_ContinueButton").objectReferenceValue = continueButton;
            so.FindProperty("m_ResponseTemplate").objectReferenceValue = template;
            var players = so.FindProperty("m_VoicePlayers");
            players.arraySize = voicePlayers.Count;
            for (int i = 0; i < voicePlayers.Count; i++) players.GetArrayElementAtIndex(i).objectReferenceValue = voicePlayers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return ui;
        }

        /// <summary>Test assemblies are loaded in the editor too: their fake players must never land in a real UI.</summary>
        private static bool IsTestType(System.Type type) =>
            System.Linq.Enumerable.Any(type.Assembly.GetReferencedAssemblies(), a => a.Name == "nunit.framework");

        private static TextMeshProUGUI TmpLabel(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button TmpButton(string name, Transform parent, TMP_FontAsset font, string caption, float size)
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
            var label = TmpLabel("Text", rect, font, size, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.text = caption;
            return button;
        }
    }
}
