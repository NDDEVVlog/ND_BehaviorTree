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
        Debug.Log("HitBoxLowLevel: OnTriggerEnter with " + other.gameObject.name);
        if (other.gameObject.GetComponent<BehaviorTreeRunner>())
        {
            BehaviorTreeRunner behaviorTreeRunner = other.gameObject.GetComponent<BehaviorTreeRunner>();
            float health = behaviorTreeRunner.RuntimeTree.blackboard.GetValue<float>("Health");
            behaviorTreeRunner.RuntimeTree.blackboard.SetValue("Health", health - 100);
        }
    }
}
