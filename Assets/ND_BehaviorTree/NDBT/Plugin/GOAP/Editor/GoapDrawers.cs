using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.GOAP.Editor
{
    internal static class GoapEditorUtil
    {
        /// <summary>The blackboard of the tree that owns the edited node (nodes are sub-assets of their tree).</summary>
        public static Blackboard BlackboardOf(SerializedObject so)
        {
            var target = so.targetObject;
            if (target == null || !EditorUtility.IsPersistent(target)) return null;
            return (AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(target)) as BehaviorTree)?.blackboard;
        }

        public static bool IsNumber(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double);

        /// <summary>Draws the GoapValue field that matches <paramref name="type"/>.</summary>
        public static void DrawValue(Rect rect, SerializedProperty value, Type type)
        {
            if (type == null) { EditorGUI.LabelField(rect, "pick a key"); return; }
            if (type == typeof(bool)) EditorGUI.PropertyField(rect, value.FindPropertyRelative("boolValue"), GUIContent.none);
            else if (type == typeof(int) || type == typeof(long)) EditorGUI.PropertyField(rect, value.FindPropertyRelative("intValue"), GUIContent.none);
            else if (type == typeof(float) || type == typeof(double)) EditorGUI.PropertyField(rect, value.FindPropertyRelative("floatValue"), GUIContent.none);
            else if (type == typeof(string)) EditorGUI.PropertyField(rect, value.FindPropertyRelative("stringValue"), GUIContent.none);
            else if (type == typeof(Vector2) || type == typeof(Vector3))
            {
                var v = value.FindPropertyRelative("vectorValue");
                v.vector3Value = EditorGUI.Vector3Field(rect, GUIContent.none, v.vector3Value);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var o = value.FindPropertyRelative("objectValue");
                o.objectReferenceValue = EditorGUI.ObjectField(rect, o.objectReferenceValue, type, false);
            }
            else EditorGUI.LabelField(rect, $"{type.Name}: not supported");
        }
    }

    /// <summary>Picker of the tree's blackboard keys for [GoapKey] fields (stores the key reference; no local key assets).</summary>
    [CustomPropertyDrawer(typeof(GoapKeyAttribute))]
    public class GoapKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Draw(position, property, label, ((GoapKeyAttribute)attribute).NumbersOnly);
        }

        public static void Draw(Rect position, SerializedProperty property, GUIContent label, bool numbersOnly)
        {
            var blackboard = GoapEditorUtil.BlackboardOf(property.serializedObject);
            if (label != GUIContent.none && !string.IsNullOrEmpty(label.text)) position = EditorGUI.PrefixLabel(position, label);
            if (blackboard == null)
            {
                EditorGUI.LabelField(position, "(tree has no blackboard)");
                return;
            }
            var keys = blackboard.AllKeys.Where(k => k != null && (numbersOnly ? GoapEditorUtil.IsNumber(k.GetValueType()) : GoapValue.IsSupported(k.GetValueType())))
                .OrderBy(k => k.keyName).ToList();
            var current = property.objectReferenceValue as Key;
            int index = current == null ? 0 : keys.FindIndex(k => k.keyName == current.keyName) + 1;
            var names = new[] { "None" }.Concat(keys.Select(k => $"{k.keyName}  ({k.GetValueType().Name})")).ToList();
            if (current != null && index == 0) { names.Add($"{current.keyName} (missing!)"); index = names.Count - 1; }

            int picked = EditorGUI.Popup(position, index, names.ToArray());
            if (picked != index) property.objectReferenceValue = picked == 0 || picked > keys.Count ? null : keys[picked - 1];
        }
    }

    /// <summary>One line: key | comparison | value.</summary>
    [CustomPropertyDrawer(typeof(KeyCondition))]
    public class KeyConditionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keyProp = property.FindPropertyRelative("key");
            float w = position.width;
            var keyRect = new Rect(position.x, position.y, w * 0.45f - 2, EditorGUIUtility.singleLineHeight);
            var opRect = new Rect(keyRect.xMax + 2, position.y, w * 0.2f - 2, keyRect.height);
            var valueRect = new Rect(opRect.xMax + 2, position.y, position.xMax - opRect.xMax - 2, keyRect.height);
            GoapKeyDrawer.Draw(keyRect, keyProp, GUIContent.none, false);
            var op = property.FindPropertyRelative("comparison");
            op.enumValueIndex = EditorGUI.Popup(opRect, op.enumValueIndex, Enum.GetValues(typeof(ComparisonType)).Cast<ComparisonType>().Select(KeyCondition.Symbol).ToArray());
            GoapEditorUtil.DrawValue(valueRect, property.FindPropertyRelative("value"), (keyProp.objectReferenceValue as Key)?.GetValueType());
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
    }

    /// <summary>One line: key | = / += | value.</summary>
    [CustomPropertyDrawer(typeof(KeyEffect))]
    public class KeyEffectDrawer : PropertyDrawer
    {
        private static readonly string[] s_Ops = { "=", "+=" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keyProp = property.FindPropertyRelative("key");
            float w = position.width;
            var keyRect = new Rect(position.x, position.y, w * 0.45f - 2, EditorGUIUtility.singleLineHeight);
            var opRect = new Rect(keyRect.xMax + 2, position.y, w * 0.2f - 2, keyRect.height);
            var valueRect = new Rect(opRect.xMax + 2, position.y, position.xMax - opRect.xMax - 2, keyRect.height);
            GoapKeyDrawer.Draw(keyRect, keyProp, GUIContent.none, false);
            var op = property.FindPropertyRelative("operation");
            op.enumValueIndex = EditorGUI.Popup(opRect, op.enumValueIndex, s_Ops);
            var type = (keyProp.objectReferenceValue as Key)?.GetValueType();
            if (op.enumValueIndex == (int)GoapEffectOperation.Add && type != null && !GoapEditorUtil.IsNumber(type))
                EditorGUI.HelpBox(valueRect, "+= needs a number key", MessageType.Error);
            else GoapEditorUtil.DrawValue(valueRect, property.FindPropertyRelative("value"), type);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
    }
}
