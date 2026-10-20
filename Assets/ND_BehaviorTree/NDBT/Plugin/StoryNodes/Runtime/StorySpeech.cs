using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Story
{
    /// <summary>
    /// Speech bubbles shown by <see cref="SaysNode"/>. Drawn by a <see cref="StorySpeechRenderer"/> in the scene
    /// (add one to any GameObject); without it, Says nodes still log to the Console.
    /// </summary>
    public static class StorySpeech
    {
        public struct Bubble
        {
            public GameObject Speaker;
            public string Text;
            public float Until;
        }

        private static readonly List<Bubble> s_Bubbles = new List<Bubble>();
        public static IReadOnlyList<Bubble> Active => s_Bubbles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Bubbles.Clear();

        /// <summary>Shows <paramref name="text"/> above <paramref name="speaker"/> for <paramref name="seconds"/> (replaces its previous bubble).</summary>
        public static void Say(GameObject speaker, string text, float seconds)
        {
            if (speaker == null) return;
            for (int i = s_Bubbles.Count - 1; i >= 0; i--)
                if (s_Bubbles[i].Speaker == speaker) s_Bubbles.RemoveAt(i);
            s_Bubbles.Add(new Bubble { Speaker = speaker, Text = text, Until = Time.time + seconds });
        }

        /// <summary>Drops expired bubbles and bubbles of destroyed speakers.</summary>
        public static void Prune()
        {
            float now = Time.time;
            for (int i = s_Bubbles.Count - 1; i >= 0; i--)
                if (s_Bubbles[i].Speaker == null || s_Bubbles[i].Until <= now) s_Bubbles.RemoveAt(i);
        }
    }
}
