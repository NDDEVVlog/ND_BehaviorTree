// FILE: SetIntValueIncrement.cs

using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// An action that reads an integer from the blackboard, adds a value to it,
    /// and then writes the result back to the same key.
    /// </summary>
    [System.Serializable]
    public class SetIntValueIncrement : ISetBlackBoardValue
    {   
        [Tooltip("The integer key on the Blackboard to read from and write to.")]
        [BlackboardKeyType(typeof(int))]
        public Key intKey; 

        [Tooltip("The amount to add to the key's current value. Use a negative number to decrement.")]
        public int incrementAmount = 1;

        public bool TrySetValue(GameObject owwnerTree,Blackboard blackboard)
        {
            // 1. Guard against missing blackboard or unassigned key
            if (blackboard == null || intKey == null)
            {
                return false;
            }

            // 2. Read the current value from the blackboard
            int currentValue = blackboard.GetValue<int>(intKey.keyName);

            // 3. Calculate the new value
            int newValue = currentValue + incrementAmount;

            // 4. Write the new value back to the blackboard and return the result
            return blackboard.SetValue<int>(intKey.keyName, newValue);
        }
    }
}