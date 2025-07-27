// --- START OF FILE AnimationEventNode.cs ---

using UnityEngine;
using ND_BehaviorTree; // Your behavior tree namespace

[NodeInfo("AnimationEventNode", "Action/AnimationEventNode", true, false,iconPath:null)]
public class AnimationEventNode : ActionNode
{
    [Header("Target Hub")]
    [Tooltip("If checked, the node will get the AnimationEventHub from a Blackboard key. Otherwise, it uses the direct reference below.")]
    public bool useBlackboardForHub = true;

    [Tooltip("Direct reference to the AnimationEventHub component in the scene.")]
    public AnimationEventHub directHub;

    [Tooltip("Blackboard key that stores the AnimationEventHub component.")]
    [BlackboardKeyType(typeof(AnimationEventHub))]
    public Key hubKey;

    [Header("Event Name")]
    [Tooltip("If checked, the node will get the event name from a Blackboard key. Otherwise, it uses the constant value below.")]
    public bool useBlackboardForEventName = false;

    [Tooltip("The constant event name to trigger (must match a name in the hub's list).")]
    public string eventName;
    
    [Tooltip("Blackboard key that stores the event name string.")]
    [BlackboardKeyType(typeof(string))]
    public Key eventNameKey;

    /// <summary>
    /// OnEnter is called only once when the node begins execution.
    /// This is the correct place for instantaneous actions.
    /// </summary>
    protected override void OnEnter()
    {
        // 1. Get the target AnimationEventHub instance
        AnimationEventHub hubInstance = null;
        if (useBlackboardForHub)
        {
            if (hubKey == null || string.IsNullOrEmpty(hubKey.keyName))
            {
                return;
            }
            hubInstance = blackboard.GetValue<AnimationEventHub>(hubKey.keyName);
        }
        else
        {
            hubInstance = directHub;
        }

        // 2. Validate the hub instance
        if (hubInstance == null)
        {
            return;
        }

        // 3. Get the event name string
        string eventToTrigger = "";
        if (useBlackboardForEventName)
        {
            if (eventNameKey == null || string.IsNullOrEmpty(eventNameKey.keyName))
            {

                return;
            }
            eventToTrigger = blackboard.GetValue<string>(eventNameKey.keyName);
        }
        else
        {
            eventToTrigger = eventName;
        }

        // 4. Validate the event name string
        if (string.IsNullOrEmpty(eventToTrigger))
        {
            return;
        }

        // 5. If all checks pass, trigger the event
        hubInstance.TriggerEvent(eventToTrigger);
    }

    /// <summary>
    /// OnProcess is called every frame. Since this is an instantaneous action,
    /// we simply return Success immediately after OnEnter has executed.
    /// </summary>
    protected override Status OnProcess()
    {
         // 1. Get the target AnimationEventHub instance
        AnimationEventHub hubInstance = null;
        if (useBlackboardForHub)
        {
            if (hubKey == null || string.IsNullOrEmpty(hubKey.keyName))
            {
                return Status.Failure; // Return Failure on error
            }
            hubInstance = blackboard.GetValue<AnimationEventHub>(hubKey.keyName);
        }
        else
        {
            hubInstance = directHub;
        }

        // 2. Validate the hub instance
        if (hubInstance == null)
        {
            return Status.Failure; // Return Failure on error
        }

        // 3. Get the event name string
        string eventToTrigger = "";
        if (useBlackboardForEventName)
        {
            if (eventNameKey == null || string.IsNullOrEmpty(eventNameKey.keyName))
            {
                return Status.Failure; // Return Failure on error
            }
            eventToTrigger = blackboard.GetValue<string>(eventNameKey.keyName);
        }
        else
        {
            eventToTrigger = eventName;
        }

        // 4. Validate the event name string
        if (string.IsNullOrEmpty(eventToTrigger))
        {
            return Status.Failure; // Return Failure on error
        }

        // 5. If all checks pass, trigger the event
        hubInstance.TriggerEvent(eventToTrigger);
        
        // 6. Return Success immediately after firing the event.
        return Status.Success;
    }

    // OnExit is not needed for this simple node.
    protected override void OnExit() { }
}
