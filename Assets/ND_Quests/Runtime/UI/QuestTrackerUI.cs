using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Quests
{
    /// <summary>
    /// On-screen quest tracker (uGUI): the tracked Active quests with their visible steps (✓ when done), and a short
    /// toast when a quest starts, completes or fails. Reads the manager's <see cref="QuestRuntime"/> and redraws when it
    /// changes. Build one with GameObject › ND Quests › Quest Tracker.
    /// </summary>
    [AddComponentMenu("ND Quests/Quest Tracker UI")]
    public class QuestTrackerUI : MonoBehaviour
    {
        private const string LogTag = "[QuestTrackerUI]";

        [SerializeField] private QuestManager m_Manager;
        [Tooltip("Rows are created under this (a vertical layout).")]
        [SerializeField] private RectTransform m_List;
        [Tooltip("Inactive template for a quest title row.")]
        [SerializeField] private Graphic m_TitleTemplate;
        [Tooltip("Inactive template for a step row.")]
        [SerializeField] private Graphic m_StepTemplate;
        [Header("Toast")]
        [SerializeField] private GameObject m_ToastRoot;
        [SerializeField] private Graphic m_ToastText;
        [SerializeField] private float m_ToastSeconds = 3f;

        private QuestRuntime m_Log;
        private readonly List<GameObject> m_Rows = new List<GameObject>();
        private readonly Queue<string> m_Toasts = new Queue<string>();
        private float m_ToastUntil;

        public QuestManager Manager { get => m_Manager; set => m_Manager = value; }

        private void OnEnable()
        {
            if (m_Manager == null)
            {
                Debug.LogError($"{LogTag} '{name}': no Quest Manager assigned.", this);
                return;
            }
            if (!m_Manager.Initialize())
            {
                Debug.LogWarning($"{LogTag} '{name}': the manager is not ready (state={m_Manager.State}); the tracker stays empty.", this);
                return;
            }
            m_Log = m_Manager.Runtime;
            m_Log.Changed += Render;
            m_Log.StateChanged += OnQuestStateChanged;
            if (m_ToastRoot != null) m_ToastRoot.SetActive(false);
            Render();
        }

        private void OnDisable()
        {
            if (m_Log == null) return;
            m_Log.Changed -= Render;
            m_Log.StateChanged -= OnQuestStateChanged;
            m_Log = null;
        }

        /// <summary>Rebuilds the rows: each tracked Active quest, then its visible steps.</summary>
        public void Render()
        {
            foreach (var row in m_Rows) if (row != null) Destroy(row);
            m_Rows.Clear();
            if (m_Log == null || m_List == null || m_TitleTemplate == null || m_StepTemplate == null) return;
            foreach (var quest in m_Log.Quests)
            {
                if (!quest.IsTracked || quest.State != QuestState.Active) continue;
                AddRow(m_TitleTemplate, quest.Goal > 0 ? $"{quest.Title}  ({quest.ProgressText})" : quest.Title);
                foreach (var step in quest.Steps)
                    AddRow(m_StepTemplate, step.Done ? $"<color=#8A8F99>✓ {step.Text}</color>" : $"• {step.Text}");
            }
        }

        private void AddRow(Graphic template, string text)
        {
            var row = Instantiate(template.gameObject, m_List);
            row.SetActive(true);
            QuestUIText.Set(row.GetComponent<Graphic>(), text);
            m_Rows.Add(row);
        }

        private void OnQuestStateChanged(QuestInfo quest, QuestState from, QuestState to)
        {
            if (m_Log.IsApplyingSave) return; // loading is not "quest started"
            string text = to switch
            {
                QuestState.Active => $"Quest started: {quest.Title}",
                QuestState.Completed => $"Quest completed: {quest.Title}",
                QuestState.Failed => $"Quest failed: {quest.Title}",
                QuestState.Abandoned => $"Quest abandoned: {quest.Title}",
                _ => null,
            };
            if (text != null) m_Toasts.Enqueue(text);
        }

        private void Update()
        {
            if (m_ToastRoot == null) return;
            if (m_ToastRoot.activeSelf && Time.unscaledTime < m_ToastUntil) return;
            if (m_Toasts.Count == 0)
            {
                if (m_ToastRoot.activeSelf) m_ToastRoot.SetActive(false);
                return;
            }
            QuestUIText.Set(m_ToastText, m_Toasts.Dequeue());
            m_ToastRoot.SetActive(true);
            m_ToastUntil = Time.unscaledTime + m_ToastSeconds;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Quest Manager.", this);
            if (m_List == null || m_TitleTemplate == null || m_StepTemplate == null) Debug.LogWarning($"{LogTag} '{name}': assign the list and the row templates.", this);
        }
#endif
    }
}
