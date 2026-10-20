
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeDescription("Moves the NavMeshAgent toward a Transform key, following it as it moves.", "The destination is refreshed every 'Update Interval'. Success within the agent's stopping distance; Failure if the agent or target is missing.")]
    [NodeInfo("Move To Transform", "Action/MoveToTransform", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/MoveToTarget.png")]
    public class MoveToTransformNode : ActionNode
    {
        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]
        [BlackboardKeyType(typeof(NavMeshAgent))]
        public Key agentKey;

        [Tooltip("The Blackboard key (of type Transform) for the target to move towards.")]
        [BlackboardKeyType(typeof(Transform))]
        public Key targetTransformKey;

        [Tooltip("How often, in seconds, to update the agent's destination. A lower value means more responsive chasing, but higher performance cost. 0 means every frame.")]
        public float updateInterval = 0.25f;

        // --- Runtime variables ---
        private NavMeshAgent agent;
        private Transform targetTransform;
        private float timeSinceLastUpdate = 0f;
        // Created once and subscribed per activation (removed in OnExit), so InteruptAction never accumulates handlers.
        [System.NonSerialized] private System.Action m_InterruptHandler;

        protected override void OnEnter()
        {
            if (m_InterruptHandler == null) m_InterruptHandler = OnInterrupt;
            InteruptAction -= m_InterruptHandler; // guard against a double subscription
            InteruptAction += m_InterruptHandler;

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
            InteruptAction -= m_InterruptHandler;
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        // Stop the agent if this node is interrupted.
        private void OnInterrupt()
        {
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
}
