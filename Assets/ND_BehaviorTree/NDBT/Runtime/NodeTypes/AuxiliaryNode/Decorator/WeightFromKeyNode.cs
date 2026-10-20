using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Sub-node: makes the node's weight follow a float blackboard key, so a Random Rate / Utility Selector
    /// re-weighs its branches as the situation changes. Has no effect on execution (it just runs the node).
    /// </summary>
    [NodeDescription(
        "Drives this node's weight from a float key (for Random Rate / Utility Selector parents).",
        "Value = key (optionally remapped by the curve) × multiplier.\n" +
        "Multiply: weight × value · Replace: value · Add: weight + value.\n" +
        "Example: a Utility Selector picks 'Check lockers' more often as the 'HideCount' key grows.\n" +
        "Only meaningful when the parent picks children by weight; the validator warns otherwise.")]
    [NodeInfo("Weight From Key", "Decorator/Weight From Key", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomSelector.png")]
    public class WeightFromKeyNode : DecoratorNode, IWeightModifier
    {
        public enum Mode { Multiply, Replace, Add }

        [BlackboardKeyType(typeof(float))] public Key key;
        public Mode mode = Mode.Multiply;
        public float multiplier = 1f;
        [Tooltip("Remap the key value through the curve before multiplying (x = key value).")]
        public bool useCurve;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [System.NonSerialized] private Key<float> m_Key;
        [System.NonSerialized] private bool m_Resolved;

        protected override Status OnProcess() => child != null ? child.Process() : Status.Success;

        public float ModifyWeight(float weight)
        {
            if (!m_Resolved)
            {
                m_Resolved = true;
                if (key != null && blackboard != null && blackboard.TryGetKey(key.keyName, out var found)) m_Key = found as Key<float>;
                if (m_Key == null) Debug.LogError($"[WeightFromKeyNode] '{name}': key '{key?.keyName}' is not a float key of the runtime blackboard; the weight is left unchanged.", this);
            }
            if (m_Key == null) return weight;

            float value = m_Key.GetValue();
            if (useCurve && curve != null) value = curve.Evaluate(value);
            value *= multiplier;
            switch (mode)
            {
                case Mode.Replace: return value;
                case Mode.Add: return weight + value;
                default: return weight * value;
            }
        }

        public string Describe()
        {
            string keyName = key != null && !string.IsNullOrEmpty(key.keyName) ? key.keyName : "?";
            string op = mode == Mode.Replace ? "=" : mode == Mode.Add ? "+" : "×";
            return multiplier == 1f ? $"{op}{keyName}" : $"{op}{keyName}·{multiplier:0.##}";
        }
    }
}
