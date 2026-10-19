using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>
    /// Sub-node: sets the NavMeshAgent's speed while the node it is attached to runs (walk / stalk / sprint), and puts
    /// the previous speed back when that node finishes or is interrupted. Replaces a "Set Agent Speed" action.
    /// </summary>
    [NodeInfo("Agent Speed", "Services/Movement/Agent Speed", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wander.png", isChildOnly: true)]
    public class AgentSpeedService : ServiceNode
    {
        [BlackboardKeyType(typeof(NavMeshAgent))] public Key agentKey;
        [Tooltip("Metres per second.")]
        public float speed = 3.5f;
        public bool restoreOnExit = true;

        [System.NonSerialized] private NavMeshAgent m_Agent;
        [System.NonSerialized] private float m_PreviousSpeed;

        public AgentSpeedService() => interval = 1000f; // the work happens on enter/exit; OnTick has nothing to do

        protected override void OnEnter()
        {
            base.OnEnter();
            m_Agent = blackboard != null && agentKey != null ? blackboard.GetValue<NavMeshAgent>(agentKey.keyName) : null;
            if (m_Agent == null)
            {
                Debug.LogError($"[AgentSpeedService] '{name}': no NavMeshAgent in the linked key.", this);
                return;
            }
            m_PreviousSpeed = m_Agent.speed;
            m_Agent.speed = speed;
        }

        protected override void OnTick() { }

        protected override void OnExit()
        {
            if (restoreOnExit && m_Agent != null) m_Agent.speed = m_PreviousSpeed;
        }
    }
}
