// --- START OF FILE DynamicEvent.cs (Updated) ---

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using Sirenix.Serialization;

// [Serializable] is no longer needed because Odin handles this class.
public class DynamicEvent
{
    [BoxGroup("Event Settings")]
    public string eventName;

    [BoxGroup("Event Settings")]
    [OnValueChanged("ClearMethodSelection")]
    public Component target;

    [BoxGroup("Event Settings")]
    [ValueDropdown("GetAvailableMethods")]
    [OnValueChanged("OnMethodSelected")]
    [ShowIf("target")]
    [DisplayAsString(false)]
    public string methodName;

    [BoxGroup("Parameters")]
    // --- KEY CHANGE HERE ---
    // Remove [OdinSerialize] and the ListDrawerSettings.
    // The GenericParameterDrawer will handle drawing each item.
    public List<GenericParameter> genericParameters = new List<GenericParameter>();
    
    public void SetParameterValue<T>(string parameterName, T value)
    {
        // ... (rest of the file is unchanged) ...
// ... (rest of the file is unchanged) ...
// ... (rest of the file is unchanged) ...
        var parameter = genericParameters.FirstOrDefault(p => p.parameterName == parameterName);

        if (parameter == null)
        {
            Debug.LogWarning($"In event '{eventName}', could not find a parameter named '{parameterName}'.", target);
            return;
        }

        parameter.SetConstantValue(value);
    }


    public void Invoke()
    {
        if (target == null || string.IsNullOrEmpty(methodName))
        {
            if(target == null) Debug.LogError($"DynamicEvent '{eventName}': Target component is null.", target);
            if(string.IsNullOrEmpty(methodName)) Debug.LogError($"DynamicEvent '{eventName}': Method name is not specified.", target);
            return;
        }

        MethodInfo methodInfo = target.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => MethodSignature(m) == this.methodName);

        if (methodInfo == null)
        {
             methodInfo = target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => MethodSignature(m) == this.methodName);
        }

        if (methodInfo != null)
        {
            var paramValues = genericParameters.Select(p => p.GetValue()).ToArray();
            methodInfo.Invoke(target, paramValues);
        }
        else
        {
            Debug.LogError($"DynamicEvent '{eventName}': Method with signature '{methodName}' not found on component '{target.GetType().Name}'.", target);
        }
    }

    private void ClearMethodSelection()
    {
        methodName = null;
        genericParameters.Clear();
    }

    private void OnMethodSelected(string newMethodName)
    {
        genericParameters.Clear();

        if (target == null || string.IsNullOrEmpty(newMethodName)) return;

        var selectedMethod = target.GetType()
                               .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                               .FirstOrDefault(m => MethodSignature(m) == newMethodName);

        if (selectedMethod == null)
        {
             selectedMethod = target.GetType()
                               .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                               .FirstOrDefault(m => MethodSignature(m) == newMethodName);
        }

        if (selectedMethod == null) return;

        foreach (var paramInfo in selectedMethod.GetParameters())
        {
            Type genericParamType = typeof(GenericParameter<>).MakeGenericType(paramInfo.ParameterType);
            GenericParameter newParam = (GenericParameter)Activator.CreateInstance(genericParamType);
            newParam.parameterName = paramInfo.Name;
            genericParameters.Add(newParam);
        }
    }

    private IEnumerable<ValueDropdownItem<string>> GetAvailableMethods()
    {
        if (target == null) yield break;

        yield return new ValueDropdownItem<string>("No Function", null);

        var methods = target.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethod && m.GetParameters().All(p => IsTypeSupported(p.ParameterType)))
            .OrderBy(m => m.Name);

        foreach (var method in methods)
        {
            string signature = MethodSignature(method);
            yield return new ValueDropdownItem<string>(signature, signature);
        }
    }

    private string MethodSignature(MethodInfo method)
    {
        string paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName));
        return $"{method.Name}({paramStr})";
    }

    private bool IsTypeSupported(Type type)
    {
        if (type.IsByRef) return false;
        if (type.IsPrimitive || type == typeof(string)) return true;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
        if (type.IsSerializable) return true;
        return false;
    }
}