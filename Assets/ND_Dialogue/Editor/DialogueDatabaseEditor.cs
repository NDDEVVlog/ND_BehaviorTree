using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Database inspector: actors, variables, quests and settings as usual; conversations are edited in the
    /// Dialogue Editor window (a raw list of entries is unusable), so they are summarised instead.
    /// </summary>
    [CustomEditor(typeof(DialogueDatabase))]
    public class DialogueDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (DialogueDatabase)target;
            if (GUILayout.Button("Open Dialogue Editor", GUILayout.Height(28))) DialogueEditorWindow.Open(database);
            EditorGUILayout.HelpBox($"{database.conversations.Count} conversation(s) — edit them in the Dialogue Editor.", MessageType.None);

            if (database.schemaSource != null)
                EditorGUILayout.HelpBox("Variables and quests come from the Schema Source; the lists below are not used.", MessageType.Info);

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", nameof(DialogueDatabase.conversations));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
