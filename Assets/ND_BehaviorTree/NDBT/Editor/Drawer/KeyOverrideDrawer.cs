// FILE: Editor/KeyOverrideDrawer.cs (CORRECTED AND FINAL)

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Events;

namespace ND_BehaviorTree.Editor
{
    [CustomPropertyDrawer(typeof(KeyOverride))]
    public class KeyOverrideDrawer : PropertyDrawer
    {
        private const string NO_OVERRIDE_LABEL = "Not Overridden";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keyNameProp = property.FindPropertyRelative("keyName");
            var dataProp = property.FindPropertyRelative("data");

            // --- DYNAMICALLY FIND THE BLACKBOARD TEMPLATE ---
            Blackboard blackboardTemplate = null;
             if (property.serializedObject.targetObject is BehaviorTreeRunner runner)
            {
                blackboardTemplate = runner.treeAsset?.blackboard;
            }
            
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
                    dataProp.managedReferenceValue = CreateOverrideDataForType(sourceKey.GetValueType());
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

        private KeyOverrideData CreateOverrideDataForType(Type keyType)
        {
            switch (true)
            {
                case bool _ when keyType == typeof(float):
                    return new OverrideDataFloat();
                    
                case bool _ when keyType == typeof(int):
                    return new OverrideDataInt();
                    
                case bool _ when keyType == typeof(bool):
                    return new OverrideDataBool();
                    
                case bool _ when keyType == typeof(string):
                    return new OverrideDataString();
                    
                case bool _ when keyType == typeof(Vector3):
                    return new OverrideDataVector3();

                case bool _ when keyType == typeof(Transform):
                    return new OverrideDataTransform();
                    
                case bool _ when keyType == typeof(UnityEvent):
                    return new OverrideDataUnityEvent();
                    
                case bool _ when keyType.IsEnum:
                    return new OverrideDataEnum { enumType = keyType.AssemblyQualifiedName };
                    
                case bool _ when typeof(UnityEngine.Object).IsAssignableFrom(keyType):
                    return new OverrideDataObject();
                    
                default:
                    Debug.LogWarning($"No dedicated override data class for type {keyType.Name}. Override may not work as expected.");
                    return null;
            }
        }
        
        private void DrawEnumField(Rect position, SerializedProperty valueProp, Type enumType)
        {
            if (enumType == null) return;
            Enum currentValue = (Enum)Enum.ToObject(enumType, valueProp.intValue);
            Enum newValue = EditorGUI.EnumPopup(position, currentValue);
            valueProp.intValue = Convert.ToInt32(newValue);
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
    }
}