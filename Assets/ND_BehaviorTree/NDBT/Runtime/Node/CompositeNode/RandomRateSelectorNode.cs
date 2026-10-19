using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// A composite node that selects one of its children to run based on a weighted random chance.
    /// The weight can be configured to treat higher or lower priority values as better.
    /// Children with a priority of 0 or less are never picked (the default priority is 0, so set it on every child).
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
        [Tooltip("LowerIsBetter: Lower priority value = higher chance.\nHigherIsBetter: Higher priority value = higher chance.\nPriorities must be > 0; children with priority <= 0 are never picked.")]
        public WeightingMode weightingMode = WeightingMode.LowerIsBetter;

        private Node runningChild = null;
        [System.NonSerialized] private bool m_NoWeightErrorLogged;

        protected override void OnEnter()
        {
            runningChild = null;
        }

        protected override Status OnProcess()
        {

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
                    if (children.Count > 0 && !m_NoWeightErrorLogged)
                    {
                        m_NoWeightErrorLogged = true;
                        Debug.LogError($"[RandomRateSelectorNode] '{name}': no child has a priority > 0, so none can be picked and the node fails. " +
                                       "Set each child's 'priority' to a positive weight. (Logged once.)", this);
                    }
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

        /// <summary>
        /// The priority that weights a child. At runtime a child may be the outermost attached decorator of the real
        /// node (parent -> decorators[0] -> ... -> node); the weight is the priority set on the real node.
        /// </summary>
        private static int GetPriority(Node child)
        {
            Node terminal = child;
            int guard = 0;
            while (terminal is DecoratorNode d && d.child != null && guard++ < 64) terminal = d.child;
            if (terminal != child && terminal.decorators != null && child is DecoratorNode cd && terminal.decorators.Contains(cd))
                return terminal.priority;
            return child.priority;
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
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null) continue;
                totalWeight += Mathf.Max(0, GetPriority(children[i]));
            }

            if (totalWeight <= 0) return null;

            // Random.Range(float, float) is max-inclusive: if it returns totalWeight, fall back to the last weighted child.
            float randomPoint = Random.Range(0, totalWeight);
            Node lastWeighted = null;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                float weight = Mathf.Max(0, GetPriority(child));
                if (weight <= 0) continue;
                lastWeighted = child;
                if (randomPoint < weight)
                {
                    return child;
                }
                randomPoint -= weight;
            }
            return lastWeighted;
        }

        private Node SelectWithLowerIsBetter()
        {
            // Children with non-positive priority are ignored (they can't be inverted).
            int maxPriority = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null) continue;
                int p = GetPriority(children[i]);
                if (p > maxPriority) maxPriority = p;
            }
            if (maxPriority <= 0) return null;

            float totalWeight = 0;
            // Calculate inverted weights. The '+1' ensures the child with the max priority still has a weight of 1.
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null) continue;
                int p = GetPriority(children[i]);
                if (p > 0) totalWeight += (maxPriority - p) + 1;
            }

            if (totalWeight <= 0) return null;

            float randomPoint = Random.Range(0, totalWeight);
            Node lastWeighted = null;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                int p = GetPriority(child);
                if (p <= 0) continue;
                float weight = (maxPriority - p) + 1;
                lastWeighted = child;
                if (randomPoint < weight)
                {
                    return child;
                }
                randomPoint -= weight;
            }
            return lastWeighted;
        }

        public override void Reset()
        {
            base.Reset();
            runningChild = null;
        }
    }
}
