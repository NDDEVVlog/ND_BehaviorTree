using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>Trigger inspector: the usual fields, plus add/remove for its [SerializeReference] condition list.</summary>
    [CustomEditor(typeof(DialogueTrigger))]
    public class DialogueTriggerEditor : UnityEditor.Editor
    {
        private const string ConditionsField = "m_Conditions";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", ConditionsField);
            DrawConditions(serializedObject.FindProperty(ConditionsField));
            serializedObject.ApplyModifiedProperties();

            var trigger = (DialogueTrigger)target;
            if (Application.isPlaying && trigger.Manager != null)
            {
                bool can = trigger.CanFire(out string reason);
                EditorGUILayout.HelpBox(can ? "Can fire now." : $"Cannot fire: {reason}.", can ? MessageType.Info : MessageType.None);
                using (new EditorGUI.DisabledScope(!can))
                    if (GUILayout.Button("Fire now")) trigger.Fire();
            }
        }

        /// <summary>Each condition with its fields and a ✕, then a typed "+ Add condition" menu.</summary>
        public static void DrawConditions(SerializedProperty list)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conditions (all must hold)", EditorStyles.boldLabel);
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string typeName = element.managedReferenceValue != null ? ObjectNames.NicifyVariableName(element.managedReferenceValue.GetType().Name) : "(empty)";
                    EditorGUILayout.PropertyField(element, new GUIContent(typeName), true);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }
            }
            if (!GUILayout.Button("+ Add condition")) return;
            var menu = new GenericMenu();
            var so = list.serializedObject;
            string path = list.propertyPath;
            foreach (var type in TypeCache.GetTypesDerivedFrom<DialogueCondition>().Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name))
            {
                var t = type;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(t.Name)), false, () =>
                {
                    so.Update();
                    var prop = so.FindProperty(path);
                    prop.InsertArrayElementAtIndex(prop.arraySize);
                    prop.GetArrayElementAtIndex(prop.arraySize - 1).managedReferenceValue = Activator.CreateInstance(t);
                    so.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
