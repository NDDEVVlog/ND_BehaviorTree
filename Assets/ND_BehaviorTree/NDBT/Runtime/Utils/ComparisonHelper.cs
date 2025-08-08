// FILE: ComparisonHelper.cs

using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    public static class ComparisonHelper
    {
        /// <summary>
        /// A robust helper method to compare two objects of potentially different types.
        /// </summary>
        public static bool PerformComparison(object val1, object val2, ComparisonType op)
        {
            // --- A. Handle Nulls ---
            if (val1 == null || val2 == null)
            {
                switch (op)
                {
                    case ComparisonType.Equal: return val1 == val2;
                    case ComparisonType.NotEqual: return val1 != val2;
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
            if (val1 is IComparable comparable1)
            {
                try
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
                catch (ArgumentException)
                {
                    if (op == ComparisonType.Equal) return val1.Equals(val2);
                    if (op == ComparisonType.NotEqual) return !val1.Equals(val2);
                    return false;
                }
            }
            
            // --- D. Fallback for non-IComparable types ---
            switch (op)
            {
                case ComparisonType.Equal: return val1.Equals(val2);
                case ComparisonType.NotEqual: return !val1.Equals(val2);
                default: return false;
            }
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