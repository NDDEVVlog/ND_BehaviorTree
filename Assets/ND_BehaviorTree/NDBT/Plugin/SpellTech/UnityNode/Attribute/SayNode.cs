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
[NodeInfo("SayNode", "Action/SayNode", true, false, iconPath: null)]
[CustomNodeTitleProperty("[GameObjectKey] say [quote]")]
public class SayNode : ActionNode
{

    public Key<GameObject> GameObjectKey;
    public string quote;



    protected override void OnEnter()
    {

    }

    protected override Status OnProcess()
    {
        return Status.Running;
    }
    protected override void OnExit()
    {

    }
}