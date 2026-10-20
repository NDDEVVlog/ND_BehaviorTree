using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Sub-node: waits <see cref="delay"/> seconds (Running) before running the decorated node. Replaces a "Wait → X" sequence.</summary>
    [NodeDescription("Sub-node: waits a number of seconds, then runs its node.", "Running during the delay (progress bar). The delay restarts every time the node is entered.")]
    [NodeInfo("Delay", "Decorator/Delay", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class DelayDecorator : DecoratorNode
    {
        public float delay = 1f;

        [NodeProgressBar(nameof(m_Elapsed), nameof(delay))]
        [System.NonSerialized] private float m_Elapsed;

        protected override void OnEnter() => m_Elapsed = 0f;

        protected override Status OnProcess()
        {
            if (m_Elapsed < delay)
            {
                m_Elapsed += Time.deltaTime;
                return Status.Running;
            }
            return child != null ? child.Process() : Status.Success;
        }
    }
}
