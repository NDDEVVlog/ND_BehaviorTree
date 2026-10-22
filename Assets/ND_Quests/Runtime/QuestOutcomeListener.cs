using UnityEngine;
using UnityEngine.Events;

namespace ND.Quests
{
    /// <summary>
    /// Runs scene reactions when a quest starts, is completed or fails (open a door, give a reward, play a sound),
    /// without code. Nothing is raised while a save is applied (loading is not "the quest started").
    /// </summary>
    [AddComponentMenu("ND Quests/Quest Outcome Listener")]
    public class QuestOutcomeListener : MonoBehaviour
    {
        private const string LogTag = "[QuestOutcomeListener]";

        [SerializeField] private QuestManager m_Manager;
        [Tooltip("The quest id.")]
        [SerializeField] private string m_Quest;
        [SerializeField] private UnityEvent m_OnStarted = new UnityEvent();
        [SerializeField] private UnityEvent m_OnCompleted = new UnityEvent();
        [SerializeField] private UnityEvent m_OnFailed = new UnityEvent();

        private QuestRuntime m_Runtime;

        public QuestManager Manager { get => m_Manager; set => m_Manager = value; }
        public string Quest { get => m_Quest; set => m_Quest = value; }
        public UnityEvent OnStarted => m_OnStarted;
        public UnityEvent OnCompleted => m_OnCompleted;
        public UnityEvent OnFailed => m_OnFailed;

        private void OnEnable()
        {
            if (m_Manager == null || string.IsNullOrEmpty(m_Quest))
            {
                Debug.LogError($"{LogTag} '{name}': assign the Quest Manager and the quest (manager={(m_Manager != null)}, quest='{m_Quest}').", this);
                return;
            }
            if (!m_Manager.Initialize()) return;
            m_Runtime = m_Manager.Runtime;
            m_Runtime.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (m_Runtime == null) return;
            m_Runtime.StateChanged -= OnStateChanged;
            m_Runtime = null;
        }

        private void OnStateChanged(QuestInfo quest, QuestState from, QuestState to)
        {
            if (quest == null || quest.Name != m_Quest || m_Runtime.IsApplyingSave) return;
            switch (to)
            {
                case QuestState.Active: m_OnStarted.Invoke(); break;
                case QuestState.Completed: m_OnCompleted.Invoke(); break;
                case QuestState.Failed: m_OnFailed.Invoke(); break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Manager == null) Debug.LogWarning($"{LogTag} '{name}': assign the Quest Manager.", this);
            if (string.IsNullOrEmpty(m_Quest)) Debug.LogWarning($"{LogTag} '{name}': choose the quest.", this);
        }
#endif
    }
}
