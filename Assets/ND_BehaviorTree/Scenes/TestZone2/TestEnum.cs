using System.Collections;
using System.Collections.Generic;
using ND_BehaviorTree;
using UnityEngine;

public enum TestEnum
{
    Idle,
    Patrolling,
    Chasing,
    Attacking
}

[CreateAssetMenu(menuName = "ND_BehaviorTree/Keys/AIState Key")]
    public class TestEnumKey : Key<TestEnum> {}