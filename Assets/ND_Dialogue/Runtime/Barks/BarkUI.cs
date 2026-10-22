using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue
{
    /// <summary>
    /// A speech bubble over a character (a world-space canvas): shows a bark's lines one after another, each for a time
    /// that grows with its length, then hides. Nothing waits for it and it takes no input.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Bark UI")]
    public class BarkUI : MonoBehaviour
    {
        private const string LogTag = "[BarkUI]";

        [Tooltip("Shown while a bark is on screen (the bubble).")]
        [SerializeField] private GameObject m_Root;
        [Tooltip("The line (UI.Text or TextMeshPro).")]
        [SerializeField] private Graphic m_Text;
        [Tooltip("Optional: the speaker's name.")]
        [SerializeField] private Graphic m_Speaker;
        [SerializeField] private float m_MinSeconds = 1.5f;
        [SerializeField] private float m_SecondsPerCharacter = 0.05f;
        [Tooltip("Turn the bubble to face the main camera every frame (3D scenes).")]
        [SerializeField] private bool m_FaceCamera = true;

        private readonly List<DialogueLine> m_Lines = new List<DialogueLine>();
        private int m_Index = -1;
        private float m_HideAt;
        private Camera m_Camera;

        public bool IsShowing => m_Index >= 0;
        /// <summary>The line on screen (default when nothing is shown).</summary>
        public DialogueLine CurrentLine => IsShowing ? m_Lines[m_Index] : default;

        private void Awake()
        {
            if (m_Root == null || m_Text == null)
            {
                Debug.LogError($"{LogTag} '{name}': assign the bubble root and its text.", this);
                return;
            }
            m_Root.SetActive(false);
        }

        /// <summary>Shows <paramref name="lines"/> in turn (replacing a bark on screen). An empty list does nothing.</summary>
        public void Show(IReadOnlyList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0) return;
            if (m_Root == null || m_Text == null)
            {
                Debug.LogError($"{LogTag} '{name}': {nameof(Show)} rejected: the bubble root or text is not assigned.", this);
                return;
            }
            m_Lines.Clear();
            m_Lines.AddRange(lines);
            ShowLine(0);
        }

        public void Hide()
        {
            m_Index = -1;
            m_Lines.Clear();
            if (m_Root != null) m_Root.SetActive(false);
        }

        /// <summary>How long a line stays: the minimum, or longer for long lines.</summary>
        public float DurationOf(string text) => Mathf.Max(m_MinSeconds, (text?.Length ?? 0) * m_SecondsPerCharacter);

        /// <summary>Moves on as if <paramref name="seconds"/> had passed (Update calls it; tests too).</summary>
        public void Tick(float seconds)
        {
            if (!IsShowing) return;
            m_HideAt -= seconds;
            if (m_HideAt > 0f) return;
            if (m_Index + 1 < m_Lines.Count) ShowLine(m_Index + 1);
            else Hide();
        }

        private void ShowLine(int index)
        {
            m_Index = index;
            var line = m_Lines[index];
            UIText.Set(m_Text, line.Text);
            if (m_Speaker != null) UIText.Set(m_Speaker, line.SpeakerName);
            m_HideAt = DurationOf(line.Text);
            m_Root.SetActive(true);
        }

        private void Update() => Tick(Time.deltaTime);

        private void LateUpdate()
        {
            if (!m_FaceCamera || !IsShowing) return;
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera != null) transform.rotation = m_Camera.transform.rotation;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Root == null) Debug.LogWarning($"{LogTag} '{name}': assign the bubble root (shown while a bark is on screen).", this);
            if (m_Text == null) Debug.LogWarning($"{LogTag} '{name}': assign the text of the bubble.", this);
        }
#endif
    }
}
