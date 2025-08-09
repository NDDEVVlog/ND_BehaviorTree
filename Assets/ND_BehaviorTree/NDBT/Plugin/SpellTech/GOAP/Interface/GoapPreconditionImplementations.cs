using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    /// <summary>
    /// A precondition that compares a key's value against a direct value (or another key).
    /// This is the most common type of precondition.
    /// </summary>
    [System.Serializable]
    public class StatePrecondition : IGoapPrecondition
    {
        [Tooltip("The primary value to check. Can be linked to the Blackboard or set directly.")]
        [BlackboardKeyType(typeof(object))]
        public Key value;

        [Tooltip("The comparison to perform.")]
        [FilterComparison(nameof(value), nameof(targetValue))]
        public Key<ComparisonType> compareType;

        [Tooltip("The value to compare against. Its type is determined by the 'Value' field.")]
        [BlackboardKeyType(typeof(object))]
        [DetachKey(nameof(value))]
        public Key targetValue;

        public bool IsMet(GameObject agent, Blackboard blackboard, Dictionary<string, object> worldState)
        {
            if (value == null || compareType == null || targetValue == null)
            {
                Debug.Log($"[StatePrecondition] FAILED: Precondition is not fully configured (a field is null). Description: {GetDescription()}");
                return false;
            }

            // --- Get the first value (val1) ---
            object val1 = null;
            if (!string.IsNullOrEmpty(value.keyName) && worldState.TryGetValue(value.keyName, out var blackboardVal1))
            {
                val1 = blackboardVal1;
            }
            else
            {
                val1 = value.GetValueObject();
            }

            var op = compareType.GetValue();

            // --- Get the second value (val2) ---
            object val2 = null;
            if (!string.IsNullOrEmpty(targetValue.keyName) && worldState.TryGetValue(targetValue.keyName, out var blackboardVal2))
            {
                val2 = blackboardVal2;
            }
            else
            {
                val2 = targetValue.GetValueObject();

            }

            bool result = ComparisonHelper.PerformComparison(val1, val2, op);


            return result;
        }

        public string GetDescription()
        {
            if (value == null || compareType == null || targetValue == null) return "Invalid State";
            var val1Name = string.IsNullOrEmpty(value.keyName) ? $"'{value.GetValueObject()}'" : value.keyName;
            var val2Name = string.IsNullOrEmpty(targetValue.keyName) ? $"'{targetValue.GetValueObject()}'" : targetValue.keyName;
            return $"State: {val1Name} {compareType.GetValue()} {val2Name}";
        }
    }

    /// <summary>
    /// A precondition that compares the live values of two different keys from the blackboard.
    /// </summary>
    [System.Serializable]
    public class KeyComparisonPrecondition : IGoapPrecondition
    {
        [Tooltip("The first key to use in the comparison.")]
        [BlackboardKeyType(typeof(object))]
        public Key keyA;

        [Tooltip("The comparison to perform.")]
        [FilterComparison(nameof(keyA), nameof(keyB))]
        public Key<ComparisonType> compareType;

        [Tooltip("The second key to use in the comparison.")]
        [BlackboardKeyType(typeof(object))]
        [DetachKey(nameof(keyA))]
        public Key keyB;

        public bool IsMet(GameObject agent, Blackboard blackboard, Dictionary<string, object> worldState)
        {
            if (keyA == null || compareType == null || keyB == null)
            {
                return false;
            }
            var op = compareType.GetValue();
            if (!worldState.TryGetValue(keyA.keyName, out object valueA))
            {
                return false;
            }

            if (!worldState.TryGetValue(keyB.keyName, out object valueB))
            {
                valueB = keyB.GetValueObject();
            }


            bool result = ComparisonHelper.PerformComparison(valueA, valueB, op);

            return result;
        }

        public string GetDescription()
        {
            if (keyA == null || compareType == null || keyB == null) return "Invalid Key Comparison";
            string nameA = string.IsNullOrEmpty(keyA.keyName) ? "INVALID_KEY_A" : keyA.keyName;
            string nameB = string.IsNullOrEmpty(keyB.keyName) ? "INVALID_KEY_B" : keyB.keyName;
            return $"Compare Keys: {nameA} {compareType.GetValue()} {nameB}";
        }
    }

    /// <summary>
    /// A precondition that checks the distance between the agent and a target.
    /// </summary>
    [System.Serializable]
    public class DistancePrecondition : IGoapPrecondition
    {
        public enum CompareOp { LessThan, GreaterThan }

        [Tooltip("The target (GameObject or Transform) to measure distance to. Can be a direct value or linked to the blackboard.")]
        [BlackboardKeyType(typeof(Transform))]
        public Key<Transform> target;

        [Tooltip("Should the distance be less than or greater than the specified value?")]
        public CompareOp comparison = CompareOp.LessThan;

        [Tooltip("The distance to compare against.")]
        public float distance = 5.0f;

        public bool IsMet(GameObject agent, Blackboard blackboard, Dictionary<string, object> worldState)
        {
            if (agent == null || target == null) return false;

            object targetValue = null;
            string source = "";
            if (!string.IsNullOrEmpty(target.keyName) && worldState.TryGetValue(target.keyName, out var blackboardTarget))
            {
                targetValue = blackboardTarget;
                source = $"WorldState['{target.keyName}']";
            }
            else
            {
                targetValue = target.GetValueObject();
                source = "Direct Value";
            }

            if (targetValue == null)
            {
                return false;
            }

            Vector3 targetPosition;
            if (targetValue is Transform targetTransform) { targetPosition = targetTransform.position; }
            else if (targetValue is GameObject targetGo) { targetPosition = targetGo.transform.position; }
            else
            {
                Debug.LogWarning($"[DistancePrecondition] FAILED: Target value is not a Transform or GameObject. It is a {targetValue.GetType().Name}.");
                return false;
            }

            float currentDistance = Vector3.Distance(agent.transform.position, targetPosition);
            bool result = (comparison == CompareOp.LessThan) ? currentDistance < distance : currentDistance > distance;

            // --- The main debug log ---
            string resultColor = result ? "lime" : "red";
            Debug.Log($"[DistancePrecondition] Comparing: Distance to '{source}' ({currentDistance:F2}m) {comparison} {distance}m -> <color={resultColor}>{result.ToString().ToUpper()}</color>");

            return result;
        }

        public string GetDescription()
        {
            if (target == null) return "Invalid Distance Check";
            var targetName = string.IsNullOrEmpty(target.keyName) ? "Direct Target" : target.keyName;
            return $"Distance to '{targetName}' {comparison} {distance}m";
        }
    }

    [System.Serializable]
    public class SoraCondition : IGoapPrecondition
    {
        public Animator animator;

        public bool IsMet(GameObject agent, Blackboard blackboard, Dictionary<string, object> worldState)
        {   
            if(animator == null)
            {
                Debug.LogWarning("[SoraCondition] Animator is not set. Cannot perform action.");
            }
            animator = agent.GetComponent<Animator>();
            animator.SetTrigger("SomeTrigger");
            return animator.enabled;
        }

        public string GetDescription()
        {
            return "Sora Condition";
        }
    }
}