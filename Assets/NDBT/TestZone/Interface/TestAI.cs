using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAI : BehaviorTreeRunner, ISawPlayer
{
    public void OnSawPlayer(Transform playerTransform, bool isSeePlayer)
    {
        RuntimeTree.blackboard .SetValue<Transform>("PlayerTransform", playerTransform);
        RuntimeTree.blackboard .SetValue<bool>("IsSawPlayer", isSeePlayer);
    }
}
