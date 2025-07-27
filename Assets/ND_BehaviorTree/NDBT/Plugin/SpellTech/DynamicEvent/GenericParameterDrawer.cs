
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

[CustomPropertyDrawer(typeof(GenericParameter), true)]
public class GenericParameterDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        var foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            var useConstantProp = property.FindPropertyRelative("useConstant");
            var constantValueProp = property.FindPropertyRelative("constantValue");
            var sourceComponentProp = property.FindPropertyRelative("sourceComponent");
            var sourceFieldNameProp = property.FindPropertyRelative("sourceFieldName");

            if (useConstantProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: Parameter properties not found.");
                EditorGUI.EndProperty();
                return;
            }

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float currentY = position.y + lineHeight + spacing;
            
            var contentRect = new Rect(position.x, currentY, position.width, lineHeight);
            EditorGUI.PropertyField(contentRect, useConstantProp);
            currentY += lineHeight + spacing;

            if (useConstantProp.boolValue)
            {
                if (constantValueProp != null)
                {
                    float valueHeight = EditorGUI.GetPropertyHeight(constantValueProp, true);
                    var valueRect = new Rect(position.x, currentY, position.width, valueHeight);
                    EditorGUI.PropertyField(valueRect, constantValueProp, new GUIContent("Value"), true);
                }
                else
                {
                    var errorRect = new Rect(position.x, currentY, position.width, lineHeight);
                    EditorGUI.LabelField(errorRect, "Error: 'constantValue' not found.");
                }
            }
            else
            {
                contentRect.y = currentY;
                EditorGUI.PropertyField(contentRect, sourceComponentProp, new GUIContent("Source Component"));
                currentY += lineHeight + spacing;

                contentRect.y = currentY;
                var sourceComponent = sourceComponentProp.objectReferenceValue as Component;

                if (sourceComponent == null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(contentRect, new GUIContent("Source Field/Property"), "Assign a Source Component first");
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    var genericParam = property.managedReferenceValue as GenericParameter;
                    if (genericParam == null) return;
                    Type parameterType = genericParam.GetParameterType();

                    var members = GetCompatibleMembers(sourceComponent.GetType(), parameterType);
                    var memberNames = members.Select(m => m.Name).ToList();

                    if (memberNames.Count > 0)
                    {
                        string currentFieldName = sourceFieldNameProp.stringValue;
                        int currentIndex = memberNames.IndexOf(currentFieldName);
                        if (currentIndex < 0) currentIndex = 0;

                        // === THIS IS THE CORRECTED LINE ===
                        int newIndex = EditorGUI.Popup(contentRect, "Source Field/Property", currentIndex, memberNames.ToArray());

                        if (newIndex != currentIndex || string.IsNullOrEmpty(currentFieldName))
                        {
                            sourceFieldNameProp.stringValue = memberNames[newIndex];
                        }
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.TextField(contentRect, new GUIContent("Source Field/Property"), $"No public {parameterType.Name} found");
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
            
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private IEnumerable<MemberInfo> GetCompatibleMembers(Type componentType, Type targetType)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        var fields = componentType.GetFields(flags)
            .Where(f => f.FieldType == targetType)
            .Cast<MemberInfo>();
            
        var properties = componentType.GetProperties(flags)
            .Where(p => p.PropertyType == targetType && p.CanRead)
            .Cast<MemberInfo>();
            
        return fields.Concat(properties).OrderBy(m => m.Name);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float totalHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        totalHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var useConstantProp = property.FindPropertyRelative("useConstant");
        if (useConstantProp == null)
        {
            return totalHeight;
        }

        if (useConstantProp.boolValue)
        {
            var constantValueProp = property.FindPropertyRelative("constantValue");
            if (constantValueProp != null)
            {
                totalHeight += EditorGUI.GetPropertyHeight(constantValueProp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        else
        {
            totalHeight += (EditorGUIUtility.singleLineHeight * 2) + (EditorGUIUtility.standardVerticalSpacing * 2);
        }

        return totalHeight;
    }
}
