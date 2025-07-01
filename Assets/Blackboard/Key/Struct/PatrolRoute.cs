// --- File: PatrolRoute.cs ---

using UnityEngine;

// This attribute is ESSENTIAL for Unity to serialize the struct.
[System.Serializable]
public struct PatrolRoute
{
    public Vector3 startPoint;
    public Vector3 endPoint;
    public float waitTimeAtEachPoint;
    public bool loop;

    // You can also add a constructor for convenience.
    public PatrolRoute(Vector3 start, Vector3 end, float waitTime, bool shouldLoop)
    {
        this.startPoint = start;
        this.endPoint = end;
        this.waitTimeAtEachPoint = waitTime;
        this.loop = shouldLoop;
    }
}