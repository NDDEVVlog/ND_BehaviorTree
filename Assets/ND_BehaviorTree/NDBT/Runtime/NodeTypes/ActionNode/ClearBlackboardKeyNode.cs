////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

namespace ND_BehaviorTree
{
    [NodeInfo("Clear Blackboard Key", "Action/Blackboard/ClearKey", true, false)]
    public class ClearBlackboardKeyNode : ActionNode
    {
        [Tooltip("The Blackboard key to clear (set to null).")]
        public Key keyToClear;

        protected override Status OnProcess()
        {
            if (keyToClear != null && blackboard != null)
            {
                blackboard.SetValue<object>(keyToClear.keyName, null);
                return Status.Success;
            }
            return Status.Failure;
        }
    }
}