using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FieldOfView : MonoBehaviour
{
    public float radius;
    [Range(0, 360)]
    public float angle;

    public GameObject playerRef;

    public LayerMask targetMask;
    public LayerMask obstructionMask;

   // public UnityEvent OnSeeEvent;

    public Vector3 offset;

    public bool _canSeePlayer;
    public bool canSeePlayer
    {
        get { return _canSeePlayer; }
        set
        {
            
            _canSeePlayer = value;
            OnPlayerSee.OnSawPlayer(playerRef.transform, value);
           /* if (_canSeePlayer != value)
            {
               
                if (_canSeePlayer)
                {
                    
                    
                }
            }*/
        }
    }


    ISawPlayer OnPlayerSee;

    private void Start()
    {
        StartCoroutine(FOVRoutine());
        OnPlayerSee = this.gameObject.GetComponent<ISawPlayer>();
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
        // If playerRef is null, we cannot perform checks
        if (playerRef == null) return;

        // Calculate direction to target
        Vector3 directionToTarget = ((offset + playerRef.transform.position) - transform.position).normalized;

        // Check if target is within field of view angle
        if (Vector3.Angle(transform.forward, directionToTarget) < angle / 2)
        {
            float distanceToTarget = Vector3.Distance(transform.position, playerRef.transform.position);

            // Check if the target is within radius and not obstructed
            if (distanceToTarget <= radius && !Physics.Raycast(transform.position + offset, directionToTarget, distanceToTarget, obstructionMask))
            {
                
                canSeePlayer = true;
            }
            else
            {
                canSeePlayer = false;
            }
        }
        else
        {
            canSeePlayer = false;
        }
    }
}
