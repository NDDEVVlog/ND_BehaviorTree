using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ExecuteCode : MonoBehaviour
{
    public UnityEvent executeEvent;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            executeEvent?.Invoke();
        }
    }
}
