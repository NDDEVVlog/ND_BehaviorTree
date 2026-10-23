using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using StopMode = FMOD.Studio.STOP_MODE; // FMODUnity has its own STOP_MODE (Timeline playables)

namespace ND.Dialogue.Fmod
{
    /// <summary>
    /// Plays lines whose voice is an <see cref="FmodVoiceCue"/>: one event instance per line, stopped (with its fade-out)
    /// when the next line starts or the conversation ends. Put it in the dialogue UI's Voice Players list.
    /// </summary>
    [AddComponentMenu("ND Dialogue/Voice/FMOD Voice Player")]
    public class FmodVoicePlayer : DialogueVoicePlayer
    {
        private const string LogTag = "[FmodVoicePlayer]";

        [Tooltip("Stop with the event's fade-out (AHDSR) instead of cutting it.")]
        [SerializeField] private bool m_AllowFadeOut = true;

        private EventInstance m_Instance;

        public override bool CanPlay(DialogueLine line) => line.VoiceCue is FmodVoiceCue cue && !cue.voiceEvent.IsNull;

        public override float Play(DialogueLine line)
        {
            Stop();
            var cue = (FmodVoiceCue)line.VoiceCue;
            try
            {
                m_Instance = RuntimeManager.CreateInstance(cue.voiceEvent);
            }
            catch (EventNotFoundException e)
            {
                Debug.LogError($"{LogTag} '{name}': {cue.Describe()} of '{line.Conversation?.title}' #{line.Entry?.id} was not found in the loaded banks: {e.Message}", this);
                return 0f;
            }
            m_Instance.start();
            RuntimeManager.GetEventDescription(cue.voiceEvent).getLength(out int milliseconds);
            return milliseconds > 0 ? milliseconds / 1000f : 0f; // 0 = looping or unknown length
        }

        public override bool IsPlaying =>
            m_Instance.isValid() && m_Instance.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK && state != PLAYBACK_STATE.STOPPED;

        public override void Stop()
        {
            if (!m_Instance.isValid()) return;
            m_Instance.stop(m_AllowFadeOut ? StopMode.ALLOWFADEOUT : StopMode.IMMEDIATE);
            m_Instance.release();
            m_Instance.clearHandle();
        }

        private void OnDestroy() => Stop();
    }
}
