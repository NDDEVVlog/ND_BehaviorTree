
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeInfo("Rotate To Target", "Service/RotateToTarget", true, true,isChildOnly:true)]
    public class RotateToTargetService : ServiceNode
    {
        [Tooltip("How quickly the agent rotates to face its target direction. Higher is faster.")]
        public float rotationSpeed = 5.0f;

        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]
        public Key agentKey;

        private NavMeshAgent agent;
        private Transform agentTransform;

        protected override void OnEnter()
        {
            // This OnEnter is for the Service itself, called when its parent branch becomes active.
            base.OnEnter(); // Calls the base service OnEnter to handle timing.
            
            if (agentKey != null && blackboard != null)
            {
                agent = blackboard.GetValue<NavMeshAgent>(agentKey.keyName);
                if (agent != null)
                {
                    agentTransform = agent.transform;
                }
            }
        }
        
        /// <summary>
        /// This is the method that gets called on the service's interval (timer).
        /// </summary>
        protected override void OnTick()
        {
            if (agent == null || agentTransform == null)
            {
                return;
            }

            // Check if the agent is actually moving.
            // We use 'velocity.sqrMagnitude' as it's more efficient than checking 'velocity.magnitude'.
            if (agent.velocity.sqrMagnitude > 0.1f) 
            {
                // Get the direction the agent is moving.
                Vector3 direction = agent.velocity.normalized;

                // Create a rotation that looks in that direction.
                Quaternion lookRotation = Quaternion.LookRotation(direction);

                // Smoothly interpolate from the current rotation to the target rotation.
                agentTransform.rotation = Quaternion.Slerp(agentTransform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
}
