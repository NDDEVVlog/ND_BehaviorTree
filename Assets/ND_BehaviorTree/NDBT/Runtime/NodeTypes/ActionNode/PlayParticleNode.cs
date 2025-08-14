////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// An Action Node that plays a Particle System.
/// It retrieves the ParticleSystem from the Blackboard.
/// </summary>
[NodeInfo("Play Particle", "Action/VFX/PlayParticle", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Effect.png")]
public class PlayParticleNode : ActionNode
{
    // --- Public Parameters ---

    [Tooltip("The Blackboard key for the ParticleSystem that will be played.")]
    public Key<ParticleSystem> particleSystemKey;

    [Tooltip("If true, any child Particle Systems will also be played.")]
    public bool withChildren = true;

    [Tooltip("If true, the node will remain in the 'Running' state until the Particle System finishes. If false, it returns 'Success' immediately.")]
    public bool waitForCompletion = false;

    // --- Private Runtime Variables ---
    private ParticleSystem _runtimeParticleSystem;

    // --- Lifecycle Methods ---

    // OnEnter is called once when the node is first processed.
    // Use it for initialization and setup.
    protected override void OnEnter()
    {
        // Get the ParticleSystem from the blackboard once for optimization.
        _runtimeParticleSystem = particleSystemKey.GetValue(blackboard);
    }

    // OnProcess is called every frame while the node is in a 'Running' state.
    // This is where the core logic of the action resides.
    protected override Status OnProcess()
    {
        // If there's no ParticleSystem, the node fails.
        if (_runtimeParticleSystem == null)
        {
            return Status.Failure;
        }

        // Start playing the effect (if it's not already playing).
        if (!_runtimeParticleSystem.isPlaying)
        {
            _runtimeParticleSystem.Play(withChildren);
        }

        // Decide on the return status.
        if (waitForCompletion)
        {
            // Check if the Particle System is still alive.
            // IsAlive() is more reliable than isPlaying() when looping is disabled.
            if (_runtimeParticleSystem.IsAlive(withChildren))
            {
                return Status.Running; // Still active.
            }
            else
            {
                return Status.Success; // Finished.
            }
        }
        else
        {
            // "Fire and forget", return Success immediately.
            return Status.Success;
        }
    }

    // OnExit is called once when the node's status is no longer 'Running'.
    // Use it for cleanup.
    protected override void OnExit()
    {
        // Not much to clean up here, as we have a dedicated Stop node.
    }
}