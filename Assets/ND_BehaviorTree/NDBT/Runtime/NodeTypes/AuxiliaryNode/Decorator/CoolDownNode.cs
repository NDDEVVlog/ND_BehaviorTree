

using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeDescription("Sub-node: after its node succeeds, blocks it for a cooldown time.", "While cooling down the decorator fails at once (the progress bar shows the remaining time).")]
    [NodeInfo("Cooldown", "Decorator/Cooldown", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Cooldown.png")]
    public class CooldownNode : DecoratorNode
    {
        [Tooltip("The duration in seconds to wait after the child succeeds before it can run again.")]
        public float cooldownDuration = 5.0f;

        // The progress bar will show the remaining cooldown time.
        // It uses 'cooldownRemaining' as the current value and 'cooldownDuration' as the max value.
        [NodeProgressBar(nameof(cooldownRemaining), nameof(cooldownDuration))] // The 'true' inverts the bar fill
        [ExposeProperty]
        private float cooldownRemaining;

        // We track the time when the cooldown will be over. Wall-clock state: it survives Reset()
        // (a parent Selector/Repeater/tree restart must not make the ability available again early).
        private float cooldownEndTime = -1.0f;

        [System.NonSerialized] private bool m_NoChildWarned;

        protected override void OnEnter()
        {
            // The cooldown state persists, so OnEnter doesn't need to do anything.
        }

        protected override Status OnProcess()
        {
            if (child == null)
            {
                if (!m_NoChildWarned)
                {
                    m_NoChildWarned = true;
                    Debug.LogWarning($"[CooldownNode] '{name}' has no child; it always fails. (Logged once.)", this);
                }
                return Status.Failure;
            }

            // Always update the remaining time for the UI.
            // Mathf.Max ensures it doesn't display a negative number.
            cooldownRemaining = Mathf.Max(0, cooldownEndTime - Time.time);

            // If the cooldown is active, fail immediately.
            if (cooldownRemaining > 0)
            {
                return Status.Failure;
            }

            // Cooldown is not active, so process the child node.
            var status = child.Process();

            // If the child succeeded, start the cooldown timer.
            if (status == Status.Success)
            {
                cooldownEndTime = Time.time + cooldownDuration;
                // We can immediately update the remaining time for the frame it succeeds on.
                cooldownRemaining = cooldownDuration;
            }

            return status;
        }

        // Reset() is intentionally not overridden: the base resets the child/processing state and the cooldown is kept.
    }
}
