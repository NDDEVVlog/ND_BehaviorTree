using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// A composite node that selects one of its children to run based on a weighted random chance (each child's
    /// <see cref="Node.weight"/>, adjusted by attached <see cref="IWeightModifier"/>s such as Weight From Key).
    /// Children with a weight of 0 or less are never picked.
    /// </summary>
    [NodeDescription(
        "Picks ONE child at random, weighted by each child's Weight, and returns its result.",
        "Each activation rolls once: chance = weight / sum of weights (HigherIsBetter), or inverted (LowerIsBetter).\n" +
        "Children with weight ≤ 0 are never picked. A Running child keeps running until it finishes.\n" +
        "The edges show each child's chance. Attach 'Weight From Key' to a child to change its weight at runtime.\n" +
        "Optional anti-repeat: a picked child's weight is reduced for a while (Fatigue) for more varied behavior.")]
    [NodeInfo("Random Rate Selector", "Composite/RandomRateSelector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomSelector.png", isChildOnly: false)]
    public class RandomRateSelectorNode : CompositeNode, IWeightedComposite
    {
        public enum WeightingMode { LowerIsBetter, HigherIsBetter }

        /// <summary>
        /// LowerIsBetter: a lower weight means a higher chance (inverted: max - weight + 1).
        /// HigherIsBetter: chance is proportional to the weight.
        /// </summary>
        [Tooltip("HigherIsBetter: chance proportional to weight.\nLowerIsBetter: lower weight = higher chance.\nChildren with weight <= 0 are never picked.")]
        public WeightingMode weightingMode = WeightingMode.LowerIsBetter;

        [Tooltip("Anti-repeat: right after a child is picked its weight is multiplied by this (1 = off), recovering linearly over 'Fatigue Recovery'.")]
        [Range(0f, 1f)] public float fatigue = 1f;
        [Tooltip("Seconds for a fatigued child to recover its full weight.")]
        public float fatigueRecovery = 5f;

        public WeightUsage WeightUsage => WeightUsage.Chance;

        private Node runningChild = null;
        [System.NonSerialized] private bool m_NoWeightErrorLogged;
        [System.NonSerialized] private float[] m_Weights;
        [System.NonSerialized] private float[] m_LastPickTime;

        protected override void OnEnter()
        {
            runningChild = null;
        }

        protected override Status OnProcess()
        {
            Node childToProcess;

            if (runningChild != null)
            {
                childToProcess = runningChild;
            }
            else
            {
                int index = SelectChildByWeight();
                if (index < 0)
                {
                    if (children.Count > 0 && !m_NoWeightErrorLogged)
                    {
                        m_NoWeightErrorLogged = true;
                        Debug.LogError($"[RandomRateSelectorNode] '{name}': no child has a weight > 0, so none can be picked and the node fails. " +
                                       "Set each child's 'Weight' to a positive value. (Logged once.)", this);
                    }
                    return Status.Failure;
                }
                childToProcess = children[index];
                if (m_LastPickTime != null) m_LastPickTime[index] = Time.time;
            }

            var status = childToProcess.Process();
            if (status == Status.Running)
            {
                runningChild = childToProcess;
                return Status.Running;
            }
            runningChild = null;
            return status;
        }

        private int SelectChildByWeight()
        {
            int count = children.Count;
            if (count == 0) return -1;
            if (m_Weights == null || m_Weights.Length != count)
            {
                m_Weights = new float[count];
                m_LastPickTime = new float[count];
                for (int i = 0; i < count; i++) m_LastPickTime[i] = float.NegativeInfinity;
            }

            for (int i = 0; i < count; i++)
            {
                float w = children[i] != null ? NodeWeight.Evaluate(children[i]) : 0f;
                if (w > 0f && fatigue < 1f && fatigueRecovery > 0f)
                {
                    float recovered = Mathf.Clamp01((Time.time - m_LastPickTime[i]) / fatigueRecovery);
                    w *= Mathf.Lerp(fatigue, 1f, recovered);
                }
                m_Weights[i] = w;
            }

            ComputeChances(m_Weights, weightingMode);
            return PickIndex(m_Weights);
        }

        /// <summary>
        /// Converts weights into selection weights in place (≤ 0 stays 0). Shared with the editor, which shows the
        /// resulting chance on each edge.
        /// </summary>
        public static void ComputeChances(float[] weights, WeightingMode mode)
        {
            if (mode == WeightingMode.HigherIsBetter)
            {
                for (int i = 0; i < weights.Length; i++) if (weights[i] < 0f) weights[i] = 0f;
                return;
            }

            float max = 0f;
            for (int i = 0; i < weights.Length; i++) if (weights[i] > max) max = weights[i];
            for (int i = 0; i < weights.Length; i++) weights[i] = weights[i] > 0f ? max - weights[i] + 1f : 0f;
        }

        private static int PickIndex(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0f) return -1;

            // Random.Range(float, float) is max-inclusive: if it returns total, fall back to the last weighted child.
            float point = Random.Range(0f, total);
            int last = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                last = i;
                if (point < weights[i]) return i;
                point -= weights[i];
            }
            return last;
        }

        public override void Reset()
        {
            base.Reset();
            runningChild = null;
        }
    }
}
