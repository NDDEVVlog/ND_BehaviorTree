using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    /// <summary>
    /// This custom drawer renders any field of type 'BehaviorTree' in the inspector.
    /// It automatically checks for node dependencies and adds required components.
    /// </summary>
    [CustomPropertyDrawer(typeof(BehaviorTree))]
    public class BehaviorTreePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Start property drawing
            EditorGUI.BeginProperty(position, label, property);

            // Draw the object field for assigning the BehaviorTree asset
            EditorGUI.PropertyField(position, property, label, true);
            
            // Get the MonoBehaviour instance that this property belongs to
            var owner = property.serializedObject.targetObject as MonoBehaviour;

            // Get the actual BehaviorTree asset assigned to the field
            var tree = property.objectReferenceValue as BehaviorTree;

            // If a tree is assigned, run our check
            if (tree != null && owner != null)
            {
                // Note: This check runs every GUI frame. It's lightweight and ensures
                // that if a user removes a component, it gets re-added, and if they
                // change the tree asset, the requirements are re-evaluated.
                string message = BehaviorTreeEditorUtilities.CheckAndEnforceNodeRequirements(owner, tree);

                // If the utility method returned a message (i.e., components were added),
                // display it in a help box below the field.
                if (!string.IsNullOrEmpty(message))
                {
                    // This is a simple way to show the box. For perfect layout, one would
                    // override GetPropertyHeight, but this is often sufficient.
                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
            }

            EditorGUI.EndProperty();
        }
    }
}