// FILE: SetBlackBoardValue.cs

using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("SetBlackBoardValue", "Action/SetBlackBoardValue", true, false, iconPath: null)]
    public class SetBlackBoardValue : ActionNode
    {   
        [SerializeReference]
        public ISetBlackBoardValue setBlackBoardValue;


        protected override Status OnProcess()
        {
            if (setBlackBoardValue == null)
            {
                Debug.LogWarning("No 'Set Value' operation is assigned in the SetBlackBoardValue node.", this);
                return Status.Failure;
            }

            if (blackboard == null)
            {
                Debug.LogError("Blackboard is missing from the tree, cannot set value.", this);
                return Status.Failure;
            }

            bool success = setBlackBoardValue.TrySetValue(this.GetOwnerTreeGameObject(),this.blackboard);

            return success ? Status.Success : Status.Failure;
        }

    }
}