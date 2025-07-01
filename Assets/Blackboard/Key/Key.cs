using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Key : ScriptableObject
{
    [SerializeField]
    private string category = string.Empty;

    [SerializeField]
    private string description = string.Empty;

    public abstract object GetValueObject();
    
    
}
