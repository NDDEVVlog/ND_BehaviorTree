////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using ND_BehaviorTree;

/// <summary>
/// An Action Node that plays a Particle System retrieved from the Blackboard.
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

    [Tooltip("Max duration in seconds to wait for completion before failing. Prevents infinite loops on looping particle systems.")]
    public float timeout = 10f;

    // --- Private Runtime Variables ---
    private ParticleSystem _runtimeParticleSystem;
    private float _startTime;

    // --- Lifecycle Methods ---

    // Called once when the node is first processed.
    protected override void OnEnter()
    {
        _runtimeParticleSystem = particleSystemKey.GetValue(blackboard);

        if (_runtimeParticleSystem == null)
        {
            Debug.LogWarning($"PlayParticleNode: ParticleSystem with key '{particleSystemKey.keyName}' not found in Blackboard.");
            return;
        }

        _runtimeParticleSystem.Play(withChildren);

        if (waitForCompletion)
        {
            _startTime = Time.time;
        }
    }

    // Called every frame while the node is 'Running'.
    protected override Status OnProcess()
    {
        if (_runtimeParticleSystem == null)
        {
            return Status.Failure;
        }

        if (!waitForCompletion)
        {
            return Status.Success;
        }

        // --- Logic for waiting ---

        if (Time.time - _startTime > timeout)
        {
            Debug.LogWarning($"PlayParticleNode: ParticleSystem '{_runtimeParticleSystem.name}' timed out after {timeout} seconds.");
            return Status.Failure;
        }

        return _runtimeParticleSystem.IsAlive(withChildren) ? Status.Running : Status.Success;
    }

    // Called once when the node's status is no longer 'Running'.
    protected override void OnExit()
    {
        _runtimeParticleSystem = null;
    }
}