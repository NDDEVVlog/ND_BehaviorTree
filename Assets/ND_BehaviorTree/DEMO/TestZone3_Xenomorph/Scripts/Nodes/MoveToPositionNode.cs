using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Moves to a Vector3 blackboard position. The key is re-read every <see cref="repathInterval"/> seconds, so a target
    /// that changes while moving (a newer noise, a fresher last-known position) is followed.
    /// Success on arrival; Failure if the agent is not on the NavMesh or no path exists.
    /// </summary>
    [NodeDescription("Moves the NavMeshAgent to a Vector3 key, following it if it changes.", "Re-reads the key every 'Repath Interval'. Success on arrival (within 'Stopping Distance'); Failure if off the NavMesh or no path exists.")]
    [NodeInfo("Move To Position", "Action/Movement/Move To Position", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wander.png")]
    public class MoveToPositionNode : ActionNode
    {
        [BlackboardKeyType(typeof(NavMeshAgent))]
        public Key agentKey;

        [BlackboardKeyType(typeof(Vector3))]
        public Key targetKey;

        public float stoppingDistance = 0.6f;
        [Tooltip("Seconds between re-reading the target key.")]
        public float repathInterval = 0.3f;

        [System.NonSerialized] private NavMeshAgent m_Agent;
        [System.NonSerialized] private Key<Vector3> m_Target;
        [System.NonSerialized] private Vector3 m_Destination;
        [System.NonSerialized] private float m_NextRepath;

        protected override void OnEnter()
        {
            m_Agent = blackboard != null && agentKey != null ? blackboard.GetValue<NavMeshAgent>(agentKey.keyName) : null;
            m_Target ??= KeyResolver.Resolve<Vector3>(this, targetKey);
            if (m_Agent == null || m_Target == null || !m_Agent.isOnNavMesh) return;

            m_Agent.stoppingDistance = stoppingDistance;
            SetDestination(m_Target.GetValue());
        }

        protected override Status OnProcess()
        {
            if (m_Agent == null || m_Target == null || !m_Agent.isOnNavMesh) return Status.Failure;

            if (Time.time >= m_NextRepath)
            {
                Vector3 target = m_Target.GetValue();
                if ((target - m_Destination).sqrMagnitude > 0.25f) SetDestination(target);
                else m_NextRepath = Time.time + repathInterval;
            }

            if (m_Agent.pathPending) return Status.Running;
            if (m_Agent.pathStatus == NavMeshPathStatus.PathInvalid) return Status.Failure;
            return m_Agent.remainingDistance <= stoppingDistance ? Status.Success : Status.Running;
        }

        private void SetDestination(Vector3 target)
        {
            m_Destination = target;
            m_NextRepath = Time.time + repathInterval;
            m_Agent.SetDestination(target);
        }

        protected override void OnExit()
        {
            if (m_Agent != null && m_Agent.isOnNavMesh && m_Agent.hasPath) m_Agent.ResetPath();
        }
    }
}
