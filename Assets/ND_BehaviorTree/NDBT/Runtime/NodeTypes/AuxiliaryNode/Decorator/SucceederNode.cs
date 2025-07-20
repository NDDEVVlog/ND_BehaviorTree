using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Succeeder", "Decorator/Succeeder", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Check.png")]
    public class SucceederNode : DecoratorNode
    {
        /// <summary>
        /// This decorator ensures that its child branch always returns Success,
        /// unless it is still Running. It's useful for making behaviors optional
        /// within a Sequence.
        /// </summary>
        protected override Status OnProcess()
        {
            if (child == null)
            {
                return Status.Success;
            }

            var status = child.Process();

            if (status == Status.Running)
            {
                return Status.Running;
            }

            return Status.Success;
        }
    }
}
