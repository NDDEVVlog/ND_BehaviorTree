using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>Plays a line's AudioClip (its Voice, or the localized one) on an AudioSource.</summary>
    [AddComponentMenu("ND Dialogue/Voice/AudioSource Voice Player")]
    public class AudioSourceVoicePlayer : DialogueVoicePlayer
    {
        private const string LogTag = "[AudioSourceVoicePlayer]";

        [SerializeField] private AudioSource m_Source;

        public AudioSource Source { get => m_Source; set => m_Source = value; }

        public override bool CanPlay(DialogueLine line) => line.Voice != null;

        public override float Play(DialogueLine line)
        {
            if (m_Source == null)
            {
                Debug.LogError($"{LogTag} '{name}': {nameof(Play)} rejected: no AudioSource assigned.", this);
                return 0f;
            }
            m_Source.Stop();
            m_Source.clip = line.Voice;
            m_Source.Play();
            return line.Voice.length;
        }

        public override bool IsPlaying => m_Source != null && m_Source.isPlaying;

        public override void Stop()
        {
            if (m_Source != null) m_Source.Stop();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_Source == null) Debug.LogWarning($"{LogTag} '{name}': assign the AudioSource that plays voices.", this);
        }
#endif
    }
}
