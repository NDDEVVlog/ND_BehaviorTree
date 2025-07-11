////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;
[NodeInfo("AnimationEventAction", "Action/AnimationEventAction", true, false,iconPath:null)]
public class AnimationEventAction : ActionNode
{
    // --- Public Parameters ---
    // These variables will be exposed in the Inspector.
    // Use [Tooltip] for clear instructions.
    [Tooltip("An example float parameter.")]
    public string animationID;
    private AnimationEventHub animationEventHub;
    protected override void OnEnter()
    {
        animationEventHub = ownerTree.self.GetComponent<AnimationEventHub>();
    }

    protected override Status OnProcess()
    {
        animationEventHub?.TriggerEvent(animationID);
        return Status.Running;
    }

    protected override void OnExit()
    {

    }
}