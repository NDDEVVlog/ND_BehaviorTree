
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree
{
    [NodeDescription("Sub-node: smoothly turns the agent toward its target direction while its node runs.", "Rotation speed is frame-rate independent (Slerp with deltaTime x Rotation Speed).")]
    [NodeInfo("Rotate To Target", "Service/RotateToTarget", true, true,isChildOnly:true)]
    public class RotateToTargetService : ServiceNode
    {
        [Tooltip("How quickly the agent rotates to face its target direction. Higher is faster.")]
        public float rotationSpeed = 5.0f;

        [Tooltip("The Blackboard key for the NavMeshAgent component on the AI.")]

        public Key<NavMeshAgent> agentKey;

        private NavMeshAgent agent;
        private Transform agentTransform;

        protected override void OnEnter()
        {
            // This OnEnter is for the Service itself, called when its parent branch becomes active.
            base.OnEnter(); // Calls the base service OnEnter to handle timing.
            ResolveAgent();
        }

        /// <summary>
        /// Called every frame the owner node is processed. The interval (base) only throttles the agent lookup;
        /// the rotation itself is applied every call so Slerp(deltaTime * speed) gives a smooth, frame-rate-correct turn.
        /// </summary>
        protected override Status OnProcess()
        {
            var status = base.OnProcess(); // runs OnTick on the interval
            RotateStep();
            return status;
        }

        /// <summary>Interval work: re-resolve the agent if it was missing (e.g. set on the blackboard later).</summary>
        protected override void OnTick()
        {
            if (agent == null || agentTransform == null) ResolveAgent();
        }

        private void ResolveAgent()
        {
            if (agentKey != null && blackboard != null)
            {
                agent = blackboard.GetValue<NavMeshAgent>(agentKey.keyName);
                agentTransform = agent != null ? agent.transform : null;
            }
        }

        private void RotateStep()
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
