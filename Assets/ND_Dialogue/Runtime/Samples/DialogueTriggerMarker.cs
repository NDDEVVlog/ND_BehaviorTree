using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// Sample-only: shows <see cref="m_Visual"/> (the character standing there, a "!" over its head…) only while its
    /// <see cref="DialogueTrigger"/> can fire, so the map shows where the story continues. Checks a few times a second.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Samples/Dialogue Trigger Marker")]
    public class DialogueTriggerMarker : MonoBehaviour
    {
        private const string LogTag = "[DialogueTriggerMarker]";
        private const float CheckInterval = 0.25f;

        [SerializeField] private DialogueTrigger m_Trigger;
        [Tooltip("Shown while the trigger can fire (a child object: this one must stay active to keep checking).")]
        [SerializeField] private GameObject m_Visual;

        private float m_NextCheck;

        public DialogueTrigger Trigger { get => m_Trigger; set => m_Trigger = value; }
        public GameObject Visual { get => m_Visual; set => m_Visual = value; }

        private void Update()
        {
            if (Time.time < m_NextCheck) return;
            m_NextCheck = Time.time + CheckInterval;
            Refresh();
        }

        /// <summary>Shows or hides the visual now.</summary>
        public void Refresh()
        {
            if (m_Trigger == null || m_Visual == null) return;
            if (m_Trigger.Manager != null && m_Trigger.Manager.IsTalking) return; // keep the map as it is during a conversation
            bool available = m_Trigger.CanFire();
            if (m_Visual.activeSelf != available) m_Visual.SetActive(available);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (m_Trigger == null) Debug.LogWarning($"{LogTag} '{name}': assign the Dialogue Trigger it follows.", this);
            if (m_Visual == null) Debug.LogWarning($"{LogTag} '{name}': assign the object to show while it can fire.", this);
            else if (m_Visual == gameObject) Debug.LogWarning($"{LogTag} '{name}': the visual must be a child (hiding this object would stop the checks).", this);
        }
#endif
    }
}
