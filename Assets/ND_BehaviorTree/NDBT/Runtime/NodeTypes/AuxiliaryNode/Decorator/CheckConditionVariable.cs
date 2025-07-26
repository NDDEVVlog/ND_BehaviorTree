

using UnityEngine;

namespace ND_BehaviorTree
{   
    [NodeInfo("Check Condition Variable", "Decorator/CheckConditionVariable", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/CheckCondition.png")]
    public class CheckConditionVariable : DecoratorNode
    {
        [Tooltip("The Blackboard key (of type bool) to check.")]
        public Key conditionKey;
        
        [Tooltip("If checked, the result of the condition will be inverted. (e.g., 'Is False' instead of 'Is True').")]
        public bool shouldInvert;

        protected override Status OnProcess()
        {
            // --- 1. Validate Configuration ---
            // A decorator without a condition to check is a configuration error.
            if (conditionKey == null)
            {
                return Status.Failure;
            }

            // --- 2. Get the Condition Value ---
            // Get the boolean value from the blackboard.
            bool conditionValue = blackboard.GetValue<bool>(conditionKey.keyName);

            // --- 3. Determine if the Condition Passes (with Inversion) ---
            // This is a clean way to handle the inversion.
            // - If shouldInvert is false, we pass if conditionValue is true. (true != false) -> true
            // - If shouldInvert is true, we pass if conditionValue is false. (false != true) -> true
            bool conditionPassed = (conditionValue != shouldInvert);

            // --- 4. Execute Logic Based on Result ---
            if (conditionPassed)
            {
                // The condition passed. If there is a child, run it and return its status.
                // If there's no child, the decorator itself succeeded.
                if (child != null)
                {
                    // CRITICAL FIX: Only call Process() once and store the result.
                    return child.Process();
                }
                return Status.Success;
            }
            else
            {
                //child.InteruptAction?.Invoke();
                // The condition failed. Do not run the child. The decorator fails.
                return Status.Failure;
            }
        }
    }
}