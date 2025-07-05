// --- START OF FILE MoveToPatrolPointNode.cs ---
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeInfo("Move To Patrol Point", "Action/Movement/MoveToPatrolPoint", true, false)]
    public class MoveToPatrolPointNode : ActionNode
    {
        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]
        public Key agentKey;
        
        [Tooltip("The Blackboard key (of type Vector3) for the destination point.")]
        public Key destinationKey;

        // --- Runtime variables ---
        private NavMeshAgent agent;

        protected override void OnEnter()
        {
            // 1. Get the NavMeshAgent from the blackboard.
            if (agentKey != null && blackboard != null)
            {
                agent = blackboard.GetValue<NavMeshAgent>(agentKey.name);
            }

            if (agent == null)
            {
                return;
            }
            
            // 2. Get the destination Vector3 from the blackboard.
            if (destinationKey != null && blackboard != null)
            {
                Vector3 destination = blackboard.GetValue<Vector3>(destinationKey.name);
                
                // 3. Set the destination for the agent.
                if (agent.SetDestination(destination))
                {
                    Debug.Log("Set Destination");
                }
            }

        }

        protected override Status OnProcess()
        {
            // If we failed to get an agent in OnEnter, the action fails.
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
                // If we've arrived, the action is a success.
                return Status.Success;
            }

            // If we are still on the way, the action is running.
            return Status.Running;
        }

        protected override void OnExit()
        {
            // When this node exits (either by succeeding or being aborted),
            // it's good practice to stop the agent's current path.
            // This prevents the AI from continuing to a patrol point if, for example,
            // a higher-priority task like "Attack Enemy" interrupts it.
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
}
// --- END OF FILE MoveToPatrolPointNode.cs ---