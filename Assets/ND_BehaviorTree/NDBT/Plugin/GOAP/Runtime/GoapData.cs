using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    /// <summary>
    /// Marks a GOAP key field: the editor shows a picker of the tree's blackboard keys and stores the reference
    /// (never a hidden "local" key asset).
    /// </summary>
    public sealed class GoapKeyAttribute : PropertyAttribute
    {
        public readonly bool NumbersOnly;
        public GoapKeyAttribute(bool numbersOnly = false) { NumbersOnly = numbersOnly; }
    }

    /// <summary>
    /// A value typed by the blackboard key it goes with: only the field matching the key's value type is used.
    /// Supported: bool, int, long, float, double, string, Vector2, Vector3 and UnityEngine.Object types (GameObject, Transform…).
    /// </summary>
    [Serializable]
    public struct GoapValue
    {
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public Vector3 vectorValue;
        public UnityEngine.Object objectValue;

        public static bool IsSupported(Type type) =>
            type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) ||
            type == typeof(string) || type == typeof(Vector2) || type == typeof(Vector3) || typeof(UnityEngine.Object).IsAssignableFrom(type);

        /// <summary>The value as <paramref name="type"/> (the key's value type); null for an unsupported type.</summary>
        public object Get(Type type)
        {
            if (type == typeof(bool)) return boolValue;
            if (type == typeof(int)) return intValue;
            if (type == typeof(long)) return (long)intValue;
            if (type == typeof(float)) return floatValue;
            if (type == typeof(double)) return (double)floatValue;
            if (type == typeof(string)) return stringValue ?? string.Empty;
            if (type == typeof(Vector2)) return (Vector2)vectorValue;
            if (type == typeof(Vector3)) return vectorValue;
            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type)) return objectValue;
            return null;
        }

        public string ToString(Type type)
        {
            var value = Get(type);
            if (value is UnityEngine.Object o) return o != null ? o.name : "None";
            if (value is bool b) return b ? "true" : "false";
            return value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : "?";
        }
    }

    /// <summary>"key comparison value", e.g. HasAxe == true, Logs &gt;= 50. Keys are blackboard keys of the tree.</summary>
    [Serializable]
    public class KeyCondition
    {
        [GoapKey] public Key key;
        public ComparisonType comparison = ComparisonType.Equal;
        public GoapValue value;

        public override string ToString() => key == null ? "(no key)" : $"{key.keyName} {Symbol(comparison)} {value.ToString(key.GetValueType())}";

        /// <summary>
        /// Evaluates the condition on <paramref name="blackboard"/> (looked up by key name, e.g. an agent's runtime
        /// blackboard). False when the key is unset or missing there.
        /// </summary>
        public bool TryEvaluate(Blackboard blackboard, out bool met)
        {
            met = false;
            if (key == null || blackboard == null || !blackboard.TryGetKey(key.keyName, out var live)) return false;
            met = ComparisonHelper.PerformComparison(live.GetValueObject(), value.Get(live.GetValueType()), comparison);
            return true;
        }

        public static string Symbol(ComparisonType op)
        {
            switch (op)
            {
                case ComparisonType.Equal: return "==";
                case ComparisonType.NotEqual: return "!=";
                case ComparisonType.Greater: return ">";
                case ComparisonType.GreaterEqual: return ">=";
                case ComparisonType.Less: return "<";
                default: return "<=";
            }
        }
    }

    public enum GoapEffectOperation
    {
        [Tooltip("key = value")] Set,
        [Tooltip("key = key + value (numbers only)")] Add,
    }

    /// <summary>What an action changes, e.g. HasAxe = true, Logs += 10. Used to plan, and written to the blackboard when the action succeeds.</summary>
    [Serializable]
    public class KeyEffect
    {
        [GoapKey] public Key key;
        public GoapEffectOperation operation = GoapEffectOperation.Set;
        public GoapValue value;

        public override string ToString() =>
            key == null ? "(no key)" : $"{key.keyName} {(operation == GoapEffectOperation.Add ? "+=" : "=")} {value.ToString(key.GetValueType())}";
    }

    /// <summary>A goal: all conditions must hold. The planner pursues the highest-priority goal that is not met and can be planned.</summary>
    [Serializable]
    public class GoapGoal
    {
        public string name = "Goal";
        public List<KeyCondition> conditions = new List<KeyCondition>();
        [Tooltip("Higher runs first. Ignored when a Priority Key is set.")]
        public float priority = 1f;
        [Tooltip("Optional number key (int / float) read as the priority each time goals are evaluated, e.g. Hunger.")]
        [GoapKey(numbersOnly: true)] public Key priorityKey;
    }
}
