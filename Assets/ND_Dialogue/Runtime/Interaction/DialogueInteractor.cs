using System;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue
{
    /// <summary>
    /// Put on the player. Finds the nearest <see cref="DialogueTrigger"/> set to OnUse that is in range and allowed to
    /// fire, shows a prompt ("E — Talk to Gate Guard"), and starts it on the use key. The prompt hides while a
    /// conversation runs. Game code can call <see cref="Interact"/> directly (e.g. from the Input System).
    /// </summary>
    [AddComponentMenu("ND Dialogue/Dialogue Interactor")]
    public class DialogueInteractor : MonoBehaviour
    {
        [Tooltip("Hides the prompt while this manager is talking (optional).")]
        [SerializeField] private DialogueManager m_Manager;
        [SerializeField] private KeyCode m_UseKey = KeyCode.E;
        [Tooltip("Shown while something can be used.")]
        [SerializeField] private GameObject m_PromptRoot;
        [Tooltip("UI.Text or TextMeshPro: receives \"E — Talk to …\".")]
        [SerializeField] private Graphic m_PromptText;

        public DialogueTrigger Target { get; private set; }
        public KeyCode UseKey { get => m_UseKey; set => m_UseKey = value; }

        /// <summary>(new target or null) The usable trigger changed.</summary>
        public event Action<DialogueTrigger> TargetChanged;

        private void Update()
        {
            RefreshTarget();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Target != null && Input.GetKeyDown(m_UseKey)) Interact();
#endif
        }

        private void OnDisable() => SetTarget(null);

        /// <summary>Re-picks the nearest usable trigger in range (and updates the prompt). Returns it, or null.</summary>
        public DialogueTrigger RefreshTarget()
        {
            DialogueTrigger best = null;
            if (m_Manager == null || !m_Manager.IsTalking)
            {
                float bestDistance = float.MaxValue;
                Vector3 position = transform.position;
                foreach (var trigger in DialogueTrigger.Enabled)
                {
                    if (trigger == null || trigger.When != DialogueTriggerEvent.OnUse || trigger.gameObject == gameObject) continue;
                    float distance = Vector3.Distance(position, trigger.transform.position);
                    if (distance > trigger.UseRange || distance >= bestDistance) continue;
                    if (!trigger.CanFire(out _)) continue;
                    best = trigger;
                    bestDistance = distance;
                }
            }
            SetTarget(best);
            return best;
        }

        /// <summary>Uses the current target. False when there is none or it refused (it logs why).</summary>
        public bool Interact()
        {
            if (Target == null) return false;
            var used = Target;
            bool started = used.Fire();
            if (started) SetTarget(null); // the prompt hides during the conversation
            return started;
        }

        private void SetTarget(DialogueTrigger target)
        {
            if (target != Target)
            {
                Target = target;
                TargetChanged?.Invoke(target);
            }
            if (m_PromptRoot != null && m_PromptRoot.activeSelf != (target != null)) m_PromptRoot.SetActive(target != null);
            if (target != null) UIText.Set(m_PromptText, $"{m_UseKey} — {target.PromptLabel}");
        }
    }
}
