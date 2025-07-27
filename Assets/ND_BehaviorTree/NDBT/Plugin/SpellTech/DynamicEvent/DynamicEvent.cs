using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable]
public class DynamicEvent
{
    public string eventName;
    public Component target;
    public string methodName;

    [SerializeReference]
    public List<GenericParameter> genericParameters = new List<GenericParameter>();

    public void Invoke()
    {
        if (target == null)
        {
            Debug.LogError($"DynamicEvent '{eventName}': Target component is null.", target);
            return;
        }

        if (string.IsNullOrEmpty(methodName))
        {
            Debug.LogError($"DynamicEvent '{eventName}': Method name is not specified.", target);
            return;
        }
        
        var paramTypes = genericParameters.Select(p => p.GetParameterType()).ToArray();
        var paramValues = genericParameters.Select(p => p.GetValue()).ToArray();

        MethodInfo methodInfo = target.GetType().GetMethod(methodName, paramTypes);

        if (methodInfo != null)
        {
            try
            {
                methodInfo.Invoke(target, paramValues);
            }
            catch (Exception e)
            {
                Debug.LogError($"DynamicEvent '{eventName}': Error invoking method '{methodName}'.\n{e}", target);
            }
        }
        else
        {
            var typesStr = string.Join(", ", paramTypes.Select(t => t.Name));
            Debug.LogError($"DynamicEvent '{eventName}': Method '{methodName}({typesStr})' not found on component '{target.GetType().Name}'.", target);
        }
    }
}