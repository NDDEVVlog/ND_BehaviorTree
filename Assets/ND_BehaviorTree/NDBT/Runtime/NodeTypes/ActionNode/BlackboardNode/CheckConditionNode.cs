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
                StopChild();
                return Status.Failure;
            }

            // --- 2. Get the Values --- (linked blackboard key if present, otherwise the inline value)
            Key key1 = ResolveKey(value);
            var op = compareType.GetValue();
            Key key2 = ResolveKey(targetValue);

            // --- 3. Perform Comparison --- fast path for float/int/bool (no boxing per tick), same results as ComparisonHelper
            if (!TryCompareTyped(key1, key2, op, out bool conditionPassed))
                conditionPassed = ComparisonHelper.PerformComparison(key1.GetValueObject(), key2.GetValueObject(), op);

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
                // Condition failed; do not run the child, and stop it if it was running.
                StopChild();
                return Status.Failure;
            }
        }

        /// <summary>The runtime blackboard key with this name, or the key itself (detached local value).</summary>
        private Key ResolveKey(Key key)
        {
            if (blackboard != null && !string.IsNullOrEmpty(key.keyName) && blackboard.TryGetKey(key.keyName, out var blackboardKey))
                return blackboardKey;
            return key;
        }

        private static bool TryCompareTyped(Key a, Key b, ComparisonType op, out bool result)
        {
            switch (a)
            {
                case Key<float> fa when b is Key<float> fb: result = Compare(fa.GetValue(), fb.GetValue(), op); return true;
                case Key<float> fa when b is Key<int> ib: result = Compare(fa.GetValue(), ib.GetValue(), op); return true;
                case Key<int> ia when b is Key<int> ib: result = Compare(ia.GetValue(), ib.GetValue(), op); return true;
                case Key<int> ia when b is Key<float> fb: result = Compare(ia.GetValue(), fb.GetValue(), op); return true;
                case Key<bool> ba when b is Key<bool> bb: result = Compare(ba.GetValue().CompareTo(bb.GetValue()), 0, op); return true;
                default: result = false; return false;
            }
        }

        private static bool Compare(double x, double y, ComparisonType op)
        {
            switch (op)
            {
                case ComparisonType.Equal: return x == y;
                case ComparisonType.NotEqual: return x != y;
                case ComparisonType.Greater: return x > y;
                case ComparisonType.GreaterEqual: return x >= y;
                case ComparisonType.Less: return x < y;
                case ComparisonType.LessEqual: return x <= y;
                default: return false;
            }
        }

        /// <summary>A child left Running would resume later without OnEnter (and never get OnExit): reset it.</summary>
        private void StopChild()
        {
            if (child != null && child.IsRunning()) child.Reset();
        }
    }
}
