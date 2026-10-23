using System;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Plain-language names for <see cref="LinkPriority"/>. Priority only matters when an entry has several links:
    /// the runner uses the highest priority that has at least one valid link and ignores the lower ones — so a
    /// lower priority is an "otherwise" branch.
    /// </summary>
    public static class LinkPriorityGUI
    {
        /// <summary>Indexed by <c>(int)LinkPriority</c>.</summary>
        public static readonly string[] Labels =
        {
            "Fallback (only if nothing else)", // Low
            "After normal",                    // BelowNormal
            "Normal",                          // Normal
            "Before normal",                   // AboveNormal
            "First (beats the others)",        // High
        };

        public const string Explanation =
            "When this line has several links, the game looks at the highest priority first: if any link there is " +
            "allowed (its conditions hold), only those are used — NPC lines play the first one, player lines become the " +
            "choices. If none is allowed, it tries the next priority down. Use 'Fallback' for an 'otherwise…' branch.";

        public static string Label(LinkPriority priority) => Labels[Mathf.Clamp((int)priority, 0, Labels.Length - 1)];
    }

    /// <summary>Plain-language names for <see cref="RouteMode"/> (indexed by its value), as in ND Behavior Tree's composites.</summary>
    public static class RouteModeGUI
    {
        public static readonly string[] Labels =
        {
            "First valid (by link order)",
            "Random",
            "Weighted random",
            "Cycle (each in turn)",
            "Shuffle (no repeat until all said)",
            "Utility (highest weight)"
        };

        public const string Explanation =
            "When several NPC lines are allowed at the highest priority, which ONE is said. First valid: the first link. " +
            "Random: any, equally likely. Weighted random: by each link's weight. Cycle: one after another on each visit. " +
            "Shuffle: all of them in a random order before any repeats. Utility: the link with the highest weight (e.g. weight × a variable). " +
            "Player responses are not picked: they still make the choice menu.";

        public static string Label(RouteMode mode) => Labels[Mathf.Clamp((int)mode, 0, Labels.Length - 1)];

        /// <summary>Badge text on the graph node for a non-default mode, else null.</summary>
        public static string Badge(RouteMode mode)
        {
            switch (mode)
            {
                case RouteMode.Random: return "🎲 random";
                case RouteMode.WeightedRandom: return "🎲 weighted";
                case RouteMode.Cycle: return "🔁 cycle";
                case RouteMode.Shuffle: return "🔀 shuffle";
                case RouteMode.Utility: return "📈 utility";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Small drop-down window to edit one link where it is drawn: its priority, its own conditions, and the target
    /// line's entry conditions (which every way into that line needs). Edits go through SerializedObject, so they are
    /// undoable. Both are addressed by property path (indices), not by object, so they survive the data being re-created.
    /// </summary>
    public class LinkEditorPopup : EditorWindow
    {
        private DialogueDatabase m_Database;
        private string m_LinkPath;
        private string m_TargetPath;
        private string m_Title;
        private Action m_OnChanged;
        private SerializedObject m_Serialized;
        private Vector2 m_Scroll;
        private bool m_ShowHelp;

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
            popup.m_TargetPath = TargetEntryPath(database, link);
            popup.m_Title = title;
            popup.m_OnChanged = onChanged;
            popup.m_Serialized = new SerializedObject(database);
            popup.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), new Vector2(460, 360));
            return popup;
        }

        /// <summary>The serialized property path of a link, or null when it is not in the database.</summary>
        public static string LinkPath(DialogueDatabase database, Conversation conversation, DialogueEntry from, DialogueLink link)
        {
            int c = database.conversations.IndexOf(conversation);
            int e = c < 0 ? -1 : database.conversations[c].entries.IndexOf(from);
            int l = e < 0 ? -1 : from.links.IndexOf(link);
            if (c < 0 || e < 0 || l < 0) return null;
            return $"{nameof(DialogueDatabase.conversations)}.Array.data[{c}].{Conversation.EntriesField}.Array.data[{e}].{nameof(DialogueEntry.links)}.Array.data[{l}]";
        }

        /// <summary>The serialized property path of the entry a link goes to, or null when it does not exist.</summary>
        public static string TargetEntryPath(DialogueDatabase database, DialogueLink link)
        {
            if (database == null || link == null) return null;
            int c = database.conversations.FindIndex(x => x != null && x.id == link.conversationId);
            int e = c < 0 ? -1 : database.conversations[c].entries.FindIndex(x => x != null && x.id == link.entryId);
            if (c < 0 || e < 0) return null;
            return $"{nameof(DialogueDatabase.conversations)}.Array.data[{c}].{Conversation.EntriesField}.Array.data[{e}]";
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
            using (new EditorGUILayout.HorizontalScope())
            {
                var priority = link.FindPropertyRelative(nameof(DialogueLink.priority));
                priority.enumValueIndex = EditorGUILayout.Popup(new GUIContent("Priority", LinkPriorityGUI.Explanation), priority.enumValueIndex, LinkPriorityGUI.Labels);
                m_ShowHelp = GUILayout.Toggle(m_ShowHelp, new GUIContent("?", "What is priority?"), EditorStyles.miniButton, GUILayout.Width(22));
            }
            if (m_ShowHelp) EditorGUILayout.HelpBox(LinkPriorityGUI.Explanation, MessageType.None);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            ConditionList(link.FindPropertyRelative(nameof(DialogueLink.conditions)), "This branch only — all must hold",
                "No conditions: this branch is always allowed.", m_LinkPath + "." + nameof(DialogueLink.conditions));

            var target = m_TargetPath != null ? m_Serialized.FindProperty(m_TargetPath) : null;
            if (target != null && target.FindPropertyRelative(nameof(DialogueEntry.id)).intValue != Conversation.StartEntryId)
            {
                EditorGUILayout.Space(6);
                ConditionList(target.FindPropertyRelative(nameof(DialogueEntry.conditions)), "Target line — needed by every way into it",
                    "None.", m_TargetPath + "." + nameof(DialogueEntry.conditions));
            }
            EditorGUILayout.EndScrollView();

            if (m_Serialized.ApplyModifiedProperties()) m_OnChanged?.Invoke();
        }

        private void ConditionList(SerializedProperty list, string heading, string emptyText, string listPath)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
                if (GUILayout.Button("+ Add", EditorStyles.miniButton, GUILayout.Width(52))) ShowAddMenu(listPath);
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                        EditorGUILayout.PropertyField(element, GUIContent.none, true);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
            if (list.arraySize == 0) EditorGUILayout.LabelField(emptyText, EditorStyles.miniLabel);
        }

        private void ShowAddMenu(string listPath)
        {
            var menu = new GenericMenu();
            foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(DialogueCondition)))
            {
                var t = type;
                menu.AddItem(new GUIContent(DialogueRowGUI.TypeLabel(t)), false, () =>
                {
                    m_Serialized.ApplyModifiedProperties();
                    DialogueRowGUI.AppendManagedReference(m_Database, listPath, t);
                    m_Serialized.Update();
                    m_OnChanged?.Invoke();
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }
    }
}
