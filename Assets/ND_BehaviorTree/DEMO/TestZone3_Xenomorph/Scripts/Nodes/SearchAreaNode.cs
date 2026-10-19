using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Sweeps an area: walks to <see cref="points"/> random NavMesh points around a center, pausing at each to look
    /// around. Succeeds after the last point (points it cannot reach are skipped), so a search never blocks a sequence.
    /// </summary>
    [NodeInfo("Search Area", "Action/Movement/Search Area", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wander.png")]
    public class SearchAreaNode : ActionNode
    {
        [BlackboardKeyType(typeof(NavMeshAgent))]
        public Key agentKey;

        [Tooltip("Center of the sweep.")]
        [BlackboardKeyType(typeof(Vector3))]
        public Key centerKey;

        public float radius = 6f;
        public int points = 3;
        [Tooltip("Seconds spent looking around at each point.")]
        public float pauseDuration = 1.5f;
        [Tooltip("Turn speed while looking around (degrees per second).")]
        public float lookAroundSpeed = 140f;

        [NodeProgressBar(nameof(m_PointsDone), nameof(points))]
        [System.NonSerialized] private int m_PointsDone;

        [System.NonSerialized] private NavMeshAgent m_Agent;
        [System.NonSerialized] private Key<Vector3> m_Center;
        [System.NonSerialized] private Vector3 m_CenterValue;
        [System.NonSerialized] private bool m_Pausing;
        [System.NonSerialized] private float m_PauseEnd;
        [System.NonSerialized] private float m_TurnDirection;

        private const int MaxSampleAttempts = 6;

        protected override void OnEnter()
        {
            m_Agent = blackboard != null && agentKey != null ? blackboard.GetValue<NavMeshAgent>(agentKey.keyName) : null;
            m_Center ??= KeyResolver.Resolve<Vector3>(this, centerKey);
            m_PointsDone = 0;
            m_Pausing = false;
            if (m_Agent == null || m_Center == null || !m_Agent.isOnNavMesh) return;

            m_CenterValue = m_Center.GetValue();
            m_Agent.stoppingDistance = 0.4f;
            MoveToNextPoint();
        }

        protected override Status OnProcess()
        {
            if (m_Agent == null || m_Center == null || !m_Agent.isOnNavMesh) return Status.Failure;
            if (m_PointsDone >= points) return Status.Success;

            if (m_Pausing)
            {
                m_Agent.transform.Rotate(0f, m_TurnDirection * lookAroundSpeed * Time.deltaTime, 0f);
                if (Time.time < m_PauseEnd) return Status.Running;
                m_Pausing = false;
                m_PointsDone++;
                if (m_PointsDone >= points) return Status.Success;
                MoveToNextPoint();
                return Status.Running;
            }

            if (m_Agent.pathPending) return Status.Running;
            if (m_Agent.pathStatus == NavMeshPathStatus.PathInvalid || m_Agent.remainingDistance <= m_Agent.stoppingDistance)
            {
                m_Pausing = true;
                m_PauseEnd = Time.time + pauseDuration;
                m_TurnDirection = Random.value < 0.5f ? -1f : 1f;
            }
            return Status.Running;
        }

        private void MoveToNextPoint()
        {
            for (int attempt = 0; attempt < MaxSampleAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = m_CenterValue + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                {
                    m_Agent.SetDestination(hit.position);
                    return;
                }
            }
            // Nothing reachable around the center: look around where we are instead.
            m_Agent.ResetPath();
        }

        protected override void OnExit()
        {
            if (m_Agent != null && m_Agent.isOnNavMesh && m_Agent.hasPath) m_Agent.ResetPath();
        }
    }
}
