using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Utility AI: scores every child (its <see cref="Node.weight"/>, adjusted by attached <see cref="IWeightModifier"/>s
    /// such as Weight From Key) and runs the best one. Scores are re-evaluated every <see cref="evaluationInterval"/>;
    /// a running child is interrupted only when another one beats it by more than <see cref="hysteresis"/>.
    /// A child that fails is skipped for the rest of this activation and the next best is tried (selector semantics).
    /// </summary>
    [NodeDescription(
        "Runs the child with the highest score (Utility AI). Scores come from each child's Weight + 'Weight From Key'.",
        "Every 'Evaluation Interval' seconds all children are scored. The best one runs; if another child's score beats " +
        "the running one by more than 'Hysteresis', the running child is interrupted and the better one starts.\n" +
        "Children with score ≤ 0 are never chosen. If the chosen child fails, the next best is tried; the node fails " +
        "when no child is left. It succeeds when the chosen child succeeds.\n" +
        "Typical use: attach 'Weight From Key' to each branch (Hunger, Fear, Menace…) and let the situation decide.\n" +
        "Live Debug shows the current score on each edge.")]
    [NodeInfo("Utility Selector", "Composite/Utility Selector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Brain.png")]
    public class UtilitySelectorNode : CompositeNode, IWeightedComposite
    {
        [Tooltip("Seconds between re-scoring while a child runs (0 = every tick).")]
        public float evaluationInterval = 0.25f;
        [Tooltip("A challenger must beat the running child's score by more than this to interrupt it (prevents flip-flopping).")]
        public float hysteresis = 0.1f;

        public WeightUsage WeightUsage => WeightUsage.Score;

        [System.NonSerialized] private float[] m_Scores;
        [System.NonSerialized] private bool[] m_Failed;
        [System.NonSerialized] private int m_Running = -1;
        [System.NonSerialized] private float m_NextEvaluation;

        /// <summary>Score of child <paramref name="index"/> at the last evaluation (editor live view). NaN if never scored.</summary>
        public float GetLastScore(int index) => m_Scores != null && index >= 0 && index < m_Scores.Length ? m_Scores[index] : float.NaN;

        /// <summary>Index of the child currently running, or -1.</summary>
        public int RunningIndex => m_Running;

        protected override void OnEnter()
        {
            Ensure();
            for (int i = 0; i < m_Failed.Length; i++) m_Failed[i] = false;
            m_Running = -1;
            m_NextEvaluation = 0f;
        }

        protected override Status OnProcess()
        {
            Ensure();
            while (true)
            {
                if (m_Running < 0 || Time.time >= m_NextEvaluation)
                {
                    m_NextEvaluation = Time.time + evaluationInterval;
                    Score();
                    int best = Best();
                    if (best < 0)
                    {
                        Interrupt(m_Running);
                        return Status.Failure;
                    }
                    if (m_Running < 0) m_Running = best;
                    else if (best != m_Running && m_Scores[best] > m_Scores[m_Running] + hysteresis)
                    {
                        Interrupt(m_Running);
                        m_Running = best;
                    }
                }

                var status = children[m_Running].Process();
                if (status == Status.Running) return Status.Running;
                if (status == Status.Success)
                {
                    m_Running = -1;
                    return Status.Success;
                }

                // Failure: skip this child for the rest of the activation and try the next best right away.
                children[m_Running].Reset();
                m_Failed[m_Running] = true;
                m_Running = -1;
            }
        }

        private void Ensure()
        {
            if (m_Scores != null && m_Scores.Length == children.Count) return;
            m_Scores = new float[children.Count];
            m_Failed = new bool[children.Count];
            for (int i = 0; i < m_Scores.Length; i++) m_Scores[i] = float.NaN;
        }

        private void Score()
        {
            for (int i = 0; i < children.Count; i++)
                m_Scores[i] = children[i] != null ? NodeWeight.Evaluate(children[i]) : 0f;
        }

        private int Best()
        {
            int best = -1;
            for (int i = 0; i < m_Scores.Length; i++)
            {
                if (m_Failed[i] || !(m_Scores[i] > 0f)) continue;
                if (best < 0 || m_Scores[i] > m_Scores[best]) best = i;
            }
            return best;
        }

        private void Interrupt(int index)
        {
            if (index < 0 || index >= children.Count || children[index] == null) return;
            children[index].InteruptAction?.Invoke();
            children[index].Reset();
        }

        public override void Reset()
        {
            base.Reset();
            m_Running = -1;
        }
    }
}
