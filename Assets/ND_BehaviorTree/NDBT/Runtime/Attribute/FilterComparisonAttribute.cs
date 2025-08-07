// FILE: FilterComparisonAttribute.cs

using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Attribute used on a ComparisonType Key to dynamically filter the available
    /// comparison options based on the types of two other Key fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FilterComparisonAttribute : PropertyAttribute
    {
        public readonly string key1FieldName;
        public readonly string key2FieldName;

        public FilterComparisonAttribute(string key1FieldName, string key2FieldName)
        {
            this.key1FieldName = key1FieldName;
            this.key2FieldName = key2FieldName;
        }
    }
}