using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TestAI : BehaviorTreeRunner, ISawPlayer
{
    public void OnSawPlayer(Transform playerTransform, bool isSeePlayer)
    {
        RuntimeTree.blackboard.SetValue<Transform>("TargetTransform", playerTransform);
        RuntimeTree.blackboard.SetValue<bool>("IsSawTarget", isSeePlayer);
    }
}
