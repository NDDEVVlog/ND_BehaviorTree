using System;
using FMODUnity;
using UnityEngine;

namespace ND.Dialogue.Fmod
{
    /// <summary>A line's voice as an FMOD event. Played by <see cref="FmodVoicePlayer"/>.</summary>
    [Serializable]
    public class FmodVoiceCue : DialogueVoiceCue
    {
        [Tooltip("The FMOD event of this line (e.g. event:/VO/Malfoy/Hmph).")]
        public EventReference voiceEvent;

        public override string Describe()
        {
            if (voiceEvent.IsNull) return "FMOD: (no event)";
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(voiceEvent.Path)) return "FMOD: " + voiceEvent.Path;
#endif
            return "FMOD: " + voiceEvent.Guid;
        }
    }
}
