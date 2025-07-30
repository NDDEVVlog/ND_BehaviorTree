using System.Linq;
using UnityEditor;
using UnityEngine;
using ND_BehaviorTree;

namespace ND_BehaviorTree
{


    [CustomPropertyDrawer(typeof(IDynamicBranchSelector), true)]
    public class DynamicBranchSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get the current object reference
            object currentObj = fieldInfo.GetValue(property.serializedObject.targetObject);

            // List available implementations of IDynamicBranchSelector
            System.Type[] implementations = GetImplementationsOfInterface(typeof(IDynamicBranchSelector));
            string[] implementationNames = new string[implementations.Length + 1];
            implementationNames[0] = "None";
            for (int i = 0; i < implementations.Length; i++)
            {
                implementationNames[i + 1] = implementations[i].Name;
            }

            // Find the current type index
            int selectedIndex = 0;
            if (currentObj != null)
            {
                for (int i = 0; i < implementations.Length; i++)
                {
                    if (currentObj.GetType() == implementations[i])
                    {
                        selectedIndex = i + 1;
                        break;
                    }
                }
            }

            // Draw dropdown to select implementation
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUI.Popup(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                                            label.text, selectedIndex, implementationNames);
            if (EditorGUI.EndChangeCheck())
            {
                // Update the field with the selected type
                if (selectedIndex == 0)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    System.Type selectedType = implementations[selectedIndex - 1];
                    object newInstance = System.Activator.CreateInstance(selectedType);
                    property.managedReferenceValue = newInstance;
                }
            }

            // Draw the properties of the selected implementation
            if (selectedIndex > 0 && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var iterator = property.Copy();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), iterator, true);
                    enterChildren = false;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue != null)
            {
                var iterator = property.Copy();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false;
                }
            }
            return height;
        }

        private System.Type[] GetImplementationsOfInterface(System.Type interfaceType)
        {
            var types = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => interfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToArray();
            return types;
        }
    }
}