using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>A vent opening. The alien goes "backstage" through these and drops out of the one nearest its target.</summary>
    public class XenoVent : MonoBehaviour
    {
        private static readonly List<XenoVent> s_All = new List<XenoVent>();
        public static IReadOnlyList<XenoVent> All => s_All;

        public Vector3 Position => transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_All.Clear();

        private void OnEnable() => s_All.Add(this);
        private void OnDisable() => s_All.Remove(this);

        /// <summary>The vent closest to <paramref name="point"/>, or null when the scene has none.</summary>
        public static XenoVent Nearest(Vector3 point)
        {
            XenoVent best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < s_All.Count; i++)
            {
                float sqr = (s_All[i].Position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = s_All[i];
                }
            }
            if (best == null) Debug.LogError("[XenoVent] No vent in the scene. The alien cannot go backstage.");
            return best;
        }

        /// <summary>
        /// The vent closest to <paramref name="point"/> that is at least <paramref name="minDistance"/> from
        /// <paramref name="avoid"/> (the alien drops out within reach of the player, not on top of them).
        /// If every vent is too close, the farthest one from <paramref name="avoid"/> is used.
        /// </summary>
        public static XenoVent NearestAwayFrom(Vector3 point, Vector3 avoid, float minDistance)
        {
            XenoVent best = null, farthest = null;
            float bestSqr = float.MaxValue, farthestSqr = -1f, minSqr = minDistance * minDistance;
            for (int i = 0; i < s_All.Count; i++)
            {
                var vent = s_All[i];
                float avoidSqr = (vent.Position - avoid).sqrMagnitude;
                if (avoidSqr > farthestSqr) { farthestSqr = avoidSqr; farthest = vent; }
                if (avoidSqr < minSqr) continue;
                float sqr = (vent.Position - point).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = vent; }
            }
            if (farthest == null) Debug.LogError("[XenoVent] No vent in the scene. The alien cannot go backstage.");
            return best != null ? best : farthest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(1.2f, 0.1f, 1.2f));
        }
    }
}
