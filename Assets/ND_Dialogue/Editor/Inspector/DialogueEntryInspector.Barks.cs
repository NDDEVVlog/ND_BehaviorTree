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
    /// START of a bark conversation: every bark line in one table — its text, emotion, priority and "says it when"
    /// conditions — instead of wiring edges and opening each link. The table edits the same data (START's links and
    /// their conditions), so the graph and the runtime stay as they were.
    /// </summary>
    public partial class DialogueEntryInspector
    {
        private void BuildBarkLines()
        {
            AddSection("Bark lines", "A Dialogue Barker says ONE of these at a time.");
            Add(new HelpBox("When the barker barks, it picks one of these lines: only lines whose conditions hold now; among them the highest " +
                            "priority wins (e.g. High \"Stop, thief!\" while the alarm is on, Normal idle lines otherwise); then at random or in turn " +
                            "(the barker's Order). How often and how close is set on the Dialogue Barker component.", HelpBoxMessageType.None));

            var start = m_Entry;
            var labels = LinkPriorityGUI.Labels.ToList();
            for (int i = 0; i < start.links.Count; i++)
            {
                var link = start.links[i];
                var line = link.conversationId == m_Conversation.id ? m_Conversation.FindEntry(link.entryId) : null;
                if (line == null) continue;
                Add(BarkCard(start, i, line, labels));
            }
            if (start.links.Count == 0) Add(new Label("No bark lines yet.") { style = { color = new Color(0.6f, 0.6f, 0.65f) } });

            var add = new Button(AddBarkLine) { text = "+ Add bark line", tooltip = "A new line, said by the same character as the others." };
            add.AddToClassList("dlg-inspector__add");
            Add(add);
        }

        private VisualElement BarkCard(DialogueEntry start, int linkIndex, DialogueEntry line, List<string> priorityLabels)
        {
            var card = new VisualElement { name = $"bark-{line.id}" };
            card.AddToClassList("dlg-link");

            var head = new VisualElement();
            head.AddToClassList("dlg-inspector__row");
            var actor = m_Database.FindActor(line.actorId);
            var player = actor != null && actor.isPlayer;
            head.Add(new Label($"#{line.id}  {(actor != null ? m_Database.DisplayNameOf(actor) : "?")}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            head.Add(BarkEmotion(line, actor));
            var priority = new PopupField<string>(priorityLabels, LinkPriorityGUI.Label(start.links[linkIndex].priority)) { tooltip = "Among the lines that may be said now, the highest priority group wins." };
            priority.AddToClassList("dlg-link__priority");
            priority.RegisterValueChangedCallback(evt =>
            {
                Edit("Change Bark Priority", () => start.links[linkIndex].priority = (LinkPriority)priorityLabels.IndexOf(evt.newValue));
                LinksChanged?.Invoke();
            });
            head.Add(priority);
            var remove = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Remove bark line", $"Delete bark line #{line.id}?", "Delete", "Cancel")) return;
                Edit("Remove Bark Line", () => m_Database.RemoveEntry(m_Conversation.id, line.id));
                LinksChanged?.Invoke();
                Rebuild();
            }) { text = "✕", tooltip = "Delete this bark line" };
            remove.AddToClassList("dlg-inspector__remove");
            head.Add(remove);
            card.Add(head);
            if (player) card.Add(new HelpBox("Said by the player: barks only say NPC lines, so this one is never said.", HelpBoxMessageType.Warning));

            var text = new TextField { multiline = true, isDelayed = true, value = DialogueTextEditing.Read(line.text, m_Locale) ?? string.Empty };
            text.style.minHeight = 22;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.RegisterValueChangedCallback(evt =>
            {
                if (!Resolve()) return;
                var fresh = m_Conversation.FindEntry(line.id);
                if (fresh != null && DialogueTextEditing.Write(m_Database, m_Conversation, fresh, false, m_Locale, evt.newValue)) { EditorUtility.SetDirty(m_Database); EntryChanged?.Invoke(fresh); }
            });
            card.Add(text);
            if (line.LineCount > 1) card.Add(new Label($"+ {line.LineCount - 1} more line(s): select #{line.id} to edit them.") { style = { fontSize = 10, color = new Color(0.6f, 0.62f, 0.68f) } });

            card.Add(new Label("Says it when… (all must hold; empty = any time)") { style = { fontSize = 11, color = new Color(0.63f, 0.66f, 0.72f), marginTop = 2 } });
            BarkConditions(card, start, linkIndex);
            if (line.conditions.Count > 0)
                card.Add(new Label($"…and the line's own conditions: {string.Join(", ", line.conditions.Where(c => c != null).Select(c => c.Describe()))}") { style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, color = new Color(0.6f, 0.62f, 0.68f) } });
            return card;
        }

        /// <summary>The link's condition list (same data as the edge's conditions), editable here.</summary>
        private void BarkConditions(VisualElement card, DialogueEntry start, int linkIndex)
        {
            m_SerializedDatabase.Update();
            string linkPath = LinkEditorPopup.LinkPath(m_Database, m_Conversation, start, start.links[linkIndex]);
            var list = linkPath != null ? m_SerializedDatabase.FindProperty(linkPath + "." + nameof(DialogueLink.conditions)) : null;
            if (list == null) return;
            for (int i = 0; i < list.arraySize; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("dlg-inspector__row");
                var field = new PropertyField(list.GetArrayElementAtIndex(i), string.Empty);
                field.Bind(m_SerializedDatabase);
                field.RegisterValueChangeCallback(_ => LinksChanged?.Invoke());
                row.Add(field);
                var remove = new Button(() =>
                {
                    Edit("Remove Bark Condition", () => start.links[linkIndex].conditions.RemoveAt(index));
                    LinksChanged?.Invoke();
                    Rebuild();
                }) { text = "✕", tooltip = "Remove" };
                remove.AddToClassList("dlg-inspector__remove");
                row.Add(remove);
                card.Add(row);
            }
            var add = new Button { text = "+ condition" };
            add.AddToClassList("dlg-condlist__add");
            add.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var type in DialogueRowGUI.ConcreteTypes(typeof(DialogueCondition)))
                {
                    var t = type;
                    menu.AddItem(new GUIContent(DialogueRowGUI.TypeLabel(t)), false, () =>
                    {
                        Edit("Add Bark Condition", () => start.links[linkIndex].conditions.Add((DialogueCondition)Activator.CreateInstance(t)));
                        LinksChanged?.Invoke();
                        Rebuild();
                    });
                }
                menu.ShowAsContext();
            };
            card.Add(add);
        }

        private VisualElement BarkEmotion(DialogueEntry line, Actor actor)
        {
            var ids = new List<string> { string.Empty };
            if (actor != null) ids.AddRange(actor.emotions.Where(e => e != null && !string.IsNullOrEmpty(e.id)).Select(e => e.id).Distinct());
            string current = line.emotion ?? string.Empty;
            if (!ids.Contains(current)) ids.Add(current);
            string Label(string id) => string.IsNullOrEmpty(id) ? "(default)" : $"{DialogueEmotionGlyph.For(id)}  {id}";
            var labels = ids.Select(Label).ToList();
            var field = new PopupField<string>(labels, Label(current)) { tooltip = "Emotion of the line" };
            field.AddToClassList("dlg-line__emotion");
            int lineId = line.id;
            field.RegisterValueChangedCallback(evt =>
            {
                string picked = ids[labels.IndexOf(evt.newValue)];
                Edit("Change Emotion", () => { var fresh = m_Conversation.FindEntry(lineId); if (fresh != null) fresh.emotion = picked; });
                EntryChanged?.Invoke(m_Conversation.FindEntry(lineId));
            });
            return field;
        }

        /// <summary>A new NPC line linked from START (speaker: the existing bark lines', else the first non-player character).</summary>
        private void AddBarkLine()
        {
            if (!Resolve()) return;
            var start = m_Entry;
            var lines = start.links.Where(l => l.conversationId == m_Conversation.id).Select(l => m_Conversation.FindEntry(l.entryId)).Where(e => e != null).ToList();
            var speaker = lines.Select(e => m_Database.FindActor(e.actorId)).FirstOrDefault(a => a != null && !a.isPlayer)
                          ?? m_Database.AllActors.FirstOrDefault(a => a != null && !a.isPlayer);
            if (speaker == null)
            {
                Debug.LogWarning("[Dialogue Editor] Add a non-player character first: barks are said by NPCs.");
                return;
            }
            Edit("Add Bark Line", () =>
            {
                var line = m_Conversation.AddEntry(speaker.id);
                line.text.Text = "...";
                var below = lines.Count > 0 ? lines.Max(e => e.position.y) + 140f : start.position.y;
                line.position = new Vector2(start.position.x + 300f, below);
                m_Conversation.Link(start, line);
            });
            LinksChanged?.Invoke();
            Rebuild();
        }
    }
}
