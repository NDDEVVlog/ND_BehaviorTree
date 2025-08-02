// FILE: SetIntValue.cs

using UnityEngine;

namespace ND_BehaviorTree
{
    [System.Serializable]
    public class SetIntValue : ISetBlackBoardValue
    {   
        [Tooltip("The integer key on the Blackboard to modify.")]
        [BlackboardKeyType(typeof(int))]
        public Key intKey; 

        [Tooltip("The new integer value to set.")]
        public int valueToSet;

        public bool TrySetValue(Blackboard blackboard)
        {
            if (blackboard == null || intKey == null)
            {
                return false;
            }
            return blackboard.SetValue<int>(intKey.keyName, valueToSet);
        }
    }
}