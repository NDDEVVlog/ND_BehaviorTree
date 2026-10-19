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

        // Reused overlap buffer (no per-tick allocation). Colliders beyond its size are ignored.
        private const int MaxColliders = 32;
        [System.NonSerialized] private Collider[] m_Hits;

        protected override Status OnProcess()
        {
            if (targetKey == null || blackboard == null) return Status.Failure;

            GameObject owner = GetOwnerTreeGameObject();
            if (owner == null) return Status.Failure;
            Vector3 origin = owner.transform.position;

            if (m_Hits == null) m_Hits = new Collider[MaxColliders];

            // Find all colliders within the search radius on the specified layer.
            int count = Physics.OverlapSphereNonAlloc(origin, searchRadius, m_Hits, searchLayer);

            Transform closestTarget = null;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var collider = m_Hits[i];
                if (collider != null && collider.CompareTag(targetTag))
                {
                    float sqrDistance = (collider.transform.position - origin).sqrMagnitude;
                    if (sqrDistance < closestDistanceSqr)
                    {
                        closestDistanceSqr = sqrDistance;
                        closestTarget = collider.transform;
                    }
                }
                m_Hits[i] = null; // don't keep destroyed objects alive in the buffer
            }

            if (closestTarget != null)
            {
                // We found a target: store it in the blackboard. Fail if the write is rejected (missing key / wrong type).
                return blackboard.SetValue<Transform>(targetKey.keyName, closestTarget) ? Status.Success : Status.Failure;
            }

            // No target found, clear the blackboard key and fail.
            blackboard.SetValue<Transform>(targetKey.keyName, null);
            return Status.Failure;
        }
    }
}
