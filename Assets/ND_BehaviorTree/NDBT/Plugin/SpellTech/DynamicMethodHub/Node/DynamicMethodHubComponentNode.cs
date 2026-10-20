////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

using SpellTech.DynamicMethodEvent;

[RequireComponentInRunner(typeof(MethodHub))]
[NodeDescription("Fires an event on the owner's MethodHub component by name (or from a key).", "Uses 'Event ID Key' when assigned, otherwise 'Event ID'.")]
[NodeInfo("Dynamic Method Hub Component Node", "Action/DynamicMethodHubComponentNode", true, false,iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/MethodHub.png")]
public class DynamicMethodHubComponentNode : ActionNode
{
    [Tooltip("The event name to trigger on the MethodHub (used when 'Event ID Key' is not assigned).")]
    public string eventID;

    [BlackboardKeyType(typeof(string))]
    public Key eventIDKey;

    MethodHub animationEventHub;
    [System.NonSerialized] private bool _missingHubLogged;

    protected override void OnEnter()
    {
        // Resolve once per agent (the component lookup searches self, children and parents).
        if (animationEventHub == null)
        {
            GameObject owner = ownerTree != null ? ownerTree.Self : null;
            if (owner != null) animationEventHub = SpellTech.SoraExtensions.CustomHelper.GetComp<MethodHub>(owner);
        }
    }

    protected override Status OnProcess()
    {
        if (animationEventHub == null)
        {
            if (!_missingHubLogged)
            {
                _missingHubLogged = true;
                Debug.LogError($"[DynamicMethodHubComponentNode] '{name}': no MethodHub found on the runner GameObject (or its children/parents); the node fails. (Logged once.)", this);
            }
            return Status.Failure;
        }

        if (eventIDKey != null)
        {
            if (blackboard == null) return Status.Failure;
            string i = blackboard.GetValue<string>(eventIDKey.keyName);
            animationEventHub.TriggerEvent(i);
            return Status.Success;
        }

        animationEventHub.TriggerEvent(eventID);
        return Status.Success;

    }

}
