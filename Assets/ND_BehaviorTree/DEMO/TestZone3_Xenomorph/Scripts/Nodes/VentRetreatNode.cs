using UnityEngine;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// "Backstage": runs to the nearest vent, crawls in, stays hidden, then drops out of the vent nearest to the
    /// emerge target (read when it emerges, so it follows a hint that moved meanwhile).
    /// If interrupted while hidden (e.g. it heard a noise), it drops out of the vent nearest that noise right away,
    /// so the alien is never left invisible in the vents by an aborted branch.
    /// </summary>
    [NodeDescription("Runs to the nearest vent, hides inside for a while, then drops out near a target.", "Hide time = Hide Duration key (or field) + crawl time between vents. If interrupted while hidden, it drops out near the latest noise at once, so the alien is never left in the vents.")]
    [NodeInfo("Vent Retreat", "Action/Xeno/Vent Retreat", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wait.png")]
    public class VentRetreatNode : ActionNode
    {
        [BlackboardKeyType(typeof(XenoBody))] public Key bodyKey;
        [Tooltip("Drop out of the vent nearest to this position.")]
        [BlackboardKeyType(typeof(Vector3))] public Key emergeNearKey;
        [Tooltip("Seconds to stay hidden. Unlinked = use 'hideDuration'.")]
        [BlackboardKeyType(typeof(float))] public Key hideDurationKey;

        public float hideDuration = 8f;
        [Tooltip("Speed while running to the vent.")]
        public float moveSpeed = 6f;
        [Tooltip("Crawl speed inside the vents: adds distance / speed seconds of travel.")]
        public float ventCrawlSpeed = 8f;
        [Tooltip("Tell the director the alien is back (resets the menace gauge).")]
        public bool notifyDirector = true;
        [Tooltip("When the hide time is over, never drop out closer than this to the player. (An interrupted retreat ignores it: the alien goes straight for the noise.)")]
        public float minDistanceFromPlayer = 8f;

        [NodeProgressBar(nameof(m_HiddenElapsed), nameof(m_HiddenTotal))]
        [System.NonSerialized] private float m_HiddenElapsed;
        [System.NonSerialized] private float m_HiddenTotal;

        [System.NonSerialized] private XenoBody m_Body;
        [System.NonSerialized] private Key<Vector3> m_EmergeNear;
        [System.NonSerialized] private XenoVent m_EntryVent;
        [System.NonSerialized] private float m_PreviousSpeed;

        protected override void OnEnter()
        {
            m_Body = KeyResolver.Resolve<XenoBody>(this, bodyKey)?.GetValue();
            m_EmergeNear = KeyResolver.Resolve<Vector3>(this, emergeNearKey);
            var durationSource = hideDurationKey != null ? KeyResolver.Resolve<float>(this, hideDurationKey) : null;
            m_HiddenElapsed = 0f;
            m_HiddenTotal = durationSource != null ? durationSource.GetValue() : hideDuration;
            m_EntryVent = null;
            if (m_Body == null || m_EmergeNear == null) return;

            m_EntryVent = m_Body.InVent ? null : XenoVent.Nearest(m_Body.transform.position);
            if (m_EntryVent == null) return;
            var agent = m_Body.Agent;
            m_PreviousSpeed = agent.speed;
            agent.speed = moveSpeed;
            agent.stoppingDistance = 0.3f;
            agent.SetDestination(m_EntryVent.Position);
        }

        protected override Status OnProcess()
        {
            if (m_Body == null || m_EmergeNear == null) return Status.Failure;

            if (!m_Body.InVent)
            {
                if (m_EntryVent == null) return Status.Failure;
                var agent = m_Body.Agent;
                if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance) return Status.Running;
                m_Body.EnterVent(m_EntryVent);
                // Crawling to the far vent takes time too.
                var exit = XenoVent.Nearest(m_EmergeNear.GetValue());
                if (exit != null) m_HiddenTotal += Vector3.Distance(m_EntryVent.Position, exit.Position) / Mathf.Max(0.1f, ventCrawlSpeed);
                return Status.Running;
            }

            m_HiddenElapsed += Time.deltaTime;
            if (m_HiddenElapsed < m_HiddenTotal) return Status.Running;

            var exitVent = XenoVent.NearestAwayFrom(m_EmergeNear.GetValue(), m_Body.Player.transform.position, minDistanceFromPlayer);
            if (exitVent != null) m_Body.EmergeAt(exitVent);
            if (notifyDirector && m_Body.Director != null) m_Body.Director.NotifyAlienReturned();
            return Status.Success;
        }

        private void Emerge(Vector3 near)
        {
            var vent = XenoVent.Nearest(near);
            if (vent != null) m_Body.EmergeAt(vent);
        }

        protected override void OnExit()
        {
            if (m_Body == null) return;
            if (m_EntryVent != null) m_Body.Agent.speed = m_PreviousSpeed;
            if (!m_Body.InVent)
            {
                if (m_Body.Agent.isOnNavMesh && m_Body.Agent.hasPath) m_Body.Agent.ResetPath();
                return;
            }
            // Interrupted while hidden: drop out near the most recent noise (what probably interrupted us), else the target.
            bool recentNoise = Time.time - m_Body.LastNoiseTime < 3f;
            Emerge(recentNoise ? m_Body.LastNoisePosition : m_EmergeNear != null ? m_EmergeNear.GetValue() : m_Body.transform.position);
        }
    }
}
