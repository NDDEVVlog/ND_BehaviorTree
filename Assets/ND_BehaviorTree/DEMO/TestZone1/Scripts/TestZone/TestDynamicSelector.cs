using System;
using System.Collections;
using System.Collections.Generic;
using ND_BehaviorTree;
using UnityEngine;

[System.Serializable]
public class TestDynamicSelector : IDynamicBranchSelector
{
    public Key<float> healthKey;

    public void OnInitialize(GameObject ownerGameObject)
    {

    }

    public bool TrySelectBranch(GameObject ownerGameObject, Blackboard blackboard, out int selectedIndex)
    {   
        float health = healthKey.GetValue(blackboard);

        if (health > 0f)
        {
            selectedIndex = 0; // Low health branch
        }
        else
        {
            selectedIndex = 1; // Normal health branch
        }

        return true;    
    }

}
