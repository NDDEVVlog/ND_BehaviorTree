using System;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>
    /// A line's voice from an audio middleware (an FMOD event…), stored on the line with [SerializeReference]. The core
    /// knows no middleware: an integration assembly (compiled only when that middleware is installed, e.g. ND.Dialogue.FMOD)
    /// subclasses it and adds a <see cref="DialogueVoicePlayer"/> that plays it. The editor lists every subclass.
    /// </summary>
    [Serializable]
    public abstract class DialogueVoiceCue
    {
        /// <summary>Short label for the editor and the test chat (e.g. the event path).</summary>
        public abstract string Describe();
    }

    /// <summary>
    /// Plays the voice of a line for a dialogue UI. A UI holds a list of players; the first that <see cref="CanPlay"/> a
    /// line plays it (an AudioClip player, an FMOD player…).
    /// </summary>
    public abstract class DialogueVoicePlayer : MonoBehaviour
    {
        /// <summary>This player plays this line's voice (its clip, its cue type…).</summary>
        public abstract bool CanPlay(DialogueLine line);

        /// <summary>Starts the line's voice. Returns its length in seconds, or 0 when unknown (looping, streaming…).</summary>
        public abstract float Play(DialogueLine line);

        /// <summary>The voice started by <see cref="Play"/> is still sounding.</summary>
        public abstract bool IsPlaying { get; }

        public abstract void Stop();
    }
}
