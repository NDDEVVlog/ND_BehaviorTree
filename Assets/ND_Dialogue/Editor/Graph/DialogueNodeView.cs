using System.Collections.Generic;
using System.Linq;
using System.Text;
using ND.GraphUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// One dialogue entry on the canvas: speaker (and emotion), a preview of the line in the preview locale, and
    /// badges for conditions, actions and links that leave the conversation. No ports: links are drawn node to node
    /// and created with the hover handle.
    /// </summary>
    public class DialogueNodeView : GraphNodeView
    {
        private static readonly PortTheme s_UnusedPortTheme = new PortTheme();
        private const int PreviewLength = 140;

        public DialogueDatabase Database { get; }
        public Conversation Conversation { get; }
        public DialogueEntry Entry { get; private set; }

        /// <summary>Points the view at a re-fetched entry (after SerializedObject edits may have replaced the data objects).</summary>
        internal void Rebind(DialogueEntry entry) => Entry = entry;

        public override string Id => Entry.id.ToString();
        public override PortTheme PortTheme => s_UnusedPortTheme;
        protected override bool AllowsNodeLinks => true;

        public bool IsStart => Entry.id == Conversation.StartEntryId;

        private readonly VisualElement m_Header;
        private readonly Label m_Avatar;
        private readonly Label m_Speaker;
        private readonly Label m_Emotion;
        private readonly Label m_Text;
        private readonly VisualElement m_Chips;
        private readonly VisualElement m_MoreLines;
        private readonly Label m_Kind;
        private readonly Label m_IdLabel;
        private readonly VisualElement m_Portrait;

        /// <summary>The portrait shown above the node (null when the speaker's emotion has none).</summary>
        public Sprite PortraitSprite { get; private set; }

        /// <summary>The chips under the text (conditions, actions, events, links out of the conversation).</summary>
        public VisualElement Chips => m_Chips;

        public DialogueNodeView(DialogueDatabase database, Conversation conversation, DialogueEntry entry, string previewLocale)
        {
            Database = database;
            Conversation = conversation;
            Entry = entry;
            viewDataKey = $"dlg-{conversation.id}-{entry.id}";
            AddToClassList("dlg-node");

            // Header: coloured initial (when there is no portrait) · speaker · emotion tag
            m_Header = new VisualElement { name = "header", pickingMode = PickingMode.Ignore };
            m_Header.AddToClassList("dlg-node__header");
            m_Avatar = new Label { name = "avatar", pickingMode = PickingMode.Ignore };
            m_Avatar.AddToClassList("dlg-node__avatar");
            m_Speaker = new Label { name = "speaker", pickingMode = PickingMode.Ignore };
            m_Speaker.AddToClassList("dlg-node__speaker");
            m_Emotion = new Label { name = "emotion", pickingMode = PickingMode.Ignore };
            m_Emotion.AddToClassList("dlg-node__emotion");
            m_Header.Add(m_Avatar);
            m_Header.Add(m_Speaker);
            m_Header.Add(m_Emotion);
            m_Kind = new Label { name = "kind", pickingMode = PickingMode.Ignore };
            m_Kind.AddToClassList("dlg-node__kind");
            m_Header.Add(m_Kind);
            m_Header.Add(new VisualElement { pickingMode = PickingMode.Ignore, style = { flexGrow = 1 } });
            m_IdLabel = new Label { name = "entry-id", tooltip = "Entry id: right-click → Copy entry id, or Test from here" };
            m_IdLabel.AddToClassList("dlg-node__id");
            m_IdLabel.selection.isSelectable = true;
            m_Header.Add(m_IdLabel);

            m_Text = new Label { name = "text", pickingMode = PickingMode.Ignore };
            m_Text.AddToClassList("dlg-node__text");
            m_Chips = new VisualElement { name = "chips" };
            m_Chips.AddToClassList("dlg-node__chips");
            Add(m_Header);
            Add(m_Text);
            m_MoreLines = new VisualElement { name = "more-lines", pickingMode = PickingMode.Ignore };
            m_MoreLines.AddToClassList("dlg-node__lines");
            Add(m_MoreLines);
            Add(m_Chips);

            // Sits on top of the node's upper edge (the node does not clip its children).
            m_Portrait = new VisualElement { name = "portrait", pickingMode = PickingMode.Ignore };
            m_Portrait.AddToClassList("dlg-node__portrait");
            hierarchy.Add(m_Portrait);

            SetPosition(entry.position);
            Refresh(previewLocale);
        }

        /// <summary>Re-reads everything shown from the entry (after inspector edits, undo or a locale change).</summary>
        public void Refresh(string previewLocale)
        {
            var actor = Database.FindActor(Entry.actorId);
            bool player = actor != null && actor.isPlayer;
            EnableInClassList("dlg-node--start", IsStart);
            EnableInClassList("dlg-node--passthrough", !IsStart && Entry.isPassthrough && !Entry.IsHub);
            EnableInClassList("dlg-node--hub", Entry.IsHub);
            EnableInClassList("dlg-node--player", !IsStart && !Entry.isPassthrough && player);
            EnableInClassList("dlg-node--npc", !IsStart && !Entry.isPassthrough && !player);

            m_Emotion.style.display = DisplayStyle.None;
            m_Avatar.style.display = DisplayStyle.None;
            if (IsStart) m_Speaker.text = "▶  START";
            else if (Entry.IsHub) m_Speaker.text = string.IsNullOrEmpty(Entry.title) ? "◆ hub" : $"◆ {Entry.title}";
            else if (Entry.isPassthrough) m_Speaker.text = string.IsNullOrEmpty(Entry.title) ? "⤳  routing" : $"⤳  {Entry.title}";
            else
            {
                m_Speaker.text = actor != null ? Database.DisplayNameOf(actor) : $"<unknown actor {Entry.actorId}>";
                m_Speaker.tooltip = actor != null ? $"{actor.name}{(actor.isPlayer ? " (player)" : string.Empty)}" : string.Empty;
                if (!string.IsNullOrEmpty(Entry.emotion))
                {
                    m_Emotion.text = $"{DialogueEmotionGlyph.For(Entry.emotion)} {Entry.emotion}";
                    m_Emotion.style.display = DisplayStyle.Flex;
                }
            }

            if (IsStart || Entry.isPassthrough)
            {
                m_Text.text = string.Empty;
                m_Text.style.display = DisplayStyle.None;
            }
            else
            {
                string line = DialogueTextEditing.Read(Entry.text, previewLocale);
                m_Text.EnableInClassList("dlg-node__text--missing", line == null);
                m_Text.text = line == null ? (Entry.text.IsLocalized ? $"<no {previewLocale} text>" : "<no text>") : Truncate(line);
                m_Text.style.display = DisplayStyle.Flex;
            }

            RefreshMoreLines(previewLocale);
            RefreshChips();
            RefreshPortrait(actor, previewLocale);
            if (PortraitSprite == null && actor != null && !IsStart && !Entry.isPassthrough)
            {
                // No picture: a coloured initial, stable per character, so speakers are easy to tell apart.
                m_Avatar.text = string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1).ToUpperInvariant();
                m_Avatar.style.backgroundColor = ActorColor(actor.name);
                m_Avatar.style.display = DisplayStyle.Flex;
            }
            bool subclass = Entry.GetType() != typeof(DialogueEntry);
            m_Kind.text = subclass ? UnityEditor.ObjectNames.NicifyVariableName(Entry.GetType().Name) : string.Empty;
            m_Kind.style.display = subclass ? DisplayStyle.Flex : DisplayStyle.None;
            m_IdLabel.text = IsStart ? string.Empty : $"#{Entry.id}";
            tooltip = Entry.title;
        }

        /// <summary>The entry's further lines, one row each with the emotion's emoji (the first line is the main text).</summary>
        private void RefreshMoreLines(string previewLocale)
        {
            m_MoreLines.Clear();
            int count = IsStart ? 0 : Entry.LineCount;
            for (int i = 1; i < count; i++)
            {
                string line = DialogueTextEditing.Read(Entry.LineText(i), previewLocale);
                string emotion = Entry.LineEmotion(i);
                var row = new Label($"{DialogueEmotionGlyph.For(emotion)}  {(line == null ? "<no text>" : Truncate(line, LinePreviewLength))}")
                {
                    pickingMode = PickingMode.Ignore,
                    tooltip = string.IsNullOrEmpty(emotion) ? string.Empty : emotion
                };
                row.AddToClassList("dlg-node__line");
                row.EnableInClassList("dlg-node__text--missing", line == null);
                m_MoreLines.Add(row);
            }
            m_MoreLines.style.display = m_MoreLines.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (count > 1 && !string.IsNullOrEmpty(m_Text.text) && m_Text.style.display != DisplayStyle.None)
                m_Text.text = $"{DialogueEmotionGlyph.For(Entry.LineEmotion(0))}  {m_Text.text}";
        }

        private const int LinePreviewLength = 70;

        /// <summary>A stable, readable colour per character name.</summary>
        public static Color ActorColor(string actorName)
        {
            unchecked
            {
                int hash = 17;
                foreach (char ch in actorName ?? string.Empty) hash = hash * 31 + ch;
                float hue = (hash & 0x7fffffff) % 360 / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.75f);
            }
        }

        private void RefreshChips()
        {
            m_Chips.Clear();
            Chip("cond", $"◆ {Entry.conditions.Count}", Entry.conditions.Count, Entry.conditions.Select(c => c?.Describe() ?? "(empty)"), "Entry conditions (every way in)");
            var events = Entry.actions.OfType<RaiseEventAction>().ToList();
            var others = Entry.actions.Where(a => !(a is RaiseEventAction)).ToList();
            Chip("act", $"✎ {others.Count}", others.Count, others.Select(a => a?.Describe() ?? "(empty)"), "Actions");
            if (!string.IsNullOrWhiteSpace(Entry.sequence))
            {
                var commands = DialogueSequence.Parse(Entry.sequence);
                Chip("sequence", $"🎬 {commands.Count}", Mathf.Max(1, commands.Count), commands.Select(c => c.ToString()), "Sequence (plays when the entry starts)");
            }
            foreach (var e in events) Chip("event", $"⚡ {e.eventName}", 1, new[] { e.Describe() }, "Fires a gameplay event");
            string route = Entry.links.Count > 1 ? RouteModeGUI.Badge(Entry.routeMode) : null;
            if (route != null) Chip("route", route, 1, new[] { RouteModeGUI.Label(Entry.routeMode) }, "Picks one of its valid NPC lines");
            foreach (var link in Entry.links)
            {
                if (link.conversationId == Conversation.id) continue;
                var target = Database.FindConversation(link.conversationId);
                Chip("external", $"↗ {(target != null ? target.title : "?")}", 1, new[] { $"Continues in '{target?.title}' entry #{link.entryId}" }, "Leaves the conversation");
            }
            m_Chips.style.display = m_Chips.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Chip(string kind, string text, int count, IEnumerable<string> details, string title)
        {
            if (count <= 0) return;
            var chip = new Label(text) { pickingMode = PickingMode.Position, tooltip = title + ":\n• " + string.Join("\n• ", details) };
            chip.AddToClassList("dlg-chip");
            chip.AddToClassList("dlg-chip--" + kind);
            m_Chips.Add(chip);
        }

        private void RefreshPortrait(Actor actor, string previewLocale)
        {
            var emotion = IsStart || Entry.isPassthrough ? null : EmotionOf(Database, actor, Entry.emotion);
            PortraitSprite = emotion != null ? EditorPortrait(emotion, previewLocale) : null;
            if (PortraitSprite == null)
            {
                m_Portrait.style.display = DisplayStyle.None;
                m_Portrait.style.backgroundImage = StyleKeyword.None;
                return;
            }
            m_Portrait.style.display = DisplayStyle.Flex;
            m_Portrait.style.backgroundImage = new StyleBackground(PortraitSprite);
            m_Portrait.style.unityBackgroundImageTintColor = emotion.tint;
            m_Portrait.tooltip = $"{actor.name} · {emotion.id}";
        }

        /// <summary>The line's emotion as it looks in <paramref name="database"/> (its look first), else the actor's — without the runtime's error log for a missing one.</summary>
        public static ActorEmotion EmotionOf(DialogueDatabase database, Actor actor, string emotionId)
        {
            if (actor == null) return null;
            string wanted = string.IsNullOrEmpty(emotionId) ? actor.defaultEmotion : emotionId;
            var overridden = database != null ? database.FindLook(actor.id)?.FindEmotion(wanted) : null;
            if (overridden != null) return overridden;
            foreach (var e in actor.emotions)
                if (e != null && e.id == wanted) return e;
            return null;
        }

        /// <summary>
        /// What the game would show, read in the editor (localization is not running): the localized portrait for the
        /// preview locale when one is assigned, else the plain portrait.
        /// </summary>
        public static Sprite EditorPortrait(ActorEmotion emotion, string previewLocale)
        {
            var localized = emotion.localizedPortrait;
            if (localized != null && !localized.IsEmpty)
                return localized.Table.TryGetValue(localized.EntryId, previewLocale, out Object value) ? value as Sprite : null;
            return emotion.portrait;
        }

        private static string Truncate(string text, int length = PreviewLength)
        {
            text = text.Replace('\n', ' ');
            return text.Length <= length ? text : text.Substring(0, length - 1) + "…";
        }
    }

    /// <summary>
    /// A link inside the conversation. The line colour shows its priority; one amber chip sums up every condition the
    /// branch needs (its own and the target line's entry conditions), with the details as tooltip. A condition-less
    /// edge shows a faint "+" instead. Clicking the chip / "+" (or double-clicking the edge) opens the link editor.
    /// </summary>
    public class DialogueEdgeView : GraphEdgeView
    {
        private const int MaxConditionLines = 3;
        private const int ChipTextLimit = 24;

        private readonly Label m_AddChip;

        public DialogueLink Link { get; set; }

        /// <summary>Raised when the chip or the "+" is clicked (the canvas opens the link editor).</summary>
        public event System.Action<DialogueEdgeView> LabelClicked;

        /// <summary>The faint "+" shown on edges without conditions (for tests and styling).</summary>
        public Label AddChip => m_AddChip;

        public DialogueEdgeView(GraphNodeView from, EdgeTheme theme) : base(from, theme)
        {
            EdgeLabel.AddToClassList("dlg-edge__label");
            // A compact amber chip instead of the base theme's text pill.
            var s = EdgeLabel.style;
            s.backgroundColor = new Color(0.35f, 0.27f, 0.08f, 0.95f);
            s.color = new Color(1f, 0.85f, 0.45f);
            s.fontSize = 10;
            s.paddingLeft = s.paddingRight = 6;
            s.paddingTop = s.paddingBottom = 1;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 8;
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 1;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new Color(0.85f, 0.6f, 0.15f, 0.8f);
            EdgeLabel.tooltip = "Click to edit this branch's conditions and priority.";
            RegisterClick(EdgeLabel);

            m_AddChip = new Label("+") { pickingMode = PickingMode.Position, tooltip = "Add a condition to this branch" };
            m_AddChip.AddToClassList("dlg-edge__add");
            m_AddChip.style.position = Position.Absolute;
            Add(m_AddChip);
            RegisterClick(m_AddChip);
        }

        private void RegisterClick(VisualElement element)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.altKey) return;
                LabelClicked?.Invoke(this);
                evt.StopPropagation();
            });
        }

        private IReadOnlyList<DialogueCondition> TargetConditions => (ToNode as DialogueNodeView)?.Entry?.conditions;

        /// <summary>Re-reads the link (and the target line's entry conditions): chip text, tooltip, priority colour.</summary>
        public void RefreshLabel()
        {
            var target = TargetConditions;
            Text = ChipText(Link, target);
            EdgeLabel.tooltip = Link == null ? string.Empty : Describe(Link, target) + "\n\nClick to edit.";
            m_AddChip.style.display = Link != null && string.IsNullOrEmpty(Text) ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyColour();
        }

        public enum Focus { None, Outgoing, Incoming, Faded }

        private Focus m_Focus;

        /// <summary>How the edge is lit for the current selection.</summary>
        public Focus CurrentFocus => m_Focus;

        /// <summary>Lights the edge up when it leaves / enters a selected entry, or fades it when it does not touch the selection.</summary>
        public void SetFocus(Focus focus)
        {
            if (m_Focus == focus) return;
            m_Focus = focus;
            ApplyColour();
        }

        private static readonly Color s_OutgoingColour = new Color(0.35f, 0.75f, 1f);
        private static readonly Color s_IncomingColour = new Color(0.45f, 0.9f, 0.55f);

        private void ApplyColour()
        {
            var priority = Link?.priority ?? LinkPriority.Normal;
            Color? colour = PriorityColor(priority);
            bool emphasized = priority == LinkPriority.High;
            switch (m_Focus)
            {
                case Focus.Outgoing: colour = s_OutgoingColour; emphasized = true; break;
                case Focus.Incoming: colour = s_IncomingColour; emphasized = true; break;
                case Focus.Faded:
                {
                    var c = colour ?? Theme.edgeColor;
                    colour = new Color(c.r, c.g, c.b, 0.18f);
                    emphasized = false;
                    break;
                }
            }
            SetHighlight(colour, emphasized, false);
            float opacity = m_Focus == Focus.Faded ? 0.3f : 1f;
            EdgeLabel.style.opacity = opacity;
            m_AddChip.style.opacity = opacity;
            // Lit edges draw above the faded ones.
            if (m_Focus == Focus.Outgoing || m_Focus == Focus.Incoming) BringToFrontOfEdges();
        }

        /// <summary>Moves this edge after the other edges (still behind the nodes).</summary>
        private void BringToFrontOfEdges()
        {
            var container = parent;
            if (container == null) return;
            int index = container.IndexOf(this);
            int last = index;
            for (int i = index + 1; i < container.childCount; i++)
            {
                if (container[i] is GraphNodeView) break;
                if (container[i] is GraphEdgeView) last = i;
            }
            if (last != index) PlaceInFront(container[last]);
        }

        protected override void OnPathDrawn(Vector2 start, Vector2 end)
        {
            if (m_AddChip.style.display == DisplayStyle.None) return;
            Vector2 mid = PointAt(0.5f, start, end);
            m_AddChip.style.left = mid.x - m_AddChip.layout.width / 2f;
            m_AddChip.style.top = mid.y - m_AddChip.layout.height / 2f;
        }

        /// <summary>Chip text of a link alone (see <see cref="ChipText(DialogueLink, IReadOnlyList{DialogueCondition})"/>).</summary>
        public static string ChipText(DialogueLink link) => ChipText(link, null);

        /// <summary>
        /// Chip text: empty without conditions; the condition when there is exactly one short one (link or target
        /// line); else "◆ count" of all of them.
        /// </summary>
        public static string ChipText(DialogueLink link, IReadOnlyList<DialogueCondition> targetConditions)
        {
            var all = new List<DialogueCondition>();
            if (link?.conditions != null) all.AddRange(link.conditions);
            if (targetConditions != null) all.AddRange(targetConditions);
            if (all.Count == 0) return string.Empty;
            if (all.Count == 1 && all[0] != null)
            {
                string single = all[0].Describe();
                if (single.Length <= ChipTextLimit) return "◆ " + single;
            }
            return $"◆ {all.Count}";
        }

        public static Color? PriorityColor(LinkPriority priority)
        {
            switch (priority)
            {
                case LinkPriority.High: return new Color(1f, 0.6f, 0.2f);
                case LinkPriority.AboveNormal: return new Color(1f, 0.8f, 0.5f);
                case LinkPriority.BelowNormal: return new Color(0.55f, 0.57f, 0.62f);
                case LinkPriority.Low: return new Color(0.4f, 0.42f, 0.47f);
                default: return null;
            }
        }

        /// <summary>"▲ High" plus one "◆ condition" line per condition (at most 3, then "… +n more"); empty for a plain link.</summary>
        public static string Describe(DialogueLink link) => Describe(link, null);

        /// <summary>Like <see cref="Describe(DialogueLink)"/>, then the target line's entry conditions under their own heading.</summary>
        public static string Describe(DialogueLink link, IReadOnlyList<DialogueCondition> targetConditions)
        {
            if (link == null) return string.Empty;
            var sb = new StringBuilder();
            if (link.priority != LinkPriority.Normal) sb.Append(link.priority == LinkPriority.High || link.priority == LinkPriority.AboveNormal ? "▲ " : "▼ ").Append(link.priority);
            AppendConditions(sb, link.conditions);
            if (targetConditions != null && targetConditions.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("Target line (every way in):");
                AppendConditions(sb, targetConditions);
            }
            return sb.ToString();
        }

        private static void AppendConditions(StringBuilder sb, IReadOnlyList<DialogueCondition> conditions)
        {
            if (conditions == null) return;
            int shown = 0;
            foreach (var condition in conditions)
            {
                if (shown == MaxConditionLines)
                {
                    sb.Append('\n').Append($"… +{conditions.Count - shown} more");
                    break;
                }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("◆ ").Append(condition != null ? condition.Describe() : "(empty)");
                shown++;
            }
        }
    }
}
