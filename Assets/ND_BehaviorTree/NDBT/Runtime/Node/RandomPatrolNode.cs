
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeDescription("Moves the NavMeshAgent to a random NavMesh point within a radius.", "A new random point within 'Patrol Radius' of the agent is picked each activation.")]
    [NodeInfo("Random Patrol", "Action/Movement/RandomPatrol", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/RandomPatrol.png")]
    public class RandomPatrolNode : ActionNode
    {
        [Tooltip("How far from its current position the agent will look for a random point.")]
        public float patrolRadius = 20.0f;

        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]
        [BlackboardKeyType(typeof(NavMeshAgent))]
        public Key agentKey;

        // --- Runtime variables ---
        private NavMeshAgent agent;
        // Created once; subscribed in OnEnter and removed in OnExit so InteruptAction never accumulates handlers.
        [System.NonSerialized] private System.Action m_InterruptHandler;

        protected override void OnEnter()
        {
            // Get the NavMeshAgent from the blackboard.
            if (agentKey != null && blackboard != null)
            {
                agent = blackboard.GetValue<NavMeshAgent>(agentKey.keyName);
            }

            if (agent == null)
            {
                Debug.LogError($"RandomPatrolNode: Blackboard key '{agentKey?.keyName}' is not set or is not a NavMeshAgent.");
                return;
            }

            if (m_InterruptHandler == null) m_InterruptHandler = OnInterupt;
            InteruptAction -= m_InterruptHandler; // guard against a double subscription
            InteruptAction += m_InterruptHandler;

            // Find a new random destination and set it.
            if (TrySetNewRandomDestination())
            {
                // We've successfully set a destination, now we just need to wait.
                // The actual "processing" happens in OnProcess.
            }
        }

        protected override Status OnProcess()
        {
            // If we don't have a valid agent, the action fails.
            if (agent == null)
            {
                return Status.Failure;
            }

            // Check if the agent is still calculating its path.
            if (agent.pathPending)
            {
                return Status.Running; // Still figuring out how to get there.
            }

            // Check if the agent has reached its destination.
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                return Status.Success;
            }

            // If we are still on the way, the action is running.
            return Status.Running;
        }

        private bool TrySetNewRandomDestination()
        {
            // Generate a random point within a sphere around the agent.
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += agent.transform.position;

            // Sample the NavMesh to find the closest valid point to our random point.
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return true;
            }

            return false;
        }

        protected override void OnExit()
        {
            InteruptAction -= m_InterruptHandler;
            if (agent != null && agent.hasPath)
            {

                agent.ResetPath();
            }
        }

        public void OnInterupt()
        {
            if (agent != null && agent.hasPath)
            {

                agent.ResetPath();
            }
            // Only unsubscribe this node (assigning null would also drop handlers added by other systems).
            InteruptAction -= m_InterruptHandler;
        }
    }
}