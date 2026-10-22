using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.Localization.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// ND Dialogue editor — everything is set up here. A top bar picks the section; the section gets the whole
    /// viewport: Conversations is the graph (with the entry inspector on the right, which can be hidden), the others
    /// (Characters, Variables, Quests, Events, Localization, Settings, plugin sections) are full-width pages.
    /// The bar folds to one line. Double-click a Dialogue Database asset to open it.
    /// </summary>
    public class DialogueEditorWindow : EditorWindow
    {
        [SerializeField] private DialogueDatabase m_Database;
        [SerializeField] private int m_ConversationId;
        [SerializeField] private string m_Locale;
        [SerializeField] private string m_Tab = DialogueDatabasePanel.ConversationsTab;
        [SerializeField] private bool m_NavFolded;
        [SerializeField] private bool m_InspectorVisible = true;

        private DialogueGraphCanvas m_Canvas;
        private DialogueEntryInspector m_Inspector;
        private DialogueDatabasePanel m_Panel;
        private DialogueTestPanel m_Test;
        private VisualElement m_Nav;
        private VisualElement m_NavTabs;
        private VisualElement m_ConversationBar;
        private TwoPaneSplitView m_Workspace;
        private ObjectField m_DatabaseField;
        private ToolbarMenu m_PluginMenu;
        private ToolbarToggle m_InspectorToggle;
        private Label m_Status;

        public DialogueDatabase Database => m_Database;
        public DialogueGraphCanvas Canvas => m_Canvas;
        public DialogueDatabasePanel Panel => m_Panel;
        public DialogueTestPanel TestPanel => m_Test;
        public string Locale => m_Locale;
        public string CurrentTab => m_Tab;

        [MenuItem("Window/ND Dialogue/Dialogue Editor")]
        public static DialogueEditorWindow ShowWindow()
        {
            var window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent("Dialogue Editor");
            window.minSize = new Vector2(720, 420);
            return window;
        }

        public static DialogueEditorWindow Open(DialogueDatabase database)
        {
            var window = ShowWindow();
            window.SetDatabase(database);
            return window;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is DialogueDatabase database)) return false;
            Open(database);
            return true;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            DialoguePluginRegistry.Changed += OnPluginsChanged;
            DialogueNameButton.Edited += OnFieldEdited;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            DialoguePluginRegistry.Changed -= OnPluginsChanged;
            DialogueNameButton.Edited -= OnFieldEdited;
        }

        /// <summary>Args for plugin callbacks, describing the window's current state.</summary>
        public DialoguePluginArgs PluginArgs(DialogueEntry entry = null, Vector2 position = default) => new DialoguePluginArgs
        {
            Window = this,
            Canvas = m_Canvas,
            Database = m_Database,
            Conversation = m_Canvas?.Conversation,
            Entry = entry,
            Position = position,
            Selection = m_Canvas != null ? m_Canvas.SelectedNodes.Select(v => v.Entry).ToList() : new System.Collections.Generic.List<DialogueEntry>(),
            Locale = m_Locale,
        };

        private void CreateGUI()
        {
            if (string.IsNullOrEmpty(m_Locale)) m_Locale = LocaleCodes.Project.FirstOrDefault() ?? "en";
            var root = rootVisualElement;
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) root.styleSheets.Add(sheet);
            root.Add(BuildToolbar());

            m_Nav = new VisualElement();
            m_Nav.AddToClassList("dlg-nav");
            root.Add(m_Nav);

            m_Panel = new DialogueDatabasePanel { PluginArgs = () => PluginArgs() };
            m_Panel.ConversationSelected += id =>
            {
                m_ConversationId = id;
                ShowTab(DialogueDatabasePanel.ConversationsTab);
                LoadConversation();
            };
            m_Panel.DatabaseChanged += OnPanelChanged;
            m_Panel.TabRequested += ShowTab;

            m_Canvas = new DialogueGraphCanvas { HostWindow = this, PluginArgs = (entry, position) => PluginArgs(entry, position) };
            m_Canvas.SelectionChanged += OnSelectionChanged;
            m_Canvas.DataChanged += () => SetStatus(string.Empty);

            m_Inspector = new DialogueEntryInspector { PluginArgs = entry => PluginArgs(entry), PanelToScreen = p => position.position + p };
            m_Inspector.EntryChanged += entry => m_Canvas.RefreshNode(entry);
            m_Inspector.LinksChanged += () => m_Canvas.Reload();
            var inspectorScroll = new ScrollView(ScrollViewMode.Vertical);
            inspectorScroll.AddToClassList("dlg-inspector-pane");
            inspectorScroll.Add(m_Inspector);

            m_Workspace = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
            m_Workspace.Add(m_Canvas);
            m_Workspace.Add(inspectorScroll);
            root.Add(m_Workspace);
            root.Add(m_Panel);

            m_Test = new DialogueTestPanel();
            root.Add(m_Test);
            m_Canvas.TestRequested += (conversationId, entryId) =>
            {
                ShowTab(DialogueDatabasePanel.TestTab);
                m_Test.StartAt(conversationId, entryId);
            };

            Refresh();
        }

        #region Toolbar and top bar

        private VisualElement BuildToolbar()
        {
            var bar = new Toolbar();
            m_DatabaseField = new ObjectField { objectType = typeof(DialogueDatabase), allowSceneObjects = false, value = m_Database, style = { width = 220 } };
            m_DatabaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as DialogueDatabase));
            bar.Add(m_DatabaseField);
            bar.Add(new ToolbarButton(CreateDatabase) { text = "New…", tooltip = "Create a new Dialogue Database (with its text table)" });

            bar.Add(new ToolbarSpacer { flex = true });

            var locales = LocaleCodes.Project.ToList();
            if (locales.Count == 0) locales.Add("en");
            if (!locales.Contains(m_Locale)) locales.Add(m_Locale);
            var localeField = new PopupField<string>(locales, m_Locale) { tooltip = "Language shown and edited", style = { width = 70 } };
            localeField.RegisterValueChangedCallback(evt =>
            {
                m_Locale = evt.newValue;
                m_Canvas.SetPreviewLocale(m_Locale);
                m_Test?.SetLocale(m_Locale);
                OnSelectionChanged();
                if (m_Tab != DialogueDatabasePanel.ConversationsTab) m_Panel.Rebuild();
            });
            bar.Add(localeField);

            m_PluginMenu = new ToolbarMenu { text = "Plugins" };
            bar.Add(m_PluginMenu);
            RebuildPluginMenu();

            bar.Add(new ToolbarButton(Validate) { text = "Validate" });
            bar.Add(new ToolbarButton(Save) { text = "Save" });
            m_InspectorToggle = new ToolbarToggle { text = "Inspector", value = m_InspectorVisible, tooltip = "Show the entry inspector next to the graph" };
            m_InspectorToggle.RegisterValueChangedCallback(evt =>
            {
                m_InspectorVisible = evt.newValue;
                ApplyLayout();
            });
            bar.Add(m_InspectorToggle);
            m_Status = new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6, color = new Color(1f, 0.75f, 0.4f) } };
            bar.Add(m_Status);
            return bar;
        }

        private void RebuildPluginMenu()
        {
            if (m_PluginMenu == null) return;
            var items = DialoguePluginRegistry.ToolbarItems.ToList();
            m_PluginMenu.style.display = items.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_PluginMenu.menu.ClearItems();
            foreach (var item in items)
            {
                var i = item;
                m_PluginMenu.menu.AppendAction(i.Path, _ => DialoguePluginRegistry.Run(i, PluginArgs()),
                    _ => DialoguePluginRegistry.IsEnabled(i, PluginArgs()) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        /// <summary>
        /// The section tabs (folded: only the current one, click to unfold), and — for Conversations — a row to pick,
        /// add, rename or delete the conversation shown on the graph.
        /// </summary>
        private void RebuildNav()
        {
            if (m_Nav == null) return;
            m_Nav.Clear();
            m_Nav.EnableInClassList("dlg-nav--folded", m_NavFolded);

            m_NavTabs = new VisualElement();
            m_NavTabs.AddToClassList("dlg-nav__tabs");
            if (m_NavFolded)
            {
                var current = new Button(() => SetNavFolded(false)) { text = $"▸  {m_Tab}", tooltip = "Show all sections" };
                current.AddToClassList("dlg-nav__tab");
                current.AddToClassList("dlg-nav__tab--active");
                m_NavTabs.Add(current);
            }
            else
            {
                foreach (var title in m_Panel.TabTitles)
                {
                    var t = title;
                    var tab = new Button(() => ShowTab(t)) { text = t };
                    tab.AddToClassList("dlg-nav__tab");
                    tab.EnableInClassList("dlg-nav__tab--active", t == m_Tab);
                    m_NavTabs.Add(tab);
                }
            }
            m_NavTabs.Add(new VisualElement { style = { flexGrow = 1 } });
            var fold = new Button(() => SetNavFolded(!m_NavFolded)) { text = m_NavFolded ? "▾" : "▴", tooltip = m_NavFolded ? "Unfold the bar" : "Fold the bar to one line" };
            fold.AddToClassList("dlg-nav__fold");
            m_NavTabs.Add(fold);
            m_Nav.Add(m_NavTabs);

            m_ConversationBar = null;
            if (!m_NavFolded && m_Tab == DialogueDatabasePanel.ConversationsTab && m_Database != null)
                m_Nav.Add(m_ConversationBar = BuildConversationBar());
        }

        private VisualElement BuildConversationBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("dlg-nav__sub");
            var conversations = m_Database.conversations.Where(c => c != null).ToList();
            var current = m_Database.FindConversation(m_ConversationId);

            if (conversations.Count > 0)
            {
                var titles = conversations.Select(c => c.title).ToList();
                var picker = new PopupField<string>(titles, current != null ? current.title : titles[0]) { tooltip = "Conversation shown on the graph" };
                picker.AddToClassList("dlg-nav__conversation");
                picker.RegisterValueChangedCallback(evt =>
                {
                    var picked = m_Database.FindConversation(evt.newValue);
                    if (picked == null) return;
                    m_ConversationId = picked.id;
                    LoadConversation();
                    RebuildNav();
                });
                bar.Add(picker);
            }
            bar.Add(new Button(() => m_Panel.AddConversation()) { text = "+ New", tooltip = "Add a conversation" });
            if (current != null)
            {
                var rename = new TextField { value = current.title, isDelayed = true, tooltip = "Rename (Enter to apply)" };
                rename.AddToClassList("dlg-nav__rename");
                rename.RegisterValueChangedCallback(evt =>
                {
                    m_Panel.RenameConversation(m_Database.FindConversation(m_ConversationId), evt.newValue);
                    RebuildNav();
                });
                bar.Add(rename);
                bar.Add(new Label($"{current.entries.Count - 1} lines") { name = "line-count" });
                bar.Add(new VisualElement { style = { flexGrow = 1 } });
                bar.Add(new Button(() => m_Panel.DeleteConversation(m_Database.FindConversation(m_ConversationId))) { text = "Delete", tooltip = "Delete this conversation" });
            }
            return bar;
        }

        private void SetNavFolded(bool folded)
        {
            m_NavFolded = folded;
            RebuildNav();
        }

        /// <summary>Shows a section: the graph for Conversations, else that section's page over the whole viewport.</summary>
        public void ShowTab(string title)
        {
            m_Tab = string.IsNullOrEmpty(title) ? DialogueDatabasePanel.ConversationsTab : title;
            if (m_Panel == null) return; // before CreateGUI
            if (m_Tab == DialogueDatabasePanel.TestTab) m_Test.Bind(m_Database, m_Locale, this, m_ConversationId);
            else if (m_Tab != DialogueDatabasePanel.ConversationsTab) m_Panel.ShowTab(m_Tab);
            ApplyLayout();
            RebuildNav();
        }

        private void ApplyLayout()
        {
            if (m_Workspace == null) return;
            bool graph = m_Tab == DialogueDatabasePanel.ConversationsTab;
            bool test = m_Tab == DialogueDatabasePanel.TestTab;
            m_Workspace.style.display = graph ? DisplayStyle.Flex : DisplayStyle.None;
            m_Test.style.display = test ? DisplayStyle.Flex : DisplayStyle.None;
            m_Panel.style.display = graph || test ? DisplayStyle.None : DisplayStyle.Flex;
            if (m_InspectorVisible) m_Workspace.UnCollapse();
            else m_Workspace.CollapseChild(1);
            m_InspectorToggle?.SetValueWithoutNotify(m_InspectorVisible);
            m_InspectorToggle?.SetEnabled(graph);
        }

        #endregion

        public void SetDatabase(DialogueDatabase database)
        {
            m_Database = database;
            m_ConversationId = database != null ? database.conversations.FirstOrDefault(c => c != null)?.id ?? 0 : 0;
            Refresh();
        }

        private void Refresh()
        {
            if (m_Canvas == null) return; // before CreateGUI
            m_DatabaseField?.SetValueWithoutNotify(m_Database);
            if (m_Database != null && m_Database.FindConversation(m_ConversationId) == null)
                m_ConversationId = m_Database.conversations.FirstOrDefault(c => c != null)?.id ?? 0;
            m_Panel.Bind(m_Database, m_ConversationId);
            if (!m_Panel.TabTitles.Contains(m_Tab)) m_Tab = DialogueDatabasePanel.ConversationsTab;
            LoadConversation();
            ShowTab(m_Tab);
            titleContent = new GUIContent(m_Database != null ? $"Dialogue: {m_Database.name}" : "Dialogue Editor");
        }

        private void LoadConversation()
        {
            var conversation = m_Database != null ? m_Database.FindConversation(m_ConversationId) : null;
            m_Canvas.Load(m_Database, conversation, m_Locale);
            if (conversation != null) m_Canvas.schedule.Execute(() => m_Canvas.FrameDefault()).StartingIn(50); // after layout
        }

        /// <summary>Redraws graph, inspector and sections after a change made outside them (plugins, tools, the bridge).</summary>
        public void NotifyDatabaseChanged()
        {
            if (m_Canvas == null) return;
            if (m_Database != null && m_Database.FindConversation(m_ConversationId) == null)
                m_ConversationId = m_Database.conversations.FirstOrDefault(c => c != null)?.id ?? 0;
            if (m_Canvas.Conversation == null || m_Canvas.Conversation.id != m_ConversationId) LoadConversation();
            else m_Canvas.Reload();
            if (m_Tab != DialogueDatabasePanel.ConversationsTab) m_Panel.Rebuild();
            RebuildNav();
            SetStatus(string.Empty);
        }

        private void OnPanelChanged()
        {
            // Characters/emotions/events/conversations changed: node labels, the inspector's pickers and the bar depend on them.
            if (m_Database != null && m_Database.FindConversation(m_ConversationId) == null)
            {
                m_ConversationId = m_Database.conversations.FirstOrDefault(c => c != null)?.id ?? 0;
                LoadConversation();
            }
            else m_Canvas.Reload();
            RebuildNav();
        }

        /// <summary>A picker or deferred menu wrote a field (variable name, OR-group row…): redraw what shows it.</summary>
        private void OnFieldEdited(Object target)
        {
            if (m_Canvas == null || target != m_Database) return;
            m_Canvas.RefreshAll();
        }

        private void OnSelectionChanged()
        {
            if (m_Inspector == null) return;
            var entry = m_Canvas.SingleSelectedEntry;
            if (entry == null) m_Inspector.Unbind(m_Canvas.SelectionCount > 1 ? "Several entries selected." : "Select one entry to edit it.");
            else m_Inspector.Bind(m_Database, m_Canvas.Conversation, entry, m_Locale); // also refreshes the same entry (locale, undo)
        }

        private void OnUndoRedo()
        {
            if (m_Database == null || m_Canvas == null) return;
            foreach (var c in m_Database.conversations) c?.InvalidateCache();
            if (m_Database.textTable != null) m_Database.textTable.InvalidateCache();
            Refresh();
        }

        private void OnPluginsChanged()
        {
            RebuildPluginMenu();
            if (m_Panel == null) return;
            if (!m_Panel.TabTitles.Contains(m_Tab)) m_Tab = DialogueDatabasePanel.ConversationsTab;
            ShowTab(m_Tab);
            OnSelectionChanged();
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Dialogue Database", "DialogueDatabase", "asset", "Where to save the database (its text table goes next to it).");
            if (string.IsNullOrEmpty(path)) return;
            var database = CreateInstance<DialogueDatabase>();
            AssetDatabase.CreateAsset(database, path);
            var table = CreateInstance<ND.Localization.StringTable>();
            table.tableName = database.name;
            AssetDatabase.CreateAsset(table, AssetDatabase.GenerateUniqueAssetPath(path.Replace(".asset", "_Text.asset")));
            database.textTable = table;
            database.AddActor("Player", isPlayer: true);
            database.AddActor("NPC");
            database.AddConversation("First Conversation");
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SetDatabase(database);
        }

        private void Validate()
        {
            if (m_Database == null) return;
            var issues = DialoguePluginRegistry.Validate(m_Database);
            if (issues.Count == 0)
            {
                SetStatus(string.Empty);
                ShowNotification(new GUIContent("No problems found."));
                return;
            }
            SetStatus($"{issues.Count} problem(s) — see Console");
            Debug.LogWarning($"[Dialogue Editor] '{m_Database.name}' has {issues.Count} problem(s):\n  - " + string.Join("\n  - ", issues), m_Database);
        }

        private void Save()
        {
            if (m_Database == null) return;
            AssetDatabase.SaveAssetIfDirty(m_Database);
            if (m_Database.textTable != null) AssetDatabase.SaveAssetIfDirty(m_Database.textTable);
            ShowNotification(new GUIContent("Saved."));
        }

        private void SetStatus(string text)
        {
            if (m_Status != null) m_Status.text = text;
        }
    }
}
