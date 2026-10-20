using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Story
{
    [StorySentence("{self} moves to {target}")]
    [NodeDescription("Walks the speaker's NavMeshAgent to a GameObject from a key, following it if it moves.",
        "Self needs a NavMeshAgent. The destination is refreshed every 'Repath Interval'. Success within 'Stopping Distance'; " +
        "Failure if there is no agent, no target or no path.")]
    [NodeInfo("Moves To", "Story/Moves To", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/MoveToTarget.png")]
    public class MoveToNode : StoryActionNode
    {
        [BlackboardKeyType(typeof(GameObject))] public Key targetKey;

        public float stoppingDistance = 1f;
        [Tooltip("Agent speed while moving (0 = keep the agent's speed).")]
        public float speed;
        public float repathInterval = 0.25f;

        [System.NonSerialized] private NavMeshAgent m_Agent;
        [System.NonSerialized] private GameObject m_AgentOwner;
        [System.NonSerialized] private GameObject m_Target;
        [System.NonSerialized] private float m_NextRepath;

        protected override void OnEnter()
        {
            var self = Self;
            if (self != m_AgentOwner)
            {
                m_AgentOwner = self;
                m_Agent = self != null ? self.GetComponent<NavMeshAgent>() : null;
                if (self != null && m_Agent == null) LogKeyError($"'{self.name}' has no NavMeshAgent.");
            }
            m_Target = targetKey != null ? Read<GameObject>(targetKey, null) : null;
            if (targetKey == null) LogKeyError("bind 'target' to a GameObject key.");
            if (m_Agent == null || m_Target == null || !m_Agent.isOnNavMesh) return;

            if (speed > 0f) m_Agent.speed = speed;
            m_Agent.stoppingDistance = stoppingDistance;
            m_Agent.SetDestination(m_Target.transform.position);
            m_NextRepath = Time.time + repathInterval;
        }

        protected override Status OnProcess()
        {
            if (m_Agent == null || m_Target == null || !m_Agent.isOnNavMesh) return Status.Failure;
            if (Time.time >= m_NextRepath)
            {
                m_NextRepath = Time.time + repathInterval;
                m_Agent.SetDestination(m_Target.transform.position);
            }
            if (m_Agent.pathPending) return Status.Running;
            if (m_Agent.pathStatus == NavMeshPathStatus.PathInvalid) return Status.Failure;
            return m_Agent.remainingDistance <= stoppingDistance ? Status.Success : Status.Running;
        }

        protected override void OnExit()
        {
            if (m_Agent != null && m_Agent.isOnNavMesh && m_Agent.hasPath) m_Agent.ResetPath();
        }
    }
}
