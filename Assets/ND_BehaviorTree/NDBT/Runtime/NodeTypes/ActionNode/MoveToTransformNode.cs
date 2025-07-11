
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeInfo("Move To Transform", "Action/MoveToTransform", true, false)]
    public class MoveToTransformNode : ActionNode
    {
        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]
        public Key agentKey;
        
        [Tooltip("The Blackboard key (of type Transform) for the target to move towards.")]
        public Key targetTransformKey;

        [Tooltip("How often, in seconds, to update the agent's destination. A lower value means more responsive chasing, but higher performance cost. 0 means every frame.")]
        public float updateInterval = 0.25f;

        // --- Runtime variables ---
        private NavMeshAgent agent;
        private Transform targetTransform;
        private float timeSinceLastUpdate = 0f;

        protected override void OnEnter()
        {
            // --- 1. Get references from the Blackboard ---
            if (agentKey != null && blackboard != null)
            {
                agent = blackboard.GetValue<NavMeshAgent>(agentKey.keyName);
            }

            if (targetTransformKey != null && blackboard != null)
            {
                targetTransform = blackboard.GetValue<Transform>(targetTransformKey.keyName);
            }

            // --- 2. Validate references ---
            if (agent == null)
            {
                return;
            }

            if (targetTransform == null)
            {
                return;
            }
            
            // --- 3. Set the first destination immediately ---
            agent.SetDestination(targetTransform.position);
            timeSinceLastUpdate = 0f; // Reset timer
        }

        protected override Status OnProcess()
        {
            // --- 1. Early exit if configuration is invalid ---
            if (agent == null || targetTransform == null)
            {
                return Status.Failure;
            }
            
            // --- 2. Update destination based on interval ---
            timeSinceLastUpdate += Time.deltaTime;
            if (timeSinceLastUpdate >= updateInterval)
            {
                // Re-calculate the path to the target's current position
                agent.SetDestination(targetTransform.position);
                timeSinceLastUpdate = 0f; // Reset timer
            }

            // --- 3. Check if we have arrived ---
            // If the agent doesn't have a path or has reached the destination
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                // We have successfully reached the target's last known position.
                return Status.Success;
            }

            // --- 4. Still on the way ---
            return Status.Running;
        }

        protected override void OnExit()
        {
            // Clean up: Stop the agent if this node is aborted or finishes.
            // This is crucial to prevent the AI from continuing to move
            // when it should be doing something else (e.g., attacking).
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
}
