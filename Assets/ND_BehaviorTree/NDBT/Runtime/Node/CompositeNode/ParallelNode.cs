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

        [Tooltip("Failure Policy: How many children need to fail for the parallel node to fail.")]
        public Policy failurePolicy = Policy.RequireAll;

        protected override void OnEnter()
        {
            // Reset all children when this node is entered.
            children.ForEach(c => c.Reset());
        }

        protected override Status OnProcess()
        {
            TickServices();

            if (children.Count == 0)
            {
                return Status.Success;
            }

            int successCount = 0;
            int failureCount = 0;

            foreach (var child in children)
            {
                // We only process children that are not already in a finished state.
                if (child.status != Status.Success && child.status != Status.Failure)
                {
                    child.Process();
                }

                // Count the number of successes and failures.
                if (child.status == Status.Success)
                {
                    successCount++;
                }
                if (child.status == Status.Failure)
                {
                    failureCount++;
                }
            }

            // Check if the success condition is met.
            if (successPolicy == Policy.RequireOne && successCount > 0)
            {
                return Status.Success;
            }
            if (successPolicy == Policy.RequireAll && successCount == children.Count)
            {
                return Status.Success;
            }

            // Check if the failure condition is met.
            if (failurePolicy == Policy.RequireOne && failureCount > 0)
            {
                return Status.Failure;
            }
            if (failurePolicy == Policy.RequireAll && failureCount == children.Count)
            {
                return Status.Failure;
            }

            // If neither success nor failure conditions are met, the node is still running.
            return Status.Running;
        }

        // When the parallel node itself is reset (e.g., by a parent), it must reset all its children.
        public override void Reset()
        {
            base.Reset();
            children.ForEach(c => c.Reset());
        }
    }
}