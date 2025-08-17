using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FieldOfView : MonoBehaviour
{
    public float radius;
    [Range(0, 360)]
    public float angle;

    public List<GameObject> targetObjects = new List<GameObject>();

    public LayerMask targetMask;
    public LayerMask obstructionMask;

    public Vector3 offset;

    private Dictionary<GameObject, bool> targetVisibility = new Dictionary<GameObject, bool>();

    ISawPlayer OnPlayerSee;

    private void Start()
    {
        StartCoroutine(FOVRoutine());
        OnPlayerSee = this.gameObject.GetComponent<ISawPlayer>();
        
        // Initialize visibility dictionary for all targets
        InitializeTargetVisibility();
    }

    private void InitializeTargetVisibility()
    {
        // Create a copy of the list to avoid modifying it during iteration
        List<GameObject> validTargets = new List<GameObject>(targetObjects);
        targetObjects.Clear();

        foreach (GameObject target in validTargets)
        {
            if (target != null)
            {
                targetObjects.Add(target);
                targetVisibility[target] = false;
            }
        }
    }

    private IEnumerator FOVRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.2f);

        while (true)
        {
            yield return wait;
            FieldOfViewCheck();
        }
    }

    private void FieldOfViewCheck()
    {
        // Create a list to store targets to remove
        List<GameObject> targetsToRemove = new List<GameObject>();

        
        // Update visibility for each target
        foreach (GameObject target in targetObjects)
        {
            if (target == null)
            {
                targetsToRemove.Add(target);
                continue;
            }

            // Calculate direction to target
            Vector3 directionToTarget = ((offset + target.transform.position) - transform.position).normalized;

            // Check if target is within field of view angle
            bool canSee = false;
            if (Vector3.Angle(transform.forward, directionToTarget) < angle / 2)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                // Check if the target is within radius and not obstructed
                if (distanceToTarget <= radius && !Physics.Raycast(transform.position + offset, directionToTarget, distanceToTarget, obstructionMask))
                {
                    canSee = true;
                }
            }

            // Update visibility status and trigger event if changed
            if (targetVisibility.ContainsKey(target) && targetVisibility[target] != canSee && target != null)
            {
                targetVisibility[target] = canSee;
                OnPlayerSee?.OnSawPlayer(target.transform, canSee);
            }
        }

        foreach (GameObject target in targetsToRemove)
        {
            if (targetObjects.Contains(target))
            {   

                targetObjects.Remove(target);
                 
            }
            if (targetVisibility.ContainsKey(target))
            {

                targetVisibility.Remove(target);
            }
        }
    }
}