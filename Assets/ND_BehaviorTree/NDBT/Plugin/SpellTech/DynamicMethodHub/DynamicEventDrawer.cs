using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

namespace SpellTech.DynamicMethodEvent
{
    [CustomPropertyDrawer(typeof(DynamicEvent))]
    public class DynamicEventDrawer : PropertyDrawer
    {
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private static Dictionary<Type, Dictionary<string, MethodInfo[]>> _methodCache = new Dictionary<Type, Dictionary<string, MethodInfo[]>>();
        private static Dictionary<Type, Type> _typeToParameterTypeMap;

        private static Dictionary<Type, Type> TypeToParameterTypeMap
        {
            get
            {
                if (_typeToParameterTypeMap == null)
                {
                    _typeToParameterTypeMap = new Dictionary<Type, Type>();
                    var gameAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                    if (gameAssembly != null)
                    {
                        var parameterTypes = gameAssembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract && IsSubclassOfGeneric(t, typeof(GenericParameter<>)));
                        foreach (var paramType in parameterTypes)
                        {
                            var baseType = paramType.BaseType;
                            if (baseType != null && baseType.IsGenericType)
                            {
                                var genericArgument = baseType.GetGenericArguments()[0];
                                if (!TypeToParameterTypeMap.ContainsKey(genericArgument))
                                {
                                    _typeToParameterTypeMap.Add(genericArgument, paramType);
                                }
                            }
                        }
                    }
                }
                return _typeToParameterTypeMap;
            }
        }

