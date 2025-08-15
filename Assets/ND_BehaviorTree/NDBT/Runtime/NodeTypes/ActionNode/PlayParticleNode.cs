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
    private float _startTime;
    private float _timeout = 10f; // Max duration in seconds

    protected override void OnEnter()
    {
        _runtimeParticleSystem = particleSystemKey.GetValue(blackboard);
        _startTime = Time.time;
    }

    protected override Status OnProcess()
    {
        if (_runtimeParticleSystem == null)
        {
            return Status.Failure;
        }

        if (!_runtimeParticleSystem.isPlaying)
        {
            _runtimeParticleSystem.Play(withChildren);
        }

        if (waitForCompletion)
        {
            if (Time.time - _startTime > _timeout)
            {
                Debug.LogWarning($"ParticleSystem '{_runtimeParticleSystem.name}' timed out after {_timeout} seconds.");
                return Status.Failure;
            }
            return _runtimeParticleSystem.IsAlive(withChildren) ? Status.Running : Status.Success;
        }
        return Status.Success;
    }

    // OnExit is called once when the node's status is no longer 'Running'.
    // Use it for cleanup.
    protected override void OnExit()
    {
        // Not much to clean up here, as we have a dedicated Stop node.
    }
}