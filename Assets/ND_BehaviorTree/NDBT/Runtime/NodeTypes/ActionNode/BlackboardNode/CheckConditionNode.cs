// FILE: CheckConditionNode.cs (FULLY IMPLEMENTED)
using System;
using UnityEngine;

namespace ND_BehaviorTree
{   
    [NodeInfo("Check Condition", "Action/Blackboard/CheckCondition", true, false)]
    public class CheckConditionNode : ActionNode
    {
        [Tooltip("The primary value to check. Can be linked to the Blackboard or set directly.")]
        [BlackboardKeyType(typeof(object))]  [ExposeProperty]
        public Key value;

        [Tooltip("The comparison to perform.")]
        [SerializeReference]
        public Key<ComparisonType> compareType; 

        [Tooltip("The value to compare against. Its type is determined by the 'Value' field.")]
        [BlackboardKeyType(typeof(object))]
        [DetachKey(nameof(value))]
        public Key targetValue;

        /// <summary>
        /// This is the core logic method that runs when the node is processed.
        /// </summary>
        protected override Status OnProcess()
        {
            // 1. Initial validation
            if (value == null || compareType == null || targetValue == null)
            {
                return Status.Failure;
            }
            
            // --- IMPLEMENTING THE REQUESTED LOGIC ---

            // --- Get val1 ---
            object val1;
            // First, try to find a fresh key from the blackboard using the assigned key's name.
            var blackboardKey1 = !string.IsNullOrEmpty(value.keyName) ? blackboard.keys.Find(a => a.keyName == value.keyName) : null;
            if (blackboardKey1 != null)
            {
                // If found, use its value. This is the prioritized path.
                val1 = blackboardKey1.GetValueObject();
            }
            else
            {
                // Else (if not found on blackboard, or it's a direct value with no name),
                // use the value from the key object assigned to the node.
                val1 = value.GetValueObject();
            }

            // --- Get Operation ---
            var op = compareType.GetValue();

            // --- Get val2 ---
            object val2;
            // Apply the same logic for the targetValue.
            var blackboardKey2 = !string.IsNullOrEmpty(targetValue.keyName) ? blackboard.keys.Find(a => a.keyName == targetValue.keyName) : null;
            if (blackboardKey2 != null)
            {
                // If found, use its value.
                val2 = blackboardKey2.GetValueObject();
            }
            else
            {
                // Else, use the direct/cached value.
                val2 = targetValue.GetValueObject();
            }

            // --- END OF IMPLEMENTATION ---


            // 3. Perform comparison
            bool comparisonResult = PerformComparison(val1, val2, op);

            // 4. Return result
            return comparisonResult ? Status.Success : Status.Failure;
        }

        /// <summary>
        /// A robust helper method to compare two objects of potentially different types.
        /// </summary>
        /// <param name="val1">The left-hand side value.</param>
        /// <param name="val2">The right-hand side value.</param>
        /// <param name="op">The comparison operation.</param>
        /// <returns>True if the condition is met, otherwise false.</returns>
        private bool PerformComparison(object val1, object val2, ComparisonType op)
        {
            // --- A. Handle Nulls ---
            // Comparisons with null are only meaningful for Equal and NotEqual.
            if (val1 == null || val2 == null)
            {
                switch (op)
                {
                    case ComparisonType.Equal:
                        return val1 == val2; // Works correctly (null == null is true)
                    case ComparisonType.NotEqual:
                        return val1 != val2; // Works correctly (null != null is false)
                    default:
                        // Any other comparison (Greater, Less, etc.) with null is invalid.
                        return false; 
                }
            }

            // --- B. Handle Numeric Types ---
            // If both values are numeric, convert them to double for a safe comparison.
            if (IsNumeric(val1) && IsNumeric(val2))
            {
                double num1 = Convert.ToDouble(val1);
                double num2 = Convert.ToDouble(val2);
                
                switch (op)
                {
                    case ComparisonType.Equal:       return num1 == num2;
                    case ComparisonType.NotEqual:    return num1 != num2;
                    case ComparisonType.Greater:     return num1 > num2;
                    case ComparisonType.GreaterEqual:return num1 >= num2;
                    case ComparisonType.Less:        return num1 < num2;
                    case ComparisonType.LessEqual:   return num1 <= num2;
                }
            }

            // --- C. Handle General Comparable Types (string, bool, etc.) ---
            // Use the standard IComparable interface for a generic solution.
            if (val1 is IComparable comparable1)
            {
                try
                {
                    // CompareTo returns: < 0 if val1 is less than val2, 0 if equal, > 0 if greater.
                    int comparisonResult = comparable1.CompareTo(val2);
                    switch (op)
                    {
                        case ComparisonType.Equal:       return comparisonResult == 0;
                        case ComparisonType.NotEqual:    return comparisonResult != 0;
                        case ComparisonType.Greater:     return comparisonResult > 0;
                        case ComparisonType.GreaterEqual:return comparisonResult >= 0;
                        case ComparisonType.Less:        return comparisonResult < 0;
                        case ComparisonType.LessEqual:   return comparisonResult <= 0;
                    }
                }
                catch (ArgumentException)
                {
                    // This catches cases where types are not comparable (e.g., string vs. int).
                    // We only allow equality checks for incompatible types.
                    if (op == ComparisonType.Equal) return val1.Equals(val2);
                    if (op == ComparisonType.NotEqual) return !val1.Equals(val2);
                    return false; // All other comparisons are false.
                }
            }
            
            // --- D. Fallback for non-IComparable types (like GameObject, Transform) ---
            // Only equality checks are meaningful.
            switch (op)
            {
                case ComparisonType.Equal:
                    return val1.Equals(val2);
                case ComparisonType.NotEqual:
                    return !val1.Equals(val2);
                default:
                    // Greater/Less comparisons are invalid for these types.
                    Debug.LogWarning($"Cannot perform ordered comparison on non-comparable type '{val1.GetType().Name}'.", this);
                    return false;
            }
        }

        /// <summary>
        /// Helper to check if an object is a numeric type.
        /// </summary>
        private bool IsNumeric(object value)
        {
            if (value == null) return false;
            // TypeCode is a fast way to check for primitive numeric types.
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single: // float
                    return true;
                default:
                    return false;
            }
        }
    }
}