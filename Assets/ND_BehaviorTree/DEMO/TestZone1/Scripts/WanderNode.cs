////////////////////////////////////////////////////////////////////////////
//
// FB: https://www.facebook.com/profile.php?id=100090693452227
// Github: https://github.com/NDDEVVlog/ND_BehaviorTree
//
////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using UnityEngine.AI;
using ND_BehaviorTree;

/// <summary>
/// An Action Node that makes the AI wander to random points within a specified radius.
/// This node will continuously return 'Running' and is intended for use as a default or idle state.
/// </summary>
[NodeInfo("Wander", "Action/Movement/Wander", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Wander.png")]
public class WanderNode : ActionNode
{
    // --- Public Parameters ---

    [Tooltip("The radius around the starting point within which the AI will wander.")]
    public float walkRadius = 10f;

    // --- Private Runtime Variables ---
    private NavMeshAgent _agent;
    private Vector3 _startPosition; // An anchor point for wandering to prevent drifting too far.

    // --- Lifecycle Methods ---

    // OnEnter is called once when the node is first processed.
    protected override void OnEnter()
    {
        GameObject owner = GetOwnerTreeGameObject();
        if (owner == null) return;

        _agent = owner.GetComponent<NavMeshAgent>();
        if (_agent == null)
        {
            Debug.LogError("WanderNode requires a NavMeshAgent component on the owner GameObject.", owner);
        }
        
        // Store the initial position to calculate wander points from.
        _startPosition = owner.transform.position;
    }

    // OnProcess is called every frame while the node is in a 'Running' state.
    protected override Status OnProcess()
    {
        if (_agent == null)
        {
            return Status.Failure;
        }

        // If the agent has reached its destination (or doesn't have a path), find a new point.
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            // Find a random direction within a sphere and scale it by the walk radius.
            Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
            
            // Add the random direction to the agent's start position to get a world-space point.
            randomDirection += _startPosition;

            // Find the nearest valid point on the NavMesh to the random point.
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, walkRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        // Wander is a continuous action, so it should always return 'Running' until interrupted.
        return Status.Running;
    }

    // OnExit is called once when the node's status is no longer 'Running'.
    protected override void OnExit()
    {
        // Stop the agent's movement when this behavior is exited to prevent it from
        // continuing to its last wander point when a higher-priority task takes over.
        if (_agent != null && _agent.hasPath)
        {
            _agent.ResetPath();
        }
    }
}