using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Quests
{
    /// <summary>
    /// Quest journal window (uGUI), opened and closed with a key (J by default) or <see cref="Toggle"/>: quests grouped
    /// by state (Active, Completed, Failed), and the selected one's title, state, summary and steps, with a button to
    /// show / hide it in the tracker. Inactive quests are not listed. Build one with GameObject › ND Quests › Quest Log.
    /// </summary>
    [AddComponentMenu("ND Quests/Quest Log UI")]
    public class QuestLogUI : MonoBehaviour
    {
        private const string LogTag = "[QuestLogUI]";

        [SerializeField] private QuestManager m_Manager;
        [Tooltip("The window (shown / hidden).")]
        [SerializeField] private GameObject m_Root;
        [SerializeField] private KeyCode m_ToggleKey = KeyCode.J;
        [Header("List")]
        [SerializeField] private RectTransform m_List;
        [Tooltip("Inactive template for a section header (Active / Completed / Failed).")]
        [SerializeField] private Graphic m_HeaderTemplate;
        [Tooltip("Inactive template for a quest button (its child text gets the title).")]
        [SerializeField] private Button m_QuestTemplate;
        [Header("Details")]
        [SerializeField] private Graphic m_Title;
        [SerializeField] private Graphic m_State;
        [SerializeField] private Graphic m_Summary;
        [SerializeField] private Graphic m_Steps;
        [SerializeField] private Button m_TrackButton;
        [SerializeField] private Graphic m_TrackLabel;
        [SerializeField] private Button m_CloseButton;

        private QuestRuntime m_Log;
        private string m_Selected;
        private readonly List<GameObject> m_Rows = new List<GameObject>();

        public bool IsOpen => m_Root != null && m_Root.activeSelf;
        public string Selected => m_Selected;

        private void Awake()
        {
            if (m_TrackButton != null) m_TrackButton.onClick.AddListener(ToggleTracked);
            if (m_CloseButton != null) m_CloseButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (m_Manager == null)
            {
                Debug.LogError($"{LogTag} '{name}': no Quest Manager assigned.", this);
                return;
            }
            if (!m_Manager.Initialize())
            {
                Debug.LogWarning($"{LogTag} '{name}': the manager is not ready (state={m_Manager.State}).", this);
                return;
            }
            m_Log = m_Manager.Runtime;
            m_Log.Changed += OnLogChanged;
        }

        private void OnDisable()
        {
            if (m_Log != null) m_Log.Changed -= OnLogChanged;
            m_Log = null;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (m_ToggleKey != KeyCode.None && Input.GetKeyDown(m_ToggleKey)) Toggle();
#endif
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (m_Root == null) return;
            m_Root.SetActive(true);
            Render();
        }

        public void Close()
        {
            if (m_Root != null) m_Root.SetActive(false);
        }

        public void Select(string questName)
        {
            m_Selected = questName;
            Render();
        }

        private void OnLogChanged()
        {
            if (IsOpen) Render();
        }

        private void ToggleTracked()
        {
            if (m_Log == null || string.IsNullOrEmpty(m_Selected)) return;
            m_Log.Track(m_Selected, !m_Log.IsTracked(m_Selected));
        }

        /// <summary>Rebuilds the list (by state) and the selected quest's details.</summary>
        public void Render()
        {
            foreach (var row in m_Rows) if (row != null) Destroy(row);
            m_Rows.Clear();
            if (m_Log == null) return;

            var listed = m_Log.Quests.Where(q => q.State != QuestState.Inactive).ToList();
            if (listed.All(q => q.Name != m_Selected)) m_Selected = listed.FirstOrDefault(q => q.State == QuestState.Active)?.Name ?? listed.FirstOrDefault()?.Name;
            Section("Active", listed.Where(q => q.State == QuestState.Active));
            Section("Completed", listed.Where(q => q.State == QuestState.Completed));
            Section("Failed", listed.Where(q => q.State == QuestState.Failed || q.State == QuestState.Abandoned));

            var selected = listed.FirstOrDefault(q => q.Name == m_Selected);
            UIText.Set(m_Title, selected != null ? selected.Title : listed.Count == 0 ? "No quests yet" : string.Empty);
            UIText.Set(m_State, selected != null ? $"{selected.Group} · {selected.State}" : string.Empty);
            UIText.Set(m_Summary, selected?.Summary ?? string.Empty);
            var steps = new StringBuilder();
            if (selected != null)
                foreach (var step in selected.Steps)
                    steps.AppendLine(step.Done ? $"<color=#8A8F99>✓ {step.Text}</color>" : $"• {step.Text}");
            UIText.Set(m_Steps, steps.ToString());
            if (m_TrackButton != null) m_TrackButton.gameObject.SetActive(selected != null && selected.State == QuestState.Active);
            UIText.Set(m_TrackLabel, selected != null && selected.IsTracked ? "Untrack" : "Track");
        }

        private void Section(string title, IEnumerable<QuestInfo> quests)
        {
            var list = quests.ToList();
            if (list.Count == 0 || m_List == null || m_HeaderTemplate == null || m_QuestTemplate == null) return;
            var header = Instantiate(m_HeaderTemplate.gameObject, m_List);
            header.SetActive(true);
            UIText.Set(header.GetComponent<Graphic>(), title);
            m_Rows.Add(header);
            foreach (var quest in list)
            {
                var button = Instantiate(m_QuestTemplate, m_List);
                button.gameObject.SetActive(true);
                string questName = quest.Name;
                button.onClick.AddListener(() => Select(questName));
                var label = FindLabel(button);
                UIText.Set(label, (quest.Name == m_Selected ? "▶ " : string.Empty) + quest.Title + (quest.IsTracked ? "  ◉" : string.Empty));
                m_Rows.Add(button.gameObject);
            }
        }

        private static Graphic FindLabel(Button button)
        {
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != button.gameObject) return graphic;
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Quest Manager.", this);
            if (m_Root == null || m_List == null || m_QuestTemplate == null) Debug.LogWarning($"{LogTag} '{name}': assign the window, the list and the templates.", this);
        }
#endif
    }
}
