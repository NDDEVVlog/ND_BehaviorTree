// --- START OF FILE GenericParameter.cs (Simplified) ---

using System;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class GenericParameter<T> : GenericParameter
{
    // This is the only value field we need now.
    [HideLabel] // We'll draw the label using the parameterName in our custom drawer.
    public T constantValue;

    public override object GetValue()
    {
        return constantValue;
    }

    public override Type GetParameterType()
    {
        return typeof(T);
    }
    
    public override void SetConstantValue(object value)
    {
        if (value is T typedValue)
        {
            this.constantValue = typedValue;
        }
        else
        {
            try
            {
                this.constantValue = (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to set parameter '{parameterName}'. Cannot convert type {value?.GetType().Name ?? "null"} to {typeof(T).Name}. Error: {e.Message}");
            }
        }
    }
}


[Serializable]
public abstract class GenericParameter
{   
    // We still need this for UI labels and for programmatic lookup.
    [HideInInspector]
    public string parameterName;

    // --- ALL DYNAMIC BINDING LOGIC HAS BEEN REMOVED ---
    
    public abstract object GetValue();
    public abstract Type GetParameterType();
    public abstract void SetConstantValue(object value);
}