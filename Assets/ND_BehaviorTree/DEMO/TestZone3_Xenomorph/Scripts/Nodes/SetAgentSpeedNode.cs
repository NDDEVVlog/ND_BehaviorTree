using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Xeno
{
    /// <summary>Sets the NavMeshAgent's speed (walk / stalk / sprint) for the moves that follow in a sequence.</summary>
    [NodeDescription("Sets the NavMeshAgent's speed (stand-alone action).", "Prefer the 'Agent Speed' service sub-node, which also restores the previous speed when its node stops.")]
    [NodeInfo("Set Agent Speed", "Action/Movement/Set Agent Speed", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wander.png")]
    public class SetAgentSpeedNode : ActionNode
    {
        [BlackboardKeyType(typeof(NavMeshAgent))]
        public Key agentKey;

        [Tooltip("Metres per second.")]
        public float speed = 3.5f;

        protected override Status OnProcess()
        {
            var agent = blackboard != null && agentKey != null ? blackboard.GetValue<NavMeshAgent>(agentKey.keyName) : null;
            if (agent == null)
            {
                Debug.LogError($"[SetAgentSpeedNode] '{name}': no NavMeshAgent in the linked key.", this);
                return Status.Failure;
            }
            agent.speed = speed;
            return Status.Success;
        }
    }
}
