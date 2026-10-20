using System;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Story.Editor
{
    /// <summary>Shared editor helpers for Story views: styles, inline value edits (one undo step each), title replacement.</summary>
    internal static class StoryEdit
    {
        public static StyleSheet Styles => ND_BTStyles.Find("StoryNodes");

        /// <summary>Hides the default title text/icon of a node view and puts <paramref name="content"/> in the title row.</summary>
        public static void ReplaceTitle(VisualElement view, VisualElement content)
        {
            var titleTexts = view.Q("title-content-container");
            if (titleTexts != null) titleTexts.style.display = DisplayStyle.None;
            var icon = view.Q("icon-image");
            if (icon != null) icon.style.display = DisplayStyle.None;
            var title = view.Q("title");
            if (title == null)
            {
                Debug.LogError("[StoryEdit] The node layout has no 'title' element; the story content cannot be shown. Use the default node UXML.");
                return;
            }
            title.Add(content);
        }

        /// <summary>Writes a typed value into a node field as one graph-history step (consecutive edits of the same field merge).</summary>
        public static void SetValue(ND_BTGraphCanvas canvas, Node node, string fieldName, object value)
        {
            if (canvas == null || node == null) return;
            using (new GraphUndoScope(canvas.TreeData, $"Edit {node.name}", $"story:{node.GetInstanceID()}:{fieldName}"))
            {
                var so = new SerializedObject(node);
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                {
                    Debug.LogError($"[StoryEdit] '{node.GetType().Name}' has no serialized field '{fieldName}'.", node);
                    return;
                }
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.String: prop.stringValue = (string)value; break;
                    case SerializedPropertyType.Float: prop.floatValue = Convert.ToSingle(value); break;
                    case SerializedPropertyType.Integer: prop.intValue = Convert.ToInt32(value); break;
                    case SerializedPropertyType.Boolean: prop.boolValue = (bool)value; break;
                    case SerializedPropertyType.Enum: prop.enumValueIndex = Array.IndexOf(prop.enumNames, value.ToString()); break;
                    default:
                        Debug.LogError($"[StoryEdit] Field '{fieldName}' ({prop.propertyType}) cannot be edited inline; use the properties window.", node);
                        return;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(node);
            }
            canvas.RequestSave();
        }

        /// <summary>The tree asset that contains <paramref name="node"/> (nodes are sub-assets of their tree).</summary>
        public static BehaviorTree OwnerTree(Node node) =>
            node != null && EditorUtility.IsPersistent(node) ? AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(node)) as BehaviorTree : null;

        /// <summary>Stops a pointer-down from reaching the node view (which would start a drag and steal the click).</summary>
        public static T NoDrag<T>(this T element) where T : VisualElement
        {
            element.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            return element;
        }

        /// <summary>Title of a node for rows that have no sentence.</summary>
        public static string Title(Node node) => WeightGraph.TitleOf(node);

        /// <summary>Picks a key slot of <paramref name="node"/> by property path, or null.</summary>
        public static KeySlot Slot(Node node, string path) => KeySlotScanner.Scan(node).FirstOrDefault(s => s.PropertyPath == path);
    }
}
