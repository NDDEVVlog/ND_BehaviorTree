using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>Barker inspector: the usual fields, its condition list, and in Play mode whether it can bark now (and why not).</summary>
    [CustomEditor(typeof(DialogueBarker))]
    public class DialogueBarkerEditor : UnityEditor.Editor
    {
        private const string ConditionsField = "m_Conditions";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", ConditionsField);
            DialogueTriggerEditor.DrawConditions(serializedObject.FindProperty(ConditionsField));
            serializedObject.ApplyModifiedProperties();

            var barker = (DialogueBarker)target;
            if (!Application.isPlaying || barker.Manager == null) return;
            bool can = barker.CanBark(out string reason);
            EditorGUILayout.HelpBox(can ? $"Can bark now ({barker.Count} so far)." : $"Cannot bark: {reason}.", can ? MessageType.Info : MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!can))
                    if (GUILayout.Button("Bark now")) barker.Bark();
                if (GUILayout.Button("Reset count")) barker.ResetBarks();
            }
            Repaint(); // the cooldown counts down
        }
    }
}
