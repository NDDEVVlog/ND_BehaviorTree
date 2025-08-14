using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TestAI : BehaviorTreeRunner, ISawPlayer
{
    public override void Init()
    {
        base.Init();
        NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
        RuntimeTree.blackboard.SetValue<NavMeshAgent>("EnermyAgent", navMeshAgent);
        RuntimeTree.blackboard.SetValue<GameObject>("Self", this.gameObject);

    }
    public void OnSawPlayer(Transform playerTransform, bool isSeePlayer)
    {
        RuntimeTree.blackboard.SetValue<Transform>("TargetTransform", playerTransform);
        RuntimeTree.blackboard.SetValue<bool>("IsSawTarget", isSeePlayer);
    }
}
