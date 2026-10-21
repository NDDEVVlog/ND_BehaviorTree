using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Side panel for the selected entry: speaker, emotion, routing flag, text in the preview locale, conditions,
    /// actions and outgoing links. Plain fields are written with Undo; conditions/actions are [SerializeReference]
    /// lists edited through bound PropertyFields (variable/quest names get schema pickers).
    /// </summary>
    public class DialogueEntryInspector : VisualElement
    {
        private DialogueDatabase m_Database;
        private Conversation m_Conversation;
        private DialogueEntry m_Entry;
        private int m_ConversationId;
        private int m_EntryId;
        private string m_Locale;
        private SerializedObject m_SerializedDatabase;

        /// <summary>The entry was edited (the canvas refreshes its node).</summary>
        public event Action<DialogueEntry> EntryChanged;
        /// <summary>Links were removed or re-prioritised here (the canvas redraws its edges).</summary>
        public event Action LinksChanged;

        public DialogueEntryInspector()
        {
            AddToClassList("dlg-inspector");
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) styleSheets.Add(sheet);
            ShowEmpty("Select one entry to edit it.");
        }

        public DialogueEntry Entry => m_Entry;

        /// <summary>Builds plugin args for plugin inspector sections (set by the window).</summary>
        public Func<DialogueEntry, Plugins.DialoguePluginArgs> PluginArgs { get; set; }

        /// <summary>Converts a panel position to a screen position (set by the window, for drop-down popups).</summary>
        public Func<Vector2, Vector2> PanelToScreen { get; set; }

        public void Bind(DialogueDatabase database, Conversation conversation, DialogueEntry entry, string locale)
        {
            m_Database = database;
            m_Conversation = conversation;
            m_Entry = entry;
            m_ConversationId = conversation != null ? conversation.id : 0;
            m_EntryId = entry != null ? entry.id : -1;
            m_Locale = locale;
            Rebuild();
        }

        public void Unbind(string message)
        {
            m_Entry = null;
            m_EntryId = -1;
            ShowEmpty(message);
        }

        private void ShowEmpty(string message)
        {
            Clear();
            Add(new HelpBox(message, HelpBoxMessageType.Info));
        }

        private void Notify()
        {
            EditorUtility.SetDirty(m_Database);
            EntryChanged?.Invoke(m_Entry);
        }

        public void Rebuild()
        {
            Clear();
            if (m_Database != null && m_EntryId >= 0)
            {
                // Re-fetch by id (the data objects may have been replaced by SerializedObject edits or undo).
                var conversation = m_Database.FindConversation(m_ConversationId);
                var entry = conversation?.FindEntry(m_EntryId);
                if (entry != null) { m_Conversation = conversation; m_Entry = entry; }
            }
            if (m_Database == null || m_Conversation == null || m_Entry == null || !m_Conversation.entries.Contains(m_Entry))
            {
                ShowEmpty("Select one entry to edit it.");
                return;
            }
            m_SerializedDatabase = new SerializedObject(m_Database);
            var entryProp = FindEntryProperty();

            bool isStart = m_Entry.id == Conversation.StartEntryId;
            var header = new Label(isStart ? "START" : $"Entry #{m_Entry.id}");
            header.AddToClassList("dlg-inspector__header");
            Add(header);

            if (isStart)
            {
                Add(new HelpBox("Where the conversation begins. Link it to the first line(s); conditions on those lines pick which one plays.", HelpBoxMessageType.None));
            }
            else
            {
                BuildIdentity();
                if (!m_Entry.isPassthrough) BuildText();
                BuildList("Entry conditions (every way in; branch rules go on links)", entryProp?.FindPropertyRelative(nameof(DialogueEntry.conditions)), typeof(DialogueCondition), m_Entry.conditions);
                BuildList("Actions (run on enter)", entryProp?.FindPropertyRelative(nameof(DialogueEntry.actions)), typeof(DialogueAction), m_Entry.actions);
            }
            BuildLinks();
            BuildPluginSections();
        }

        private void BuildPluginSections()
        {
            foreach (var section in Plugins.DialoguePluginRegistry.InspectorSections)
            {
                var args = PluginArgs?.Invoke(m_Entry) ?? new Plugins.DialoguePluginArgs { Database = m_Database, Conversation = m_Conversation, Entry = m_Entry, Locale = m_Locale };
                var foldout = new Foldout { text = section.Title, value = true };
                foldout.Add(Plugins.DialoguePluginRegistry.BuildPanel(section, args));
                Add(foldout);
            }
        }

        private SerializedProperty FindEntryProperty()
        {
            int c = m_Database.conversations.IndexOf(m_Conversation);
            int e = m_Conversation.entries.IndexOf(m_Entry);
            if (c < 0 || e < 0) return null;
            return m_SerializedDatabase.FindProperty($"{nameof(DialogueDatabase.conversations)}.Array.data[{c}].{nameof(Conversation.entries)}.Array.data[{e}]");
        }

        #region Identity

        private void BuildIdentity()
        {
            var title = new TextField("Title") { value = m_Entry.title ?? string.Empty, isDelayed = true, tooltip = "Editor label only." };
            title.RegisterValueChangedCallback(evt => Edit("Rename Dialogue Entry", () => m_Entry.title = evt.newValue));
            Add(title);

            var actors = m_Database.actors.Where(a => a != null).ToList();
            if (actors.Count == 0)
            {
                Add(new HelpBox("The database has no actors. Add them in the database inspector.", HelpBoxMessageType.Warning));
            }
            else
            {
                var names = actors.Select(a => a.isPlayer ? $"{a.name} (player)" : a.name).ToList();
                int index = Mathf.Max(0, actors.FindIndex(a => a.id == m_Entry.actorId));
                var speaker = new PopupField<string>("Speaker", names, index);
                speaker.RegisterValueChangedCallback(evt =>
                {
                    var actor = actors[names.IndexOf(evt.newValue)];
                    Edit("Change Speaker", () => m_Entry.actorId = actor.id);
                    Rebuild(); // emotions and menu text depend on the speaker
                });
                Add(speaker);

                var current = m_Database.FindActor(m_Entry.actorId);
                if (current != null && current.emotions.Count > 0)
                {
                    const string DefaultLabel = "(actor default)";
                    var emotions = new List<string> { DefaultLabel };
                    emotions.AddRange(current.emotions.Where(e => e != null).Select(e => e.id));
                    if (!string.IsNullOrEmpty(m_Entry.emotion) && !emotions.Contains(m_Entry.emotion)) emotions.Add(m_Entry.emotion); // keep an unknown value visible
                    var emotion = new PopupField<string>("Emotion", emotions, string.IsNullOrEmpty(m_Entry.emotion) ? DefaultLabel : m_Entry.emotion);
                    emotion.RegisterValueChangedCallback(evt => Edit("Change Emotion", () => m_Entry.emotion = evt.newValue == DefaultLabel ? string.Empty : evt.newValue));
                    Add(emotion);
                }
            }

            var routing = new Toggle("Passthrough") { value = m_Entry.isPassthrough, tooltip = "Shows no line: only runs its actions and routes by its links' conditions." };
            routing.RegisterValueChangedCallback(evt =>
            {
                Edit("Toggle Passthrough", () => m_Entry.isPassthrough = evt.newValue);
                Rebuild();
            });
            Add(routing);
        }

        /// <summary>
        /// Re-fetches the conversation and entry by id. Bound PropertyFields write through SerializedObject, which may
        /// replace the data objects; every direct edit resolves first so it never writes into a stale copy.
        /// </summary>
        private bool Resolve()
        {
            var conversation = m_Database != null ? m_Database.FindConversation(m_ConversationId) : null;
            var entry = conversation?.FindEntry(m_EntryId);
            if (entry == null)
            {
                Debug.LogWarning($"[Dialogue Editor] Entry {m_ConversationId}#{m_EntryId} is no longer in the database; reselect it.");
                return false;
            }
            m_Conversation = conversation;
            m_Entry = entry;
            return true;
        }

        private void Edit(string undoLabel, Action change)
        {
            if (!Resolve()) return;
            Undo.RecordObject(m_Database, undoLabel);
            change();
            Notify();
        }

        #endregion

        #region Text

        private void BuildText()
        {
            AddTextBlock("Text", menu: false);
            var actor = m_Database.FindActor(m_Entry.actorId);
            if (actor != null && actor.isPlayer) AddTextBlock("Choice label (empty = Text)", menu: true);
        }

        /// <summary>Text box + its mode: plain (same in every language) or connected to the table (per language).</summary>
        private void AddTextBlock(string title, bool menu)
        {
            var text = menu ? m_Entry.menuText : m_Entry.text;
            AddSection(text.IsLocalized ? $"{title}  [{m_Locale}]" : $"{title}  (plain)");
            Add(TextBox(menu));

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            if (text.IsLocalized)
            {
                text.Table.TryGetKey(text.EntryId, out string key);
                row.Add(new Label($"table '{text.Table.DisplayName}' · key '{key}'") { style = { flexGrow = 1, color = new Color(0.6f, 0.65f, 0.7f) } });
                row.Add(new Button(() =>
                {
                    string current = DialogueTextEditing.Read(text, m_Locale) ?? string.Empty;
                    Edit("Unlink Dialogue Text", () => (menu ? m_Entry.menuText : m_Entry.text).Unlink(current));
                    Rebuild();
                }) { text = "Unlink", tooltip = "Back to plain text (keeps this language's text; the table entry stays)." });
            }
            else if (!string.IsNullOrEmpty(text.Text))
            {
                var localize = new Button(() =>
                {
                    if (DialogueTextEditing.Localize(m_Database, m_Conversation, m_Entry, menu, m_Locale)) Notify();
                    Rebuild();
                }) { text = $"Localize → table ({m_Locale})", tooltip = "Move this text into the database's Text Table so it can be translated." };
                localize.SetEnabled(m_Database.textTable != null);
                if (m_Database.textTable == null) localize.tooltip = "Assign a Text Table in the Settings tab first.";
                row.Add(localize);
            }
            Add(row);
        }

        private TextField TextBox(bool menu)
        {
            var reference = menu ? m_Entry.menuText : m_Entry.text;
            var field = new TextField { multiline = true, isDelayed = true, value = DialogueTextEditing.Read(reference, m_Locale) ?? string.Empty };
            field.style.minHeight = menu ? 22 : 54;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterValueChangedCallback(evt =>
            {
                if (Resolve() && DialogueTextEditing.Write(m_Database, m_Conversation, m_Entry, menu, m_Locale, evt.newValue)) Notify();
            });
            return field;
        }

        #endregion

        #region Conditions / actions

        private void BuildList(string title, SerializedProperty listProp, Type baseType, System.Collections.IList list)
        {
            AddSection(title);
            if (listProp == null) return;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                int index = i;
                var element = listProp.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                var label = list[i] != null ? ObjectNames.NicifyVariableName(list[i].GetType().Name) : "(empty)";
                var field = new PropertyField(element, label);
                field.Bind(m_SerializedDatabase);
                field.RegisterValueChangeCallback(_ => EntryChanged?.Invoke(m_Entry));
                row.Add(field);
                var remove = new Button(() =>
                {
                    Edit($"Remove {baseType.Name}", () => list.RemoveAt(index));
                    Rebuild();
                }) { text = "✕", tooltip = "Remove" };
                remove.AddToClassList("dlg-inspector__remove");
                row.Add(remove);
                Add(row);
            }

            var add = new Button { text = $"+ Add {(baseType == typeof(DialogueCondition) ? "condition" : "action")}" };
            add.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var type in TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name))
                {
                    var t = type;
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(t.Name)), false, () =>
                    {
                        Edit($"Add {t.Name}", () => list.Add(Activator.CreateInstance(t)));
                        Rebuild();
                    });
                }
                menu.ShowAsContext();
            };
            Add(add);
        }

        #endregion

        #region Links

        private void BuildLinks()
        {
            AddSection("Links (tried by priority, highest first)");
            if (m_Entry.links.Count == 0)
            {
                Add(new Label("No links: the conversation ends after this entry.") { style = { color = new Color(0.6f, 0.6f, 0.65f) } });
                return;
            }
            for (int i = 0; i < m_Entry.links.Count; i++)
            {
                int index = i; // by index: the link object may be replaced by SerializedObject edits
                var link = m_Entry.links[i];
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                var priority = new EnumField(LinkTargetLabel(link), link.priority);
                priority.RegisterValueChangedCallback(evt =>
                {
                    Edit("Change Link Priority", () => m_Entry.links[index].priority = (LinkPriority)evt.newValue);
                    LinksChanged?.Invoke();
                });
                row.Add(priority);
                var edit = new Button() { text = "Edit", tooltip = "Edit this link's conditions" };
                edit.clicked += () =>
                {
                    if (!Resolve() || index >= m_Entry.links.Count) return;
                    Vector2 screen = PanelToScreen != null ? PanelToScreen(edit.worldBound.position) : GUIUtility.GUIToScreenPoint(edit.worldBound.position);
                    LinkEditorPopup.Open(screen, m_Database, m_Conversation, m_Entry, m_Entry.links[index], LinkTargetLabel(m_Entry.links[index]), () =>
                    {
                        EditorUtility.SetDirty(m_Database);
                        LinksChanged?.Invoke();
                    });
                };
                row.Add(edit);
                var remove = new Button(() =>
                {
                    Edit("Remove Dialogue Link", () => m_Entry.links.RemoveAt(index));
                    LinksChanged?.Invoke();
                    Rebuild();
                }) { text = "✕", tooltip = "Remove link" };
                remove.AddToClassList("dlg-inspector__remove");
                row.Add(remove);
                Add(row);

                string conditions = DialogueEdgeView.Describe(new DialogueLink { conditions = link.conditions });
                if (!string.IsNullOrEmpty(conditions))
                    Add(new Label(conditions) { style = { marginLeft = 16, fontSize = 10, color = new Color(0.65f, 0.7f, 0.78f), whiteSpace = WhiteSpace.Normal } });
            }
        }

        private string LinkTargetLabel(DialogueLink link)
        {
            var conversation = m_Database.FindConversation(link.conversationId);
            var target = conversation?.FindEntry(link.entryId);
            if (target == null) return $"→ missing {link.conversationId}#{link.entryId}";
            string where = conversation == m_Conversation ? string.Empty : $"{conversation.title} ";
            string label = !string.IsNullOrEmpty(target.title) ? target.title : DialogueTextEditing.Read(target.text, m_Locale) ?? (target.isPassthrough ? "(routing)" : "(no text)");
            if (label.Length > 28) label = label.Substring(0, 27) + "…";
            return $"→ {where}#{target.id} {label}";
        }

        #endregion

        private void AddSection(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dlg-inspector__section");
            Add(label);
        }
    }
}
