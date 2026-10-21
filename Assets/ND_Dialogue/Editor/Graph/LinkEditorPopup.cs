using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Small drop-down window to edit one link where it is drawn: priority and its conditions (schema pickers for
    /// variables/quests, "+ Add condition", ✕ to remove). Edits go through SerializedObject, so they are undoable.
    /// The link is addressed by its property path (indices), not by object, so it survives the data being re-created.
    /// </summary>
    public class LinkEditorPopup : EditorWindow
    {
        private DialogueDatabase m_Database;
        private string m_LinkPath;
        private string m_Title;
        private Action m_OnChanged;
        private SerializedObject m_Serialized;
        private Vector2 m_Scroll;

        /// <summary>Opens the editor for <paramref name="link"/> (a link of <paramref name="from"/>) at <paramref name="screenPosition"/>.</summary>
        public static LinkEditorPopup Open(Vector2 screenPosition, DialogueDatabase database, Conversation conversation, DialogueEntry from,
            DialogueLink link, string title, Action onChanged)
        {
            string path = LinkPath(database, conversation, from, link);
            if (path == null)
            {
                Debug.LogError("[Dialogue Editor] Cannot edit this link: it is no longer in the database (reload the graph).");
                return null;
            }
            var popup = CreateInstance<LinkEditorPopup>();
            popup.m_Database = database;
            popup.m_LinkPath = path;
            popup.m_Title = title;
            popup.m_OnChanged = onChanged;
            popup.m_Serialized = new SerializedObject(database);
            popup.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), new Vector2(440, 320));
            return popup;
        }

        /// <summary>The serialized property path of a link, or null when it is not in the database.</summary>
        public static string LinkPath(DialogueDatabase database, Conversation conversation, DialogueEntry from, DialogueLink link)
        {
            int c = database.conversations.IndexOf(conversation);
            int e = c < 0 ? -1 : database.conversations[c].entries.IndexOf(from);
            int l = e < 0 ? -1 : from.links.IndexOf(link);
            if (c < 0 || e < 0 || l < 0) return null;
            return $"{nameof(DialogueDatabase.conversations)}.Array.data[{c}].{nameof(Conversation.entries)}.Array.data[{e}].{nameof(DialogueEntry.links)}.Array.data[{l}]";
        }

        private void OnGUI()
        {
            if (m_Database == null || m_Serialized == null) { Close(); return; }
            m_Serialized.Update();
            var link = m_Serialized.FindProperty(m_LinkPath);
            if (link == null)
            {
                EditorGUILayout.HelpBox("This link no longer exists.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(m_Title, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(link.FindPropertyRelative(nameof(DialogueLink.priority)));
            EditorGUILayout.LabelField("Conditions of this branch (all must hold)", EditorStyles.miniBoldLabel);

            var list = link.FindPropertyRelative(nameof(DialogueLink.conditions));
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string typeName = element.managedReferenceValue != null ? ObjectNames.NicifyVariableName(element.managedReferenceValue.GetType().Name) : "(empty)";
                    using (new EditorGUILayout.VerticalScope())
                        EditorGUILayout.PropertyField(element, new GUIContent(typeName), true);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
            if (list.arraySize == 0) EditorGUILayout.LabelField("No conditions: this branch is always allowed.", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add condition")) ShowAddMenu();

            if (m_Serialized.ApplyModifiedProperties()) m_OnChanged?.Invoke();
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<DialogueCondition>().Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name))
            {
                var t = type;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(t.Name)), false, () =>
                {
                    m_Serialized.Update();
                    var list = m_Serialized.FindProperty(m_LinkPath).FindPropertyRelative(nameof(DialogueLink.conditions));
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(t);
                    m_Serialized.ApplyModifiedProperties();
                    m_OnChanged?.Invoke();
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }
    }
}
