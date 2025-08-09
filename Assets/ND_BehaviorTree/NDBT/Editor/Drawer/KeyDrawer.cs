
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
                // Nếu bị vô hiệu hóa, hiển thị thông báo hướng dẫn
                
                DrawDirectValueEditor(fieldRect, property, GUIContent.none);
                
            }
            
            if (GUI.Button(buttonRect, "●", EditorStyles.miniButton))
            {
                ShowBlackboardMenu(property);
            }
            
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();
        }

        private void DrawDirectValueEditor(Rect position, SerializedProperty property, GUIContent label)
        {
            // Nếu key hiện tại là null, chúng ta cần tạo một instance mới
            if (property.objectReferenceValue == null)
            {
                Type valueType = GetValueTypeForField(property);
                
                // Nếu vẫn không xác định được kiểu, hiển thị lỗi.
                if (valueType == null)
                {
                    EditorGUI.HelpBox(position, "Type is ambiguous. Use a specific Key<T> or link the leader key.", MessageType.Warning);
                    return;
                }
                
                var newKeyInstance = CreateKeyInstance(valueType, property.serializedObject.targetObject);
                if (newKeyInstance != null)
                {
                    property.objectReferenceValue = newKeyInstance;
                    GUI.changed = true; // Báo cho Unity biết cần vẽ lại
                    return; // Thoát ngay để frame sau vẽ lại cho đúng
                }
                else
                {
                    EditorGUI.HelpBox(position, "Failed to create key instance.", MessageType.Error);
                    return;
                }
            }

            // Nếu đã có instance, vẽ UI cho giá trị của nó
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
                string controlPropertyPath;
                string currentPath = property.propertyPath;
                int lastDot = currentPath.LastIndexOf('.');


                if (lastDot == -1)
                {


                    controlPropertyPath = detachAttribute.ControlKeyFieldName;
                }
                else
                {

                    string basePath = currentPath.Substring(0, lastDot + 1);
                    controlPropertyPath = basePath + detachAttribute.ControlKeyFieldName;
                }

                // Phần logic tìm kiếm bây giờ sẽ hoạt động cho cả hai trường hợp
                var controlProperty = property.serializedObject.FindProperty(controlPropertyPath);
                if (controlProperty?.objectReferenceValue is Key controlKey && controlKey.GetValueType() != null)
                {
                    Type type = controlKey.GetValueType();
                    // Vẫn giữ lại kiểm tra quan trọng này để tránh trả về kiểu mơ hồ
                    return type == typeof(object) ? null : type; 
                }
            }

            var keyTypeAttribute = fieldInfo.GetCustomAttribute<BlackboardKeyTypeAttribute>();
            if (keyTypeAttribute != null)
            {
                return keyTypeAttribute.RequiredType;
            }

            return null;
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
                var validKeys = tree.blackboard.keys
                    .Where(k => k != null && !string.IsNullOrEmpty(k.keyName) && (valueType == typeof(object) || valueType.IsAssignableFrom(k.GetValueType())))
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
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Key currentKey = property.objectReferenceValue as Key;
            if (currentKey == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!string.IsNullOrEmpty(currentKey.keyName))
            {
                return EditorGUIUtility.singleLineHeight;
            }
            else
            {
                var keyObject = new SerializedObject(currentKey);
                var valueProperty = keyObject.FindProperty("value");
                return (valueProperty != null) ? EditorGUI.GetPropertyHeight(valueProperty, true) : EditorGUIUtility.singleLineHeight;
            }
        }
        
        private Key CreateKeyInstance(Type valueType, UnityEngine.Object assetObject)
        {
            var keyClassType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .FirstOrDefault(t => !t.IsAbstract && t.BaseType == typeof(Key<>).MakeGenericType(valueType));

            if (keyClassType == null)
            {
                 Debug.LogError($"Could not find a concrete Key class for type '{valueType.Name}'. Please create a class like 'public class {valueType.Name}Key : Key<{valueType.Name}> {{}}'.");
                 return null;
            }

            var newKeyInstance = ScriptableObject.CreateInstance(keyClassType) as Key;
            if (newKeyInstance != null)
            {
                newKeyInstance.name = $"{valueType.Name} (local)";
                // Quan trọng: Phải thêm vào asset để nó được lưu lại
                AssetDatabase.AddObjectToAsset(newKeyInstance, assetObject);
                AssetDatabase.SaveAssets(); // Có thể cần hoặc không, tùy vào ngữ cảnh
                AssetDatabase.Refresh();
            }
            return newKeyInstance;
        }

        private BehaviorTree FindBehaviorTreeFromProperty(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is BehaviorTree tree) return tree;
            if (property.serializedObject.targetObject is Node node)
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