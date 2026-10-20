using UnityEngine;

namespace ND_BehaviorTree.Demo.Plugins
{
    /// <summary>
    /// The village burns wood: every <see cref="interval"/> seconds the woodcutter's "Stockpile" key drops by
    /// <see cref="usePerInterval"/>. Once it is below the GOAP goal, the woodcutter's planner gets back to work.
    /// Also shows the amount on a TextMesh sign.
    /// </summary>
    public class WoodStockpile : MonoBehaviour
    {
        [SerializeField] private BehaviorTreeRunner woodcutter;
        [SerializeField] private string keyName = "Stockpile";
        [SerializeField] private int usePerInterval = 10;
        [SerializeField] private float interval = 6f;
        [SerializeField] private TextMesh label;

        private Key<int> m_Key;
        private float m_NextUse;
        private int m_Shown = int.MinValue;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && woodcutter == null)
                Debug.LogWarning($"[WoodStockpile] '{name}': assign the woodcutter's BehaviorTreeRunner.", this);
        }
#endif

        private void Start()
        {
            if (woodcutter == null)
            {
                Debug.LogError($"[WoodStockpile] '{name}': the woodcutter runner is not assigned.", this);
                enabled = false;
                return;
            }
            m_NextUse = Time.time + interval;
        }

        private void Update()
        {
            if (m_Key == null)
            {
                var blackboard = woodcutter.RuntimeTree != null ? woodcutter.RuntimeTree.blackboard : null;
                if (blackboard == null) return; // the runner builds its tree in Start
                if (!blackboard.TryGetKey(keyName, out var key) || !(key is Key<int> intKey))
                {
                    Debug.LogError($"[WoodStockpile] '{name}': the woodcutter's blackboard has no Int key '{keyName}'.", this);
                    enabled = false;
                    return;
                }
                m_Key = intKey;
            }

            int value = m_Key.GetValue();
            if (Time.time >= m_NextUse)
            {
                m_NextUse = Time.time + interval;
                if (value > 0)
                {
                    value = Mathf.Max(0, value - usePerInterval);
                    m_Key.SetValue(value);
                }
            }
            if (label != null && value != m_Shown)
            {
                m_Shown = value;
                label.text = $"Stockpile: {value}"; // only when the amount changes
            }
        }
    }
}
