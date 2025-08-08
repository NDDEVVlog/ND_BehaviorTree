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
public class TestEnumKey : Key<TestEnum> {}