using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    [NodeInfo("Find Target", "Action/Perception/FindTarget", true, false)]
    public class FindTargetNode : ActionNode
    {
        [Tooltip("The tag of the object to find (e.g., 'Player').")]
        public string targetTag = "Player";

        [Tooltip("The radius to search within.")]
        public float searchRadius = 10.0f;

        [Tooltip("The Blackboard key (of type Transform) to store the found target.")]
        public Key targetKey;

        [Tooltip("The layer mask to use for the physics check.")]
        public LayerMask searchLayer;

        protected override Status OnProcess()
        {
            if (targetKey == null) return Status.Failure;

            // Find all colliders within the search radius on the specified layer.
            Collider[] colliders = Physics.OverlapSphere(GetOwnerTreeGameObject().transform.position, searchRadius, searchLayer);

            Transform closestTarget = null;
            float closestDistanceSqr = float.MaxValue;

            foreach (var collider in colliders)
            {
                if (collider.CompareTag(targetTag))
                {
                    float sqrDistance = (collider.transform.position - GetOwnerTreeGameObject().transform.position).sqrMagnitude;
                    if (sqrDistance < closestDistanceSqr)
                    {
                        closestDistanceSqr = sqrDistance;
                        closestTarget = collider.transform;
                    }
                }
            }

            if (closestTarget != null)
            {
                // We found a target, store it in the blackboard and succeed.
                blackboard.SetValue<Transform>(targetKey.name, closestTarget);
                return Status.Success;
            }

            // No target found, clear the blackboard key and fail.
            blackboard.SetValue<Transform>(targetKey.name, null);
            return Status.Failure;
        }
    }
}

