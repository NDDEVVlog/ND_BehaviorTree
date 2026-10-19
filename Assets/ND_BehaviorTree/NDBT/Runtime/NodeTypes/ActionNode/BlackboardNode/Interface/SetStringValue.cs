using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [System.Serializable]
    public class SetStringValue : ISetBlackBoardValue
    {
        [BlackboardKeyType(typeof(string))]
        public Key stringKey;


        public string stringValue ;

        public bool TrySetValue(GameObject ownerTree, Blackboard blackboard)
        {
            if (blackboard == null || stringKey == null || string.IsNullOrEmpty(stringKey.keyName))
            {
                return false;
            }

            return blackboard.SetValue<string>(stringKey.keyName, stringValue);
        }
    }
}
