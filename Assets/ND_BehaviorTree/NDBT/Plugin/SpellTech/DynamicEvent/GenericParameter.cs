

using System;
using System.Reflection;
using UnityEngine;

[Serializable]
public class GenericParameter<T> : GenericParameter
{
    public T constantValue;

    public override object GetValue()
    {
        if (useConstant)
        {
            return constantValue;
        }
        
        object sourceValue = GetValueFromSource();
        if (sourceValue == null) return default(T);
        
        if (sourceValue is T variable)
        {
            return variable;
        }
        
        try
        {
            return Convert.ChangeType(sourceValue, typeof(T));
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not convert dynamic parameter '{sourceFieldName}' of type {sourceValue.GetType().Name} to {typeof(T).Name}. Error: {e.Message}", sourceComponent);
            return default(T);
        }
    }

    public override Type GetParameterType()
    {
        return typeof(T);
    }
}

[Serializable]
public abstract class GenericParameter
{
    public string parameterName;
    public bool useConstant = true;
    public Component sourceComponent;
    public string sourceFieldName;

    public abstract object GetValue();
    public abstract Type GetParameterType();

    protected object GetValueFromSource()
    {
        if (sourceComponent == null || string.IsNullOrEmpty(sourceFieldName))
        {
            Debug.LogWarning("Dynamic parameter source is not configured.", sourceComponent);
            return null;
        }

        Type sourceType = sourceComponent.GetType();

        PropertyInfo propInfo = sourceType.GetProperty(sourceFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (propInfo != null && propInfo.CanRead)
        {
            return propInfo.GetValue(sourceComponent);
        }

        FieldInfo fieldInfo = sourceType.GetField(sourceFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (fieldInfo != null)
        {
            return fieldInfo.GetValue(sourceComponent);
        }

        Debug.LogError($"Could not find public property or field named '{sourceFieldName}' on component '{sourceType.Name}'.", sourceComponent);
        return null;
    }
}

// Primitives
