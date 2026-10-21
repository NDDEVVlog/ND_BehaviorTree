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

            m_Text = new Label { name = "text", pickingMode = PickingMode.Ignore };
            m_Text.AddToClassList("dlg-node__text");
            m_Chips = new VisualElement { name = "chips" };
            m_Chips.AddToClassList("dlg-node__chips");
            Add(m_Header);
            Add(m_Text);
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
            EnableInClassList("dlg-node--passthrough", !IsStart && Entry.isPassthrough);
            EnableInClassList("dlg-node--player", !IsStart && !Entry.isPassthrough && player);
            EnableInClassList("dlg-node--npc", !IsStart && !Entry.isPassthrough && !player);

            m_Emotion.style.display = DisplayStyle.None;
            m_Avatar.style.display = DisplayStyle.None;
            if (IsStart) m_Speaker.text = "▶  START";
            else if (Entry.isPassthrough) m_Speaker.text = string.IsNullOrEmpty(Entry.title) ? "⤳  routing" : $"⤳  {Entry.title}";
            else
            {
                m_Speaker.text = actor != null ? actor.ResolveDisplayName() : $"<unknown actor {Entry.actorId}>";
                m_Speaker.tooltip = actor != null ? $"{actor.name}{(actor.isPlayer ? " (player)" : string.Empty)}" : string.Empty;
                if (!string.IsNullOrEmpty(Entry.emotion))
                {
                    m_Emotion.text = Entry.emotion;
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

            RefreshChips();
            RefreshPortrait(actor, previewLocale);
            if (PortraitSprite == null && actor != null && !IsStart && !Entry.isPassthrough)
            {
                // No picture: a coloured initial, stable per character, so speakers are easy to tell apart.
                m_Avatar.text = string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1).ToUpperInvariant();
                m_Avatar.style.backgroundColor = ActorColor(actor.name);
                m_Avatar.style.display = DisplayStyle.Flex;
            }
            tooltip = Entry.title;
        }

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
            foreach (var e in events) Chip("event", $"⚡ {e.eventName}", 1, new[] { e.Describe() }, "Fires a gameplay event");
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
            var emotion = IsStart || Entry.isPassthrough ? null : EmotionOf(actor, Entry.emotion);
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

        /// <summary>The line's emotion, else the actor's default — without the runtime's error log for a missing one.</summary>
        private static ActorEmotion EmotionOf(Actor actor, string emotionId)
        {
            if (actor == null) return null;
            string wanted = string.IsNullOrEmpty(emotionId) ? actor.defaultEmotion : emotionId;
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

        private static string Truncate(string text)
        {
            text = text.Replace('\n', ' ');
            return text.Length <= PreviewLength ? text : text.Substring(0, PreviewLength - 1) + "…";
        }
    }

    /// <summary>A link inside the conversation; shows its priority when it is not Normal.</summary>
    /// <summary>
    /// A link inside the conversation. Its label shows the priority (when not Normal) and the link's conditions;
    /// clicking the label (or double-clicking the edge) opens the link editor.
    /// </summary>
    public class DialogueEdgeView : GraphEdgeView
    {
        private const int MaxConditionLines = 3;

        public DialogueLink Link { get; set; }

        /// <summary>Raised when the label is clicked (the canvas opens the link editor).</summary>
        public event System.Action<DialogueEdgeView> LabelClicked;

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
            EdgeLabel.tooltip = "Click to edit this link's conditions and priority.";
            EdgeLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.altKey) return;
                LabelClicked?.Invoke(this);
                evt.StopPropagation();
            });
        }

        private const int ChipTextLimit = 24;

        /// <summary>
        /// The edge shows its link at a glance: priority as line colour/weight (High = thick orange, Low = dim), and
        /// conditions as one small chip — the condition itself when there is one short one, else "◆ n". The full
        /// description is the chip's tooltip.
        /// </summary>
        public void RefreshLabel()
        {
            Text = ChipText(Link);
            EdgeLabel.tooltip = Link == null ? string.Empty : Describe(Link) + "\n\nClick to edit.";
            var priority = Link?.priority ?? LinkPriority.Normal;
            SetHighlight(PriorityColor(priority), priority == LinkPriority.High, false);
        }

        /// <summary>Chip text: empty without conditions; the condition when there is one short one; else "◆ count".</summary>
        public static string ChipText(DialogueLink link)
        {
            var conditions = link?.conditions;
            if (conditions == null || conditions.Count == 0) return string.Empty;
            if (conditions.Count == 1 && conditions[0] != null)
            {
                string single = conditions[0].Describe();
                if (single.Length <= ChipTextLimit) return "◆ " + single;
            }
            return $"◆ {conditions.Count}";
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
        public static string Describe(DialogueLink link)
        {
            if (link == null) return string.Empty;
            var sb = new StringBuilder();
            if (link.priority != LinkPriority.Normal) sb.Append(link.priority == LinkPriority.High || link.priority == LinkPriority.AboveNormal ? "▲ " : "▼ ").Append(link.priority);
            var conditions = link.conditions;
            if (conditions != null)
            {
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
            return sb.ToString();
        }
    }
}
