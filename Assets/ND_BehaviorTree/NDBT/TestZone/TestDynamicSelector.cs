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
    
    public void OnInitialize(GameObject ownerGameObject)
    {

    }

    public bool TrySelectBranch(GameObject ownerGameObject, Blackboard blackboard, out int selectedIndex)
    {
        selectedIndex = 1;
        return true;
    }

}
