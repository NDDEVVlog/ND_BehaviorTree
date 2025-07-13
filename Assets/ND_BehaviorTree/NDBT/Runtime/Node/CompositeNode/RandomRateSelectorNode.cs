using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// A composite node that selects one of its children to run based on a weighted random chance.
    /// The weight can be configured to treat higher or lower priority values as better.
    /// </summary>
    [NodeInfo("Random Rate Selector", "Composite/RandomRateSelector", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomSelector.png", isChildOnly: false)]
    public class RandomRateSelectorNode : CompositeNode
    {
        public enum WeightingMode { LowerIsBetter, HigherIsBetter }

        /// <summary>
        /// Determines how priority values are translated into selection weights.
        /// LowerIsBetter: A lower priority value means a higher chance of selection. (e.g., Priority 1 > Priority 10)
        /// HigherIsBetter: A higher priority value means a higher chance of selection. (e.g., Priority 10 > Priority 1)
        /// </summary>
        [Tooltip("LowerIsBetter: Lower priority value = higher chance.\nHigherIsBetter: Higher priority value = higher chance.")]
        public WeightingMode weightingMode = WeightingMode.LowerIsBetter;

        private Node runningChild = null;

        protected override void OnEnter()
        {
            runningChild = null;
        }

        protected override Status OnProcess()
        {
            // Tick any attached services.
            TickServices();

            Node childToProcess;

            // If a child is already running, continue to process it.
            if (runningChild != null)
            {
                childToProcess = runningChild;
            }
            else
            {
                // Otherwise, select a new child based on the configured weighting mode.
                childToProcess = SelectChildByWeight();
                if (childToProcess == null)
                {
                    // Fail if no valid child could be selected.
                    return Status.Failure;
                }
            }
            
            // Process the chosen child
            var status = childToProcess.Process();

            switch (status)
            {
                case Status.Running:
                    runningChild = childToProcess;
                    return Status.Running;

                case Status.Success:
                    runningChild = null;
                    return Status.Success;
                
                default: // Status.Failure
                    runningChild = null;
                    return Status.Failure;
            }
        }

        private Node SelectChildByWeight()
        {
            if (children.Count == 0) return null;

            // Use different weight calculation based on the selected mode
            if (weightingMode == WeightingMode.HigherIsBetter)
            {
                return SelectWithHigherIsBetter();
            }
            else
            {
                return SelectWithLowerIsBetter();
            }
        }

        private Node SelectWithHigherIsBetter()
        {
            float totalWeight = 0;
            foreach (var child in children)
            {
                totalWeight += Mathf.Max(0, child.priority);
            }

            if (totalWeight <= 0) return null;

            float randomPoint = Random.Range(0, totalWeight);
            foreach (var child in children)
            {
                float weight = Mathf.Max(0, child.priority);
                if (randomPoint < weight)
                {
                    return child;
                }
                randomPoint -= weight;
            }
            return null; // Should not be reached if totalWeight > 0
        }

        private Node SelectWithLowerIsBetter()
        {
            // Filter out children with non-positive priority, as they can't be inverted.
            var validChildren = children.Where(c => c.priority > 0).ToList();
            if (validChildren.Count == 0) return null;
            
            // Find the maximum priority value among valid children.
            int maxPriority = validChildren.Max(c => c.priority);

            float totalWeight = 0;
            // Calculate inverted weights.
            foreach (var child in validChildren)
            {
                // The '+1' ensures the child with the max priority still has a weight of 1.
                totalWeight += (maxPriority - child.priority) + 1;
            }

            if (totalWeight <= 0) return null;

            float randomPoint = Random.Range(0, totalWeight);
            foreach (var child in validChildren)
            {
                float weight = (maxPriority - child.priority) + 1;
                if (randomPoint < weight)
                {
                    return child;
                }
                randomPoint -= weight;
            }
            return null; // Should not be reached if totalWeight > 0
        }

        public override void Reset()
        {
            base.Reset();
            runningChild = null;
        }
    }
}