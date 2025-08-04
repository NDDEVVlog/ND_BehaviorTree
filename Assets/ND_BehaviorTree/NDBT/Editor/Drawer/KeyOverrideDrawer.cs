// FILE: Editor/KeyOverrideDrawer.cs (Corrected and Final Version)

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;

namespace ND_BehaviorTree.Editor
{
    [CustomPropertyDrawer(typeof(KeyOverride))]
    public class KeyOverrideDrawer : PropertyDrawer
    {
        private const string NO_OVERRIDE_LABEL = "Not Overridden";
        private static Dictionary<Type, Type> _typeToOverrideDataMap;

        // Static constructor runs once when the editor loads the class.
        static KeyOverrideDrawer()
        {
            BuildTypeMap();
        }

        private static void BuildTypeMap()
        {
            _typeToOverrideDataMap = new Dictionary<Type, Type>();

            var overrideDataTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(KeyOverrideData)) && !t.IsAbstract);

            foreach (var odType in overrideDataTypes)
            {
                // Make the binding flags more robust to find the 'value' field even in base classes.
                FieldInfo valueField = odType.GetField("value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (valueField == null)
                {
                     // If not in the declared type, check the base type (for our generic wrappers)
                     if(odType.BaseType != null)
                     {
                        valueField = odType.BaseType.GetField("value", BindingFlags.Public | BindingFlags.Instance);
                     }
                }

                if (valueField != null)
                {
                    Type valueType = valueField.FieldType;
                    if (!_typeToOverrideDataMap.ContainsKey(valueType))
                    {
                        _typeToOverrideDataMap.Add(valueType, odType);
                    }
                }
            }
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keyNameProp = property.FindPropertyRelative("keyName");
            var dataProp = property.FindPropertyRelative("data");

            Blackboard blackboardTemplate = GetBlackboardTemplateFromProperty(property);
            
            if (blackboardTemplate == null)
            {
                EditorGUI.LabelField(position, "Assign a Tree/Blackboard with keys first.");
                EditorGUI.EndProperty();
                return;
            }

            var keys = blackboardTemplate.keys;
            List<string> keyNames = keys.Where(k => k != null && !string.IsNullOrEmpty(k.keyName)).Select(k => k.keyName).ToList();
            keyNames.Insert(0, NO_OVERRIDE_LABEL);

            int currentIndex = string.IsNullOrEmpty(keyNameProp.stringValue) ? 0 : keyNames.IndexOf(keyNameProp.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            Rect popupRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            int newIndex = EditorGUI.Popup(popupRect, "Key To Override", currentIndex, keyNames.ToArray());

            if (newIndex != currentIndex)
            {
                keyNameProp.stringValue = (newIndex == 0) ? "" : keyNames[newIndex];
                Key sourceKey = (newIndex > 0) ? keys.FirstOrDefault(k => k.keyName == keyNameProp.stringValue) : null;

                if (sourceKey != null)
                {
                    // Pass the property to get the context object for the warning message.
                    dataProp.managedReferenceValue = CreateOverrideDataForType(sourceKey.GetValueType(), property);
                }
                else
                {
                    dataProp.managedReferenceValue = null;
                }
            }

            if (newIndex > 0 && dataProp.managedReferenceValue != null)
            {
                var valueProp = dataProp.FindPropertyRelative("value");
                if (valueProp != null)
                {
                    float valueHeight = EditorGUI.GetPropertyHeight(valueProp, true);
                    Rect valueRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, valueHeight);
                    
                    if (dataProp.managedReferenceValue is OverrideDataEnum)
                    {
                         Key sourceKey = keys.FirstOrDefault(k => k.keyName == keyNameProp.stringValue);
                         DrawEnumField(valueRect, valueProp, sourceKey.GetValueType());
                    }
                    else
                    {
                         EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var dataProp = property.FindPropertyRelative("data");
            float baseHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (dataProp.managedReferenceValue != null)
            {
                var valueProp = dataProp.FindPropertyRelative("value");
                if(valueProp != null)
                {
                    return baseHeight + EditorGUI.GetPropertyHeight(valueProp, true);
                }
            }
            return EditorGUIUtility.singleLineHeight;
        }
        
        // *** FIX APPLIED HERE: Added 'property' parameter to get the context object ***
        private KeyOverrideData CreateOverrideDataForType(Type keyType, SerializedProperty property)
        {
            if (_typeToOverrideDataMap.TryGetValue(keyType, out Type overrideDataType))
            {
                return Activator.CreateInstance(overrideDataType) as KeyOverrideData;
            }

            if (keyType.IsEnum)
            {
                return new OverrideDataEnum { enumType = keyType.AssemblyQualifiedName };
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(keyType))
            {
                return new OverrideDataObject();
            }

            // *** FIX APPLIED HERE: Use property.serializedObject.targetObject as the context ***
            Debug.LogWarning($"No dedicated override data class found for type '{keyType.Name}'. Please create a class like '[Serializable] public class OverrideData{keyType.Name} : OverrideDataValue<{keyType.Name}> {{}}' to support it.", property.serializedObject.targetObject);
            return null;
        }
        
        private void DrawEnumField(Rect position, SerializedProperty valueProp, Type enumType)
        {
            if (enumType == null) return;
            Enum currentValue = (Enum)Enum.ToObject(enumType, valueProp.intValue);
            Enum newValue = EditorGUI.EnumPopup(position, currentValue);
            valueProp.intValue = Convert.ToInt32(newValue);
        }

        private Blackboard GetBlackboardTemplateFromProperty(SerializedProperty property)
        {

            if (property.serializedObject.targetObject is BehaviorTreeRunner runner)
            {
                return runner.treeAsset?.blackboard;
            }
            return null;
        }
    }
}