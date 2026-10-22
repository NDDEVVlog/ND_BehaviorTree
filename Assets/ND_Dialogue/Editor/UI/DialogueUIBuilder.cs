using System.Collections.Generic;
using System.Linq;
using ND.Quests;
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
    public static partial class DialogueUIBuilder
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

        [MenuItem("GameObject/ND Dialogue/Quest Tracker", false, 12)]
        private static void CreateTrackerFromMenu(MenuCommand command)
        {
            var manager = SceneManagerOrLog("Quest Tracker");
            if (manager == null) return;
            var tracker = CreateQuestTracker(CanvasOf(manager), manager);
            Undo.RegisterCreatedObjectUndo(tracker.gameObject, "Create Quest Tracker");
            Selection.activeObject = tracker.gameObject;
        }

        [MenuItem("GameObject/ND Dialogue/Quest Log", false, 13)]
        private static void CreateLogFromMenu(MenuCommand command)
        {
            var manager = SceneManagerOrLog("Quest Log");
            if (manager == null) return;
            var log = CreateQuestLog(CanvasOf(manager), manager);
            Undo.RegisterCreatedObjectUndo(log.gameObject, "Create Quest Log");
            Selection.activeObject = log.gameObject;
        }

        [MenuItem("GameObject/ND Dialogue/Add Barker (bark bubble) to selected", false, 14)]
        private static void CreateBarkFromMenu(MenuCommand command)
        {
            var target = command.context as GameObject ?? Selection.activeGameObject;
            if (target == null)
            {
                Debug.LogError("[Dialogue UI Builder] Select the character that barks first: the bubble is made above it.");
                return;
            }
            var manager = SceneManagerOrLog("Dialogue Barker");
            if (manager == null) return;
            var barker = CreateBark(target, manager, null);
            Selection.activeObject = barker.gameObject;
        }

        /// <summary>
        /// Adds a <see cref="DialogueBarker"/> to <paramref name="character"/>, wired to <paramref name="manager"/> and to the
        /// character's speech bubble (a world-space <see cref="BarkUI"/>; made the first time). Run it again for another kind
        /// of bark (idle mutter, greeting, after a talk…): every barker of the character shares the bubble. Undoable.
        /// </summary>
        public static DialogueBarker CreateBark(GameObject character, DialogueManager manager, string conversation, float height = 2.2f)
        {
            var ui = character.GetComponentInChildren<BarkUI>(true);
            if (ui == null) ui = CreateBarkBubble(character, height);
            var barker = Undo.AddComponent<DialogueBarker>(character);
            var so = new SerializedObject(barker);
            so.FindProperty("m_Manager").objectReferenceValue = manager;
            so.FindProperty("m_UI").objectReferenceValue = ui;
            if (!string.IsNullOrEmpty(conversation)) so.FindProperty("m_Conversation").stringValue = conversation;
            so.ApplyModifiedProperties();
            return barker;
        }

        private static BarkUI CreateBarkBubble(GameObject character, float height)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Bark Bubble", typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Bark Bubble");
            canvasGo.transform.SetParent(character.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(420f, 120f);
            canvasRect.localPosition = new Vector3(0f, height, 0f);
            canvasRect.localScale = Vector3.one * 0.01f;

            var bubble = Rect("Bubble", canvasRect);
            Stretch(bubble, Vector2.zero, Vector2.one);
            bubble.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
            var speaker = Label("Speaker", bubble, font, 22, FontStyle.Bold, TextAnchor.UpperLeft);
            speaker.color = new Color(0.25f, 0.3f, 0.45f);
            Place(speaker.rectTransform, 6f, 28f);
            speaker.rectTransform.offsetMin = new Vector2(14f, speaker.rectTransform.offsetMin.y);
            var text = Label("Text", bubble, font, 28, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.color = new Color(0.08f, 0.08f, 0.1f);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(14f, 6f);
            text.rectTransform.offsetMax = new Vector2(-14f, -32f);

            var ui = canvasGo.AddComponent<BarkUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("m_Root").objectReferenceValue = bubble.gameObject;
            uiSo.FindProperty("m_Text").objectReferenceValue = text;
            uiSo.FindProperty("m_Speaker").objectReferenceValue = speaker;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
            bubble.gameObject.SetActive(false);
            return ui;
        }

        private static DialogueManager SceneManagerOrLog(string what)
        {
            var manager = Object.FindAnyObjectByType<DialogueManager>();
            if (manager == null) Debug.LogError($"[Dialogue UI Builder] Add a Dialogue Manager to the scene first (GameObject › ND Dialogue › Dialogue Manager + UI): the {what} reads its quest log.");
            return manager;
        }

        /// <summary>The canvas of the manager's view, or a new overlay canvas.</summary>
        public static Transform CanvasOf(DialogueManager manager)
        {
            if (manager != null && manager.View != null) return manager.View.transform.root;
            var canvasGo = new GameObject("Quest Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            EnsureEventSystem();
            return canvasGo.transform;
        }

        /// <summary>
        /// The dialogue manager's Quest Manager. When it has none, one is made (with the database's Quest Databases) and
        /// assigned, so dialogue, the quest UI and other systems share the same quests. Undoable.
        /// </summary>
        public static QuestManager EnsureQuestManager(DialogueManager manager)
        {
            if (manager.QuestManager != null) return manager.QuestManager;
            var go = new GameObject("Quest Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Quest Manager");
            var quests = go.AddComponent<QuestManager>();
            var questsSo = new SerializedObject(quests);
            var databases = questsSo.FindProperty("m_Databases");
            var referenced = manager.Database != null ? manager.Database.questDatabases.Where(d => d != null).ToList() : new List<QuestDatabase>();
            databases.arraySize = referenced.Count;
            for (int i = 0; i < referenced.Count; i++) databases.GetArrayElementAtIndex(i).objectReferenceValue = referenced[i];
            questsSo.ApplyModifiedPropertiesWithoutUndo();
            var so = new SerializedObject(manager);
            so.FindProperty("m_QuestManager").objectReferenceValue = quests;
            so.ApplyModifiedProperties();
            return quests;
        }

        /// <summary>Top-right tracker (tracked quests and their steps) plus a toast, on the dialogue manager's Quest Manager (made if missing).</summary>
        public static QuestTrackerUI CreateQuestTracker(Transform canvas, DialogueManager manager)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var host = Rect("Quest Tracker", canvas);
            host.anchorMin = host.anchorMax = new Vector2(1f, 1f);
            host.pivot = new Vector2(1f, 1f);
            host.anchoredPosition = new Vector2(-24f, -24f);
            host.sizeDelta = new Vector2(460f, 400f);

            var list = Rect("List", host);
            Stretch(list, Vector2.zero, Vector2.one);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = Label("Title Template", list, font, 26, FontStyle.Bold, TextAnchor.UpperRight);
            title.color = new Color(1f, 0.85f, 0.45f);
            title.gameObject.AddComponent<Shadow>();
            title.gameObject.SetActive(false);
            var step = Label("Step Template", list, font, 22, FontStyle.Normal, TextAnchor.UpperRight);
            step.gameObject.AddComponent<Shadow>();
            step.gameObject.SetActive(false);

            var toast = Rect("Toast", canvas);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.anchoredPosition = new Vector2(0f, -40f);
            toast.sizeDelta = new Vector2(640f, 64f);
            toast.gameObject.AddComponent<Image>().color = PanelColor;
            var toastText = Label("Text", toast, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform, Vector2.zero, Vector2.one);
            toastText.color = new Color(1f, 0.85f, 0.45f);
            toast.gameObject.SetActive(false);

            var tracker = host.gameObject.AddComponent<QuestTrackerUI>();
            var so = new SerializedObject(tracker);
            so.FindProperty("m_Manager").objectReferenceValue = EnsureQuestManager(manager);
            so.FindProperty("m_List").objectReferenceValue = list;
            so.FindProperty("m_TitleTemplate").objectReferenceValue = title;
            so.FindProperty("m_StepTemplate").objectReferenceValue = step;
            so.FindProperty("m_ToastRoot").objectReferenceValue = toast.gameObject;
            so.FindProperty("m_ToastText").objectReferenceValue = toastText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tracker;
        }

        /// <summary>A quest journal window (hidden; J toggles it): list by state on the left, details on the right; on the dialogue manager's Quest Manager (made if missing).</summary>
        public static QuestLogUI CreateQuestLog(Transform canvas, DialogueManager manager)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var host = Rect("Quest Log", canvas);
            Stretch(host, Vector2.zero, Vector2.one);

            var window = Rect("Window", host);
            window.anchorMin = new Vector2(0.15f, 0.12f);
            window.anchorMax = new Vector2(0.85f, 0.9f);
            window.offsetMin = window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<Image>().color = PanelColor;

            var heading = Label("Heading", window, font, 34, FontStyle.Bold, TextAnchor.UpperLeft);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0f, 1f);
            heading.rectTransform.offsetMin = new Vector2(28f, -70f);
            heading.rectTransform.offsetMax = new Vector2(-28f, -18f);
            heading.text = "Quests  <size=20><color=#8A8F99>(J)</color></size>";

            var close = Button("Close", window, font, "✕", 26);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-16f, -16f);
            crt.sizeDelta = new Vector2(52f, 52f);

            var list = Rect("List", window);
            list.anchorMin = new Vector2(0f, 0f);
            list.anchorMax = new Vector2(0.38f, 1f);
            list.offsetMin = new Vector2(24f, 24f);
            list.offsetMax = new Vector2(-12f, -86f);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var header = Label("Header Template", list, font, 22, FontStyle.Bold, TextAnchor.LowerLeft);
            header.color = new Color(0.55f, 0.6f, 0.68f);
            header.rectTransform.sizeDelta = new Vector2(0f, 34f);
            header.gameObject.SetActive(false);
            var quest = Button("Quest Template", list, font, "Quest", 24);
            ((RectTransform)quest.transform).sizeDelta = new Vector2(0f, 48f);
            var questLabel = quest.GetComponentInChildren<Text>();
            questLabel.alignment = TextAnchor.MiddleLeft;
            questLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            quest.gameObject.SetActive(false);

            var details = Rect("Details", window);
            details.anchorMin = new Vector2(0.38f, 0f);
            details.anchorMax = new Vector2(1f, 1f);
            details.offsetMin = new Vector2(12f, 24f);
            details.offsetMax = new Vector2(-24f, -86f);
            var title = Label("Title", details, font, 32, FontStyle.Bold, TextAnchor.UpperLeft);
            Place(title.rectTransform, 0f, 44f);
            title.color = new Color(1f, 0.85f, 0.45f);
            var state = Label("State", details, font, 22, FontStyle.Italic, TextAnchor.UpperLeft);
            Place(state.rectTransform, 48f, 30f);
            state.color = new Color(0.6f, 0.65f, 0.72f);
            var summary = Label("Summary", details, font, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            Place(summary.rectTransform, 86f, 150f);
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            var steps = Label("Steps", details, font, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            Place(steps.rectTransform, 246f, 300f);
            steps.horizontalOverflow = HorizontalWrapMode.Wrap;
            var track = Button("Track", details, font, "Track", 24);
            var trt = (RectTransform)track.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(1f, 0f);
            trt.sizeDelta = new Vector2(180f, 52f);

            var ui = host.gameObject.AddComponent<QuestLogUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("m_Manager").objectReferenceValue = EnsureQuestManager(manager);
            so.FindProperty("m_Root").objectReferenceValue = window.gameObject;
            so.FindProperty("m_List").objectReferenceValue = list;
            so.FindProperty("m_HeaderTemplate").objectReferenceValue = header;
            so.FindProperty("m_QuestTemplate").objectReferenceValue = quest;
            so.FindProperty("m_Title").objectReferenceValue = title;
            so.FindProperty("m_State").objectReferenceValue = state;
            so.FindProperty("m_Summary").objectReferenceValue = summary;
            so.FindProperty("m_Steps").objectReferenceValue = steps;
            so.FindProperty("m_TrackButton").objectReferenceValue = track;
            so.FindProperty("m_TrackLabel").objectReferenceValue = track.GetComponentInChildren<Text>();
            so.FindProperty("m_CloseButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
            return ui;
        }

        /// <summary>A full-width row at <paramref name="top"/> px from the top, <paramref name="height"/> px tall.</summary>
        private static void Place(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(0f, -(top + height));
            rect.offsetMax = new Vector2(0f, -top);
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
