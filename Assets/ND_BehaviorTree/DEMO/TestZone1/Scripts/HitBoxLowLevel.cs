using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitBoxLowLevel : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<BehaviorTreeRunner>())
        {
            BehaviorTreeRunner behaviorTreeRunner = other.gameObject.GetComponent<BehaviorTreeRunner>();
            behaviorTreeRunner.RuntimeTree.blackboard.SetValue("HitBoxLowLevel", true);
        }
    }
}
