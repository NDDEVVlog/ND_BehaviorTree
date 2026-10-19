

using UnityEngine;

namespace ND_BehaviorTree
{   
    [NodeInfo("Check Condition Variable", "Decorator/CheckConditionVariable", true, true, iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/CheckCondition.png")]
    public class CheckConditionVariable : DecoratorNode
    {



        [Tooltip("The Blackboard key (of type bool) to check.")]
        [BlackboardKeyType(typeof(bool))]
        public Key conditionKey;
        
        [Tooltip("If checked, the result of the condition will be inverted. (e.g., 'Is False' instead of 'Is True').")]
        public bool shouldInvert;

       

        protected override Status OnProcess()
        {
            // --- 1. Validate Configuration ---
            // A decorator without a condition to check is a configuration error.
            if (conditionKey == null || blackboard == null)
            {
                StopChild();
                return Status.Failure;
            }

            // --- 2. Get the Condition Value ---
            // Get the boolean value from the blackboard.
            bool conditionValue = blackboard.GetValue<bool>(conditionKey.keyName);

            bool conditionPassed = (conditionValue != shouldInvert);

            // --- 4. Execute Logic Based on Result ---
            if (conditionPassed)
            {

                if (child != null)
                {
                    // CRITICAL FIX: Only call Process() once and store the result.
                    return child.Process();
                }
                return Status.Success;
            }
            else
            {
                // The condition failed. Do not run the child; stop it if it was running. The decorator fails.
                StopChild();
                return Status.Failure;
            }
        }

        /// <summary>
        /// A child left Running when the condition flips would otherwise resume later without OnEnter
        /// (and never get OnExit). Interrupt it and reset it so it restarts cleanly next time.
        /// </summary>
        private void StopChild()
        {
            if (child == null || !child.IsRunning()) return;
            child.InteruptAction?.Invoke();
            child.Reset();
        }
    }
}
