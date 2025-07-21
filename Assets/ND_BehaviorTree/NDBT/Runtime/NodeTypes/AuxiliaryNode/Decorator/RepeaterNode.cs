using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Repeater", "Decorator/Repeater", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Repeater.png")]
    public class RepeaterNode : DecoratorNode
    {
        [Tooltip("The number of times to repeat the child. Set to 0 to repeat forever.")]
        
        public int repeatCount = 3;
        [ExposeProperty]
        public int currentCount = 0;

        protected override void OnEnter()
        {
            currentCount = 0;
        }

        protected override Status OnProcess()
        {
            if (child == null)
            {
                return Status.Success;
            }

            // Check if we have completed the required number of repetitions.
            // A repeatCount of 0 means repeat forever.
            if (repeatCount > 0 && currentCount >= repeatCount)
            {
                return Status.Success;
            }

            // Process the child node.
            var childStatus = child.Process();

            switch (childStatus)
            {
                case Status.Running:
                    return Status.Running;

                case Status.Failure:
                    // If the child fails, the whole repeater fails.
                    return Status.Failure;

                case Status.Success:
                    // The child succeeded, so increment our count.
                    currentCount++;
                    
                    // Reset the child so it can be run again.
                    child.Reset();

                    // If we've now hit our repeat count, we're done and succeed.
                    if (repeatCount > 0 && currentCount >= repeatCount)
                    {
                        return Status.Success;
                    }
                    else
                    {
                        // We need to repeat again. Return Running to ensure we get ticked on the next frame.
                        // This pattern is compatible with one-step-per-frame composites.
                        return Status.Running;
                    }
            }

            return Status.Running;
        }
    }
}
