using UnityEngine;

namespace ND.Localization
{
    /// <summary>Initializes <see cref="LocalizationService"/> before other scripts run. Put one in the first scene.</summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("ND Localization/Localization Bootstrap")]
    public class LocalizationBootstrap : MonoBehaviour
    {
        [SerializeField] private LocalizationSettings m_Settings;

        private void Awake()
        {
            if (m_Settings == null)
            {
                Debug.LogError($"[Localization] {name}: {nameof(LocalizationBootstrap)} has no settings assigned.", this);
                return;
            }
            if (LocalizationService.IsReady && LocalizationService.Settings == m_Settings) return;
            LocalizationService.Initialize(m_Settings);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_Settings == null)
                Debug.LogWarning($"[Localization] {name}: assign a LocalizationSettings asset to {nameof(LocalizationBootstrap)}.", this);
        }
#endif
    }
}
