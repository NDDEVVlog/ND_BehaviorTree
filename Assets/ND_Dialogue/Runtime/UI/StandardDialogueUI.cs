using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.Dialogue
{
    /// <summary>
    /// Ready-made dialogue UI (uGUI): subtitle panel (portrait, speaker name tinted by emotion, typewriter text,
    /// continue button) and a response menu built from a button template.
    /// <list type="bullet">
    /// <item>Continue: the button, Space/Enter, or a click while text is typing (shows the whole line first).</item>
    /// <item>Choices: click, or keys 1–9. Optional timeout picks <see cref="m_TimeoutChoice"/>.</item>
    /// <item>Text fields accept any Graphic with a string <c>text</c> property (UI.Text, TextMeshPro).</item>
    /// </list>
    /// Build one with GameObject → ND Dialogue → Dialogue UI.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Standard Dialogue UI")]
    public class StandardDialogueUI : DialogueViewBase
    {
        [Header("Panels")]
        [Tooltip("Shown while a conversation runs. Use a CHILD of this object (this component must stay active).")]
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject m_SubtitlePanel;
        [SerializeField] private GameObject m_ResponsePanel;

        [Header("Subtitle")]
        [SerializeField] private Image m_Portrait;
        [Tooltip("UI.Text or TextMeshPro.")]
        [SerializeField] private Graphic m_SpeakerName;
        [Tooltip("UI.Text or TextMeshPro.")]
        [SerializeField] private Graphic m_LineText;
        [SerializeField] private Button m_ContinueButton;

        [Header("Responses")]
        [Tooltip("Inactive button used as the template; each choice gets a copy under the same parent.")]
        [SerializeField] private Button m_ResponseTemplate;
        [Tooltip("Seconds before a choice is made for the player (0 = wait forever).")]
        [SerializeField] private float m_ResponseTimeout;
        [Tooltip("Index picked when the timeout runs out.")]
        [SerializeField] private int m_TimeoutChoice;

        [Header("Typewriter")]
        [Tooltip("Characters per second (0 = show the whole line at once).")]
        [SerializeField] private float m_CharactersPerSecond = 45f;
        [SerializeField] private bool m_KeyboardInput = true;

        private readonly List<Button> m_ResponseButtons = new List<Button>();
        private string m_FullText = string.Empty;
        private float m_Typed;
        private int m_TypedVisible = -1; // characters shown so far: the text is rebuilt only when it grows
        private bool m_Typing;
        private float m_ResponseDeadline;

        public bool IsTyping => m_Typing;

        private void Awake()
        {
            if (m_ContinueButton != null) m_ContinueButton.onClick.AddListener(Continue);
            if (m_ResponseTemplate != null) m_ResponseTemplate.gameObject.SetActive(false);
            if (AttachedRunner == null) SetVisible(false); // first activation may come from Attach itself
        }

        #region DialogueViewBase

        protected override void OnAttached() => SetVisible(true);

        protected override void OnDetached()
        {
            ClearResponses();
            m_Typing = false;
            SetVisible(false);
        }

        protected override void OnLine(DialogueLine line)
        {
            ClearResponses();
            Show(m_SubtitlePanel, true);
            Show(m_ResponsePanel, false);

            Color tint = line.Emotion != null ? line.Emotion.ResolveTint() : Color.white;
            SetText(m_SpeakerName, line.SpeakerName);
            if (m_SpeakerName != null) m_SpeakerName.color = tint;

            if (m_Portrait != null)
            {
                var sprite = line.Emotion?.ResolvePortrait();
                m_Portrait.sprite = sprite;
                m_Portrait.color = tint;
                m_Portrait.gameObject.SetActive(sprite != null);
            }

            m_FullText = LineTimeline.Final(line.Text); // markup → rich text (the TMP UI animates it)
            m_Typed = 0f;
            m_TypedVisible = -1;
            m_Typing = m_CharactersPerSecond > 0f && m_FullText.Length > 0;
            SetText(m_LineText, m_Typing ? string.Empty : m_FullText);
            if (m_ContinueButton != null) m_ContinueButton.gameObject.SetActive(true);
        }

        protected override void OnChoices(IReadOnlyList<DialogueChoice> choices)
        {
            m_Typing = false;
            SetText(m_LineText, m_FullText); // keep the question readable above the menu
            if (m_ContinueButton != null) m_ContinueButton.gameObject.SetActive(false);
            Show(m_ResponsePanel, true);
            ClearResponses();
            if (m_ResponseTemplate == null)
            {
                Debug.LogError($"[StandardDialogueUI] '{name}': no Response Template assigned; cannot show {choices.Count} choice(s).", this);
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var button = Instantiate(m_ResponseTemplate, m_ResponseTemplate.transform.parent);
                button.name = $"Response {i + 1}";
                button.gameObject.SetActive(true);
                var label = FindLabel(button);
                SetText(label, m_KeyboardInput && i < 9 ? $"{i + 1}. {choice.Text}" : choice.Text);
                int index = choice.Index;
                button.onClick.AddListener(() => Choose(index));
                m_ResponseButtons.Add(button);
            }
            if (m_ResponseButtons.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(m_ResponseButtons[0].gameObject);
            m_ResponseDeadline = m_ResponseTimeout > 0f ? Time.unscaledTime + m_ResponseTimeout : float.PositiveInfinity;
        }

        #endregion

        /// <summary>Finishes the typing line, or moves on when the line is complete.</summary>
        public void Continue()
        {
            if (AttachedRunner == null) return;
            if (m_Typing)
            {
                m_Typing = false;
                SetText(m_LineText, m_FullText);
                return;
            }
            if (AttachedRunner.State == DialogueRunnerState.ShowingLine) AttachedRunner.Continue();
        }

        public void Choose(int index)
        {
            if (AttachedRunner == null || AttachedRunner.State != DialogueRunnerState.WaitingForChoice) return;
            ClearResponses();
            AttachedRunner.Choose(index);
        }

        private void Update()
        {
            if (AttachedRunner == null) return;

            if (m_Typing)
            {
                m_Typed += Time.unscaledDeltaTime * m_CharactersPerSecond;
                int visible = Mathf.FloorToInt(m_Typed);
                if (visible != m_TypedVisible) // no new character: no new string, no text rebuild
                {
                    m_TypedVisible = visible;
                    SetText(m_LineText, TypewriterSlice(m_FullText, visible, out bool complete));
                    if (complete) m_Typing = false;
                }
            }

            if (AttachedRunner.State == DialogueRunnerState.WaitingForChoice && Time.unscaledTime >= m_ResponseDeadline)
            {
                m_ResponseDeadline = float.PositiveInfinity;
                int count = AttachedRunner.CurrentChoices.Count;
                if (count > 0) Choose(Mathf.Clamp(m_TimeoutChoice, 0, count - 1));
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!m_KeyboardInput) return;
            if (AttachedRunner.State == DialogueRunnerState.ShowingLine &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Continue();
            else if (AttachedRunner.State == DialogueRunnerState.WaitingForChoice)
            {
                for (int i = 0; i < 9 && i < AttachedRunner.CurrentChoices.Count; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Choose(i); break; }
            }
#endif
        }

        private static readonly char[] TagNameEnd = { '=', ' ', '>' };

        /// <summary>
        /// The first <paramref name="visibleChars"/> visible characters of <paramref name="text"/>: rich-text tags
        /// (&lt;b&gt;, &lt;color=…&gt;) are kept whole and do not count, and tags still open are closed so they never
        /// show as raw text mid-typing.
        /// </summary>
        public static string TypewriterSlice(string text, int visibleChars, out bool complete)
        {
            var sb = new System.Text.StringBuilder(text.Length + 16);
            var open = new Stack<string>();
            int visible = 0, i = 0;
            for (; i < text.Length && visible < visibleChars; i++)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        string tag = text.Substring(i, close - i + 1);
                        sb.Append(tag);
                        if (tag.StartsWith("</", StringComparison.Ordinal)) { if (open.Count > 0) open.Pop(); }
                        else if (!tag.EndsWith("/>", StringComparison.Ordinal))
                        {
                            int end = tag.IndexOfAny(TagNameEnd, 1);
                            open.Push(tag.Substring(1, end - 1));
                        }
                        i = close;
                        continue;
                    }
                }
                sb.Append(text[i]);
                visible++;
            }
            // Trailing closing tags belong to the visible text.
            while (i < text.Length && text[i] == '<' && i + 1 < text.Length && text[i + 1] == '/')
            {
                int close = text.IndexOf('>', i);
                if (close < 0) break;
                sb.Append(text, i, close - i + 1);
                if (open.Count > 0) open.Pop();
                i = close + 1;
            }
            complete = i >= text.Length;
            while (open.Count > 0) sb.Append("</").Append(open.Pop()).Append('>');
            return sb.ToString();
        }

        private void ClearResponses()
        {
            foreach (var button in m_ResponseButtons)
            {
                if (button == null) continue;
                if (Application.isPlaying) Destroy(button.gameObject);
                else DestroyImmediate(button.gameObject); // edit-mode previews and tests
            }
            m_ResponseButtons.Clear();
        }

        private void SetVisible(bool visible)
        {
            Show(m_Root != null ? m_Root : gameObject, visible);
            if (!visible) return;
            Show(m_SubtitlePanel, true);
            Show(m_ResponsePanel, false);
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

        private static Graphic FindLabel(Button button)
        {
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                if (graphic != button.targetGraphic && UIText.TextProperty(graphic) != null) return graphic;
            return null;
        }

        /// <summary>Sets the text of a UI.Text / TMP_Text (see <see cref="UIText"/>).</summary>
        protected void SetText(Graphic graphic, string text) => UIText.Set(graphic, text);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_LineText == null) Debug.LogWarning($"[StandardDialogueUI] '{name}': assign the Line Text.", this);
            if (m_ResponseTemplate == null) Debug.LogWarning($"[StandardDialogueUI] '{name}': assign a Response Template button.", this);
        }
#endif
    }
}
