using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.Dialogue
{
    /// <summary>
    /// Dialogue UI with TextMeshPro: the line is typed by a <see cref="TypewriterText"/> (markup: pauses, speed, wave,
    /// shake, err/fix, events), the speaker's name and portrait follow the emotion, and choices are buttons (or keys 1–9).
    /// <para>Voice: a line's clip plays on its own source; the typing can last as long as the clip, and the line can
    /// move on by itself when the voice ends. Continue (button, Space, Enter) first completes the typing, then moves on.</para>
    /// Build one with GameObject › ND Dialogue › Dialogue Manager + UI (TextMeshPro).
    /// </summary>
    [AddComponentMenu("ND Dialogue/Typewriter Dialogue UI (TextMeshPro)")]
    public class TypewriterDialogueUI : DialogueViewBase
    {
        private const string LogTag = "[TypewriterDialogueUI]";

        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject m_SubtitlePanel;
        [SerializeField] private GameObject m_ResponsePanel;
        [SerializeField] private Image m_Portrait;
        [SerializeField] private TMP_Text m_SpeakerName;
        [SerializeField] private TypewriterText m_Line;
        [SerializeField] private Button m_ContinueButton;
        [Tooltip("Inactive template for a choice button (its child TMP text gets the label).")]
        [SerializeField] private Button m_ResponseTemplate;
        [Tooltip("Seconds to answer (0 = no limit); then the Timeout Choice is taken.")]
        [SerializeField, Min(0f)] private float m_ResponseTimeout;
        [SerializeField, Min(0)] private int m_TimeoutChoice;
        [SerializeField] private bool m_KeyboardInput = true;

        [Header("Voice")]
        [Tooltip("Plays each line's voice clip (empty = voice is not played).")]
        [SerializeField] private AudioSource m_VoiceSource;
        [Tooltip("Typing lasts as long as the voice clip.")]
        [SerializeField] private bool m_TypeWithVoice = true;
        [Tooltip("Move on by itself when the voice ends (choices still wait for the player).")]
        [SerializeField] private bool m_ContinueAfterVoice;
        [Tooltip("Seconds after the voice before moving on by itself.")]
        [SerializeField, Min(0f)] private float m_ContinueDelay = 0.6f;

        private readonly List<Button> m_ResponseButtons = new List<Button>();
        private float m_ResponseDeadline = float.PositiveInfinity;
        private float m_AutoContinueAt = float.PositiveInfinity;

        public TypewriterText Line => m_Line;
        public bool IsTyping => m_Line != null && m_Line.IsPlaying;

        private void Awake()
        {
            if (m_ContinueButton != null) m_ContinueButton.onClick.AddListener(Continue);
            if (m_ResponseTemplate != null) m_ResponseTemplate.gameObject.SetActive(false);
            if (m_Line != null) m_Line.EventReached += OnLineEvent;
            if (AttachedRunner == null) SetVisible(false); // first activation may come from Attach itself
        }

        private void OnDestroy()
        {
            if (m_Line != null) m_Line.EventReached -= OnLineEvent;
        }

        protected override void OnAttached() => SetVisible(true);

        protected override void OnDetached()
        {
            ClearResponses();
            if (m_Line != null) m_Line.Clear();
            if (m_VoiceSource != null) m_VoiceSource.Stop();
            m_AutoContinueAt = float.PositiveInfinity;
            SetVisible(false);
        }

        protected override void OnLine(DialogueLine line)
        {
            ClearResponses();
            Show(m_SubtitlePanel, true);
            Show(m_ResponsePanel, false);
            Color tint = line.Emotion != null ? line.Emotion.ResolveTint() : Color.white;
            if (m_SpeakerName != null)
            {
                m_SpeakerName.text = line.SpeakerName;
                m_SpeakerName.color = tint;
            }
            if (m_Portrait != null)
            {
                var sprite = line.Emotion?.ResolvePortrait();
                m_Portrait.sprite = sprite;
                m_Portrait.color = tint;
                m_Portrait.gameObject.SetActive(sprite != null);
            }

            float voiceSeconds = 0f;
            if (m_VoiceSource != null)
            {
                m_VoiceSource.Stop();
                if (line.Voice != null)
                {
                    m_VoiceSource.clip = line.Voice;
                    m_VoiceSource.Play();
                    voiceSeconds = line.Voice.length;
                }
            }
            if (m_Line != null) m_Line.Play(line.Text, m_TypeWithVoice ? voiceSeconds : 0f);
            else Debug.LogError($"{LogTag} '{name}': no Typewriter Text assigned: the line is not shown.", this);
            m_AutoContinueAt = m_ContinueAfterVoice && voiceSeconds > 0f ? Time.unscaledTime + voiceSeconds + m_ContinueDelay : float.PositiveInfinity;
            if (m_ContinueButton != null) m_ContinueButton.gameObject.SetActive(true);
        }

        protected override void OnChoices(IReadOnlyList<DialogueChoice> choices)
        {
            if (m_Line != null) m_Line.Complete(); // the question stays readable above the menu
            m_AutoContinueAt = float.PositiveInfinity;
            if (m_ContinueButton != null) m_ContinueButton.gameObject.SetActive(false);
            Show(m_ResponsePanel, true);
            ClearResponses();
            if (m_ResponseTemplate == null)
            {
                Debug.LogError($"{LogTag} '{name}': no Response Template assigned; cannot show {choices.Count} choice(s).", this);
                return;
            }
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var button = Instantiate(m_ResponseTemplate, m_ResponseTemplate.transform.parent);
                button.name = $"Response {i + 1}";
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = (m_KeyboardInput && i < 9 ? $"{i + 1}. " : string.Empty) + LineTimeline.Final(choice.Text);
                else Debug.LogError($"{LogTag} '{name}': the Response Template has no TextMeshPro text child.", this);
                int index = choice.Index;
                button.onClick.AddListener(() => Choose(index));
                m_ResponseButtons.Add(button);
            }
            if (m_ResponseButtons.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(m_ResponseButtons[0].gameObject);
            m_ResponseDeadline = m_ResponseTimeout > 0f ? Time.unscaledTime + m_ResponseTimeout : float.PositiveInfinity;
        }

        /// <summary>Completes the typing line, or moves on when the line is complete.</summary>
        public void Continue()
        {
            if (AttachedRunner == null) return;
            if (IsTyping)
            {
                m_Line.Complete();
                return;
            }
            if (AttachedRunner.State == DialogueRunnerState.ShowingLine)
            {
                m_AutoContinueAt = float.PositiveInfinity;
                AttachedRunner.Continue();
            }
        }

        public void Choose(int index)
        {
            if (AttachedRunner == null || AttachedRunner.State != DialogueRunnerState.WaitingForChoice) return;
            ClearResponses();
            AttachedRunner.Choose(index);
        }

        /// <summary>An &lt;event&gt; in the line: sent to the scene like a RaiseEvent action (DialogueEvents).</summary>
        private void OnLineEvent(LineEvent e) => DialogueEvents.Raise(e.Name, e.Argument);

        private void Update()
        {
            if (AttachedRunner == null) return;
            if (AttachedRunner.State == DialogueRunnerState.ShowingLine && !IsTyping && Time.unscaledTime >= m_AutoContinueAt)
            {
                m_AutoContinueAt = float.PositiveInfinity;
                AttachedRunner.Continue();
                return;
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

        private void ClearResponses()
        {
            foreach (var button in m_ResponseButtons) if (button != null) Destroy(button.gameObject);
            m_ResponseButtons.Clear();
            m_ResponseDeadline = float.PositiveInfinity;
        }

        private void SetVisible(bool visible)
        {
            Show(m_Root, visible);
            if (!visible) return;
            Show(m_SubtitlePanel, false);
            Show(m_ResponsePanel, false);
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Line == null) Debug.LogWarning($"{LogTag} '{name}': assign the Typewriter Text of the line.", this);
            if (m_ResponseTemplate == null) Debug.LogWarning($"{LogTag} '{name}': assign the choice button template.", this);
        }
#endif
    }
}
