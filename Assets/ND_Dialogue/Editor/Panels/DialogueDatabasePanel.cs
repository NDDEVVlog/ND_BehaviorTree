using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Sidebar of the dialogue editor: everything about the database besides the graph — conversations, characters,
    /// variables, quests, events, settings, and tabs added by plugins. Edits go through bound PropertyFields
    /// (undoable); <see cref="DatabaseChanged"/> tells the window to redraw the graph and inspector.
    /// </summary>
    public class DialogueDatabasePanel : VisualElement
    {
        public const string ConversationsTab = "Conversations";
        public const string CharactersTab = "Characters";
        public const string VariablesTab = "Variables";
        public const string QuestsTab = "Quests";
        public const string EventsTab = "Events";
        public const string SettingsTab = "Settings";
        private static readonly string[] BuiltInTabs = { ConversationsTab, CharactersTab, VariablesTab, QuestsTab, EventsTab, SettingsTab };

        private DialogueDatabase m_Database;
        private SerializedObject m_Serialized;
        private int m_SelectedConversationId;
        private int m_SelectedActorIndex;
        private string m_Tab = ConversationsTab;
        private readonly VisualElement m_TabStrip;
        private readonly ScrollView m_Content;
        private IVisualElementScheduledItem m_PendingChange;

        /// <summary>The user picked a conversation to show on the canvas.</summary>
        public event Action<int> ConversationSelected;
        /// <summary>The database was edited from this panel (debounced).</summary>
        public event Action DatabaseChanged;
        /// <summary>Supplies plugin args for plugin tabs.</summary>
        public Func<DialoguePluginArgs> PluginArgs { get; set; }

        public string CurrentTab => m_Tab;

        public DialogueDatabasePanel()
        {
            AddToClassList("dlg-panel");
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) styleSheets.Add(sheet);
            style.flexGrow = 1;

            m_TabStrip = new VisualElement();
            m_TabStrip.AddToClassList("dlg-panel__tabs");
            Add(m_TabStrip);
            m_Content = new ScrollView(ScrollViewMode.Vertical);
            m_Content.AddToClassList("dlg-panel__content");
            m_Content.style.flexGrow = 1;
            Add(m_Content);
        }

        public void Bind(DialogueDatabase database, int selectedConversationId)
        {
            m_Database = database;
            m_SelectedConversationId = selectedConversationId;
            Rebuild();
        }

        public IEnumerable<string> TabTitles => BuiltInTabs.Concat(DialoguePluginRegistry.DatabaseTabs.Select(t => t.Title));

        public void ShowTab(string title)
        {
            m_Tab = title;
            Rebuild();
        }

        /// <summary>Rebuilds tabs and the current tab's content from the database.</summary>
        public void Rebuild()
        {
            m_TabStrip.Clear();
            foreach (var title in TabTitles)
            {
                var t = title;
                var button = new Button(() => ShowTab(t)) { text = t };
                button.AddToClassList("dlg-panel__tab");
                button.EnableInClassList("dlg-panel__tab--active", t == m_Tab);
                m_TabStrip.Add(button);
            }

            m_Content.Clear();
            this.Unbind();
            if (m_Database == null)
            {
                m_Content.Add(new HelpBox("Pick or create a Dialogue Database (toolbar).", HelpBoxMessageType.Info));
                return;
            }
            m_Serialized = new SerializedObject(m_Database);
            switch (m_Tab)
            {
                case ConversationsTab: BuildConversations(); break;
                case CharactersTab: BuildCharacters(); break;
                case VariablesTab: BuildSchemaList(nameof(DialogueDatabase.variables), "variables", s => s.VariableNames); break;
                case QuestsTab: BuildSchemaList(nameof(DialogueDatabase.quests), "quests", s => s.QuestNames); break;
                case EventsTab: BuildEvents(); break;
                case SettingsTab: BuildSettings(); break;
                default: BuildPluginTab(); break;
            }
            this.TrackSerializedObjectValue(m_Serialized, _ => QueueChanged());
        }

        private void QueueChanged()
        {
            // Many property callbacks per edit: report once, shortly after.
            if (m_PendingChange == null) m_PendingChange = schedule.Execute(() => DatabaseChanged?.Invoke());
            m_PendingChange.ExecuteLater(150);
        }

        private void Edit(string undoLabel, Action change)
        {
            Undo.RecordObject(m_Database, undoLabel);
            change();
            EditorUtility.SetDirty(m_Database);
            DatabaseChanged?.Invoke();
            Rebuild();
        }

        #region Conversations

        private void BuildConversations()
        {
            var conversations = m_Database.conversations.Where(c => c != null).ToList();
            var list = new ListView(conversations, 22,
                () => new Label(),
                (e, i) => ((Label)e).text = $"{conversations[i].title}   ·  #{conversations[i].id}  ·  {conversations[i].entries.Count - 1} entries")
            {
                selectionType = SelectionType.Single,
                style = { minHeight = 120, maxHeight = 320 }
            };
            int selected = conversations.FindIndex(c => c.id == m_SelectedConversationId);
            if (selected >= 0) list.SetSelectionWithoutNotify(new[] { selected });
            list.selectionChanged += items =>
            {
                var picked = items.OfType<Conversation>().FirstOrDefault();
                if (picked == null) return;
                m_SelectedConversationId = picked.id;
                ConversationSelected?.Invoke(picked.id);
                Rebuild();
            };
            m_Content.Add(list);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(new Button(AddConversation) { text = "+ New" });
            var current = m_Database.FindConversation(m_SelectedConversationId);
            if (current != null)
                buttons.Add(new Button(() => DeleteConversation(current)) { text = "Delete" });
            m_Content.Add(buttons);

            if (current == null) return;
            Section($"Conversation #{current.id}");
            int index = m_Database.conversations.IndexOf(current);
            var prop = m_Serialized.FindProperty($"{nameof(DialogueDatabase.conversations)}.Array.data[{index}]");
            var title = new TextField("Title") { value = current.title, isDelayed = true };
            title.RegisterValueChangedCallback(evt => RenameConversation(current, evt.newValue));
            m_Content.Add(title);
            m_Content.Add(Bound(prop.FindPropertyRelative(nameof(Conversation.description))));
        }

        private void AddConversation()
        {
            var existing = m_Database.conversations.Where(c => c != null).Select(c => c.title).ToArray();
            string title = ObjectNames.GetUniqueName(existing, "New Conversation");
            Conversation created = null;
            Edit("Add Conversation", () => created = m_Database.AddConversation(title));
            if (created == null) return;
            m_SelectedConversationId = created.id;
            ConversationSelected?.Invoke(created.id);
            Rebuild();
        }

        private void RenameConversation(Conversation conversation, string title)
        {
            title = title?.Trim();
            if (string.IsNullOrEmpty(title) || title == conversation.title) return;
            if (m_Database.FindConversation(title) != null)
            {
                Debug.LogWarning($"[Dialogue Editor] A conversation named '{title}' already exists.");
                Rebuild();
                return;
            }
            Edit("Rename Conversation", () => conversation.title = title);
        }

        private void DeleteConversation(Conversation conversation)
        {
            if (!EditorUtility.DisplayDialog("Delete conversation",
                    $"Delete '{conversation.title}' and its {conversation.entries.Count} entries? Links from other conversations into it are removed too.", "Delete", "Cancel"))
                return;
            Edit("Delete Conversation", () => m_Database.RemoveConversation(conversation.id));
            m_SelectedConversationId = m_Database.conversations.FirstOrDefault(c => c != null)?.id ?? 0;
            ConversationSelected?.Invoke(m_SelectedConversationId);
            Rebuild();
        }

        #endregion

        #region Characters

        private void BuildCharacters()
        {
            var actors = m_Database.actors;
            var list = new ListView(actors, 22,
                () => new Label(),
                (e, i) => ((Label)e).text = actors[i] == null ? "(empty)" : $"{actors[i].name}{(actors[i].isPlayer ? "   (player)" : string.Empty)}   ·  #{actors[i].id}")
            {
                selectionType = SelectionType.Single,
                style = { minHeight = 100, maxHeight = 260 }
            };
            m_SelectedActorIndex = Mathf.Clamp(m_SelectedActorIndex, 0, Mathf.Max(0, actors.Count - 1));
            if (actors.Count > 0) list.SetSelectionWithoutNotify(new[] { m_SelectedActorIndex });
            list.selectionChanged += _ =>
            {
                m_SelectedActorIndex = list.selectedIndex;
                Rebuild();
            };
            m_Content.Add(list);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var addMenu = new Button { text = "+ Character ▾", tooltip = "Pick the kind of character (every Actor subclass is listed)" };
            addMenu.clicked += ShowAddActorMenu;
            buttons.Add(addMenu);
            buttons.Add(new Button(() => AddActor(typeof(Actor), true)) { text = "+ Player" });
            if (actors.Count > 0) buttons.Add(new Button(() => RemoveActor(m_SelectedActorIndex)) { text = "Remove" });
            m_Content.Add(buttons);

            if (actors.Count == 0)
            {
                m_Content.Add(new HelpBox("No characters yet. Every line is spoken by a character; add the player too (its lines become choices).", HelpBoxMessageType.Info));
                return;
            }
            var actor = actors[m_SelectedActorIndex];
            Section(actor != null ? $"Character #{actor.id}  ·  {ObjectNames.NicifyVariableName(actor.GetType().Name)}" : "Character");
            if (actor == null) return;
            if (!string.IsNullOrEmpty(actor.EditorNote)) m_Content.Add(new HelpBox(actor.EditorNote, HelpBoxMessageType.Info));
            var prop = m_Serialized.FindProperty($"{nameof(DialogueDatabase.actors)}.Array.data[{m_SelectedActorIndex}]");
            // Every field, the subclass's included (managed reference). The id is not editable.
            var child = prop.Copy();
            var end = prop.GetEndProperty();
            for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                if (child.name == nameof(Actor.id)) continue;
                m_Content.Add(Bound(child.Copy()));
            }
        }

        /// <summary>Concrete, serializable <see cref="Actor"/> types with a parameterless constructor (base first).</summary>
        public static List<Type> ActorTypes()
        {
            var types = new List<Type> { typeof(Actor) };
            types.AddRange(TypeCache.GetTypesDerivedFrom<Actor>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name));
            return types;
        }

        private void ShowAddActorMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in ActorTypes())
            {
                var t = type;
                string label = t == typeof(Actor) ? "Character (base)" : ObjectNames.NicifyVariableName(t.Name);
                menu.AddItem(new GUIContent(label), false, () => AddActor(t, false));
            }
            menu.ShowAsContext();
        }

        /// <summary>Adds a character of <paramref name="type"/> with a unique name and one emotion (its default).</summary>
        public Actor AddActor(Type type, bool player)
        {
            var names = m_Database.actors.Where(a => a != null).Select(a => a.name).ToArray();
            string baseName = player ? "Player" : type == typeof(Actor) ? "Character" : type.Name;
            string actorName = ObjectNames.GetUniqueName(names, baseName).Replace(Actor.AttributeSeparator, '_');
            Actor added = null;
            Edit("Add Character", () =>
            {
                var actor = (Actor)Activator.CreateInstance(type);
                actor.name = actorName;
                actor.isPlayer = player;
                added = m_Database.AddActor(actor);
                if (added != null) added.emotions.Add(new ActorEmotion { id = added.defaultEmotion });
            });
            m_SelectedActorIndex = m_Database.actors.Count - 1;
            Rebuild();
            return added;
        }

        private void RemoveActor(int index)
        {
            if (index < 0 || index >= m_Database.actors.Count) return;
            var actor = m_Database.actors[index];
            int uses = actor == null ? 0 : m_Database.conversations.Where(c => c != null).SelectMany(c => c.entries).Count(e => e != null && e.id != Conversation.StartEntryId && e.actorId == actor.id);
            if (uses > 0 && !EditorUtility.DisplayDialog("Remove character",
                    $"'{actor.name}' speaks {uses} line(s). Those lines will report an unknown speaker until you reassign them.", "Remove", "Cancel"))
                return;
            Edit("Remove Character", () => m_Database.actors.RemoveAt(index));
        }

        #endregion

        #region Variables / quests / events / settings

        private void BuildSchemaList(string listField, string kind, Func<IDialogueSchema, IEnumerable<string>> names)
        {
            if (m_Database.schemaSource != null)
            {
                m_Content.Add(new HelpBox($"The {kind} come from '{m_Database.schemaSource.name}' (Settings → Schema Source). Edit them there.", HelpBoxMessageType.Info));
                var found = names(m_Database.Schema).OrderBy(n => n).ToList();
                if (found.Count == 0) m_Content.Add(new Label($"(no {kind})"));
                foreach (var n in found) m_Content.Add(new Label("•  " + n));
                m_Content.Add(new Button(() => Selection.activeObject = m_Database.schemaSource) { text = "Select Schema Source" });
                return;
            }
            m_Content.Add(Bound(m_Serialized.FindProperty(listField)));
        }

        private void BuildEvents()
        {
            m_Content.Add(new HelpBox("Gameplay events dialogue can fire (Raise Event action). Scenes react with a Dialogue Event Listener component. " +
                                      "Once any event is declared, Validate reports actions that use undeclared names.", HelpBoxMessageType.None));
            m_Content.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.events))));
        }

        private void BuildSettings()
        {
            m_Content.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.textTable))));
            if (m_Database.textTable == null)
                m_Content.Add(new Button(CreateTextTable) { text = "Create Text Table next to the database" });
            if (m_Database.textTable != null)
            {
                string locale = PluginArgs?.Invoke()?.Locale;
                int plain = m_Database.conversations.Where(c => c != null).SelectMany(c => c.entries)
                    .Count(e => e != null && ((!e.text.IsLocalized && !string.IsNullOrEmpty(e.text.Text)) || (!e.menuText.IsLocalized && !string.IsNullOrEmpty(e.menuText.Text))));
                var localizeAll = new Button(() =>
                {
                    int done = DialogueTextEditing.LocalizeAll(m_Database, locale);
                    Debug.Log($"[Dialogue Editor] Connected {done} plain text(s) of '{m_Database.name}' to '{m_Database.textTable.name}' as '{locale}'.", m_Database);
                    DatabaseChanged?.Invoke();
                    Rebuild();
                }) { text = $"Localize all plain lines → Text Table ({plain})", tooltip = $"Moves every plain line/choice label into the table as the '{locale}' text and connects it." };
                localizeAll.SetEnabled(plain > 0 && !string.IsNullOrEmpty(locale));
                m_Content.Add(localizeAll);
            }
            m_Content.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.schemaSource))));
            m_Content.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.runnerOptions))));

            Section("Plugins");
            if (DialoguePluginRegistry.Entries.Count == 0) m_Content.Add(new Label("(none installed)"));
            foreach (var entry in DialoguePluginRegistry.Entries)
            {
                var label = new Label($"{entry.Plugin.DisplayName}  {entry.Plugin.Version}{(entry.Installed ? string.Empty : "  — FAILED: " + entry.Error)}");
                label.tooltip = entry.Plugin.Description;
                m_Content.Add(label);
            }
        }

        private void CreateTextTable()
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_Database))?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError("[Dialogue Editor] Save the database as an asset first.");
                return;
            }
            var table = ScriptableObject.CreateInstance<StringTable>();
            table.tableName = m_Database.name;
            AssetDatabase.CreateAsset(table, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{m_Database.name}_Text.asset"));
            Edit("Assign Text Table", () => m_Database.textTable = table);
        }

        private void BuildPluginTab()
        {
            var panel = DialoguePluginRegistry.DatabaseTabs.FirstOrDefault(t => t.Title == m_Tab);
            if (panel == null)
            {
                m_Tab = ConversationsTab;
                Rebuild();
                return;
            }
            var args = PluginArgs?.Invoke() ?? new DialoguePluginArgs { Database = m_Database };
            m_Content.Add(DialoguePluginRegistry.BuildPanel(panel, args));
        }

        #endregion

        private VisualElement Bound(SerializedProperty property)
        {
            if (property == null) return new HelpBox("Missing property (database version mismatch?).", HelpBoxMessageType.Error);
            var field = new PropertyField(property);
            field.Bind(m_Serialized);
            return field;
        }

        private void Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dlg-inspector__section");
            m_Content.Add(label);
        }
    }
}
