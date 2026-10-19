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

    /// <summary>
    /// Runs every 0.2 s. Allocation-free: iterates backwards so destroyed targets are removed in place
    /// (previously a new List was allocated on every check).
    /// </summary>
    private void FieldOfViewCheck()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        float halfAngle = angle * 0.5f;

        for (int i = targetObjects.Count - 1; i >= 0; i--)
        {
            GameObject target = targetObjects[i];
            if (target == null)
            {
                // Destroyed target: drop it (the dictionary key is the same reference, so Remove still finds it).
                targetVisibility.Remove(target);
                targetObjects.RemoveAt(i);
                continue;
            }

            Vector3 targetPosition = target.transform.position;
            Vector3 directionToTarget = ((offset + targetPosition) - origin).normalized;

            bool canSee = false;
            if (Vector3.Angle(forward, directionToTarget) < halfAngle)
            {
                float distanceToTarget = Vector3.Distance(origin, targetPosition);
                canSee = distanceToTarget <= radius && !Physics.Raycast(origin + offset, directionToTarget, distanceToTarget, obstructionMask);
            }

            // Notify only when the visibility of this target changes.
            if (targetVisibility.TryGetValue(target, out bool wasVisible) && wasVisible != canSee)
            {
                targetVisibility[target] = canSee;
                OnPlayerSee?.OnSawPlayer(target.transform, canSee);
            }
        }
    }
}