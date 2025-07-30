////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;
using UnityEditor.UI;

/// <summary>
/// Action nodes are the leaves of the tree. They perform the actual work, 
/// such as moving, attacking, or playing an animation. They do not have 
/// children and return a status of Success, Failure, or Running.
/// </summary>
[RequireComponentInRunner(typeof(AnimationEventHub))]
[NodeInfo("AnimationHubComponentNode", "Action/AnimationHubComponentNode", true, false,iconPath:null)]
public class AnimationHubComponentNode : ActionNode
{
    [Tooltip("An example float parameter.")]
    public string eventID;

    [BlackboardKeyType(typeof(string))]
    public Key eventIDKey;

    AnimationEventHub animationEventHub;
    protected override void OnEnter()
    {
        animationEventHub = ownerTree.Self.GetComponent<AnimationEventHub>();
    }

    // OnProcess is called every frame while the node is in a 'Running' state.
    // This is where the core logic of the action resides.
    protected override Status OnProcess()
    {

        if (eventIDKey != null)
        {   

            string i = blackboard.GetValue<string>(eventIDKey.keyName);
            animationEventHub.TriggerEvent(i);
            return Status.Success;
        }

        animationEventHub.TriggerEvent(eventID);
        return Status.Success;

    }

    // OnExit is called once when the node's status is no longer 'Running'.
    // Use it for cleanup, regardless of whether the node succeeded, failed, or was aborted.
    protected override void OnExit()
    {

    }
}