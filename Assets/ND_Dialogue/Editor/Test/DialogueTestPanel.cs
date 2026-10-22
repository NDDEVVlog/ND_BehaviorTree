using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// "Test Dialogue" section: plays a conversation in a chat like a messenger app, inside the editor (no Play mode).
    /// NPC lines come as bubbles on the left (portrait, name, emotion, entry id), the player's on the right; choices are
    /// reply buttons (or keys 1–9; Space / Enter continues). After each step, small lines show what happened: entries
    /// passed and their actions, variables and quests that changed, events fired. Optionally the conversation graph is
    /// shown next to the chat, with the current entry and the seen ones highlighted. Start from any entry by its id.
    /// The run itself is a <see cref="DialogueTestSession"/> (the AI bridge drives the same thing).
    /// </summary>
    public class DialogueTestPanel : VisualElement
    {
        private const string GraphPref = "ND.Dialogue.Test.ShowGraph";
        private const string ValuesPref = "ND.Dialogue.Test.ShowValues";
        private const string AllValuesPref = "ND.Dialogue.Test.AllValues";
        private const string TypingPref = "ND.Dialogue.Test.Typing";

        private DialogueDatabase m_Database;
        private string m_Locale;
        private DialogueTestSession m_Session;
        private int m_LastSpeakerId = int.MinValue;

        private readonly PopupField<string> m_ConversationField;
        private readonly IntegerField m_EntryField;
        private readonly Toggle m_GraphToggle;
        private readonly Toggle m_ValuesToggle;
        private readonly Toggle m_AllValuesToggle;
        private readonly Toggle m_TypingToggle;
        // The bubble being typed (the same LineTimeline as the game's TextMeshPro typewriter), or null.
        private Label m_TypingBubble;
        private LineTimeline m_TypingTimeline;
        private readonly LineFrame m_TypingFrame = new LineFrame();
        private double m_TypingStart;
        private IVisualElementScheduledItem m_TypingTick;
        private readonly VisualElement m_Values;
        private readonly ScrollView m_ValueRows;
        private readonly Label m_Status;
        private readonly TwoPaneSplitView m_Split;
        private readonly ScrollView m_Log;
        private readonly VisualElement m_Replies;
        private readonly DialogueGraphCanvas m_Graph;
        private readonly List<string> m_Titles = new List<string>();

        public DialogueTestSession Session => m_Session;
        public DialogueRunner Runner => m_Session?.Runner;
        public ScrollView Log => m_Log;
        public VisualElement Replies => m_Replies;
        public DialogueGraphCanvas Graph => m_Graph;
        /// <summary>The variables panel's rows (one per variable / quest shown).</summary>
        public VisualElement ValueRows => m_ValueRows.contentContainer;

        public DialogueTestPanel()
        {
            AddToClassList("dlg-test");
            var sheet = DialogueEditorStyles.Graph;
            if (sheet != null) styleSheets.Add(sheet);
            style.flexGrow = 1;
            focusable = true;

            var bar = new VisualElement();
            bar.AddToClassList("dlg-test__toolbar");
            m_ConversationField = new PopupField<string>(m_Titles, 0) { tooltip = "Conversation to play" };
            m_ConversationField.AddToClassList("dlg-test__conversation");
            m_ConversationField.RegisterValueChangedCallback(_ => OnConversationPicked());
            bar.Add(m_ConversationField);
            bar.Add(new Label("from #") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6 } });
            m_EntryField = new IntegerField { value = Conversation.StartEntryId, tooltip = "Entry id to start at (0 = START). Every node shows its id; right-click a node → Test from here." };
            m_EntryField.AddToClassList("dlg-test__entry");
            bar.Add(m_EntryField);
            bar.Add(new Button(() => Start()) { text = "▶ Start", tooltip = "Start (again) at the entry above. Variables and seen lines are kept: use Reset world to start fresh." });
            bar.Add(new Button(() => Bark()) { text = "💬 Bark", tooltip = "Say one bark of the conversation, as a Dialogue Barker would: a valid START line (random), its actions run, no choices." });
            bar.Add(new Button(Stop) { text = "■ Stop" });
            bar.Add(new Button(ResetWorld) { text = "⟲ Reset world", tooltip = "Back to the initial values, nothing seen, empty chat" });
            bar.Add(new VisualElement { style = { flexGrow = 1 } });
            m_Status = new Label();
            m_Status.AddToClassList("dlg-test__status");
            bar.Add(m_Status);
            m_GraphToggle = new Toggle("Graph") { value = EditorPrefs.GetBool(GraphPref, true), tooltip = "Show the conversation next to the chat, with the current entry highlighted" };
            m_GraphToggle.AddToClassList("dlg-test__graph-toggle");
            m_GraphToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(GraphPref, evt.newValue);
                ApplyGraphVisibility();
            });
            bar.Add(m_GraphToggle);
            m_ValuesToggle = new Toggle("Variables") { value = EditorPrefs.GetBool(ValuesPref, true), tooltip = "Show the variables and quests this conversation uses, and change them to see where it goes" };
            m_ValuesToggle.AddToClassList("dlg-test__graph-toggle");
            m_ValuesToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(ValuesPref, evt.newValue);
                m_Values.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshValues();
            });
            bar.Add(m_ValuesToggle);
            m_TypingToggle = new Toggle("Typing") { value = EditorPrefs.GetBool(TypingPref, true), tooltip = "Type lines out as the game does (markup pauses, speed, err/fix, voice length). Space completes the line first." };
            m_TypingToggle.AddToClassList("dlg-test__graph-toggle");
            m_TypingToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(TypingPref, evt.newValue);
                if (!evt.newValue) CompleteTyping();
            });
            bar.Add(m_TypingToggle);
            Add(bar);

            var chat = new VisualElement();
            chat.AddToClassList("dlg-chat");
            m_Log = new ScrollView(ScrollViewMode.Vertical);
            m_Log.AddToClassList("dlg-chat__log");
            chat.Add(m_Log);
            m_Replies = new VisualElement();
            m_Replies.AddToClassList("dlg-chat__replies");
            chat.Add(m_Replies);

            // Variables panel: what this conversation reads / writes, editable ("what if trust were 5?").
            m_Values = new VisualElement();
            m_Values.AddToClassList("dlg-values");
            var head = new VisualElement();
            head.AddToClassList("dlg-values__head");
            head.Add(new Label("Variables") { name = "title" });
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            m_AllValuesToggle = new Toggle("All") { value = EditorPrefs.GetBool(AllValuesPref, false), tooltip = "Every variable and quest of the database, not only the ones this conversation uses" };
            m_AllValuesToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(AllValuesPref, evt.newValue);
                RefreshValues();
            });
            head.Add(m_AllValuesToggle);
            m_Values.Add(head);
            m_ValueRows = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            m_Values.Add(m_ValueRows);
            var hint = new Label("Changes apply from the next step. ▶ Start replays from the entry with these values.");
            hint.AddToClassList("dlg-values__hint");
            m_Values.Add(hint);
            m_Values.style.display = m_ValuesToggle.value ? DisplayStyle.Flex : DisplayStyle.None;

            var chatRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            chatRow.Add(chat);
            chatRow.Add(m_Values);

            m_Graph = new DialogueGraphCanvas();
            m_Split = new TwoPaneSplitView(0, 560, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
            m_Split.Add(chatRow);
            m_Split.Add(m_Graph);
            Add(m_Split);
            ApplyGraphVisibility();

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        /// <summary>Points the panel at a database (the chat restarts only when the database changes).</summary>
        public void Bind(DialogueDatabase database, string locale, EditorWindow host, int preferredConversationId = 0)
        {
            bool changed = database != m_Database;
            m_Database = database;
            m_Locale = locale;
            m_Graph.HostWindow = host;
            m_Titles.Clear();
            if (database != null) m_Titles.AddRange(database.conversations.Where(c => c != null).Select(c => c.title));
            m_ConversationField.choices = m_Titles;
            var preferred = database?.FindConversation(preferredConversationId);
            string current = m_ConversationField.value;
            if (changed || string.IsNullOrEmpty(current) || !m_Titles.Contains(current))
                m_ConversationField.SetValueWithoutNotify(preferred != null ? preferred.title : m_Titles.FirstOrDefault() ?? string.Empty);
            if (changed || m_Session == null) ResetWorld();
            else
            {
                m_Session.Locale = locale;
                UpdateGraph();
                RefreshValues();
            }
        }

        public void SetLocale(string locale)
        {
            m_Locale = locale;
            if (m_Session != null) m_Session.Locale = locale;
            m_Graph.SetPreviewLocale(locale);
        }

        /// <summary>Starts <paramref name="conversationId"/> at <paramref name="entryId"/> (e.g. "Test from here" on a node).</summary>
        public bool StartAt(int conversationId, int entryId)
        {
            var conversation = m_Database?.FindConversation(conversationId);
            if (conversation == null) return false;
            m_ConversationField.SetValueWithoutNotify(conversation.title);
            m_EntryField.SetValueWithoutNotify(entryId);
            return Start();
        }

        /// <summary>Starts the chosen conversation at the chosen entry id (the world and seen lines are kept).</summary>
        public bool Start()
        {
            if (m_Session == null) { SetStatus("Pick a database."); return false; }
            var items = m_Session.Start(m_Database.FindConversation(m_ConversationField.value), m_EntryField.value);
            Focus();
            return Show(items);
        }

        /// <summary>One bark of the chosen conversation (see <see cref="DialogueTestSession.Bark"/>).</summary>
        public bool Bark()
        {
            if (m_Session == null) { SetStatus("Pick a database."); return false; }
            var conversation = m_Database.FindConversation(m_ConversationField.value);
            return Show(m_Session.Bark(conversation));
        }

        public void Stop() => Show(m_Session?.Stop());

        /// <summary>Moves on (a line still typing is completed first, then the conversation goes on).</summary>
        public bool Continue()
        {
            CompleteTyping();
            return m_Session != null && m_Session.Runner?.State == DialogueRunnerState.ShowingLine && Show(m_Session.Continue());
        }

        /// <summary>Lines type out as in the game (the Typing toggle; this setter does not save the preference).</summary>
        public bool TypingEnabled
        {
            get => m_TypingToggle.value;
            set
            {
                m_TypingToggle.SetValueWithoutNotify(value);
                if (!value) CompleteTyping();
            }
        }

        /// <summary>True while a bubble is typing out.</summary>
        public bool IsTyping => m_TypingBubble != null;

        /// <summary>Shows the rest of the typing bubble at once.</summary>
        public void CompleteTyping()
        {
            if (m_TypingBubble == null) return;
            m_TypingBubble.text = m_TypingTimeline.Compose(float.PositiveInfinity).Text;
            StopTyping();
        }

        private void StopTyping()
        {
            m_TypingTick?.Pause();
            m_TypingTick = null;
            m_TypingBubble = null;
            m_TypingTimeline = null;
        }

        private void StartTyping(Label bubble, DialogueTestSession.Item item)
        {
            CompleteTyping();
            var timeline = LineTimeline.Build(item.Text, LineTimeline.DefaultCharactersPerSecond, item.Voice != null ? item.Voice.length : 0f);
            if (timeline.Duration <= 0f) return;
            m_TypingBubble = bubble;
            m_TypingTimeline = timeline;
            m_TypingStart = EditorApplication.timeSinceStartup;
            bubble.text = timeline.Compose(0f).Text;
            m_TypingFrame.Reset();
            m_TypingTick = bubble.schedule.Execute(() =>
            {
                if (m_TypingBubble != bubble) return;
                var frame = m_TypingTimeline.Compose((float)(EditorApplication.timeSinceStartup - m_TypingStart), m_TypingFrame);
                if (frame.Changed) bubble.text = frame.Text; // a string only when a character appears
                if (frame.Done) StopTyping();
            }).Every(16);
        }

        public bool Choose(int index) => m_Session != null && m_Session.Runner?.State == DialogueRunnerState.WaitingForChoice && Show(m_Session.Choose(index));

        /// <summary>New world (initial values), nothing seen, empty chat.</summary>
        public void ResetWorld()
        {
            StopTyping();
            m_Session?.ResetWorld();
            m_Session = m_Database != null ? new DialogueTestSession(m_Database, m_Locale) : null;
            m_Log.Clear();
            m_Replies.Clear();
            m_LastSpeakerId = int.MinValue;
            SetStatus(m_Database == null ? "Pick a database." : EditorApplication.isPlaying
                ? "Play mode: the test world shares runtime state (e.g. shared blackboards) with the game."
                : "Press ▶ Start (or right-click a node → Test from here).");
            UpdateGraph();
            RefreshValues();
        }

        /// <summary>Renders a step's items, then the replies, the graph highlight and the status.</summary>
        private bool Show(List<DialogueTestSession.Item> items)
        {
            if (items == null)
            {
                if (m_Session?.Error != null) SetStatus(m_Session.Error);
                return false;
            }
            foreach (var item in items) Render(item);
            UpdateReplies();
            UpdateGraph();
            RefreshValues();
            var runner = m_Session.Runner;
            if (runner != null)
                SetStatus(runner.IsRunning ? $"#{runner.CurrentEntry?.id} · {runner.State} · seen {runner.Memory.Count}" : $"seen {runner.Memory.Count}");
            m_Log.schedule.Execute(() => m_Log.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(10);
            return true;
        }

        private void Render(DialogueTestSession.Item item)
        {
            switch (item.Kind)
            {
                case DialogueTestSession.ItemKind.Line:
                case DialogueTestSession.ItemKind.Reply:
                    AddBubble(item);
                    break;
                case DialogueTestSession.ItemKind.Start:
                    m_LastSpeakerId = int.MinValue;
                    AddLabel(item.Text, "dlg-chat__system", "dlg-chat__system--start");
                    break;
                case DialogueTestSession.ItemKind.End:
                    AddLabel(item.Text, "dlg-chat__system", "dlg-chat__system--end");
                    break;
                default:
                    AddLabel(item.Text, "dlg-effect", "dlg-effect--" + item.Kind.ToString().ToLowerInvariant());
                    break;
            }
        }

        #region Chat items

        /// <summary>A bubble: left for NPCs (portrait, name, emotion, #id), right for the player.</summary>
        private void AddBubble(DialogueTestSession.Item item)
        {
            var speaker = item.Speaker;
            var entry = item.Entry;
            bool player = speaker != null && speaker.isPlayer;
            int speakerId = speaker?.id ?? -1;
            bool first = speakerId != m_LastSpeakerId;
            m_LastSpeakerId = speakerId;

            var row = new VisualElement();
            row.AddToClassList("dlg-msg");
            row.AddToClassList(player ? "dlg-msg--player" : "dlg-msg--npc");
            row.EnableInClassList("dlg-msg--first", first);

            var emotion = item.Emotion ?? (speaker != null && entry != null ? DialogueNodeView.EmotionOf(m_Database, speaker, entry.emotion) : null);
            if (!player)
            {
                var avatar = new VisualElement();
                avatar.AddToClassList("dlg-msg__avatar");
                var sprite = emotion != null ? DialogueNodeView.EditorPortrait(emotion, m_Locale) : null;
                if (sprite != null) avatar.style.backgroundImage = new StyleBackground(sprite);
                else if (speaker != null)
                {
                    avatar.style.backgroundColor = DialogueNodeView.ActorColor(speaker.name);
                    avatar.Add(new Label(string.IsNullOrEmpty(speaker.name) ? "?" : speaker.name.Substring(0, 1).ToUpperInvariant()));
                }
                avatar.style.visibility = first || sprite != null ? Visibility.Visible : Visibility.Hidden;
                row.Add(avatar);
            }

            var body = new VisualElement();
            body.AddToClassList("dlg-msg__body");
            var meta = new VisualElement();
            meta.AddToClassList("dlg-msg__meta");
            if (!player && first) meta.Add(new Label(speaker != null ? m_Database.DisplayNameOf(speaker) : "?") { name = "speaker" });
            if (emotion != null && !string.IsNullOrEmpty(emotion.id)) meta.Add(new Label($"{DialogueEmotionGlyph.For(emotion.id)} {emotion.id}") { name = "emotion" });
            if (entry != null)
            {
                string part = item.LineCount > 1 ? $"  {item.LineIndex + 1}/{item.LineCount}" : string.Empty;
                var id = new Label($"#{entry.id}{part}") { name = "entry-id", tooltip = "Click: copy the id (▶ Start begins there) and show the entry on the graph" };
                id.AddToClassList("dlg-msg__id");
                int entryId = entry.id;
                id.RegisterCallback<ClickEvent>(_ =>
                {
                    EditorGUIUtility.systemCopyBuffer = entryId.ToString();
                    m_EntryField.SetValueWithoutNotify(entryId);
                    var view = m_Graph.GetNodeView(entryId.ToString());
                    if (view != null) CenterOn(view);
                    SetStatus($"#{entryId} copied — ▶ Start begins there.");
                });
                meta.Add(id);
            }
            body.Add(meta);

            var bubble = new Label(item.Shown) { name = "bubble", enableRichText = true };
            bubble.AddToClassList("dlg-bubble");
            bubble.AddToClassList(player ? "dlg-bubble--player" : "dlg-bubble--npc");
            if (!player && emotion != null && emotion.tint != Color.white) bubble.style.borderLeftColor = emotion.tint;
            body.Add(bubble);
            row.Add(body);
            m_Log.Add(row);
            if (item.Kind == DialogueTestSession.ItemKind.Line && m_TypingToggle.value) StartTyping(bubble, item);
        }

        private void AddLabel(string text, string baseClass, string kindClass)
        {
            var label = new Label(text);
            label.AddToClassList(baseClass);
            label.AddToClassList(kindClass);
            m_Log.Add(label);
            m_LastSpeakerId = int.MinValue; // the next bubble shows its speaker again
        }

        /// <summary>Continue, the choices as reply buttons, or Play again at the end.</summary>
        private void UpdateReplies()
        {
            m_Replies.Clear();
            var runner = m_Session?.Runner;
            if (runner == null) return;
            switch (runner.State)
            {
                case DialogueRunnerState.ShowingLine:
                {
                    var next = new Button(() => { if (IsTyping) CompleteTyping(); else Continue(); }) { text = runner.CurrentLine.HasMoreLines ? "Next line ▸  (Space)" : "Continue ▸  (Space)" };
                    next.AddToClassList("dlg-reply");
                    next.AddToClassList("dlg-reply--continue");
                    m_Replies.Add(next);
                    break;
                }
                case DialogueRunnerState.WaitingForChoice:
                    foreach (var choice in runner.CurrentChoices)
                    {
                        int index = choice.Index;
                        var reply = new Button(() => Choose(index)) { text = $"{index + 1}.  {LineTimeline.Final(choice.Text)}", tooltip = $"#{choice.Entry.id}" };
                        reply.AddToClassList("dlg-reply");
                        m_Replies.Add(reply);
                    }
                    break;
                default:
                {
                    var again = new Button(() => Start()) { text = "↻ Play again" };
                    again.AddToClassList("dlg-reply");
                    m_Replies.Add(again);
                    break;
                }
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target is TextField || evt.target is IntegerField) return;
            if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (IsTyping) { CompleteTyping(); evt.StopPropagation(); }
                else if (Continue()) evt.StopPropagation();
            }
            else if (evt.keyCode >= KeyCode.Alpha1 && evt.keyCode <= KeyCode.Alpha9)
            {
                if (Choose(evt.keyCode - KeyCode.Alpha1)) evt.StopPropagation();
            }
        }

        #endregion

        #region Graph

        private void ApplyGraphVisibility()
        {
            if (m_GraphToggle.value) m_Split.UnCollapse();
            else m_Split.CollapseChild(1);
        }

        /// <summary>Shows the conversation being played; the current entry and the seen ones are highlighted.</summary>
        private void UpdateGraph()
        {
            if (m_Database == null) { m_Graph.Load(null, null, m_Locale); return; }
            var runner = m_Session?.Runner;
            var conversation = SelectedConversation;
            if (conversation == null) return;
            if (m_Graph.Database != m_Database || m_Graph.Conversation == null || m_Graph.Conversation.id != conversation.id)
            {
                m_Graph.Load(m_Database, conversation, m_Locale);
                m_Graph.schedule.Execute(() => m_Graph.FrameDefault()).StartingIn(50);
            }
            int current = runner != null && runner.IsRunning ? runner.CurrentEntry?.id ?? -1 : -1;
            foreach (var view in m_Graph.Nodes)
            {
                view.EnableInClassList("dlg-node--current", view.Entry.id == current);
                view.EnableInClassList("dlg-node--visited", runner != null && runner.Memory.WasSeen(conversation.id, view.Entry.id));
            }
            var currentView = current >= 0 ? m_Graph.GetNodeView(current.ToString()) : null;
            if (currentView != null) m_Graph.schedule.Execute(() => CenterOn(currentView)).StartingIn(60);
        }

        /// <summary>Pans (keeping the zoom) so the node is in the middle of the graph.</summary>
        private void CenterOn(GraphUI.GraphNodeView view)
        {
            var size = view.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0f) size = new Vector2(240f, 80f);
            m_Graph.FrameRect(new Rect(view.ViewPosition, size), m_Graph.Zoom);
        }

        #endregion

        /// <summary>The conversation being played while one runs (it may have jumped elsewhere), else the one picked.</summary>
        private Conversation SelectedConversation
        {
            get
            {
                var runner = m_Session?.Runner;
                if (runner != null && runner.IsRunning && runner.CurrentConversation != null) return runner.CurrentConversation;
                return m_Database?.FindConversation(m_ConversationField.value);
            }
        }

        /// <summary>Picks <paramref name="title"/> as if chosen in the toolbar (see <see cref="OnConversationPicked"/>).</summary>
        public bool PickConversation(string title)
        {
            if (!m_Titles.Contains(title)) { SetStatus($"No conversation '{title}'."); return false; }
            m_ConversationField.SetValueWithoutNotify(title);
            OnConversationPicked();
            return true;
        }

        /// <summary>Another conversation was picked: stop the one running, start at its START, show its graph and values.</summary>
        private void OnConversationPicked()
        {
            m_EntryField.SetValueWithoutNotify(Conversation.StartEntryId);
            if (m_Session?.Runner != null && m_Session.Runner.IsRunning) Show(m_Session.Stop());
            UpdateGraph();
            RefreshValues();
            bool bark = m_Database?.FindConversation(m_ConversationField.value)?.isBark ?? false;
            SetStatus(bark ? $"'{m_ConversationField.value}' is a bark conversation: press 💬 Bark (▶ Start plays it as a conversation)." : $"Press ▶ Start to play '{m_ConversationField.value}'.");
        }

        #region Variables

        /// <summary>One row per variable / quest the conversation uses (or all of them): name, read / set by, editable value.</summary>
        private void RefreshValues()
        {
            m_ValueRows.Clear();
            if (!m_ValuesToggle.value || m_Session == null) return;
            if (!m_Session.EnsureWorld())
            {
                m_ValueRows.Add(new Label(m_Session.Error ?? "No test world.") { name = "error" });
                return;
            }
            var uses = DialogueValueUses.Collect(SelectedConversation);
            if (m_AllValuesToggle.value)
            {
                var schema = m_Database.Schema;
                var known = new HashSet<string>(uses.Select(u => u.Key));
                foreach (var name in schema.VariableNames.Distinct().OrderBy(n => n))
                    if (known.Add(name)) uses.Add(new DialogueValueUses.Use { Key = name, Name = name });
                foreach (var name in schema.QuestNames.Distinct().OrderBy(n => n))
                    if (known.Add("quest " + name)) uses.Add(new DialogueValueUses.Use { Key = "quest " + name, Name = name, IsQuest = true });
            }
            if (uses.Count == 0)
            {
                var empty = new Label("This conversation uses no variables or quests. Tick All to see every one.");
                empty.AddToClassList("dlg-values__hint");
                m_ValueRows.Add(empty);
                return;
            }
            foreach (var use in uses) m_ValueRows.Add(ValueRow(use));
        }

        private VisualElement ValueRow(DialogueValueUses.Use use)
        {
            var row = new VisualElement { name = use.Key };
            row.AddToClassList("dlg-values__row");
            var head = new VisualElement();
            head.AddToClassList("dlg-values__name-row");
            var name = new Label(use.IsQuest ? $"◆ {use.Name}" : use.Name);
            name.AddToClassList("dlg-values__name");
            head.Add(name);
            if (use.ReadBy.Count > 0) head.Add(Tag("if", "Read by " + string.Join(", ", use.ReadBy.Select(id => "#" + id))));
            if (use.WrittenBy.Count > 0) head.Add(Tag("set", "Set by " + string.Join(", ", use.WrittenBy.Select(id => "#" + id))));
            row.Add(head);

            if (use.IsQuest)
            {
                if (!m_Session.TryGetQuestState(use.Name, out var state)) { row.Add(new Label("(not in the world)")); return row; }
                var field = new EnumField(state);
                field.RegisterValueChangedCallback(evt => SetValue(use.Key, evt.newValue.ToString()));
                row.Add(field);
                return row;
            }
            if (!m_Session.TryGetVariable(use.Name, out var value)) { row.Add(new Label("(not in the world)")); return row; }
            if (value.type == VariableType.Bool)
            {
                var toggle = new Toggle { value = value.boolValue };
                toggle.RegisterValueChangedCallback(evt => SetValue(use.Key, evt.newValue ? "true" : "false"));
                row.Add(toggle);
            }
            else
            {
                var text = new TextField { value = value.ToString(), isDelayed = true, tooltip = $"{value.type}: Enter to apply" };
                text.RegisterValueChangedCallback(evt => SetValue(use.Key, evt.newValue));
                row.Add(text);
            }
            return row;
        }

        private static Label Tag(string text, string tooltip)
        {
            var tag = new Label(text) { tooltip = tooltip };
            tag.AddToClassList("dlg-values__tag");
            tag.AddToClassList("dlg-values__tag--" + text);
            return tag;
        }

        /// <summary>Sets a test-world value ("what if"): the change shows in the chat; a bad value is reported and the row reverts.</summary>
        public bool SetValue(string key, string text)
        {
            if (m_Session == null) return false;
            var items = m_Session.SetValue(key, text);
            if (items == null)
            {
                SetStatus(m_Session.Error);
                RefreshValues();
                return false;
            }
            foreach (var item in items) Render(item);
            RefreshValues();
            m_Log.schedule.Execute(() => m_Log.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(10);
            return true;
        }

        #endregion

        private void SetStatus(string text) => m_Status.text = text;
    }
}
