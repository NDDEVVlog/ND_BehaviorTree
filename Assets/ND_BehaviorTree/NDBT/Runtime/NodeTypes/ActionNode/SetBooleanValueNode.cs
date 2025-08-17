////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;
using Key = ND_BehaviorTree.Key;

/// <summary>
/// Action nodes are the leaves of the tree. They perform the actual work, 
/// such as moving, attacking, or playing an animation. They do not have 
/// children and return a status of Success, Failure, or Running.
/// </summary>
[NodeInfo("SetBooleanValueNode", "Action/SetBooleanValueNode", true, false,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/boolean.png")]
public class SetBooleanValueNode : ActionNode
{

    [BlackboardKeyType(typeof(bool))]
    [Tooltip("The Blackboard key (of type bool) to set value.")]
    public Key conditionKey;

    public bool ValueToSet;
   
    protected override Status OnProcess()
    {   
         if (conditionKey == null)
        {
            return Status.Failure;
        }

        blackboard.SetValue<bool>(conditionKey.keyName,ValueToSet);

        // Return Running while the action is in progress.
        return Status.Success;

    }

}