// FILE: ComparisonHelper.cs

using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    public static class ComparisonHelper
    {
        /// <summary>
        /// A robust helper method to compare two objects of potentially different types.
        /// Destroyed UnityEngine.Objects count as null (Unity null semantics).
        /// </summary>
        public static bool PerformComparison(object val1, object val2, ComparisonType op)
        {
            // --- A. Handle Nulls ---
            bool null1 = IsNull(val1);
            bool null2 = IsNull(val2);
            if (null1 || null2)
            {
                switch (op)
                {
                    case ComparisonType.Equal: return null1 && null2;
                    case ComparisonType.NotEqual: return null1 != null2;
                    default: return false;
                }
            }

            // --- B. Handle Numeric Types ---
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
            // Only compare same-typed values: IComparable.CompareTo throws ArgumentException on a type mismatch,
            // and this runs every tick, so mismatched types go to the Equals fallback instead of a caught exception.
            if (val1 is IComparable comparable1 && val1.GetType() == val2.GetType())
            {
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

            // --- D. Fallback for non-IComparable or mismatched types ---
            switch (op)
            {
                case ComparisonType.Equal: return val1.Equals(val2);
                case ComparisonType.NotEqual: return !val1.Equals(val2);
                default: return false;
            }
        }

        /// <summary>True for null and for destroyed UnityEngine.Objects (which are non-null when boxed as object).</summary>
        private static bool IsNull(object value)
        {
            if (value == null) return true;
            return value is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// Helper to check if an object is a numeric type.
        /// </summary>
        public static bool IsNumeric(object value)
        {
            if (value == null) return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.UInt16: case TypeCode.UInt32:
                case TypeCode.UInt64: case TypeCode.Int16: case TypeCode.Int32: case TypeCode.Int64:
                case TypeCode.Decimal: case TypeCode.Double: case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}