        private static bool IsSubclassOfGeneric(Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        private struct MethodSelection
        {
            public Component TargetComponent;
            public MethodInfo Method;
            public SerializedProperty Property;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string uniqueId = property.propertyPath;
            if (!foldoutStates.ContainsKey(uniqueId))
            {
                foldoutStates[uniqueId] = true;
            }

            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float currentY = position.y;

            Rect foldoutRect = new Rect(position.x, currentY, position.width, lineHeight);
            foldoutStates[uniqueId] = EditorGUI.Foldout(foldoutRect, foldoutStates[uniqueId], label, true);
            currentY += lineHeight + spacing;

            if (foldoutStates[uniqueId])
            {
                EditorGUI.indentLevel++;

                SerializedProperty eventNameProp = property.FindPropertyRelative("eventName");
                SerializedProperty targetProp = property.FindPropertyRelative("target");
                SerializedProperty methodNameProp = property.FindPropertyRelative("methodName");
                SerializedProperty genericParamsProp = property.FindPropertyRelative("genericParameters");

                EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), eventNameProp);
                currentY += lineHeight + spacing;

                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), targetProp);
                if (EditorGUI.EndChangeCheck())
                {
                    methodNameProp.stringValue = string.Empty;
                    genericParamsProp.ClearArray();
                    property.serializedObject.ApplyModifiedProperties();
                }
                currentY += lineHeight + spacing;

                Component targetComponent = targetProp.objectReferenceValue as Component;

                Rect methodRect = new Rect(position.x, currentY, position.width, lineHeight);
                
                string buttonLabel = "No Function";
                MethodInfo selectedMethod = null;
                if (targetComponent != null && !string.IsNullOrEmpty(methodNameProp.stringValue))
                {
                    var parameterTypes = GetParameterTypesFromSerializedProperty(genericParamsProp);
                    if (parameterTypes != null)
                    {
                        selectedMethod = targetComponent.GetType().GetMethod(methodNameProp.stringValue, parameterTypes);
                    }

                    if (selectedMethod != null)
                    {
                        buttonLabel = $"{targetComponent.GetType().Name}/{MethodSignature(selectedMethod)}";
                    }
                    else
                    {
                        buttonLabel = "Missing: " + methodNameProp.stringValue;
                        methodNameProp.stringValue = string.Empty;
                    }
                }

                if (EditorGUI.DropdownButton(methodRect, new GUIContent(buttonLabel), FocusType.Keyboard))
                {
                    GenerateMethodMenu(property).ShowAsContext();
                }
                currentY += lineHeight + spacing;
                
                if (selectedMethod != null)
                {
                    var parameters = selectedMethod.GetParameters();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i >= genericParamsProp.arraySize) continue;
                        
                        SerializedProperty paramElement = genericParamsProp.GetArrayElementAtIndex(i);
                        var paramInfo = parameters[i];
                        
                        float paramHeight = EditorGUI.GetPropertyHeight(paramElement, true);
                        Rect paramPropertyRect = new Rect(position.x, currentY, position.width, paramHeight);
                        EditorGUI.PropertyField(paramPropertyRect, paramElement, new GUIContent(ObjectNames.NicifyVariableName(paramInfo.Name)), true);
                        currentY += paramHeight + spacing;
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        private GenericMenu GenerateMethodMenu(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            
            SerializedProperty targetProp = property.FindPropertyRelative("target");
            Component targetComponent = targetProp.objectReferenceValue as Component;
            
            if (targetComponent == null)
            {
                menu.AddDisabledItem(new GUIContent("Assign a target component first"));
                return menu;
            }

            menu.AddItem(new GUIContent("No Function"), false, () => {
                var prop = property.Copy();
                prop.FindPropertyRelative("methodName").stringValue = string.Empty;
                prop.FindPropertyRelative("genericParameters").arraySize = 0;
                prop.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            var components = targetComponent.gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;
                
                var componentType = component.GetType();
                if (!_methodCache.ContainsKey(componentType))
                {
                    var methods = componentType
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(m => !m.IsSpecialName && !m.IsGenericMethod && !m.GetParameters().Any(p => p.IsOut) && m.GetParameters().All(p => TypeToParameterTypeMap.ContainsKey(p.ParameterType) || p.ParameterType.IsEnum))
                        .OrderBy(m => m.Name)
                        .ToArray();
                    _methodCache[componentType] = methods.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToArray());
                }

                var cachedMethods = _methodCache[componentType];
                foreach (var methodGroup in cachedMethods)
                {
                    foreach (var method in methodGroup.Value)
                    {
                        string menuPath = $"{componentType.Name}/{MethodSignature(method)}";
                        var selection = new MethodSelection { TargetComponent = component, Method = method, Property = property };
                        menu.AddItem(new GUIContent(menuPath), false, OnMethodSelected, selection);
                    }
                }
            }
            
            return menu;
        }
        
        private void OnMethodSelected(object userData)
        {
            var selection = (MethodSelection)userData;
            var property = selection.Property.Copy();
            var targetProp = property.FindPropertyRelative("target");
            var methodNameProp = property.FindPropertyRelative("methodName");
            var genericParamsProp = property.FindPropertyRelative("genericParameters");
            
            targetProp.objectReferenceValue = selection.TargetComponent;
            methodNameProp.stringValue = selection.Method.Name;

            var parameters = selection.Method.GetParameters();
            genericParamsProp.arraySize = parameters.Length;
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramInfo = parameters[i];
                var paramElement = genericParamsProp.GetArrayElementAtIndex(i);
                
                if (TypeToParameterTypeMap.TryGetValue(paramInfo.ParameterType, out Type concreteParameterType))
                {
                    paramElement.managedReferenceValue = Activator.CreateInstance(concreteParameterType);
                    var nameProp = paramElement.FindPropertyRelative("parameterName");
                    if (nameProp != null) nameProp.stringValue = paramInfo.Name;
                }
                else
                {
                    Debug.LogError($"Could not find a GenericParameter wrapper for type {paramInfo.ParameterType}. Make sure a class like '[Serializable] public class MyTypeParameter : GenericParameter<MyType> {{}}' exists.");
                    paramElement.managedReferenceValue = null;
                }
            }

            property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string uniqueId = property.propertyPath;
            if (!foldoutStates.ContainsKey(uniqueId) || !foldoutStates[uniqueId])
            {
                return EditorGUIUtility.singleLineHeight;
            }

            float totalHeight = EditorGUIUtility.singleLineHeight; // Foldout
            totalHeight += EditorGUIUtility.standardVerticalSpacing;

            totalHeight += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3; // Name, Target, Method button

            SerializedProperty genericParamsProp = property.FindPropertyRelative("genericParameters");
            for (int i = 0; i < genericParamsProp.arraySize; i++)
            {
                totalHeight += EditorGUI.GetPropertyHeight(genericParamsProp.GetArrayElementAtIndex(i), true) + EditorGUIUtility.standardVerticalSpacing;
            }
            
            return totalHeight;
        }

        private string MethodSignature(MethodInfo method)
        {
            string paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{method.Name}({paramStr})";
        }

        private Type[] GetParameterTypesFromSerializedProperty(SerializedProperty genericParamsProp)
        {
            if (genericParamsProp == null) return null;
            var types = new Type[genericParamsProp.arraySize];
            for (int i = 0; i < genericParamsProp.arraySize; i++)
            {
                var paramElement = genericParamsProp.GetArrayElementAtIndex(i);
                if (paramElement.managedReferenceValue is GenericParameter gp)
                {
                    types[i] = gp.GetParameterType();
                }
                else
                {
                    return null;
                }
            }
            return types;
        }
    }
}