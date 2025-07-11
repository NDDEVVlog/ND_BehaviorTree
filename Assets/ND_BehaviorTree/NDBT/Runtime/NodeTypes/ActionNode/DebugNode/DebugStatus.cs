////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// Action nodes are the leaves of the tree. They perform the actual work, 
/// such as moving, attacking, or playing an animation. They do not have 
/// children and return a status of Success, Failure, or Running.
/// </summary>
[NodeInfo("DebugStatus", "Action/Debug/DebugStatus", true, false,iconPath:"Assets/ND_BehaviorTree/NDBT/Icons/CheckList.png")]
public class DebugStatus : ActionNode
{
    public Status returningStatus;


    protected override Status OnProcess()
    {
        // Return Running while the action is in progress.
        return returningStatus;

    }

}