using ND.Quests;
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
    /// Content of the editor's database sections — characters, variables, quests, events, localization, settings, and
    /// sections added by plugins. The window's top bar picks the section and gives it the whole viewport (the
    /// Conversations section is the graph itself). Edits go through bound PropertyFields or <see cref="Edit"/>
    /// (undoable); <see cref="DatabaseChanged"/> tells the window to redraw the graph and inspector.
    /// </summary>
    public partial class DialogueDatabasePanel : VisualElement
    {
        public const string ConversationsTab = "Conversations";
        /// <summary>The test chat (shown by the Dialogue Editor window itself).</summary>
        public const string TestTab = "Test Dialogue";
        public const string CharactersTab = "Characters";
        public const string VariablesTab = "Variables";
        public const string QuestsTab = "Quests";
        public const string EventsTab = "Events";
        public const string LocalizationTab = "Localization";
        public const string SettingsTab = "Settings";
        public const string ToolsTab = "Tools";
        private static readonly string[] BuiltInTabs = { ConversationsTab, TestTab, CharactersTab, VariablesTab, QuestsTab, EventsTab, LocalizationTab, ToolsTab, SettingsTab };

        private DialogueDatabase m_Database;
        private SerializedObject m_Serialized;
        private int m_SelectedConversationId;
        private int m_SelectedActorId;
        private string m_Tab = ConversationsTab;
        private readonly ScrollView m_Content;
        private IVisualElementScheduledItem m_PendingChange;

        /// <summary>The user picked a conversation to show on the canvas.</summary>
        public event Action<int> ConversationSelected;
        /// <summary>(conversation id, entry id) Show that entry on the graph (from a search result).</summary>
        public event Action<int, int> EntryRequested;
        /// <summary>The database was edited from this panel (debounced).</summary>
        public event Action DatabaseChanged;
        /// <summary>A section asked to show another section (e.g. "Edit in Characters").</summary>
        public event Action<string> TabRequested;
        /// <summary>Supplies plugin args for plugin tabs.</summary>
        public Func<DialoguePluginArgs> PluginArgs { get; set; }

        public string CurrentTab => m_Tab;
        public DialogueDatabase Database => m_Database;

        public DialogueDatabasePanel()
        {
            AddToClassList("dlg-panel");
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) styleSheets.Add(sheet);
            style.flexGrow = 1;

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

        /// <summary>Rebuilds the current section's content from the database.</summary>
        public void Rebuild()
        {
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
                case TestTab: Intro("Open this section in the Dialogue Editor window to play conversations in a test chat."); break;
                case CharactersTab: BuildCharacters(); break;
                case VariablesTab: BuildVariables(); break;
                case QuestsTab: BuildQuests(); break;
                case ToolsTab: BuildTools(); break;
                case EventsTab: BuildEvents(); break;
                case LocalizationTab: BuildLocalization(); break;
                case SettingsTab: BuildSettings(); break;
                default: BuildPluginTab(); break;
            }
            this.TrackSerializedObjectValue(m_Serialized, _ => QueueChanged());
        }

        /// <summary>
        /// Reports edits of another asset shown in the section (a Character Database). An element can track only one
        /// SerializedObject, so each one gets its own hidden tracker, removed with the section's content.
        /// </summary>
        private void TrackOther(SerializedObject other)
        {
            var tracker = new VisualElement { style = { display = DisplayStyle.None } };
            m_Content.Add(tracker);
            tracker.TrackSerializedObjectValue(other, _ => QueueChanged());
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

        private DialoguePluginArgs Args() => PluginArgs?.Invoke() ?? new DialoguePluginArgs { Database = m_Database };

        #region Conversations

        /// <summary>Overview cards (the graph itself is the window's Conversations section).</summary>
        private void BuildConversations()
        {
            Intro("Every conversation of the database. Open one to edit it on the graph.");
            var grid = new VisualElement();
            grid.AddToClassList("dlg-cards");
            foreach (var conversation in m_Database.conversations.Where(c => c != null))
            {
                var c = conversation;
                var card = Card(c.id == m_SelectedConversationId);
                card.Add(CardTitle(c.title));
                card.Add(CardSub($"#{c.id}  ·  {c.entries.Count - 1} lines{(c.isBark ? "  ·  💬 bark" : string.Empty)}"));
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    m_SelectedConversationId = c.id;
                    ConversationSelected?.Invoke(c.id);
                    Rebuild();
                });
                grid.Add(card);
            }
            var add = AddCard("New conversation");
            add.RegisterCallback<ClickEvent>(_ => AddConversation());
            grid.Add(add);
            m_Content.Add(grid);
        }

        public Conversation AddConversation()
        {
            var existing = m_Database.conversations.Where(c => c != null).Select(c => c.title).ToArray();
            string title = ObjectNames.GetUniqueName(existing, "New Conversation");
            Conversation created = null;
            Edit("Add Conversation", () => created = m_Database.AddConversation(title));
            if (created == null) return null;
            m_SelectedConversationId = created.id;
            ConversationSelected?.Invoke(created.id);
            Rebuild();
            return created;
        }

        /// <summary>Marks a conversation as barks (said in passing by a Dialogue Barker) or as a normal conversation.</summary>
        public void SetBark(Conversation conversation, bool isBark)
        {
            if (conversation == null || conversation.isBark == isBark) return;
            Edit(isBark ? "Mark as Bark Conversation" : "Mark as Conversation", () => conversation.isBark = isBark);
        }

        public void RenameConversation(Conversation conversation, string title)
        {
            title = title?.Trim();
            if (conversation == null || string.IsNullOrEmpty(title) || title == conversation.title) return;
            if (m_Database.FindConversation(title) != null)
            {
                Debug.LogWarning($"[Dialogue Editor] A conversation named '{title}' already exists.");
                return;
            }
            Edit("Rename Conversation", () => conversation.title = title);
        }

        public void DeleteConversation(Conversation conversation)
        {
            if (conversation == null) return;
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

        /// <summary>Where a character is stored: this database (source null) or a Character Database.</summary>
        private readonly struct CharacterSlot
        {
            public readonly Actor Actor;
            public readonly CharacterDatabase Source;
            public readonly int Index;

            public CharacterSlot(Actor actor, CharacterDatabase source, int index) { Actor = actor; Source = source; Index = index; }

            public bool IsShared => Source != null;
            public string ListPath => IsShared ? nameof(CharacterDatabase.characters) : nameof(DialogueDatabase.actors);
            public UnityEngine.Object Asset(DialogueDatabase db) => IsShared ? (UnityEngine.Object)Source : db;
        }

        private List<CharacterSlot> CharacterSlots()
        {
            var slots = new List<CharacterSlot>();
            for (int i = 0; i < m_Database.actors.Count; i++) slots.Add(new CharacterSlot(m_Database.actors[i], null, i));
            var visited = new HashSet<CharacterDatabase>();
            foreach (var source in m_Database.characterDatabases)
            {
                if (source == null || !visited.Add(source)) continue;
                for (int i = 0; i < source.characters.Count; i++) slots.Add(new CharacterSlot(source.characters[i], source, i));
            }
            return slots;
        }

        private void BuildCharacters()
        {
            BuildCast();

            var slots = CharacterSlots();
            int selected = slots.FindIndex(s => s.Actor != null && s.Actor.id == m_SelectedActorId);
            if (selected < 0 && slots.Count > 0) { selected = 0; m_SelectedActorId = slots[0].Actor?.id ?? 0; }

            // A horizontal row of selectable character cards, "+" at the end.
            var strip = new ScrollView(ScrollViewMode.Horizontal);
            strip.AddToClassList("dlg-charstrip");
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var card = CharacterCard(slot, i == selected);
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    m_SelectedActorId = slot.Actor?.id ?? 0;
                    Rebuild();
                });
                strip.Add(card);
            }
            var add = new VisualElement { tooltip = "Add a character here or to a Character Database (pick its kind: every Actor subclass is listed)" };
            add.AddToClassList("dlg-charcard");
            add.AddToClassList("dlg-charcard--add");
            add.Add(new Label("+") { pickingMode = PickingMode.Ignore });
            add.RegisterCallback<ClickEvent>(_ => ShowAddActorMenu());
            strip.Add(add);
            m_Content.Add(strip);

            if (slots.Count == 0)
            {
                m_Content.Add(new HelpBox("No characters yet. Every line is spoken by a character; add the player too (its lines become choices).", HelpBoxMessageType.Info));
                return;
            }
            var current = slots[selected];
            var actor = current.Actor;
            if (actor == null)
            {
                m_Content.Add(new HelpBox("This slot is empty (its type may have been deleted).", HelpBoxMessageType.Warning));
                m_Content.Add(new Button(() => RemoveActor(current)) { text = "Remove the empty slot" });
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("dlg-detail__header");
            header.Add(new Label(m_Database.DisplayNameOf(actor)) { name = "detail-title" });
            header.Add(Pill(ObjectNames.NicifyVariableName(actor.GetType().Name)));
            if (actor.isPlayer) header.Add(Pill("PLAYER", "dlg-pill--player"));
            header.Add(Pill(current.IsShared ? $"shared · {current.Source.name}" : "this database only", current.IsShared ? "dlg-pill--shared" : null));
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            header.Add(new Button(() => RemoveActor(current)) { text = "Remove", tooltip = current.IsShared ? $"Remove from '{current.Source.name}' (every dialogue database using it loses this character)" : "Remove this character" });
            m_Content.Add(header);
            if (current.IsShared)
                m_Content.Add(new HelpBox($"Shared character from '{current.Source.name}' (id {actor.id}): edits below change it in every dialogue database that uses it. " +
                                          "How it looks in THIS database is set under \"Look in this database\".", HelpBoxMessageType.None));
            if (!string.IsNullOrEmpty(actor.EditorNote)) m_Content.Add(new HelpBox(actor.EditorNote, HelpBoxMessageType.Info));

            var so = current.IsShared ? new SerializedObject(current.Source) : m_Serialized;
            var prop = so.FindProperty($"{current.ListPath}.Array.data[{current.Index}]");
            CharacterSections.BuildDetails(m_Content, actor, so, prop, value => EditCharacter(current, "Change Default Emotion", a => a.defaultEmotion = value),
                () => { DatabaseChanged?.Invoke(); Rebuild(); });
            m_Content.Add(LookGroup(actor));
            if (current.IsShared) TrackOther(so);
        }

        /// <summary>The Character Databases this database uses, and ways to make / fill one.</summary>
        private void BuildCast()
        {
            var cast = Group("Cast", "Character Databases hold characters shared by many dialogue databases (same id everywhere). " +
                                     "A Character Database can also be a template: characters that pick it get its attributes (they cannot remove them, but can give each its own value).");
            cast.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.characterDatabases))));
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(new Button(CreateCharacterDatabase) { text = "+ New Character Database", tooltip = "Create one next to this database and use it" });
            var local = m_Database.actors.Where(a => a != null).ToList();
            var targets = m_Database.characterDatabases.Where(d => d != null).Distinct().ToList();
            if (local.Count > 0 && targets.Count > 0)
            {
                var move = new Button { text = $"Move this database's {local.Count} character(s) to… ▾", tooltip = "Make them shared. Their ids stay the same, so every line keeps its speaker." };
                move.clicked += () =>
                {
                    var menu = new GenericMenu();
                    foreach (var target in targets)
                    {
                        var t = target;
                        menu.AddItem(new GUIContent(t.name), false, () => MoveLocalCharacters(t));
                    }
                    menu.ShowAsContext();
                };
                row.Add(move);
            }
            cast.Add(row);
            foreach (var source in targets)
            {
                var template = new Foldout { text = $"Template attributes of '{source.name}' ({source.templateAttributes.Count})", value = false };
                var so = new SerializedObject(source);
                var list = new PropertyField(so.FindProperty(nameof(CharacterDatabase.templateAttributes)), string.Empty);
                list.Bind(so);
                template.Add(list);
                cast.Add(template);
                TrackOther(so);
            }
            m_Content.Add(cast);
        }

        private VisualElement CharacterCard(CharacterSlot slot, bool selected)
        {
            var actor = slot.Actor;
            if (actor == null) return CharacterSections.Card(null, null, null, null, null, selected, slot.IsShared);
            var looked = m_Database.FindLook(actor.id)?.FindEmotion(actor.defaultEmotion);
            var sprite = looked != null ? DialogueNodeView.EditorPortrait(looked, Args().Locale) : CharacterSections.DefaultPortrait(actor, Args().Locale);
            string kind = CharacterSections.Kind(actor);
            return CharacterSections.Card(actor, sprite, m_Database.DisplayNameOf(actor), slot.IsShared ? $"{kind} · {slot.Source.name}" : kind,
                $"{actor.name}  ·  #{actor.id}  ·  {(slot.IsShared ? slot.Source.name : "this database")}", selected, slot.IsShared);
        }


        /// <summary>
        /// "Look in this database": for each emotion of the character, an override portrait / tint used only by this
        /// dialogue database (other ages, outfits…). No override = the character's own.
        /// </summary>
        private VisualElement LookGroup(Actor actor)
        {
            var group = Group("Look in this database", "Change how the character looks in this dialogue database only (e.g. older, other clothes). " +
                                                      "Emotions without an override use the character's own portrait.");
            var look = m_Database.FindLook(actor.id);
            var nameField = new TextField("Display name here") { value = look != null && look.displayName != null ? look.displayName.Text ?? string.Empty : string.Empty, isDelayed = true,
                tooltip = $"Empty = '{actor.ResolveDisplayName()}'." };
            nameField.RegisterValueChangedCallback(evt => EditLook(actor, "Change Display Name Here", l => l.displayName.Text = evt.newValue));
            group.Add(nameField);

            foreach (var emotion in actor.emotions.Where(e => e != null && !string.IsNullOrEmpty(e.id)))
            {
                var id = emotion.id;
                var overridden = look?.FindEmotion(id);
                var row = new VisualElement();
                row.AddToClassList("dlg-lookrow");
                row.Add(Thumbnail(emotion.portrait));
                row.Add(new Label($"{DialogueEmotionGlyph.For(id)}  {id}") { style = { width = 130, unityTextAlign = TextAnchor.MiddleLeft } });
                row.Add(new Label("→") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });
                var portrait = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false, value = overridden?.portrait, style = { flexGrow = 1 },
                    tooltip = "Portrait in this database (empty = the character's)" };
                portrait.RegisterValueChangedCallback(evt => EditLook(actor, "Change Look Portrait", l => LookEmotion(l, id, emotion).portrait = evt.newValue as Sprite));
                row.Add(portrait);
                var tint = new ColorField { value = overridden?.tint ?? emotion.tint, showAlpha = false, style = { width = 60 } };
                tint.RegisterValueChangedCallback(evt => EditLook(actor, "Change Look Tint", l => LookEmotion(l, id, emotion).tint = evt.newValue));
                row.Add(tint);
                var reset = new Button(() => EditLook(actor, "Reset Look", l => l.emotions.RemoveAll(e => e != null && e.id == id))) { text = "✕", tooltip = "Use the character's own look" };
                reset.SetEnabled(overridden != null);
                reset.AddToClassList("dlg-inspector__remove");
                row.Add(reset);
                group.Add(row);
            }
            return group;
        }

        private static VisualElement Thumbnail(Sprite sprite)
        {
            var thumb = new VisualElement { style = { width = 28, height = 28, marginRight = 6, backgroundColor = new Color(0f, 0f, 0f, 0.25f) } };
            if (sprite != null) thumb.style.backgroundImage = new StyleBackground(sprite);
            thumb.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            return thumb;
        }

        /// <summary>The look's entry for an emotion, created (copying the character's) on first edit.</summary>
        private static ActorEmotion LookEmotion(ActorLook look, string id, ActorEmotion original)
        {
            var e = look.FindEmotion(id);
            if (e != null) return e;
            e = new ActorEmotion { id = id, portrait = original.portrait, tint = original.tint };
            look.emotions.Add(e);
            return e;
        }

        private void EditLook(Actor actor, string undoLabel, Action<ActorLook> change)
        {
            Edit(undoLabel, () =>
            {
                var look = m_Database.FindLook(actor.id);
                if (look == null) m_Database.looks.Add(look = new ActorLook { actorId = actor.id });
                change(look);
                if (look.emotions.Count == 0 && (look.displayName == null || !look.displayName.HasContent)) m_Database.looks.Remove(look); // nothing overridden
            });
        }

        /// <summary>Changes a character where it is stored (this database, or its Character Database), as one undo step.</summary>
        private void EditCharacter(CharacterSlot slot, string undoLabel, Action<Actor> change)
        {
            if (!slot.IsShared)
            {
                Edit(undoLabel, () => change(slot.Actor));
                return;
            }
            Undo.RecordObject(slot.Source, undoLabel);
            change(slot.Actor);
            EditorUtility.SetDirty(slot.Source);
            DatabaseChanged?.Invoke();
            Rebuild();
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
            var destinations = new List<(string label, CharacterDatabase target)> { ("This database", null) };
            destinations.AddRange(m_Database.characterDatabases.Where(d => d != null).Distinct().Select(d => ($"{d.name} (shared)", d)));
            foreach (var (label, target) in destinations)
            {
                var t0 = target;
                string prefix = destinations.Count > 1 ? label + "/" : string.Empty;
                menu.AddItem(new GUIContent(prefix + "Player"), false, () => AddActor(typeof(Actor), true, t0));
                foreach (var type in ActorTypes())
                {
                    var t = type;
                    string name = t == typeof(Actor) ? "Character (base)" : ObjectNames.NicifyVariableName(t.Name);
                    menu.AddItem(new GUIContent(prefix + name), false, () => AddActor(t, false, t0));
                }
            }
            menu.ShowAsContext();
        }

        /// <summary>Adds a character of <paramref name="type"/> with a unique name and one emotion (its default), here or to <paramref name="target"/>.</summary>
        public Actor AddActor(Type type, bool player, CharacterDatabase target = null)
        {
            var names = m_Database.AllActors.Where(a => a != null).Select(a => a.name).ToArray();
            string baseName = player ? "Player" : type == typeof(Actor) ? "Character" : type.Name;
            string actorName = ObjectNames.GetUniqueName(names, baseName).Replace(Actor.AttributeSeparator, '_');
            var actor = (Actor)Activator.CreateInstance(type);
            actor.name = actorName;
            actor.isPlayer = player;
            actor.emotions.Add(new ActorEmotion { id = actor.defaultEmotion });
            Actor added = null;
            if (target == null) Edit("Add Character", () => added = m_Database.AddActor(actor));
            else
            {
                Undo.RecordObject(target, "Add Character");
                added = target.AddCharacter(actor);
                EditorUtility.SetDirty(target);
                DatabaseChanged?.Invoke();
            }
            if (added != null) m_SelectedActorId = added.id;
            Rebuild();
            return added;
        }

        private void RemoveActor(CharacterSlot slot)
        {
            var actor = slot.Actor;
            int uses = actor == null ? 0 : m_Database.conversations.Where(c => c != null).SelectMany(c => c.entries).Count(e => e != null && e.id != Conversation.StartEntryId && e.actorId == actor.id);
            string where = slot.IsShared ? $" from '{slot.Source.name}' — every dialogue database using it loses this character" : string.Empty;
            if ((uses > 0 || slot.IsShared) && !EditorUtility.DisplayDialog("Remove character",
                    $"Remove '{actor?.name}'{where}? It speaks {uses} line(s) here; they will report an unknown speaker until you reassign them.", "Remove", "Cancel"))
                return;
            if (!slot.IsShared) { Edit("Remove Character", () => m_Database.actors.RemoveAt(slot.Index)); return; }
            Undo.RecordObject(slot.Source, "Remove Character");
            slot.Source.characters.RemoveAt(slot.Index);
            EditorUtility.SetDirty(slot.Source);
            DatabaseChanged?.Invoke();
            Rebuild();
        }

        private void CreateCharacterDatabase()
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_Database))?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError("[Dialogue Editor] Save the dialogue database as an asset first.");
                return;
            }
            var created = ScriptableObject.CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{m_Database.name}_Characters.asset"));
            Edit("Use Character Database", () => m_Database.characterDatabases.Add(created));
        }

        /// <summary>Moves this database's own characters into <paramref name="target"/>, keeping their ids (lines keep their speakers).</summary>
        public int MoveLocalCharacters(CharacterDatabase target)
        {
            var movable = m_Database.actors.Where(a => a != null && target.FindCharacter(a.id) == null && target.FindCharacter(a.name) == null).ToList();
            int skipped = m_Database.actors.Count(a => a != null) - movable.Count;
            if (skipped > 0) Debug.LogWarning($"[Dialogue Editor] {skipped} character(s) of '{m_Database.name}' were not moved: '{target.name}' already has their id or name.", target);
            Undo.RecordObjects(new UnityEngine.Object[] { m_Database, target }, "Move Characters To Character Database");
            foreach (var actor in movable)
            {
                if (!target.AdoptCharacter(actor)) continue;
                m_Database.actors.Remove(actor);
            }
            EditorUtility.SetDirty(m_Database);
            EditorUtility.SetDirty(target);
            DatabaseChanged?.Invoke();
            Rebuild();
            return movable.Count;
        }

        #endregion

        #region Variables / quests / events

        private void BuildVariables()
        {
            Intro("World state that conditions read and actions change. Each colour is a type (the same colours as behavior-tree keys).");
            var source = m_Database.schemaSource;
            var world = Group("World");
            if (source == null) world.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.variables))));
            else
            {
                var editor = DialoguePluginRegistry.SchemaEditorFor(source);
                if (editor?.Variables != null) world.Add(DialoguePluginRegistry.BuildSchemaEditor(editor, editor.Variables, Args()));
                else world.Add(ReadOnlySchema(source, m_Database.Schema.VariableNames.Where(n => !m_Database.TryResolveAttribute(n, out _, out _)), "variables"));
            }
            m_Content.Add(world);

            var characters = Group("Character attributes", "Edited on each character (Characters section).");
            bool any = false;
            foreach (var actor in m_Database.AllActors.Where(a => a != null))
            {
                var attributes = actor.AttributeDefinitions.Where(a => a != null && !string.IsNullOrEmpty(a.name)).ToList();
                if (attributes.Count == 0) continue;
                any = true;
                var row = new VisualElement();
                row.AddToClassList("dlg-attrgroup");
                var head = new VisualElement();
                head.AddToClassList("dlg-attrgroup__head");
                head.Add(new Label(actor.name) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
                int actorId = actor.id;
                head.Add(new Button(() =>
                {
                    m_SelectedActorId = actorId;
                    TabRequested?.Invoke(CharactersTab);
                }) { text = "Edit →" });
                row.Add(head);
                foreach (var attribute in attributes)
                    row.Add(VariableRow(actor.AttributeVariable(attribute.name), attribute.initialValue.type.ToString(),
                        DialogueVariableStyle.Of(m_Database, actor.AttributeVariable(attribute.name))));
                characters.Add(row);
            }
            if (!any) characters.Add(new Label("(no character has attributes)") { style = { color = new Color(0.6f, 0.6f, 0.65f) } });
            m_Content.Add(characters);
        }

        /// <summary>Where a quest card's data lives: this dialogue database's own list, or a Quest Database asset.</summary>
        private sealed class QuestList
        {
            public UnityEngine.Object Owner;
            public SerializedObject Serialized;
            public List<QuestDefinition> Quests;
            public string Path;
        }

        private QuestList OwnQuests() => new QuestList { Owner = m_Database, Serialized = m_Serialized, Quests = m_Database.quests, Path = nameof(DialogueDatabase.quests) };

        private static QuestList QuestsOf(QuestDatabase database) =>
            new QuestList { Owner = database, Serialized = new SerializedObject(database), Quests = database.quests, Path = nameof(QuestDatabase.quests) };

        private void EditQuests(QuestList list, string undoLabel, Action change)
        {
            Undo.RecordObject(list.Owner, undoLabel);
            change();
            EditorUtility.SetDirty(list.Owner);
            DatabaseChanged?.Invoke();
            Rebuild();
        }

        private void BuildQuests()
        {
            Intro("Quests have a state (Inactive, Active, Completed, Failed, Abandoned) that lines can check and change, and " +
                  "optionally a goal (a counter gameplay reports progress to). They live in Quest Databases, which a scene's " +
                  "Quest Manager shares with every other system; this database lists the ones its lines use.");

            var databases = Group("Quest Databases", "The quests this database's lines may name. Shared with gameplay and UI through a Quest Manager.");
            databases.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.questDatabases))));
            m_Content.Add(databases);
            foreach (var questDatabase in m_Database.questDatabases.Where(d => d != null).Distinct())
            {
                var list = QuestsOf(questDatabase);
                var group = Group(questDatabase.name, AssetDatabase.GetAssetPath(questDatabase));
                for (int i = 0; i < list.Quests.Count; i++) group.Add(QuestCard(list, i, removable: true, renamable: true, showInitialState: true));
                group.Add(AddQuestButton(list));
                m_Content.Add(group);
            }

            var own = OwnQuests();
            var source = m_Database.schemaSource;
            if (source != null)
            {
                var world = Group("Quests in the world", $"They live in '{source.name}'.");
                var editor = DialoguePluginRegistry.SchemaEditorFor(source);
                if (editor?.Quests != null) world.Add(DialoguePluginRegistry.BuildSchemaEditor(editor, editor.Quests, Args()));
                else world.Add(ReadOnlySchema(source, m_Database.Schema.QuestNames, "quests"));
                m_Content.Add(world);

                var journal = Group("Journal", "What players read about each quest of the world (matched by name).");
                foreach (var name in m_Database.Schema.QuestNames.Distinct().OrderBy(n => n))
                {
                    int index = own.Quests.FindIndex(q => q != null && q.name == name);
                    if (index >= 0) { journal.Add(QuestCard(own, index, removable: true, renamable: false, showInitialState: false)); continue; }
                    if (m_Database.FindQuest(name) != null) continue; // its journal is in a Quest Database above
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    row.Add(VariableRow(name, "no journal yet", DialogueVariableStyle.QuestColor));
                    string questName = name;
                    row.Add(new Button(() => EditQuests(own, "Add Quest Journal", () => own.Quests.Add(new QuestDefinition { name = questName })))
                        { text = "+ Journal", tooltip = "Give this quest a title, summary and steps for the quest log" });
                    journal.Add(row);
                }
                foreach (var orphan in own.Quests.Where(q => q != null && !m_Database.Schema.HasQuest(q.name)).ToList())
                    journal.Add(new HelpBox($"Journal '{orphan.name}' matches no quest of the world.", HelpBoxMessageType.Warning));
                m_Content.Add(journal);
            }
            else if (own.Quests.Count > 0)
            {
                var inline = Group("Quests in this database", "Older content: new quests belong in a Quest Database (shared with gameplay).");
                for (int i = 0; i < own.Quests.Count; i++) inline.Add(QuestCard(own, i, removable: true, renamable: true, showInitialState: true));
                inline.Add(AddQuestButton(own));
                m_Content.Add(inline);
            }
            if (source == null && m_Database.questDatabases.All(d => d == null) && own.Quests.Count == 0)
                m_Content.Add(new HelpBox("No quests yet: assign or create a Quest Database above (Assets › Create › ND Quests › Quest Database).", HelpBoxMessageType.Info));
        }

        private Button AddQuestButton(QuestList list)
        {
            var add = new Button(() => EditQuests(list, "Add Quest", () =>
            {
                var names = m_Database.AllQuests.Select(q => q.name).Concat(list.Quests.Where(q => q != null).Select(q => q.name)).ToArray();
                list.Quests.Add(new QuestDefinition { name = ObjectNames.GetUniqueName(names, "new_quest") });
            })) { text = "+ Add quest" };
            add.AddToClassList("dlg-inspector__add");
            return add;
        }

        /// <summary>One quest's card: id, state, group, tracking, goal, title, summary, note, and its steps with conditions.</summary>
        private VisualElement QuestCard(QuestList list, int index, bool removable, bool renamable, bool showInitialState)
        {
            var quest = list.Quests[index];
            var prop = list.Serialized.FindProperty($"{list.Path}.Array.data[{index}]");
            var card = new VisualElement();
            card.AddToClassList("dlg-questcard");
            if (quest == null || prop == null) { card.Add(new Label("(empty)")); return card; }

            var head = new VisualElement();
            head.AddToClassList("dlg-questcard__head");
            var dot = new VisualElement { style = { backgroundColor = DialogueVariableStyle.QuestColor } };
            dot.AddToClassList("dlg-varrow__dot");
            head.Add(dot);
            var id = new TextField { value = quest.name, isDelayed = true, tooltip = "Quest id (dialogue, gameplay and saves use it)" };
            id.AddToClassList("dlg-questcard__id");
            id.SetEnabled(renamable); // a world's quest is renamed in the world
            id.RegisterValueChangedCallback(evt => RenameQuest(list, index, evt.newValue));
            head.Add(id);
            if (showInitialState) head.Add(CompactField(list, prop.FindPropertyRelative(nameof(QuestDefinition.initialState)), "starts", 150));
            head.Add(CompactField(list, prop.FindPropertyRelative(nameof(QuestDefinition.group)), "group", 140));
            head.Add(CompactField(list, prop.FindPropertyRelative(nameof(QuestDefinition.trackWhenActive)), "track", 60));
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            if (removable)
            {
                var remove = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("Remove quest", $"Remove '{quest.name}' and its journal?", "Remove", "Cancel"))
                        EditQuests(list, "Remove Quest", () => list.Quests.RemoveAt(index));
                }) { text = "✕", tooltip = "Remove" };
                remove.AddToClassList("dlg-inspector__remove");
                head.Add(remove);
            }
            card.Add(head);

            card.Add(Bound(prop.FindPropertyRelative(nameof(QuestDefinition.title)), list.Serialized));
            card.Add(Bound(prop.FindPropertyRelative(nameof(QuestDefinition.summary)), list.Serialized));
            card.Add(Bound(prop.FindPropertyRelative(nameof(QuestDefinition.description)), list.Serialized));

            var goal = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            goal.Add(CompactField(list, prop.FindPropertyRelative(nameof(QuestDefinition.goal)), "goal", 120));
            if (quest.HasGoal)
            {
                goal.Add(CompactField(list, prop.FindPropertyRelative(nameof(QuestDefinition.completeAtGoal)), "complete at goal", 140));
                var progression = new Button(() =>
                {
                    var menu = new GenericMenu();
                    foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(IQuestProgression)))
                    {
                        var t = type;
                        menu.AddItem(new GUIContent(DialogueRowGUI.TypeLabel(t)), quest.progression?.GetType() == t, () =>
                            EditQuests(list, "Change Quest Progression", () => quest.progression = (IQuestProgression)Activator.CreateInstance(t)));
                    }
                    menu.ShowAsContext();
                }) { text = $"counts: {DialogueRowGUI.TypeLabel((quest.progression ?? new IncrementProgression()).GetType())} ▾", tooltip = "How a reported value changes the progress: add it (Increment) or keep the highest (Max Value)." };
                progression.AddToClassList("dlg-questcard__progression");
                goal.Add(progression);
            }
            else goal.Add(new Label("no counter (dialogue or code completes it)") { style = { color = new Color(0.6f, 0.62f, 0.68f), fontSize = 11, marginLeft = 6 } });
            card.Add(goal);

            var steps = new Label("Steps") { tooltip = "Listed under the quest in the log and tracker. Each shows and ticks itself by conditions." };
            steps.AddToClassList("dlg-questcard__steps");
            card.Add(steps);
            var stepsProp = prop.FindPropertyRelative(nameof(QuestDefinition.steps));
            for (int s = 0; s < quest.steps.Count; s++) card.Add(StepCard(list, index, s, stepsProp.GetArrayElementAtIndex(s)));
            var addStep = new Button(() => EditQuests(list, "Add Quest Step", () => list.Quests[index].steps.Add(new QuestStep()))) { text = "+ Add step" };
            addStep.AddToClassList("dlg-inspector__add");
            card.Add(addStep);
            return card;
        }

        private VisualElement StepCard(QuestList list, int questIndex, int stepIndex, SerializedProperty stepProp)
        {
            var box = new VisualElement();
            box.AddToClassList("dlg-line");
            var head = new VisualElement();
            head.AddToClassList("dlg-line__head");
            var number = new Label((stepIndex + 1).ToString());
            number.AddToClassList("dlg-line__number");
            head.Add(number);
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            var remove = new Button(() => EditQuests(list, "Remove Quest Step", () => list.Quests[questIndex].steps.RemoveAt(stepIndex))) { text = "✕", tooltip = "Remove this step" };
            remove.AddToClassList("dlg-line__button");
            head.Add(remove);
            box.Add(head);
            box.Add(Bound(stepProp.FindPropertyRelative(nameof(QuestStep.text)), list.Serialized));
            box.Add(ConditionList(list, stepProp.FindPropertyRelative(nameof(QuestStep.visibleWhen)), "Shown when (empty = always)"));
            box.Add(ConditionList(list, stepProp.FindPropertyRelative(nameof(QuestStep.doneWhen)), "Done when (empty = when the quest is Completed)"));
            return box;
        }

        /// <summary>
        /// A [SerializeReference] quest-condition list: one row per condition (its drawer), ✕, and "+ condition" with the
        /// dialogue conditions and the quest conditions (state, progress).
        /// </summary>
        private VisualElement ConditionList(QuestList owner, SerializedProperty list, string title)
        {
            var box = new VisualElement();
            box.AddToClassList("dlg-condlist");
            box.Add(new Label(title) { style = { color = new Color(0.63f, 0.66f, 0.72f), fontSize = 11, marginTop = 2 } });
            string path = list.propertyPath;
            for (int i = 0; i < list.arraySize; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                row.Add(Bound(list.GetArrayElementAtIndex(i), owner.Serialized));
                var remove = new Button(() =>
                {
                    Undo.RecordObject(owner.Owner, "Remove Condition");
                    var so = new SerializedObject(owner.Owner);
                    so.FindProperty(path).DeleteArrayElementAtIndex(index);
                    so.ApplyModifiedProperties();
                    DatabaseChanged?.Invoke();
                    Rebuild();
                }) { text = "✕", tooltip = "Remove" };
                remove.AddToClassList("dlg-inspector__remove");
                row.Add(remove);
                box.Add(row);
            }
            var add = new Button { text = "+ condition" };
            add.AddToClassList("dlg-condlist__add");
            add.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(DialogueCondition)))
                    AddItem(type, string.Empty);
                foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(IQuestCondition)).Where(t => !typeof(DialogueCondition).IsAssignableFrom(t)))
                    AddItem(type, "Quest/");
                menu.ShowAsContext();

                void AddItem(Type t, string folder) => menu.AddItem(new GUIContent(folder + DialogueRowGUI.TypeLabel(t)), false, () =>
                {
                    DialogueRowGUI.AppendManagedReference(owner.Owner, path, t);
                    DatabaseChanged?.Invoke();
                    Rebuild();
                });
            };
            box.Add(add);
            return box;
        }

        private static VisualElement CompactField(QuestList list, SerializedProperty property, string label, float width)
        {
            var field = new PropertyField(property, label) { style = { width = width, marginLeft = 6 } };
            field.AddToClassList("dlg-compact");
            field.Bind(list.Serialized);
            return field;
        }

        private void RenameQuest(QuestList list, int index, string newName)
        {
            newName = newName?.Trim();
            var quest = list.Quests[index];
            if (string.IsNullOrEmpty(newName) || newName == quest.name) return;
            if (m_Database.FindQuest(newName) != null || list.Quests.Any(q => q != null && q.name == newName))
            {
                Debug.LogWarning($"[Dialogue Editor] A quest named '{newName}' already exists.");
                Rebuild();
                return;
            }
            EditQuests(list, "Rename Quest", () => quest.name = newName);
        }

        /// <summary>Names from a source no plugin can edit here: listed with colours, plus a way to the source asset.</summary>
        private VisualElement ReadOnlySchema(DialogueSchemaSource source, IEnumerable<string> names, string kind)
        {
            var box = new VisualElement();
            box.Add(new HelpBox($"These {kind} come from '{source.name}', which this editor cannot change: edit that asset.", HelpBoxMessageType.Info));
            var found = names.Distinct().OrderBy(n => n).ToList();
            if (found.Count == 0) box.Add(new Label($"(no {kind})"));
            foreach (var n in found)
                box.Add(kind == "quests" ? VariableRow(n, "quest", DialogueVariableStyle.QuestColor) : VariableRow(n, DialogueVariableStyle.TypeLabel(m_Database, n), DialogueVariableStyle.Of(m_Database, n)));
            box.Add(new Button(() => Selection.activeObject = source) { text = "Select the source asset" });
            return box;
        }

        private void BuildEvents()
        {
            Intro("Gameplay events dialogue can fire (Raise Event action). Scenes react with a Dialogue Event Listener component. " +
                  "Once any event is declared, Validate reports actions that use undeclared names.");
            var group = Group("Events");
            group.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.events))));
            m_Content.Add(group);
        }

        #endregion

        #region Localization / settings

        private void BuildLocalization()
        {
            Intro("Lines start as plain text; connect them to the Text Table to translate them. Edit the table here, or " +
                  "keep it in a Google Sheet and pull / push it (Google Sheets below).");
            var table = m_Database.textTable;
            var group = Group("Text table");
            group.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.textTable))));
            if (table == null)
            {
                group.Add(new Button(CreateTextTable) { text = "Create Text Table next to the database" });
                m_Content.Add(group);
                return;
            }

            string locale = Args().Locale;
            int plain = DialogueTextEditing.CountPlain(m_Database);
            var localizeAll = new Button(() =>
            {
                int done = DialogueTextEditing.LocalizeAll(m_Database, locale);
                Debug.Log($"[Dialogue Editor] Connected {done} plain text(s) of '{m_Database.name}' to '{table.name}' as '{locale}'.", m_Database);
                DatabaseChanged?.Invoke();
                Rebuild();
            }) { text = $"Localize all plain lines → Text Table ({plain})", tooltip = $"Moves every plain line/choice label into the table as the '{locale}' text and connects it." };
            localizeAll.SetEnabled(plain > 0 && !string.IsNullOrEmpty(locale));
            group.Add(localizeAll);
            m_Content.Add(group);

            var editor = Group("Translations");
            editor.Add(new InspectorElement(table));
            m_Content.Add(editor);
        }

        private void BuildSettings()
        {
            var text = Group("Text");
            text.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.textTable))));
            if (m_Database.textTable == null) text.Add(new Button(CreateTextTable) { text = "Create Text Table next to the database" });
            m_Content.Add(text);

            var world = Group("World", "Where variables and quests live: the database itself (empty), or a source such as a behavior-tree blackboard.");
            world.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.schemaSource))));
            m_Content.Add(world);

            var runner = Group("Runner");
            runner.Add(Bound(m_Serialized.FindProperty(nameof(DialogueDatabase.runnerOptions))));
            m_Content.Add(runner);

            var plugins = Group("Plugins");
            if (DialoguePluginRegistry.Entries.Count == 0) plugins.Add(new Label("(none installed)"));
            foreach (var entry in DialoguePluginRegistry.Entries)
            {
                var label = new Label($"{entry.Plugin.DisplayName}  {entry.Plugin.Version}{(entry.Installed ? string.Empty : "  — FAILED: " + entry.Error)}");
                label.tooltip = entry.Plugin.Description;
                plugins.Add(label);
            }
            m_Content.Add(plugins);

            var project = Group("Project");
            project.Add(new Button(() => SettingsService.OpenProjectSettings(DialogueProjectSettings.SettingsPath)) { text = "ND Dialogue project settings →" });
            m_Content.Add(project);
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
            m_Content.Add(DialoguePluginRegistry.BuildPanel(panel, Args()));
        }

        #endregion

        #region Building blocks

        private VisualElement Bound(SerializedProperty property) => CharacterSections.Bound(property, m_Serialized);

        private static VisualElement Bound(SerializedProperty property, SerializedObject owner) => CharacterSections.Bound(property, owner);

        private void Intro(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dlg-panel__intro");
            m_Content.Add(label);
        }

        private static VisualElement Group(string title, string hint = null) => CharacterSections.Group(title, hint);

        /// <summary><c>● name ........ Type</c></summary>
        public static VisualElement VariableRow(string name, string typeLabel, Color color) => CharacterSections.VariableRow(name, typeLabel, color);

        private static Label Pill(string text, string extraClass = null) => CharacterSections.Pill(text, extraClass);

        private static VisualElement Card(bool selected)
        {
            var card = new VisualElement();
            card.AddToClassList("dlg-card");
            card.EnableInClassList("dlg-card--selected", selected);
            return card;
        }

        private static VisualElement AddCard(string text)
        {
            var card = new VisualElement();
            card.AddToClassList("dlg-card");
            card.AddToClassList("dlg-card--add");
            card.Add(new Label("+  " + text) { pickingMode = PickingMode.Ignore });
            return card;
        }

        private static Label CardTitle(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("dlg-card__title");
            return label;
        }

        private static Label CardSub(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("dlg-card__sub");
            return label;
        }

        #endregion
    }
}
