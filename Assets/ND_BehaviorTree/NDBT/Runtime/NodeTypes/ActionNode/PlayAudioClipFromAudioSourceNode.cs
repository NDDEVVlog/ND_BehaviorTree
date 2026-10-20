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
[NodeDescription("Plays an AudioClip key on an AudioSource key.", "With 'Wait For Completion' it stays Running until the clip ends.")]
[NodeInfo("PlayAudioClipFromAudioSourceNode", "Action/PlayAudioClipFromAudioSourceNode", true, false,iconPath:null)]
public class PlayAudioClipFromAudioSourceNode : ActionNode
{
    
    public Key<AudioSource> audioSourceKey;

    public Key<AudioClip> audioClipKey;
    public bool waitForCompletion = false;

    // --- Private Runtime Variables ---
    private AudioSource _runtimeAudioSource;
    private bool _hasStartedPlaying = false;


    [System.NonSerialized] private bool _configErrorLogged;

    protected override void OnEnter()
    {
        _hasStartedPlaying = false;
        _runtimeAudioSource = null;
        if (audioSourceKey == null || blackboard == null)
        {
            if (!_configErrorLogged)
            {
                _configErrorLogged = true;
                Debug.LogError($"[PlayAudioClipFromAudioSourceNode] '{name}': {(audioSourceKey == null ? "audioSourceKey is not assigned" : "the tree has no blackboard")}; the node fails. (Logged once.)", this);
            }
            return; // OnProcess fails because _runtimeAudioSource is null
        }
        _runtimeAudioSource = blackboard.GetValue<AudioSource>(audioSourceKey.keyName);
    }

    // OnProcess is called every frame while the node is in a 'Running' state.
    // This is where the core logic of the action resides.
    protected override Status OnProcess()
    {   
        if (_runtimeAudioSource == null)
        {
            return Status.Failure;
        }

        if (!_hasStartedPlaying)
        {

            // No clip key assigned: fall back to the source's own clip (documented behaviour below).
            AudioClip clipToPlay = audioClipKey != null ? audioClipKey.GetValue(blackboard) : null;
            if (clipToPlay == null)
            {
                clipToPlay = _runtimeAudioSource.clip;
            }

            if (clipToPlay == null)
            {
                return Status.Failure;
            }


            _runtimeAudioSource.PlayOneShot(clipToPlay);
            _hasStartedPlaying = true;
        }


        if (waitForCompletion)
        {
            
            if (_runtimeAudioSource.isPlaying)
            {
                return Status.Running; 
            }
            else
            {
                return Status.Success; 
            }
        }
        else
        {

            return Status.Success;
        }
    }

    // OnExit is called once when the node's status is no longer 'Running'.
    // Use it for cleanup, regardless of whether the node succeeded, failed, or was aborted.
    protected override void OnExit()
    {
        _hasStartedPlaying = false;
    }
}