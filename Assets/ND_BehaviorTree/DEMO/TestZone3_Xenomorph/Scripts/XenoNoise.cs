using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    public enum XenoNoiseSource { Footstep, Noisemaker, Flamethrower }

    public struct XenoNoiseEvent
    {
        public Vector3 position;
        public float radius;
        public float time;
        public XenoNoiseSource source;
    }

    /// <summary>
    /// World noise hub. Anything that makes a sound calls <see cref="Emit"/>; listeners (the alien's sense service)
    /// poll <see cref="TryHear"/> with their own read cursor. A fixed ring buffer: no allocation, no subscriptions.
    /// </summary>
    public static class XenoNoise
    {
        private const int Capacity = 32;
        private static readonly XenoNoiseEvent[] s_Events = new XenoNoiseEvent[Capacity];
        private static int s_Head;

        /// <summary>Total number of noises emitted since play started. Listeners keep the last value they read.</summary>
        public static int Version { get; private set; }

        /// <summary>The most recent noise, for debug drawing.</summary>
        public static XenoNoiseEvent Last => s_Events[(s_Head - 1 + Capacity) % Capacity];

        // Statics survive "Enter Play Mode" without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Head = 0;
            Version = 0;
        }

        public static void Emit(Vector3 position, float radius, XenoNoiseSource source)
        {
            if (radius <= 0f) return;
            s_Events[s_Head] = new XenoNoiseEvent { position = position, radius = radius, time = Time.time, source = source };
            s_Head = (s_Head + 1) % Capacity;
            Version++;
        }

        /// <summary>
        /// Picks the clearest noise emitted after <paramref name="lastVersion"/> that reaches <paramref name="listener"/>
        /// (largest margin between its radius and the distance), then advances the cursor.
        /// </summary>
        public static bool TryHear(Vector3 listener, float hearingMultiplier, ref int lastVersion, out XenoNoiseEvent heard)
        {
            heard = default;
            int unread = Mathf.Min(Version - lastVersion, Capacity);
            lastVersion = Version;

            float bestMargin = float.NegativeInfinity;
            for (int k = 0; k < unread; k++)
            {
                var e = s_Events[(s_Head - 1 - k + Capacity * 2) % Capacity];
                float margin = e.radius * hearingMultiplier - Vector3.Distance(listener, e.position);
                if (margin >= 0f && margin > bestMargin)
                {
                    bestMargin = margin;
                    heard = e;
                }
            }
            return bestMargin >= 0f;
        }
    }
}
