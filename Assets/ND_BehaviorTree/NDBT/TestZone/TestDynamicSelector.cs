using System;
using System.Collections;
using System.Collections.Generic;
using ND_BehaviorTree;
using UnityEngine;

[System.Serializable]
public class TestDynamicSelector : IDynamicBranchSelector
{
    public int hoh;
    [BlackboardKeyType(typeof(int))]
    public Key testKey;

    public Animator animator;

    public void OnInitialize(GameObject ownerGameObject)
    {

    }

    public bool TrySelectBranch(GameObject ownerGameObject, Blackboard blackboard, out int selectedIndex)
    {
        if (animator == null)
        {
            Debug.LogWarning("[SoraCondition] Animator is not set. Cannot perform action.");
            selectedIndex = 0;
        }
        animator = ownerGameObject.GetComponent<Animator>();
        animator.SetTrigger("SomeTrigger");
        selectedIndex = 1;
        return animator.enabled;
            
    }

}
