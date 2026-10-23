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
    public partial class DialogueEntryInspector : VisualElement
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
            string kind = m_Entry.GetType() == typeof(DialogueEntry) ? string.Empty : $"  ·  {ObjectNames.NicifyVariableName(m_Entry.GetType().Name)}";
            var header = new Label(isStart ? "START" : $"Entry #{m_Entry.id}{kind}");
            header.AddToClassList("dlg-inspector__header");
            Add(header);
            if (!string.IsNullOrEmpty(m_Entry.EditorNote)) Add(new HelpBox(m_Entry.EditorNote, HelpBoxMessageType.Info));

            if (isStart && m_Conversation.isBark)
            {
                BuildBarkLines();
                BuildPluginSections();
                return;
            }
            if (isStart)
            {
                Add(new HelpBox("Where the conversation begins. Link it to the first line(s); conditions on those lines pick which one plays.", HelpBoxMessageType.None));
            }
            else
            {
                if (m_Conversation.isBark) Add(new HelpBox("A bark line. When it is said (its conditions, priority) is set on START: select START to see every bark line in one table.", HelpBoxMessageType.Info));
                BuildIdentity();
                if (!m_Entry.isPassthrough) BuildText();
                BuildSubclassFields(entryProp);
                BuildList("Plays only if…", "Entry conditions: every way into this line needs them (all must hold). A condition for one branch only goes on that link (click its edge).", entryProp?.FindPropertyRelative(nameof(DialogueEntry.conditions)), typeof(DialogueCondition), m_Entry.conditions);
                BuildList("When it plays, do…", "Actions run when the line starts.", entryProp?.FindPropertyRelative(nameof(DialogueEntry.actions)), typeof(DialogueAction), m_Entry.actions);
                if (ShowSequence && !m_Entry.IsHub) BuildSequence();
            }
            BuildLinks();
            BuildPluginSections();
        }

        /// <summary>
        /// Sequence editing is hidden for now (2026-10-23, to be reworked). Data and runtime are untouched: sequences already
        /// on entries still play and still show in the test chat. Set to true to edit them again.
        /// </summary>
        private const bool ShowSequence = false;

        /// <summary>The entry's sequence (camera, animation, sound…) with its parse errors under it.</summary>
        private void BuildSequence()
        {
            var foldout = new Foldout { text = "Sequence (camera, animation, sound…)", value = !string.IsNullOrWhiteSpace(m_Entry.sequence) };
            var field = new TextField { value = m_Entry.sequence ?? string.Empty, multiline = true, isDelayed = true,
                tooltip = "Name(args)@seconds, separated by ; or new lines. Built-in: Wait(s), Anim(target, trigger or state), Audio(target), SetActive(target, true|false), Camera(target), LookAt(target, at), SendMessage(target, method, arg?), Continue(). Targets: speaker, a Sequencer target, or a character (Dialogue Actor Object)." };
            field.style.minHeight = 40;
            var errors = new Label { name = "sequence-errors" };
            errors.AddToClassList("dlg-inspector__error");
            void ShowErrors(string text)
            {
                var list = new List<string>();
                DialogueSequence.Parse(text, list);
                errors.text = string.Join("\n", list);
                errors.style.display = list.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ShowErrors(m_Entry.sequence);
            field.RegisterValueChangedCallback(evt =>
            {
                Edit("Edit Sequence", () => m_Entry.sequence = evt.newValue);
                ShowErrors(evt.newValue);
            });
            foldout.Add(field);
            foldout.Add(errors);
            foldout.Add(new Label("e.g.  Anim(speaker, Blush); Camera(CloseUp)@0.5; Wait(2); Continue()@2") { style = { fontSize = 10, color = new Color(0.6f, 0.62f, 0.68f) } });
            Add(foldout);
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
            return m_SerializedDatabase.FindProperty($"{nameof(DialogueDatabase.conversations)}.Array.data[{c}].{Conversation.EntriesField}.Array.data[{e}]");
        }

        #region Identity

        private void BuildIdentity()
        {
            var title = new TextField("Title") { value = m_Entry.title ?? string.Empty, isDelayed = true, tooltip = "Editor label only." };
            title.RegisterValueChangedCallback(evt => Edit("Rename Dialogue Entry", () => m_Entry.title = evt.newValue));
            Add(title);

            var actors = m_Database.AllActors.Where(a => a != null).ToList();
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
                // Emotions are chosen per line (Lines section).
            }

            var routing = new Toggle("Passthrough (no line, only routes)") { value = m_Entry.isPassthrough,
                tooltip = "Shows nothing. When the conversation reaches it, it runs its actions, then immediately follows its links (by their conditions). Use it as a hub / branching point." };
            routing.RegisterValueChangedCallback(evt =>
            {
                Edit("Toggle Passthrough", () => m_Entry.isPassthrough = evt.newValue);
                Rebuild();
            });
            Add(routing);

            var hub = new Toggle("Hub (merges links)") { value = m_Entry.isHub,
                tooltip = "Never entered: a link into the hub counts as a link to each of its targets (its links' conditions and priorities apply). Use it when many lines lead to the same replies — N × M edges become N + M. Turns Passthrough on." };
            hub.RegisterValueChangedCallback(evt =>
            {
                Edit("Toggle Hub", () =>
                {
                    m_Entry.isHub = evt.newValue;
                    if (evt.newValue) m_Entry.isPassthrough = true;
                });
                Rebuild();
            });
            Add(hub);
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

        /// <summary>Lines (each with its emotion), then the choice label for player responses.</summary>
        private void BuildText()
        {
            int count = m_Entry.LineCount;
            AddSection(count > 1 ? $"Lines  ({count})  [{m_Locale}]" : $"Line  [{m_Locale}]",
                "What the speaker says here, one line after another: Continue (button, Space, Enter) shows the next one. Each line can have its own emotion.");
            for (int i = 0; i < count; i++) Add(LineBlock(i, count));

            var add = new Button(() =>
            {
                Edit("Add Dialogue Line", () => m_Entry.moreLines.Add(new DialoguePage()));
                Rebuild();
            }) { text = "+ Add line", tooltip = "The same speaker says one more line here (no need for another node)." };
            add.AddToClassList("dlg-inspector__add");
            Add(add);

            var actor = m_Database.FindActor(m_Entry.actorId);
            if (actor != null && actor.isPlayer)
            {
                AddSection("Choice label", "Shown in the choice menu. Empty = the first line.");
                Add(TextArea(m_Entry.menuText, 22, value => DialogueTextEditing.Write(m_Database, m_Conversation, m_Entry, true, m_Locale, value)));
                Add(LocalizeRow(m_Entry.menuText, () => DialogueTextEditing.Localize(m_Database, m_Conversation, m_Entry, true, m_Locale), () => m_Entry.menuText));
            }
        }

        /// <summary>One line: number · emotion · (move/remove) / text / localize row.</summary>
        private VisualElement LineBlock(int index, int count)
        {
            var block = new VisualElement();
            block.AddToClassList("dlg-line");
            var head = new VisualElement();
            head.AddToClassList("dlg-line__head");
            var number = new Label((index + 1).ToString());
            number.AddToClassList("dlg-line__number");
            head.Add(number);
            head.Add(EmotionField(index));
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            if (index > 0)
            {
                int page = index - 1;
                var up = new Button(() => MoveLine(page, -1)) { text = "▲", tooltip = "Move up" };
                up.SetEnabled(index > 1);
                var down = new Button(() => MoveLine(page, +1)) { text = "▼", tooltip = "Move down" };
                down.SetEnabled(index < count - 1);
                var remove = new Button(() =>
                {
                    Edit("Remove Dialogue Line", () => m_Entry.moreLines.RemoveAt(page));
                    Rebuild();
                }) { text = "✕", tooltip = "Remove this line" };
                foreach (var b in new[] { up, down, remove }) b.AddToClassList("dlg-line__button");
                head.Add(up);
                head.Add(down);
                head.Add(remove);
            }
            block.Add(head);

            var text = m_Entry.LineText(index);
            var area = TextArea(text, 40, value => DialogueTextEditing.WriteLine(m_Database, m_Entry, index, m_Locale, value));
            area.tooltip = "Markup: <pause 0.5>  <speed 15>…</speed>  <wave>…</wave>  <shake>…</shake>  <err id=\"a\">…</err><fix id=\"a\" text=\"…\">  <br posX=\"40\">  <event name=\"door\" arg=\"open\">  — other tags (<b>, <color=…>) are TextMeshPro rich text.";
            block.Add(area);
            var markupErrors = new Label { name = "markup-errors" };
            markupErrors.AddToClassList("dlg-inspector__error");
            void ShowMarkupErrors(string value)
            {
                var list = new List<string>();
                if (!string.IsNullOrEmpty(value) && value.IndexOf('<') >= 0) LineMarkup.Parse(value, list);
                markupErrors.text = string.Join("\n", list);
                markupErrors.style.display = list.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ShowMarkupErrors(area.value);
            area.RegisterValueChangedCallback(evt => ShowMarkupErrors(evt.newValue));
            block.Add(markupErrors);
            block.Add(LocalizeRow(text, () => DialogueTextEditing.LocalizeLine(m_Database, m_Conversation, m_Entry, index, m_Locale), () => m_Entry.LineText(index)));
            block.Add(VoiceFoldout(index));
            return block;
        }

        /// <summary>The line's voice clip, and its per-language clip (an Asset Table entry, which wins when set).</summary>
        private VisualElement VoiceFoldout(int index)
        {
            var page = index == 0 ? null : m_Entry.moreLines[index - 1];
            var clip = index == 0 ? m_Entry.voice : page?.voice;
            var localized = index == 0 ? m_Entry.localizedVoice : page?.localizedVoice;
            var cue = m_Entry.LineVoiceCue(index);
            bool hasLocalized = localized != null && !localized.IsEmpty;
            string summary = cue != null ? cue.Describe() : hasLocalized ? "per language" : clip != null ? clip.name : null;
            var foldout = new Foldout { text = summary != null ? $"🔊 Voice: {summary}" : "🔊 Voice", value = false };
            foldout.AddToClassList("dlg-line__voice");
            var field = new ObjectField("Clip") { objectType = typeof(AudioClip), allowSceneObjects = false, value = clip,
                tooltip = "Played with the line by the TextMeshPro dialogue UI; the typing can last as long as the clip." };
            field.RegisterValueChangedCallback(evt => Edit("Change Line Voice", () =>
            {
                var target = index == 0 ? null : m_Entry.moreLines[index - 1];
                if (index == 0) m_Entry.voice = evt.newValue as AudioClip;
                else if (target != null) target.voice = evt.newValue as AudioClip;
            }));
            foldout.Add(field);
            var entryProp = FindEntryProperty();
            var localizedProp = index == 0
                ? entryProp?.FindPropertyRelative(nameof(DialogueEntry.localizedVoice))
                : entryProp?.FindPropertyRelative($"{nameof(DialogueEntry.moreLines)}.Array.data[{index - 1}].{nameof(DialoguePage.localizedVoice)}");
            if (localizedProp != null)
            {
                var localizedField = new PropertyField(localizedProp, "Per language");
                localizedField.Bind(m_SerializedDatabase);
                localizedField.RegisterValueChangeCallback(_ => EntryChanged?.Invoke(m_Entry));
                foldout.Add(localizedField);
            }
            var cueProp = index == 0
                ? entryProp?.FindPropertyRelative(nameof(DialogueEntry.voiceCue))
                : entryProp?.FindPropertyRelative($"{nameof(DialogueEntry.moreLines)}.Array.data[{index - 1}].{nameof(DialoguePage.voiceCue)}");
            if (cueProp != null) AddVoiceCueField(foldout, cueProp, cue);
            return foldout;
        }

        /// <summary>
        /// The line's middleware voice (FMOD event…): a picker of every <see cref="DialogueVoiceCue"/> type compiled in
        /// (none while no integration is on, so nothing shows), then its fields (e.g. FMOD's own event browser).
        /// </summary>
        private void AddVoiceCueField(VisualElement parent, SerializedProperty cueProp, DialogueVoiceCue cue)
        {
            var types = TypeCache.GetTypesDerivedFrom<DialogueVoiceCue>().Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null).ToList();
            if (types.Count == 0 && cue == null) return;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label("Middleware") { style = { minWidth = 120 } });
            var choose = new Button { text = cue != null ? ObjectNames.NicifyVariableName(cue.GetType().Name) + " ▾" : "None ▾",
                tooltip = "Voice from an audio middleware (e.g. an FMOD event). It wins over the clip when the dialogue UI has a matching voice player." };
            choose.clicked += () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("None"), cue == null, () => SetVoiceCue(cueProp, null));
                foreach (var type in types)
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), cue != null && cue.GetType() == type,
                        () => SetVoiceCue(cueProp, (DialogueVoiceCue)Activator.CreateInstance(type)));
                menu.ShowAsContext();
            };
            row.Add(choose);
            parent.Add(row);
            if (cue == null) return;
            // IMGUI on purpose: a UI Toolkit PropertyField draws a [SerializeReference] as nested foldouts and skips the
            // middleware's own IMGUI drawers (FMOD's event picker). Each child is drawn by its drawer, sized by IMGUI.
            string path = cueProp.propertyPath;
            var fields = new IMGUIContainer(() =>
            {
                if (m_SerializedDatabase == null || m_SerializedDatabase.targetObject == null) return;
                m_SerializedDatabase.Update();
                var cueNow = m_SerializedDatabase.FindProperty(path);
                if (cueNow == null || cueNow.managedReferenceValue == null) return;
                var child = cueNow.Copy();
                var end = cueNow.GetEndProperty();
                for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                    EditorGUILayout.PropertyField(child, true);
                if (m_SerializedDatabase.ApplyModifiedProperties()) EntryChanged?.Invoke(m_Entry);
            });
            fields.AddToClassList("dlg-line__voice-cue");
            parent.Add(fields);
        }

        private void SetVoiceCue(SerializedProperty cueProp, DialogueVoiceCue cue)
        {
            if (!Resolve()) return;
            cueProp.serializedObject.Update();
            cueProp.managedReferenceValue = cue;
            cueProp.serializedObject.ApplyModifiedProperties(); // one undo step, through the database's SerializedObject
            Notify();
            Rebuild();
        }

        private void MoveLine(int page, int delta)
        {
            int other = page + delta;
            Edit("Reorder Dialogue Lines", () =>
            {
                if (other < 0 || other >= m_Entry.moreLines.Count) return;
                (m_Entry.moreLines[page], m_Entry.moreLines[other]) = (m_Entry.moreLines[other], m_Entry.moreLines[page]);
            });
            Rebuild();
        }

        /// <summary>Emotion popup of a line, each option with its emoji. Line 1 defaults to the actor's emotion; the others to line 1's.</summary>
        private VisualElement EmotionField(int index)
        {
            var actor = m_Database.FindActor(m_Entry.actorId);
            string defaultLabel = index == 0 ? "(actor default)" : "(same as line 1)";
            string current = (index == 0 ? m_Entry.emotion : m_Entry.moreLines[index - 1]?.emotion) ?? string.Empty;
            var ids = new List<string> { string.Empty };
            if (actor != null) ids.AddRange(actor.emotions.Where(e => e != null && !string.IsNullOrEmpty(e.id)).Select(e => e.id).Distinct());
            if (!ids.Contains(current)) ids.Add(current); // keep an unknown value visible
            string Label(string id) => string.IsNullOrEmpty(id) ? defaultLabel : $"{DialogueEmotionGlyph.For(id)}  {id}";
            var labels = ids.Select(Label).ToList();
            var field = new PopupField<string>(labels, Label(current)) { tooltip = "Emotion (portrait and tint) of the speaker for this line" };
            field.AddToClassList("dlg-line__emotion");
            field.RegisterValueChangedCallback(evt =>
            {
                string picked = ids[labels.IndexOf(evt.newValue)];
                Edit("Change Emotion", () =>
                {
                    if (index == 0) m_Entry.emotion = picked;
                    else if (m_Entry.moreLines[index - 1] != null) m_Entry.moreLines[index - 1].emotion = picked;
                });
            });
            return field;
        }

        private TextField TextArea(ND.Localization.LocalizableString text, float minHeight, Func<string, bool> write)
        {
            var field = new TextField { multiline = true, isDelayed = true, value = DialogueTextEditing.Read(text, m_Locale) ?? string.Empty };
            field.style.minHeight = minHeight;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterValueChangedCallback(evt =>
            {
                if (Resolve() && write(evt.newValue)) Notify();
            });
            return field;
        }

        /// <summary>Plain text: "Localize". Connected: its table/key and "Unlink".</summary>
        private VisualElement LocalizeRow(ND.Localization.LocalizableString text, Func<bool> localize, Func<ND.Localization.LocalizableString> fresh)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            if (text == null) return row;
            if (text.IsLocalized)
            {
                text.Table.TryGetKey(text.EntryId, out string key);
                row.Add(new Label($"table '{text.Table.DisplayName}' · key '{key}'") { style = { flexGrow = 1, color = new Color(0.6f, 0.65f, 0.7f), fontSize = 10 } });
                row.Add(new Button(() =>
                {
                    string current = DialogueTextEditing.Read(text, m_Locale) ?? string.Empty;
                    Edit("Unlink Dialogue Text", () => fresh()?.Unlink(current));
                    Rebuild();
                }) { text = "Unlink", tooltip = "Back to plain text (keeps this language's text; the table entry stays)." });
            }
            else
            {
                row.Add(new Label("plain text (same in every language)") { style = { flexGrow = 1, color = new Color(0.55f, 0.58f, 0.64f), fontSize = 10 } });
                if (!string.IsNullOrEmpty(text.Text))
                {
                    var button = new Button(() =>
                    {
                        if (Resolve() && localize()) Notify();
                        Rebuild();
                    }) { text = $"Localize ({m_Locale})", tooltip = "Move this text into the database's Text Table so it can be translated." };
                    button.SetEnabled(m_Database.textTable != null);
                    if (m_Database.textTable == null) button.tooltip = "Assign a Text Table in the Settings tab first.";
                    row.Add(button);
                }
            }
            return row;
        }

        #endregion

        #region Subclass fields

        /// <summary>Fields a <see cref="DialogueEntry"/> subclass adds (e.g. ShopEntry.items), drawn as bound fields.</summary>
        private void BuildSubclassFields(SerializedProperty entryProp)
        {
            if (entryProp == null || m_Entry.GetType() == typeof(DialogueEntry)) return;
            var baseFields = new HashSet<string>(typeof(DialogueEntry).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Select(f => f.Name));
            var fields = new List<VisualElement>();
            var child = entryProp.Copy();
            var end = entryProp.GetEndProperty();
            for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                if (baseFields.Contains(child.name)) continue;
                var field = new PropertyField(child.Copy());
                field.Bind(m_SerializedDatabase);
                field.RegisterValueChangeCallback(_ => EntryChanged?.Invoke(m_Entry));
                fields.Add(field);
            }
            if (fields.Count == 0) return;
            AddSection($"{ObjectNames.NicifyVariableName(m_Entry.GetType().Name)} settings");
            foreach (var field in fields) Add(field);
        }

        #endregion

        #region Conditions / actions

        private void BuildList(string title, string hint, SerializedProperty listProp, Type baseType, System.Collections.IList list)
        {
            AddSection(title, hint);
            if (listProp == null) return;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                int index = i;
                var element = listProp.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                var field = new PropertyField(element, string.Empty);
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

            bool conditions = baseType == typeof(DialogueCondition);
            var add = new Button { text = conditions ? "+ Add condition" : "+ Add action" };
            add.AddToClassList("dlg-inspector__add");
            add.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var type in DialogueRowGUI.ConcreteTypes(baseType))
                {
                    var t = type;
                    menu.AddItem(new GUIContent(DialogueRowGUI.TypeLabel(t)), false, () =>
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
            AddSection("Next  (links)", m_Entry.links.Count > 1 ? LinkPriorityGUI.Explanation : "Where the conversation goes after this line.");
            if (m_Entry.links.Count == 0)
            {
                Add(new Label("No links: the conversation ends after this entry.") { style = { color = new Color(0.6f, 0.6f, 0.65f) } });
                return;
            }
            if (m_Entry.links.Count > 1)
            {
                var help = new Foldout { text = "What does priority do?", value = false };
                help.AddToClassList("dlg-inspector__help");
                help.Add(new Label(LinkPriorityGUI.Explanation) { style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.7f, 0.74f, 0.8f) } });
                Add(help);
                var modes = RouteModeGUI.Labels.ToList();
                var pick = new PopupField<string>("Pick", modes, RouteModeGUI.Label(m_Entry.routeMode)) { tooltip = RouteModeGUI.Explanation };
                pick.AddToClassList("dlg-link__route");
                pick.RegisterValueChangedCallback(evt =>
                {
                    Edit("Change Route Mode", () => m_Entry.routeMode = (RouteMode)modes.IndexOf(evt.newValue));
                    LinksChanged?.Invoke();
                    Rebuild(); // weights show only for weighted modes
                });
                Add(pick);
            }
            bool weighted = m_Entry.links.Count > 1 && (m_Entry.routeMode == RouteMode.WeightedRandom || m_Entry.routeMode == RouteMode.Utility);
            var linksProp = weighted ? FindEntryProperty()?.FindPropertyRelative(nameof(DialogueEntry.links)) : null;
            var labels = LinkPriorityGUI.Labels.ToList();
            for (int i = 0; i < m_Entry.links.Count; i++)
            {
                int index = i; // by index: the link object may be replaced by SerializedObject edits
                var link = m_Entry.links[i];
                var card = new VisualElement();
                card.AddToClassList("dlg-link");
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                var target = new Label(LinkTargetLabel(link));
                target.AddToClassList("dlg-link__target");
                row.Add(target);
                var priority = new PopupField<string>(labels, LinkPriorityGUI.Label(link.priority)) { tooltip = LinkPriorityGUI.Explanation };
                priority.AddToClassList("dlg-link__priority");
                priority.RegisterValueChangedCallback(evt =>
                {
                    Edit("Change Link Priority", () => m_Entry.links[index].priority = (LinkPriority)labels.IndexOf(evt.newValue));
                    LinksChanged?.Invoke();
                });
                row.Add(priority);
                var edit = new Button() { text = "Conditions…", tooltip = "Edit this branch's conditions (and the target line's)" };
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
                card.Add(row);
                if (linksProp != null && index < linksProp.arraySize)
                {
                    var linkProp = linksProp.GetArrayElementAtIndex(index);
                    var weightRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var weight = new PropertyField(linkProp.FindPropertyRelative(nameof(DialogueLink.weight)), "Weight") { style = { flexGrow = 1 } };
                    var weightVariable = new PropertyField(linkProp.FindPropertyRelative(nameof(DialogueLink.weightVariable)), "× variable") { style = { flexGrow = 1 } };
                    foreach (var field in new[] { weight, weightVariable })
                    {
                        field.Bind(m_SerializedDatabase);
                        field.RegisterValueChangeCallback(_ => LinksChanged?.Invoke());
                        weightRow.Add(field);
                    }
                    card.Add(weightRow);
                }

                var targetEntry = m_Database.FindConversation(link.conversationId)?.FindEntry(link.entryId);
                string chip = DialogueEdgeView.ChipText(link, targetEntry?.conditions);
                if (!string.IsNullOrEmpty(chip))
                {
                    var conditions = new Label(chip) { tooltip = DialogueEdgeView.Describe(link, targetEntry?.conditions) };
                    conditions.AddToClassList("dlg-chip");
                    conditions.AddToClassList("dlg-chip--cond");
                    conditions.AddToClassList("dlg-link__chip");
                    card.Add(conditions);
                }
                Add(card);
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

        private void AddSection(string text, string hint = null)
        {
            var label = new Label(text) { tooltip = hint ?? string.Empty };
            label.AddToClassList("dlg-inspector__section");
            Add(label);
        }
    }
}
