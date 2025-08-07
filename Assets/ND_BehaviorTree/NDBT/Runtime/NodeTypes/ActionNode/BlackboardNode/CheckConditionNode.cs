// FILE: CheckConditionNode.cs (CORRECTED)
using System;
using UnityEngine;

namespace ND_BehaviorTree
{   [NodeInfo("Check Condition", "Action/Blackboard/CheckCondition", true, false)]
    public class CheckConditionNode : ActionNode
    {
        [Tooltip("The primary value to check.")]
        [BlackboardKeyType(typeof(object))] // <-- THIS IS THE FIX
        public Key value;

        [Tooltip("The comparison to perform.")]
        [FilterComparison(nameof(value), nameof(targetValue))]
        public Key<ComparisonType> compareType;

        [Tooltip("The value to compare against. Its type is determined by the 'Value' field.")]
        [DetachKey(nameof(value))]
        public Key targetValue; // No attribute needed here, it gets its type from 'value'

        protected override Status OnProcess()
        {
            if (value == null || compareType == null || targetValue == null) return Status.Failure;
            
            var blackboardKey1 = !string.IsNullOrEmpty(value.keyName) ? blackboard.keys.Find(a => a.keyName == value.keyName) : null;
            object val1 = (blackboardKey1 != null) ? blackboardKey1.GetValueObject() : value.GetValueObject();
            
            var op = compareType.GetValue();
            
            var blackboardKey2 = !string.IsNullOrEmpty(targetValue.keyName) ? blackboard.keys.Find(a => a.keyName == targetValue.keyName) : null;
            object val2 = (blackboardKey2 != null) ? blackboardKey2.GetValueObject() : targetValue.GetValueObject();

            return ComparisonHelper.PerformComparison(val1, val2, op) ? Status.Success : Status.Failure;
        }
    }
}