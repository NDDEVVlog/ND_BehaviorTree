// FILE: MustBeLinkedAttribute.cs
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// When placed on a Key field, this attribute enforces that the key must be
    /// linked to the Blackboard and cannot be a direct value.
    /// It also disables the "[None] (Direct Value)" option in the context menu.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class MustBeLinkedAttribute : PropertyAttribute { }
}