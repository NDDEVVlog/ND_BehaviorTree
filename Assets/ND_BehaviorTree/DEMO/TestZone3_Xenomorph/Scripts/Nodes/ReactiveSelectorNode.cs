using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Priority selector that re-evaluates every child from the left each tick. When a higher-priority child starts
    /// running (or succeeds), the lower-priority child that was running is interrupted and reset.
    /// The plain Selector keeps running its current child until it finishes, so it cannot react to a new threat;
    /// gate each branch with a condition decorator so the children on the left fail fast when they do not apply.
    /// </summary>
    [NodeDescription("Priority selector that re-checks every child from the left each tick and interrupts lower branches.", "Left = highest priority. Each tick the first child that is Running or succeeds wins; a lower child that was running is interrupted and reset.\nUnlike Selector (which sticks with its running child) it reacts at once. Gate each branch with a condition decorator so branches that do not apply fail fast.")]
    [NodeInfo("Reactive Selector", "Composite/Reactive Selector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Selector.png")]
    public class ReactiveSelectorNode : CompositeNode
    {
        [System.NonSerialized] private int m_RunningIndex = -1;

        protected override void OnEnter() => m_RunningIndex = -1;

        protected override Status OnProcess()
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var status = child.Process();

                if (status == Status.Failure)
                {
                    if (i == m_RunningIndex) m_RunningIndex = -1;
                    child.Reset();
                    continue;
                }

                // A higher-priority branch took over: interrupt the one that was running.
                if (m_RunningIndex > i) AbortChild(m_RunningIndex);

                if (status == Status.Running)
                {
                    m_RunningIndex = i;
                    return Status.Running;
                }

                m_RunningIndex = -1;
                child.Reset();
                return Status.Success;
            }

            m_RunningIndex = -1;
            return Status.Failure;
        }

        private void AbortChild(int index)
        {
            var child = children[index];
            child.InteruptAction?.Invoke();
            child.Reset();
            m_RunningIndex = -1;
        }

        public override void Reset()
        {
            base.Reset();
            m_RunningIndex = -1;
        }
    }
}
