using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Key<T> : Key
{
    [SerializeField]
    private T value;


    public event Action ValueChanged;
    /// <summary>
    /// System object reference value.
    /// </summary>
    public override object GetValueObject()
    {
        return value;
    }

    public T GetValue()
    {
        return value;
    }


    public void SetValue(T newValue)
    {
        // Use C#'s default equality comparer. Works for primitives, structs, and reference types.
        if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(value, newValue))
        {
            this.value = newValue;
            ValueChanged?.Invoke(); // Fire the event
        }
    }    
}
