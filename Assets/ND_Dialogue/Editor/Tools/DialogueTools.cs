using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ND.Localization;
using UnityEditor;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Whole-database utilities: find and replace in every line and choice label, statistics (lines and words per
    /// conversation and speaker, missing translations), and exports (CSV for translators or spreadsheets, a readable
    /// screenplay). Texts are read in one locale (plain text is the same in every locale).
    /// </summary>
    public static class DialogueTools
    {
        /// <summary>One text of the database: a line of an entry (0 = its text) or its choice label (<see cref="Line"/> = -1).</summary>
        public sealed class TextSlot
        {
            public Conversation Conversation;
            public DialogueEntry Entry;
            public int Line;
            public LocalizableString Text;
            public string Value;

            public bool IsChoiceLabel => Line < 0;
            public string Where => $"{Conversation.title} #{Entry.id}{(IsChoiceLabel ? " (choice label)" : Entry.LineCount > 1 ? $" line {Line + 1}" : string.Empty)}";
        }

        /// <summary>Every line and choice label (read in <paramref name="locale"/>; empty ones included with a null value).</summary>
        public static List<TextSlot> Texts(DialogueDatabase database, string locale)
        {
            var slots = new List<TextSlot>();
            if (database == null) return slots;
            foreach (var conversation in database.conversations)
            {
                if (conversation == null) continue;
                foreach (var entry in conversation.entries)
                {
                    if (entry == null || entry.id == Conversation.StartEntryId) continue;
                    for (int i = 0; i < entry.LineCount; i++)
                        slots.Add(new TextSlot { Conversation = conversation, Entry = entry, Line = i, Text = entry.LineText(i), Value = Read(entry.LineText(i), locale) });
                    if (entry.menuText != null && entry.menuText.HasContent)
                        slots.Add(new TextSlot { Conversation = conversation, Entry = entry, Line = -1, Text = entry.menuText, Value = Read(entry.menuText, locale) });
                }
            }
            return slots;
        }

        /// <summary>The text in <paramref name="locale"/> (plain text: always); null when empty or when no locale is chosen for a connected text.</summary>
        private static string Read(LocalizableString text, string locale) =>
            text != null && text.IsLocalized && string.IsNullOrEmpty(locale) ? null : DialogueTextEditing.Read(text, locale);

        #region Find and replace

        public static List<TextSlot> Find(DialogueDatabase database, string query, bool matchCase, string locale)
        {
            if (string.IsNullOrEmpty(query)) return new List<TextSlot>();
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return Texts(database, locale).Where(t => t.Value != null && t.Value.IndexOf(query, comparison) >= 0).ToList();
        }

        /// <summary>Replaces <paramref name="query"/> in every text that has it (connected texts: in <paramref name="locale"/>). One undo step. Returns how many texts changed.</summary>
        public static int ReplaceAll(DialogueDatabase database, string query, string replacement, bool matchCase, string locale)
        {
            var hits = Find(database, query, matchCase, locale);
            if (hits.Count == 0) return 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var pattern = new Regex(Regex.Escape(query), matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
            int changed = 0;
            foreach (var hit in hits)
            {
                string updated = pattern.Replace(hit.Value, (replacement ?? string.Empty).Replace("$", "$$"));
                if (DialogueTextEditing.Write(database, hit.Text, locale, updated, "Replace Dialogue Text")) changed++;
            }
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName($"Replace '{query}' ({changed})");
            return changed;
        }

        #endregion

        #region Statistics

        public sealed class Stats
        {
            public sealed class Row
            {
                public string Name;
                public int Entries;
                public int Lines;
                public int Choices;
                public int Words;
            }

            public readonly List<Row> Conversations = new List<Row>();
            public readonly List<Row> Speakers = new List<Row>();
            public int Lines, Choices, Words;
            /// <summary>Texts connected to a table without a translation in the locale.</summary>
            public readonly List<TextSlot> Missing = new List<TextSlot>();
            /// <summary>Texts that are still plain (not connected to a table: they are not translated).</summary>
            public int Plain;
        }

        public static int WordCount(string text) => string.IsNullOrWhiteSpace(text) ? 0 : Regex.Matches(text, @"[\p{L}\p{N}']+").Count;

        public static Stats Statistics(DialogueDatabase database, string locale)
        {
            var stats = new Stats();
            var speakers = new Dictionary<string, Stats.Row>();
            foreach (var slot in Texts(database, locale))
            {
                var conversationRow = stats.Conversations.FirstOrDefault(r => r.Name == slot.Conversation.title);
                if (conversationRow == null) stats.Conversations.Add(conversationRow = new Stats.Row { Name = slot.Conversation.title, Entries = slot.Conversation.entries.Count - 1 });
                var actor = database.FindActor(slot.Entry.actorId);
                string speaker = actor != null ? actor.name : "?";
                if (!speakers.TryGetValue(speaker, out var speakerRow)) speakers[speaker] = speakerRow = new Stats.Row { Name = speaker };
                int words = WordCount(slot.Value);
                if (!slot.IsChoiceLabel)
                {
                    conversationRow.Lines++; speakerRow.Lines++; stats.Lines++;
                    conversationRow.Words += words; speakerRow.Words += words; stats.Words += words;
                }
                if (actor != null && actor.isPlayer && slot.Line == 0) { conversationRow.Choices++; speakerRow.Choices++; stats.Choices++; } // one per player entry
                if (slot.Text.IsLocalized) { if (slot.Value == null) stats.Missing.Add(slot); }
                else if (!string.IsNullOrEmpty(slot.Text.Text)) stats.Plain++;
            }
            foreach (var conversation in database.conversations)
                if (conversation != null && stats.Conversations.All(r => r.Name != conversation.title))
                    stats.Conversations.Add(new Stats.Row { Name = conversation.title, Entries = conversation.entries.Count - 1 });
            stats.Speakers.AddRange(speakers.Values.OrderByDescending(r => r.Lines));
            return stats;
        }

        #endregion

        #region Export

        /// <summary>One row per line / choice label: conversation, entry, line, speaker, emotion, kind, text (RFC 4180 quoting).</summary>
        public static string ExportCsv(DialogueDatabase database, string locale)
        {
            var sb = new StringBuilder("conversation,entry,line,speaker,emotion,kind,text\n");
            foreach (var slot in Texts(database, locale))
            {
                var actor = database.FindActor(slot.Entry.actorId);
                string kind = slot.IsChoiceLabel ? "choice label" : actor != null && actor.isPlayer ? "player line" : "line";
                string emotion = slot.IsChoiceLabel ? string.Empty : slot.Entry.LineEmotion(slot.Line);
                sb.Append(Csv(slot.Conversation.title)).Append(',').Append(slot.Entry.id).Append(',').Append(slot.IsChoiceLabel ? string.Empty : (slot.Line + 1).ToString())
                  .Append(',').Append(Csv(actor != null ? actor.name : "?")).Append(',').Append(Csv(emotion)).Append(',').Append(kind)
                  .Append(',').Append(Csv(slot.Value ?? string.Empty)).Append('\n');
            }
            return sb.ToString();
        }

        private static string Csv(string value) => value == null ? string.Empty :
            value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        /// <summary>
        /// Every conversation as a script: <c>MALFOY (Angry): line</c>, the player's replies as <c>→ choice</c>, routing
        /// entries as notes, and where each entry goes next — to read through or send to a voice actor.
        /// </summary>
        public static string ExportScreenplay(DialogueDatabase database, string locale)
        {
            var sb = new StringBuilder();
            foreach (var conversation in database.conversations.Where(c => c != null))
            {
                sb.Append("== ").Append(conversation.title).Append(conversation.isBark ? "  (barks)" : string.Empty).Append(" ==\n");
                if (!string.IsNullOrEmpty(conversation.description)) sb.Append(conversation.description).Append('\n');
                sb.Append('\n');
                foreach (var entry in conversation.entries.Where(e => e != null && e.id != Conversation.StartEntryId))
                {
                    var actor = database.FindActor(entry.actorId);
                    if (entry.isPassthrough) sb.Append($"#{entry.id}  [{(entry.IsHub ? "hub" : "routing")}{(string.IsNullOrEmpty(entry.title) ? string.Empty : ": " + entry.title)}]\n");
                    else if (actor != null && actor.isPlayer)
                        sb.Append($"#{entry.id}  → {Read(entry.menuText, locale) ?? Read(entry.text, locale)}\n");
                    else
                        for (int i = 0; i < entry.LineCount; i++)
                        {
                            string emotion = entry.LineEmotion(i);
                            string who = (actor != null ? database.DisplayNameOf(actor) : "?").ToUpperInvariant();
                            sb.Append(i == 0 ? $"#{entry.id}  " : new string(' ', entry.id.ToString().Length + 3))
                              .Append(who).Append(string.IsNullOrEmpty(emotion) ? string.Empty : $" ({emotion})").Append(": ")
                              .Append(Read(entry.LineText(i), locale) ?? "…").Append('\n');
                        }
                    var next = entry.links.Where(l => l.conversationId == conversation.id).Select(l => "#" + l.entryId).ToList();
                    if (next.Count > 1) sb.Append("      ⤷ ").Append(string.Join(", ", next)).Append('\n');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        #endregion
    }
}
