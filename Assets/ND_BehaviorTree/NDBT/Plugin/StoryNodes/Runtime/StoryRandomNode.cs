using UnityEngine;

namespace ND_BehaviorTree.Story
{
    /// <summary>Control-flow node: runs ONE random branch (chance by weight, equal by default) and returns its result.</summary>
    [NodeDescription("Runs one branch picked at random and returns its result.",
        "Every branch has the same chance unless you set weights (child Weight, or 'Weight From Key'); the edges show each chance.\n" +
        "A Running branch keeps running until it finishes; the next activation picks again.")]
    [NodeInfo("Random", "Story/Random", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomSelector.png")]
    public class StoryRandomNode : CompositeNode, IWeightedComposite
    {
        public WeightUsage WeightUsage => WeightUsage.Chance;

        [System.NonSerialized] private Node m_Picked;
        [System.NonSerialized] private bool m_NothingLogged;

        protected override void OnEnter() => m_Picked = Pick();

        protected override Status OnProcess()
        {
            if (m_Picked == null)
            {
                if (!m_NothingLogged && children.Count > 0)
                {
                    m_NothingLogged = true;
                    Debug.LogError($"[StoryRandomNode] '{name}': every branch has weight <= 0, nothing can be picked. (Logged once.)", this);
                }
                return Status.Failure;
            }
            return m_Picked.Process();
        }

        private Node Pick()
        {
            float total = 0f;
            for (int i = 0; i < children.Count; i++) if (children[i] != null) total += Mathf.Max(0f, NodeWeight.Evaluate(children[i]));
            if (total <= 0f) return null;
            float point = Random.Range(0f, total);
            Node last = null;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null) continue;
                float w = Mathf.Max(0f, NodeWeight.Evaluate(children[i]));
                if (w <= 0f) continue;
                last = children[i];
                if (point < w) return last;
                point -= w;
            }
            return last;
        }

        public override void Reset()
        {
            base.Reset();
            m_Picked = null;
        }
    }
}
