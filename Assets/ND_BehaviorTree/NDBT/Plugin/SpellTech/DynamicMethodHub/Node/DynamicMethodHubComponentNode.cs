////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;
using UnityEditor.UI;
using SpellTech.DynamicMethodEvent;

/// <summary>
/// Action nodes are the leaves of the tree. They perform the actual work, 
/// such as moving, attacking, or playing an animation. They do not have 
/// children and return a status of Success, Failure, or Running.
/// </summary>
[RequireComponentInRunner(typeof(MethodHub))]
[NodeInfo("Dynamic Method Hub Component Node", "Action/DynamicMethodHubComponentNode", true, false,iconPath:null)]
public class DynamicMethodHubComponentNode : ActionNode
{
    [Tooltip("An example float parameter.")]
    public string eventID;

    [BlackboardKeyType(typeof(string))]
    public Key eventIDKey;

    MethodHub animationEventHub;
    protected override void OnEnter()
    {   
        //Simple and quick. Yes sirrrr
        animationEventHub = SpellTech.SoraExtensions.CustomHelper.GetComp<MethodHub>(ownerTree.Self);
    }

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

}