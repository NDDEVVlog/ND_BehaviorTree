// FILE: SetFloatValue.cs

using UnityEngine;

namespace ND_BehaviorTree
{
    // A concrete implementation for setting a float value.
    [System.Serializable] // Important for serialization with SerializeReference
    public class SetFloatValue : ISetBlackBoardValue
    {
        // This is a placeholder for the real key. We will use its name to find the key on the blackboard.
        [Tooltip("The name of the float key on the Blackboard to modify.")]
        [BlackboardKeyType(typeof(float))]
        public Key floatKey;

        [Tooltip("The new float value to set.")]
        public float valueToSet;

        public bool TrySetValue(GameObject owwnerTree,Blackboard blackboard)
        {
            if (blackboard == null || string.IsNullOrEmpty(floatKey.keyName))
            {
                return false;
            }
            // Use the generic SetValue for type safety.
            return blackboard.SetValue<float>(floatKey.keyName, valueToSet);
        }
    }
}