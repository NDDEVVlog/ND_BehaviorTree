using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Parallel", "Composite/Parallel", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Parallel.png")]
    public class ParallelNode : CompositeNode
    {
        public enum Policy { RequireOne, RequireAll }

        [Tooltip("Success Policy: How many children need to succeed for the parallel node to succeed.")]
        public Policy successPolicy = Policy.RequireOne;

        [Tooltip("Failure Policy: How many children need to fail for the parallel node to fail.\n" +
                 "If every child has finished and neither policy is met (e.g. RequireAll/RequireAll with mixed results), the node fails.")]
        public Policy failurePolicy = Policy.RequireAll;

        protected override void OnEnter()
        {
            // Reset all children when this node is entered.
            for (int i = 0; i < children.Count; i++) children[i]?.Reset();
        }

        protected override Status OnProcess()
        {

            if (children.Count == 0)
            {
                return Status.Success;
            }

            int childCount = 0;
            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                childCount++;

                // We only process children that are not already in a finished state.
                if (child.status != Status.Success && child.status != Status.Failure)
                {
                    child.Process();
                }

                // Count the number of successes and failures.
                if (child.status == Status.Success) successCount++;
                else if (child.status == Status.Failure) failureCount++;
            }

            if (childCount == 0) return Status.Success;

            Status result;
            bool successMet = successPolicy == Policy.RequireOne ? successCount > 0 : successCount >= childCount;
            bool failureMet = failurePolicy == Policy.RequireOne ? failureCount > 0 : failureCount >= childCount;

            if (successMet) result = Status.Success;
            else if (failureMet) result = Status.Failure;
            // Every child finished but no policy is satisfied (e.g. RequireAll/RequireAll with mixed results).
            else if (successCount + failureCount >= childCount) result = Status.Failure;
            // Neither success nor failure conditions are met: still running.
            else return Status.Running;

            // The parallel is done: children that are still running must not keep running.
            AbortRunningChildren();
            return result;
        }

        private void AbortRunningChildren()
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != null && child.IsRunning()) child.Reset();
            }
        }
    }
}
