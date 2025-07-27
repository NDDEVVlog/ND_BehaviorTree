

using UnityEngine;

namespace ND_BehaviorTree
{
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

        // We track the time when the cooldown will be over.
        private float cooldownEndTime = -1.0f;

        protected override void OnEnter()
        {
            // The cooldown state persists, so OnEnter doesn't need to do anything.
        }

        protected override Status OnProcess()
        {
            if (child == null)
            {
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

        public override void Reset()
        {
            base.Reset();
            // When the tree is fully reset, we should also reset the cooldown
            // so the ability is available on the next run.
            cooldownEndTime = -1.0f;
            cooldownRemaining = 0;
        }
    }
}
