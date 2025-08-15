using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Check Condition", "Decorator/CheckCondition", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/CheckCondition.png")]
    public class CheckConditionNode : DecoratorNode
    {
        [Tooltip("The primary value to check.")]
        [BlackboardKeyType(typeof(object))]
        public Key value;

        [Tooltip("The comparison to perform.")]
        [FilterComparison(nameof(value), nameof(targetValue))]
        public Key<ComparisonType> compareType;

        [Tooltip("The value to compare against. Its type is determined by the 'Value' field.")]
        [DetachKey(nameof(value))]
        public Key targetValue;

        [Tooltip("If checked, the result of the comparison will be inverted.")]
        public bool shouldInvert;

        protected override Status OnProcess()
        {
            // --- 1. Validate Configuration ---
            if (value == null || compareType == null || targetValue == null)
            {
                return Status.Failure;
            }

            // --- 2. Get the Values ---
            var blackboardKey1 = !string.IsNullOrEmpty(value.keyName) ? blackboard.keys.Find(a => a.keyName == value.keyName) : null;
            object val1 = (blackboardKey1 != null) ? blackboardKey1.GetValueObject() : value.GetValueObject();

            var op = compareType.GetValue();

            var blackboardKey2 = !string.IsNullOrEmpty(targetValue.keyName) ? blackboard.keys.Find(a => a.keyName == targetValue.keyName) : null;
            object val2 = (blackboardKey2 != null) ? blackboardKey2.GetValueObject() : targetValue.GetValueObject();

            // --- 3. Perform Comparison ---
            bool conditionPassed = ComparisonHelper.PerformComparison(val1, val2, op);

            // --- 4. Apply Inversion ---
            conditionPassed = (conditionPassed != shouldInvert);

            // --- 5. Execute Logic Based on Result ---
            if (conditionPassed)
            {
                // Condition passed; run the child if it exists
                if (child != null)
                {
                    return child.Process();
                }
                return Status.Success;
            }
            else
            {
                // Condition failed; do not run the child
                return Status.Failure;
            }
        }
    }
}