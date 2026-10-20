////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

namespace ND_BehaviorTree
{   [RequireComponentInRunner(typeof(Animator))]
    [NodeDescription("Sets a trigger on an Animator key.", "The trigger name must exist in the Animator Controller.")]
    [NodeInfo("Set Animator Trigger", "Action/Animation/SetAnimatorTrigger", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Animator.png")]
    public class SetAnimatorTriggerNode : ActionNode
    {
        [Tooltip("The Blackboard key for the Animator component.")]
        public Key animatorKey;

        [Tooltip("The name of the trigger parameter in the Animator Controller to set.")]
        public string triggerName;

        protected override Status OnProcess()
        {
            if (animatorKey == null || string.IsNullOrEmpty(triggerName))
            {
                return Status.Failure;
            }

            Animator animator = blackboard.GetValue<Animator>(animatorKey.keyName);
            if (animator == null)
            {
                Debug.LogWarning($"Animator not found on blackboard key '{animatorKey.keyName}'.");
                return Status.Failure;
            }

            animator.SetTrigger(triggerName);
            return Status.Success;
        }
    }
}