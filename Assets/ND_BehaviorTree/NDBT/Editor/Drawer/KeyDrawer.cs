// FILE: Editor/KeyDrawer.cs

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    [CustomPropertyDrawer(typeof(Key), true)]
    public class KeyDrawer : PropertyDrawer
    {
        private const float k_ButtonWidth = 22f;
        private const float k_Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (label != GUIContent.none)
            {
                position = EditorGUI.PrefixLabel(position, label);
            }

            Rect fieldRect = position;
            fieldRect.width -= (k_ButtonWidth + k_Spacing);
            Rect buttonRect = position;
            buttonRect.x = fieldRect.xMax + k_Spacing;
            buttonRect.width = k_ButtonWidth;

            Key currentKey = property.objectReferenceValue as Key;
            bool isLinkedToBlackboard = currentKey != null && !string.IsNullOrEmpty(currentKey.keyName);

            if (isLinkedToBlackboard)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(fieldRect, currentKey.keyName, EditorStyles.objectField);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                DrawDirectValueEditor(fieldRect, property, GUIContent.none);
            }

            if (GUI.Button(buttonRect, "●", EditorStyles.miniButton))
            {
                ShowBlackboardMenu(property);
            }
            EditorGUI.EndProperty();
        }

        private void ShowBlackboardMenu(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            BehaviorTree tree = FindBehaviorTreeFromProperty(property);

            menu.AddItem(new GUIContent("[None] (Direct Value)"), false, () =>
            {
                property.objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            });

            if (tree != null && tree.blackboard != null)
            {
                menu.AddSeparator("");
                Type valueType = GetValueTypeForField(property);
                
                // FIXED: Added a check for valueType being null.
                var validKeys = tree.blackboard.keys
                    .Where(k => k != null && (valueType == null || valueType == typeof(object) || valueType.IsAssignableFrom(k.GetValueType())))
                    .ToList();
                    
                if (validKeys.Any())
                {
                    foreach (Key key in validKeys)
                    {
                        menu.AddItem(new GUIContent(key.keyName), false, () =>
                        {
                            property.objectReferenceValue = key;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("No compatible keys on Blackboard"));
                }
            }
            menu.ShowAsContext();
        }

        private void DrawDirectValueEditor(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.objectReferenceValue == null)
            {
                Type valueType = GetValueTypeForField(property);
                if (valueType == null)
                {
                    EditorGUI.HelpBox(position, "Type is ambiguous.", MessageType.Warning);
                    return;
                }
                
                var newKeyInstance = CreateKeyInstance(valueType, property.serializedObject.targetObject);
                if (newKeyInstance != null)
                {
                    property.objectReferenceValue = newKeyInstance;
                    GUI.changed = true; 
                }
                else
                {
                    EditorGUI.HelpBox(position, "Failed to create key.", MessageType.Error);
                    return;
                }
            }

            var keyObject = new SerializedObject(property.objectReferenceValue);
            var valueProperty = keyObject.FindProperty("value");
            if (valueProperty != null)
            {
                EditorGUI.BeginChangeCheck();
                keyObject.Update();
                
                Type valueType = GetValueTypeForField(property);
                if (valueType != null && valueType.IsEnum)
                {
                    Enum currentValue = (Enum)Enum.ToObject(valueType, valueProperty.intValue);
                    var newValue = EditorGUI.EnumPopup(position, currentValue);
                    if (!currentValue.Equals(newValue))
                    {
                        valueProperty.intValue = Convert.ToInt32(newValue);
                    }
                }
                else
                {
                    EditorGUI.PropertyField(position, valueProperty, label, true);
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                     keyObject.ApplyModifiedProperties();
                     EditorUtility.SetDirty(property.serializedObject.targetObject);
                }
            }
            else
            {
                EditorGUI.LabelField(position, label, new GUIContent("(Value field not found)"));
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Key currentKey = property.objectReferenceValue as Key;
            bool isLinkedToBlackboard = currentKey != null && !string.IsNullOrEmpty(currentKey.keyName);

            if (isLinkedToBlackboard)
            {
                return EditorGUIUtility.singleLineHeight;
            }
            else
            {
                if (currentKey == null)
                {
                    return EditorGUIUtility.singleLineHeight;
                }
                var keyObject = new SerializedObject(currentKey);
                var valueProperty = keyObject.FindProperty("value");
                if (valueProperty != null)
                {
                    return EditorGUI.GetPropertyHeight(valueProperty, true);
                }
                return EditorGUIUtility.singleLineHeight;
            }
        }

        private Type GetValueTypeForField(SerializedProperty property)
        {
            var fieldType = fieldInfo.FieldType;
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Key<>))
            {
                return fieldType.GetGenericArguments()[0];
            }
            if (fieldType.BaseType != null && fieldType.BaseType.IsGenericType && fieldType.BaseType.GetGenericTypeDefinition() == typeof(Key<>))
            {
                return fieldType.BaseType.GetGenericArguments()[0];
            }

            var detachAttribute = fieldInfo.GetCustomAttribute<DetachKeyAttribute>();
            if (detachAttribute != null)
            {
                var controlProperty = property.serializedObject.FindProperty(detachAttribute.ControlKeyFieldName);
                // FIXED: Added a check for GetValueType() not being null.
                if (controlProperty?.objectReferenceValue is Key controlKey && controlKey.GetValueType() != null)
                {
                    return controlKey.GetValueType();
                }
            }

            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            if (keyTypeAttribute != null)
            {
                return keyTypeAttribute.RequiredType;
            }

            return null;
        }

        private Key CreateKeyInstance(Type valueType, UnityEngine.Object assetObject)
        {
            var keyClassType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .FirstOrDefault(t => !t.IsAbstract && t.BaseType == typeof(Key<>).MakeGenericType(valueType));

            Type typeToCreate = keyClassType ?? typeof(Key<>).MakeGenericType(valueType);
            var newKeyInstance = ScriptableObject.CreateInstance(typeToCreate) as Key;
            if (newKeyInstance != null)
            {
                newKeyInstance.name = $"{valueType.Name} (local)";
                AssetDatabase.AddObjectToAsset(newKeyInstance, assetObject);
            }
            return newKeyInstance;
        }

        private BehaviorTree FindBehaviorTreeFromProperty(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target is BehaviorTree tree) return tree;
            if (target is Node node)
            {
                string assetPath = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    return AssetDatabase.LoadMainAssetAtPath(assetPath) as BehaviorTree;
                }
            }
            return null;
        }
    }
}