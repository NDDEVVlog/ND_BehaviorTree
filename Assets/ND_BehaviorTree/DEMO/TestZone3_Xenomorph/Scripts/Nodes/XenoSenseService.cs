using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// The alien's senses — the ONLY way the behavior tree learns about the player (the director's hint aside).
    ///   Sight: a cone + line of sight fills a detection meter (faster when close, slower when the player crouches);
    ///          at 1 the player is spotted. A glimpse above the suspicion threshold sets a last-known position to check.
    ///   Proximity: very close, the player is sensed in any direction.
    ///   Hearing: noises from <see cref="XenoNoise"/> within range (works from inside the vents too).
    ///   Lockers: seeing the player get into a locker marks it, and the alien will check it.
    /// "Event" keys (HasLastKnown, HeardNoise) are only ever set to true here; the tree clears them when it has dealt
    /// with them. State keys (PlayerVisible, Detection, InAttackRange) are written every tick.
    /// </summary>
    [NodeDescription("The alien's senses: sight + detection meter, hearing and proximity, written to the blackboard.", "Sight fills Detection (faster when close, slower when the player crouches); at 1 the player is spotted. A glimpse sets a last-known position to check. Noises within range set HeardNoise. Seeing the player enter a locker marks it.")]
    [NodeInfo("Xeno Senses", "Services/Xeno/Senses", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/antivirus.png", isChildOnly: true)]
    public class XenoSenseService : ServiceNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;
        [BlackboardKeyType(typeof(bool))] public Key playerVisibleKey;
        [BlackboardKeyType(typeof(float))] public Key detectionKey;
        [BlackboardKeyType(typeof(Vector3))] public Key lastKnownPositionKey;
        [BlackboardKeyType(typeof(bool))] public Key hasLastKnownKey;
        [BlackboardKeyType(typeof(bool))] public Key heardNoiseKey;
        [BlackboardKeyType(typeof(Vector3))] public Key noisePositionKey;
        [BlackboardKeyType(typeof(bool))] public Key inAttackRangeKey;

        [Tooltip("Seconds of player movement to extrapolate when line of sight is lost (the alien guesses where you went).")]
        public float predictionTime = 0.6f;

        [System.NonSerialized] private bool m_Resolved;
        [System.NonSerialized] private XenoBody m_Body;
        [System.NonSerialized] private Key<bool> m_Visible, m_HasLastKnown, m_HeardNoise, m_InAttackRange;
        [System.NonSerialized] private Key<float> m_Detection;
        [System.NonSerialized] private Key<Vector3> m_LastKnown, m_NoisePosition;

        [System.NonSerialized] private float m_LastTickTime = -1f;
        [System.NonSerialized] private float m_LastSeenTime = float.NegativeInfinity;
        [System.NonSerialized] private int m_NoiseCursor;
        [System.NonSerialized] private bool m_PlayerWasHidden;
        [System.NonSerialized] private bool m_SawPlayerLastTick;
        [System.NonSerialized] private bool m_PlayerWasDead;

        private bool Resolve()
        {
            if (m_Resolved) return m_Body != null;
            m_Resolved = true;
            m_Body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
            m_Visible = KeyResolver.Resolve<bool>(this, playerVisibleKey);
            m_Detection = KeyResolver.Resolve<float>(this, detectionKey);
            m_LastKnown = KeyResolver.Resolve<Vector3>(this, lastKnownPositionKey);
            m_HasLastKnown = KeyResolver.Resolve<bool>(this, hasLastKnownKey);
            m_HeardNoise = KeyResolver.Resolve<bool>(this, heardNoiseKey);
            m_NoisePosition = KeyResolver.Resolve<Vector3>(this, noisePositionKey);
            m_InAttackRange = KeyResolver.Resolve<bool>(this, inAttackRangeKey);
            m_NoiseCursor = XenoNoise.Version; // ignore noises made before the alien existed
            if (m_Body == null) Debug.LogError($"[XenoSenseService] '{name}': the Body key holds no XenoBody (is the agent run by a XenoRunner?).", this);
            return m_Body != null;
        }

        protected override void OnTick()
        {
            if (!Resolve()) return;

            float now = Time.time;
            float dt = m_LastTickTime < 0f ? interval : now - m_LastTickTime;
            m_LastTickTime = now;

            var body = m_Body;
            var player = body.Player;

            // ---- Player death: forget everything about them.
            if (player.IsDead)
            {
                if (!m_PlayerWasDead)
                {
                    m_PlayerWasDead = true;
                    m_HasLastKnown?.SetValue(false);
                    m_HeardNoise?.SetValue(false);
                }
                body.Detection = 0f;
                body.PlayerVisible = false;
                m_SawPlayerLastTick = false;
                WriteState(false, 0f, false);
                return;
            }
            m_PlayerWasDead = false;

            // ---- Hearing (also from inside the vents).
            if (XenoNoise.TryHear(body.EyePosition, body.hearingMultiplier, ref m_NoiseCursor, out var noise))
            {
                body.OnHeardNoise(noise.position);
                m_NoisePosition?.SetValue(noise.position);
                m_HeardNoise?.SetValue(true);
            }

            // ---- Sight.
            Vector3 toPlayer = player.SightTarget - body.EyePosition;
            float distance = toPlayer.magnitude;
            bool canSee = !body.InVent && !player.IsHidden && CanSee(body, player, toPlayer, distance);
            bool close = !body.InVent && !player.IsHidden && distance <= body.proximityRadius;

            // ---- Locker: the alien watched the player get in.
            if (player.IsHidden && !m_PlayerWasHidden && (m_SawPlayerLastTick || body.PlayerVisible))
            {
                player.CurrentSpot.MarkSeen();
                SetLastKnown(player.CurrentSpot.StandPosition);
            }
            m_PlayerWasHidden = player.IsHidden;
            m_SawPlayerLastTick = canSee;

            // ---- Detection meter.
            float detection = body.Detection;
            if (close) detection += body.detectionRate * 4f * dt;
            else if (canSee) detection += body.detectionRate * player.Visibility * Mathf.Lerp(1.6f, 0.4f, distance / body.sightRange) * dt;
            else detection -= body.detectionDecay * dt;
            detection = Mathf.Clamp01(detection);
            body.Detection = detection;

            bool visible = body.PlayerVisible;
            if ((canSee || close) && detection >= 1f)
            {
                visible = true;
                m_LastSeenTime = now;
                SetLastKnown(player.transform.position);
            }
            else if (visible && now - m_LastSeenTime > body.loseSightGrace)
            {
                visible = false;
                // Lost them: guess where they were heading.
                SetLastKnown(player.transform.position + player.Velocity * predictionTime);
            }
            else if (canSee && detection >= body.suspicionThreshold && !visible)
            {
                SetLastKnown(player.transform.position); // a glimpse: come and look
            }
            body.PlayerVisible = visible;

            bool inAttackRange = visible && Vector3.Distance(body.transform.position, player.transform.position) <= body.attackRange;
            WriteState(visible, detection, inAttackRange);
        }

        private static bool CanSee(XenoBody body, XenoPlayer player, Vector3 toPlayer, float distance)
        {
            if (distance > body.sightRange) return false;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            Vector3 forward = new Vector3(body.Forward.x, 0f, body.Forward.z);
            if (Vector3.Angle(forward, flat) > body.fieldOfView * 0.5f) return false;
            // Line of sight: the first thing the ray hits must be the player.
            if (Physics.Raycast(body.EyePosition, toPlayer / distance, out var hit, distance, body.sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.transform == player.transform;
            return true;
        }

        private void SetLastKnown(Vector3 position)
        {
            m_Body.LastKnownPosition = position;
            m_LastKnown?.SetValue(position);
            m_HasLastKnown?.SetValue(true);
        }

        private void WriteState(bool visible, float detection, bool inAttackRange)
        {
            m_Visible?.SetValue(visible);
            m_Detection?.SetValue(detection);
            m_InAttackRange?.SetValue(inAttackRange);
        }
    }
}
