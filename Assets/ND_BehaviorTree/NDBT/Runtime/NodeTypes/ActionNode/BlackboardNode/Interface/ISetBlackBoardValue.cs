using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    public interface ISetBlackBoardValue
    {
        public bool TrySetValue(GameObject ownerTree,Blackboard blackboard);
    }
}
