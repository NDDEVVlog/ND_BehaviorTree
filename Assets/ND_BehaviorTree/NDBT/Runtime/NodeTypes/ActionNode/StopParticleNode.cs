////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// An Action Node that stops a Particle System.
/// It retrieves the ParticleSystem from the Blackboard.
/// </summary>
[NodeInfo("Stop Particle", "Action/VFX/StopParticle", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/StopEffect.png")]
public class StopParticleNode : ActionNode
{
    // --- Public Parameters ---

    [Tooltip("The Blackboard key for the ParticleSystem that will be stopped.")]
    public Key<ParticleSystem> particleSystemKey;

    [Tooltip("If true, any child Particle Systems will also be stopped.")]
    public bool withChildren = true;

    [Tooltip("The manner in which to stop the Particle System. 'StopEmitting' allows existing particles to complete their lifecycle, while 'StopEmittingAndClear' removes them immediately.")]
    public ParticleSystemStopBehavior stopMode = ParticleSystemStopBehavior.StopEmitting;

    // --- Lifecycle Methods ---
    
    // This node is simple enough that it doesn't require complex OnEnter or OnExit logic.

    // OnProcess is called every frame while the node is 'Running'.
    protected override Status OnProcess()
    {
        // Get the ParticleSystem from the blackboard.
        ParticleSystem ps = particleSystemKey.GetValue(blackboard);

        // If no ParticleSystem is found, the node fails.
        if (ps == null)
        {
            return Status.Failure;
        }

        // Issue the stop command.
        ps.Stop(withChildren, stopMode);

        // The stop action is considered successful immediately.
        return Status.Success;
    }
}