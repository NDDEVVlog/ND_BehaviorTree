using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpellTech.DynamicMethodEvent
{
    [Serializable]
    public class DynamicEvent
    {
        public string eventName;
        public Component target;
        public string methodName;

        [SerializeReference]
        public List<GenericParameter> genericParameters = new List<GenericParameter>();

        // MethodInfo cache: reflection lookup only when the target type, method name or parameter types change.
        [NonSerialized] private MethodInfo m_CachedMethod;
        [NonSerialized] private Type m_CachedTargetType;
        [NonSerialized] private string m_CachedMethodName;
        [NonSerialized] private Type[] m_CachedParamTypes;
        [NonSerialized] private object[] m_ParamValues;

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

            int count = genericParameters != null ? genericParameters.Count : 0;
            MethodInfo methodInfo = ResolveMethod(count);

            if (methodInfo != null)
            {
                if (m_ParamValues == null || m_ParamValues.Length != count) m_ParamValues = new object[count];
                for (int i = 0; i < count; i++) m_ParamValues[i] = genericParameters[i]?.GetValue();

                try
                {
                    methodInfo.Invoke(target, m_ParamValues);
                }
                catch (Exception e)
                {
                    Debug.LogError($"DynamicEvent '{eventName}': Error invoking method '{methodName}'.\n{e}", target);
                }
            }
            else
            {
                var typesStr = string.Join(", ", m_CachedParamTypes.Select(t => t != null ? t.Name : "null"));
                Debug.LogError($"DynamicEvent '{eventName}': Method '{methodName}({typesStr})' not found on component '{target.GetType().Name}'.", target);
            }
        }

        private MethodInfo ResolveMethod(int paramCount)
        {
            Type targetType = target.GetType();
            bool valid = m_CachedParamTypes != null
                         && m_CachedTargetType == targetType
                         && m_CachedMethodName == methodName
                         && m_CachedParamTypes.Length == paramCount;
            if (valid)
            {
                for (int i = 0; i < paramCount; i++)
                {
                    if (m_CachedParamTypes[i] != genericParameters[i]?.GetParameterType()) { valid = false; break; }
                }
            }
            if (valid) return m_CachedMethod;

            var paramTypes = new Type[paramCount];
            for (int i = 0; i < paramCount; i++) paramTypes[i] = genericParameters[i]?.GetParameterType();

            m_CachedTargetType = targetType;
            m_CachedMethodName = methodName;
            m_CachedParamTypes = paramTypes;
            m_CachedMethod = Array.IndexOf(paramTypes, null) >= 0 ? null : targetType.GetMethod(methodName, paramTypes);
            return m_CachedMethod;
        }
    }
}
