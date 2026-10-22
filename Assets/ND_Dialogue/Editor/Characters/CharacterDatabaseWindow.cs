using System;
using System.Collections.Generic;
using System.Linq;
using ND.Localization.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Character Database editor: a top bar with three sections, each over the whole window —
    /// <b>Characters</b> (card strip, then the character's identity, emotions, attributes with the template's (kept, values editable)
    /// ones, and the looks other databases give it), <b>Template</b> (the attributes every character using this
    /// database as template gets, and who uses it) and <b>Used by</b> (the dialogue databases that use these
    /// characters). Double-click a Character Database asset to open it.
    /// </summary>
    public class CharacterDatabaseWindow : EditorWindow
    {
        public const string CharactersTab = "Characters";
        public const string TemplateTab = "Template";
        public const string UsedByTab = "Used by";
        private static readonly string[] Tabs = { CharactersTab, TemplateTab, UsedByTab };

        [SerializeField] private CharacterDatabase m_Database;
        [SerializeField] private string m_Tab = CharactersTab;
        [SerializeField] private int m_SelectedId;

        private ObjectField m_DatabaseField;
        private VisualElement m_Nav;
        private ScrollView m_Content;
        private VisualElement m_Strip;
        private SerializedObject m_Serialized;
        private Label m_Status;

        public CharacterDatabase Database => m_Database;
        public string CurrentTab => m_Tab;

        [MenuItem("Window/ND Dialogue/Character Editor")]
        public static CharacterDatabaseWindow ShowWindow()
        {
            var window = GetWindow<CharacterDatabaseWindow>();
            window.titleContent = new GUIContent("Character Editor");
            window.minSize = new Vector2(560, 360);
            return window;
        }

        public static CharacterDatabaseWindow Open(CharacterDatabase database)
        {
            var window = ShowWindow();
            window.SetDatabase(database);
            return window;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is CharacterDatabase database)) return false;
            Open(database);
            return true;
        }

        private void OnEnable() => Undo.undoRedoPerformed += Rebuild;
        private void OnDisable() => Undo.undoRedoPerformed -= Rebuild;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) root.styleSheets.Add(sheet);

            var bar = new Toolbar();
            m_DatabaseField = new ObjectField { objectType = typeof(CharacterDatabase), allowSceneObjects = false, value = m_Database, style = { width = 220 } };
            m_DatabaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as CharacterDatabase));
            bar.Add(m_DatabaseField);
            bar.Add(new ToolbarButton(CreateDatabase) { text = "New…", tooltip = "Create a new Character Database" });
            bar.Add(new ToolbarSpacer { flex = true });
            bar.Add(new ToolbarButton(Validate) { text = "Validate" });
            bar.Add(new ToolbarButton(() => { if (m_Database != null) AssetDatabase.SaveAssetIfDirty(m_Database); ShowNotification(new GUIContent("Saved.")); }) { text = "Save" });
            m_Status = new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6, color = new Color(1f, 0.75f, 0.4f) } };
            bar.Add(m_Status);
            root.Add(bar);

            m_Nav = new VisualElement();
            m_Nav.AddToClassList("dlg-nav");
            root.Add(m_Nav);

            var panel = new VisualElement { style = { flexGrow = 1 } };
            panel.AddToClassList("dlg-panel");
            m_Content = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            m_Content.AddToClassList("dlg-panel__content");
            panel.Add(m_Content);
            root.Add(panel);
            Rebuild();
        }

        public void SetDatabase(CharacterDatabase database)
        {
            m_Database = database;
            m_SelectedId = database != null ? database.characters.FirstOrDefault(c => c != null)?.id ?? 0 : 0;
            Rebuild();
        }

        public void ShowTab(string tab)
        {
            m_Tab = Tabs.Contains(tab) ? tab : CharactersTab;
            Rebuild();
        }

        /// <summary>Redraws the bar and the current section from the asset.</summary>
        public void Rebuild()
        {
            if (m_Content == null) return; // before CreateGUI
            m_DatabaseField.SetValueWithoutNotify(m_Database);
            titleContent = new GUIContent(m_Database != null ? $"Characters: {m_Database.name}" : "Character Editor");

            m_Nav.Clear();
            var tabs = new VisualElement();
            tabs.AddToClassList("dlg-nav__tabs");
            foreach (var t in Tabs)
            {
                var tab = t;
                var button = new Button(() => ShowTab(tab)) { text = tab };
                button.AddToClassList("dlg-nav__tab");
                button.EnableInClassList("dlg-nav__tab--active", tab == m_Tab);
                tabs.Add(button);
            }
            m_Nav.Add(tabs);

            m_Content.Clear();
            m_Content.Unbind();
            m_Strip = null;
            if (m_Database == null)
            {
                m_Content.Add(new HelpBox("Pick or create a Character Database (toolbar).", HelpBoxMessageType.Info));
                return;
            }
            m_Serialized = new SerializedObject(m_Database);
            switch (m_Tab)
            {
                case TemplateTab: BuildTemplate(); break;
                case UsedByTab: BuildUsedBy(); break;
                default: BuildCharacters(); break;
            }
        }

        #region Characters

        private void BuildCharacters()
        {
            m_Strip = new ScrollView(ScrollViewMode.Horizontal);
            m_Strip.AddToClassList("dlg-charstrip");
            m_Content.Add(m_Strip);
            RefreshStrip();

            int index = m_Database.characters.FindIndex(c => c != null && c.id == m_SelectedId);
            if (index < 0 && m_Database.characters.Count > 0) { index = 0; m_SelectedId = m_Database.characters[0]?.id ?? 0; }
            if (index < 0)
            {
                m_Content.Add(new HelpBox("No characters yet: add one with the + card. Dialogue databases that list this Character Database in their Cast can use them.", HelpBoxMessageType.Info));
                return;
            }
            var actor = m_Database.characters[index];
            if (actor == null)
            {
                m_Content.Add(new HelpBox("This slot is empty (its type may have been deleted).", HelpBoxMessageType.Warning));
                m_Content.Add(new Button(() => Remove(index)) { text = "Remove the empty slot" });
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("dlg-detail__header");
            header.Add(new Label(actor.ResolveDisplayName()) { name = "detail-title" });
            header.Add(CharacterSections.Pill(ObjectNames.NicifyVariableName(actor.GetType().Name)));
            if (actor.isPlayer) header.Add(CharacterSections.Pill("PLAYER", "dlg-pill--player"));
            header.Add(CharacterSections.Pill($"id {actor.id}"));
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            header.Add(new Button(() => Remove(index)) { text = "Remove" });
            m_Content.Add(header);
            if (!string.IsNullOrEmpty(actor.EditorNote)) m_Content.Add(new HelpBox(actor.EditorNote, HelpBoxMessageType.Info));

            var prop = m_Serialized.FindProperty($"{nameof(CharacterDatabase.characters)}.Array.data[{index}]");
            CharacterSections.BuildDetails(m_Content, actor, m_Serialized, prop, value => Edit("Change Default Emotion", () => actor.defaultEmotion = value, rebuild: true), Rebuild);
            m_Content.Add(LooksElsewhere(actor));
            // Names and portraits show on the cards: refresh them as fields change (the details keep their focus).
            var tracker = new VisualElement { style = { display = DisplayStyle.None } }; // one tracked object per element
            m_Content.Add(tracker);
            tracker.TrackSerializedObjectValue(m_Serialized, _ => RefreshStrip());
        }

        private void RefreshStrip()
        {
            if (m_Strip == null) return;
            m_Strip.Clear();
            string locale = LocaleCodes.Project.FirstOrDefault();
            foreach (var c in m_Database.characters)
            {
                var character = c;
                var card = CharacterSections.Card(character, CharacterSections.DefaultPortrait(character, locale), character?.ResolveDisplayName(),
                    character != null ? CharacterSections.Kind(character) : null, character != null ? $"{character.name}  ·  #{character.id}" : null,
                    character != null && character.id == m_SelectedId, false);
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    m_SelectedId = character?.id ?? 0;
                    Rebuild();
                });
                m_Strip.Add(card);
            }
            var add = new VisualElement { tooltip = "Add a character (pick its kind: every Actor subclass is listed)" };
            add.AddToClassList("dlg-charcard");
            add.AddToClassList("dlg-charcard--add");
            add.Add(new Label("+") { pickingMode = PickingMode.Ignore });
            add.RegisterCallback<ClickEvent>(_ => ShowAddMenu());
            m_Strip.Add(add);
        }

        /// <summary>Which dialogue databases give this character another look (read-only here, edited in each one).</summary>
        private VisualElement LooksElsewhere(Actor actor)
        {
            var group = CharacterSections.Group("Looks in dialogue databases", "Each dialogue database can show this character differently (other portraits, name). Edit a look in that database's Characters section.");
            var found = false;
            foreach (var db in UsingDatabases())
            {
                var look = db.FindLook(actor.id);
                if (look == null) continue;
                found = true;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                string changes = string.Join(", ", look.emotions.Where(e => e != null).Select(e => $"{DialogueEmotionGlyph.For(e.id)} {e.id}"));
                if (look.displayName != null && look.displayName.HasContent) changes = $"name \"{look.displayName.Text}\"" + (changes.Length > 0 ? ", " + changes : string.Empty);
                row.Add(new Label($"{db.name}:  {changes}") { style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } });
                var d = db;
                row.Add(new Button(() => DialogueEditorWindow.Open(d).ShowTab(DialogueDatabasePanel.CharactersTab)) { text = "Open" });
                group.Add(row);
            }
            if (!found) group.Add(new Label("(none: every dialogue database shows the character as defined here)") { style = { color = new Color(0.6f, 0.62f, 0.68f) } });
            return group;
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Player"), false, () => Add(typeof(Actor), true));
            menu.AddSeparator(string.Empty);
            foreach (var type in DialogueDatabasePanel.ActorTypes())
            {
                var t = type;
                menu.AddItem(new GUIContent(t == typeof(Actor) ? "Character (base)" : ObjectNames.NicifyVariableName(t.Name)), false, () => Add(t, false));
            }
            menu.ShowAsContext();
        }

        public Actor Add(Type type, bool player)
        {
            var names = m_Database.characters.Where(a => a != null).Select(a => a.name).ToArray();
            var actor = (Actor)Activator.CreateInstance(type);
            actor.name = ObjectNames.GetUniqueName(names, player ? "Player" : type == typeof(Actor) ? "Character" : type.Name).Replace(Actor.AttributeSeparator, '_');
            actor.isPlayer = player;
            actor.emotions.Add(new ActorEmotion { id = actor.defaultEmotion });
            Actor added = null;
            Edit("Add Character", () => added = m_Database.AddCharacter(actor), rebuild: false);
            if (added != null) m_SelectedId = added.id;
            Rebuild();
            return added;
        }

        private void Remove(int index)
        {
            var actor = m_Database.characters[index];
            int uses = actor == null ? 0 : UsingDatabases().SelectMany(d => d.conversations).Where(c => c != null)
                .SelectMany(c => c.entries).Count(e => e != null && e.id != Conversation.StartEntryId && e.actorId == actor.id);
            if (!EditorUtility.DisplayDialog("Remove character",
                    $"Remove '{actor?.name}' from '{m_Database.name}'? Dialogue databases using it lose it: {uses} line(s) will report an unknown speaker.", "Remove", "Cancel"))
                return;
            Edit("Remove Character", () => m_Database.characters.RemoveAt(index), rebuild: true);
        }

        #endregion

        #region Template / used by

        private void BuildTemplate()
        {
            var group = CharacterSections.Group("Template attributes",
                "Characters that pick this Character Database as their Template get these attributes: they cannot remove them, and follow your changes here (name, type, default value); each character may give one its own starting value.");
            group.Add(CharacterSections.Bound(m_Serialized.FindProperty(nameof(CharacterDatabase.templateAttributes)), m_Serialized));
            m_Content.Add(group);

            var users = CharacterSections.Group("Characters using this template");
            var found = AllCharacterOwners().SelectMany(o => o.characters.Select(c => (owner: o.owner, character: c)))
                .Where(x => x.character != null && x.character.template == m_Database).ToList();
            if (found.Count == 0) users.Add(new Label("(none yet: pick this database in a character's Template field)") { style = { color = new Color(0.6f, 0.62f, 0.68f) } });
            foreach (var (owner, character) in found)
                users.Add(CharacterSections.VariableRow(character.name, owner.name, DialogueVariableStyle.QuestColor));
            m_Content.Add(users);
        }

        private void BuildUsedBy()
        {
            var group = CharacterSections.Group("Dialogue databases using these characters", "Add this Character Database to a dialogue database's Cast (Characters section) to use its characters there.");
            var users = UsingDatabases().ToList();
            if (users.Count == 0) group.Add(new Label("(none)") { style = { color = new Color(0.6f, 0.62f, 0.68f) } });
            var ids = new HashSet<int>(m_Database.characters.Where(c => c != null).Select(c => c.id));
            foreach (var db in users)
            {
                var d = db;
                int lines = d.conversations.Where(c => c != null).SelectMany(c => c.entries).Count(e => e != null && ids.Contains(e.actorId));
                int looks = d.looks.Count(l => l != null && ids.Contains(l.actorId));
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
                row.Add(new Label($"{d.name}   ·   {lines} line(s)   ·   {looks} look(s)") { style = { flexGrow = 1 } });
                row.Add(new Button(() => DialogueEditorWindow.Open(d)) { text = "Open in Dialogue Editor" });
                group.Add(row);
            }
            m_Content.Add(group);
        }

        /// <summary>Every dialogue database in the project that lists this Character Database in its Cast.</summary>
        private IEnumerable<DialogueDatabase> UsingDatabases() =>
            AssetDatabase.FindAssets("t:DialogueDatabase").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueDatabase>)
                .Where(d => d != null && d.characterDatabases.Contains(m_Database));

        /// <summary>Character lists of every Character Database and dialogue database (for "who uses this template").</summary>
        private static IEnumerable<(UnityEngine.Object owner, List<Actor> characters)> AllCharacterOwners()
        {
            foreach (var path in AssetDatabase.FindAssets("t:CharacterDatabase").Select(AssetDatabase.GUIDToAssetPath))
            {
                var c = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(path);
                if (c != null) yield return (c, c.characters);
            }
            foreach (var path in AssetDatabase.FindAssets("t:DialogueDatabase").Select(AssetDatabase.GUIDToAssetPath))
            {
                var d = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(path);
                if (d != null) yield return (d, d.actors);
            }
        }

        #endregion

        private void Edit(string undoLabel, Action change, bool rebuild)
        {
            Undo.RecordObject(m_Database, undoLabel);
            change();
            EditorUtility.SetDirty(m_Database);
            if (rebuild) Rebuild();
        }

        private void Validate()
        {
            if (m_Database == null) return;
            var issues = m_Database.Validate();
            if (issues.Count == 0)
            {
                m_Status.text = string.Empty;
                ShowNotification(new GUIContent("No problems found."));
                return;
            }
            m_Status.text = $"{issues.Count} problem(s) — see Console";
            Debug.LogWarning($"[Character Editor] '{m_Database.name}' has {issues.Count} problem(s):\n  - " + string.Join("\n  - ", issues), m_Database);
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Character Database", "CharacterDatabase", "asset", "Where to save the Character Database.");
            if (string.IsNullOrEmpty(path)) return;
            var database = CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            SetDatabase(database);
        }
    }

    /// <summary>Character Database inspector: a summary and the way into the Character Editor.</summary>
    [CustomEditor(typeof(CharacterDatabase))]
    public class CharacterDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (CharacterDatabase)target;
            if (GUILayout.Button("Open Character Editor", GUILayout.Height(28))) CharacterDatabaseWindow.Open(database);
            var names = database.characters.Where(c => c != null).Select(c => c.name).ToList();
            EditorGUILayout.HelpBox($"{names.Count} character(s): {string.Join(", ", names)}\n" +
                                    $"{database.templateAttributes.Count} template attribute(s).", MessageType.None);
        }
    }
}
