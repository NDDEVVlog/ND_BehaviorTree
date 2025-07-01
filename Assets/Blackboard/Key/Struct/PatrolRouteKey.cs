// --- File: PatrolRouteKey.cs ---

using UnityEngine;

// We add CreateAssetMenu so we can create instances of this key in the project.
[CreateAssetMenu(fileName = "New PatrolRouteKey", menuName = "AI/Keys/Patrol Route Key")]
public class PatrolRouteKey : Key<PatrolRoute>
{
    // That's it! The parent class Key<PatrolRoute> handles everything else.
    // You could add custom logic or editor code here if you wanted, but it's not required.
}