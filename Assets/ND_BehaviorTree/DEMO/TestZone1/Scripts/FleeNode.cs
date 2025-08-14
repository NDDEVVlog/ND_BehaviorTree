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
/// An Action Node that makes the AI flee from a target specified on the Blackboard.
/// It calculates a destination in the opposite direction of the target.
/// </summary>
[NodeInfo("Flee", "Action/Movement/Flee", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Flee.png")]
public class FleeNode : ActionNode
{
    // --- Public Parameters ---
    
    [Tooltip("The Blackboard key for the target (Transform) to flee from.")]
    public Key<Transform> targetKey;

    [Tooltip("How far the AI will try to run away from the target.")]
    public float fleeDistance = 20f;
    
    // --- Private Runtime Variables ---
    private NavMeshAgent _agent;
    private Transform _ownerTransform;

    // --- Lifecycle Methods ---

    protected override void OnEnter()
    {
        GameObject owner = GetOwnerTreeGameObject();
        if (owner == null) return;
        
        _agent = owner.GetComponent<NavMeshAgent>();
        _ownerTransform = owner.transform;

        if (_agent == null)
        {
            Debug.LogError("FleeNode requires a NavMeshAgent component on the owner GameObject.", owner);
        }
    }

    protected override Status OnProcess()
    {
        if (_agent == null)
        {
            return Status.Failure;
        }

        // Get the target to flee from out of the blackboard.
        Transform target = targetKey.GetValue(blackboard);

        // If there is no target, this node cannot function, so it fails.
        if (target == null)
        {
            return Status.Failure;
        }

        // Calculate the direction vector away from the target.
        Vector3 directionToFlee = (_ownerTransform.position - target.position).normalized;
        
        // Calculate the potential destination point.
        Vector3 fleeDestination = _ownerTransform.position + directionToFlee * fleeDistance;
        
        // Find the nearest valid point on the NavMesh to the calculated destination.
        if (NavMesh.SamplePosition(fleeDestination, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }

        // Fleeing is an ongoing action, so return Running as long as this node is active.
        return Status.Running;
    }

    protected override void OnExit()
    {
        // Stop the agent when exiting to ensure a clean transition to the next behavior.
        if (_agent != null && _agent.hasPath)
        {
            _agent.ResetPath();
        }
    }
}