using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public class SetStringValue : ISetBlackBoardValue
    {
        [BlackboardKeyType(typeof(string))]
        public Key stringKey; 


        public string stringValue ;

        public bool TrySetValue(GameObject ownerTree, Blackboard blackboard)
        {
              
            if (blackboard == null || string.IsNullOrEmpty(stringValue))
            {
                return false;
            }

            string currentValue = blackboard.GetValue<string>(stringKey.keyName);

            return blackboard.SetValue<string>(stringKey.keyName, currentValue);
        }
    }
}
