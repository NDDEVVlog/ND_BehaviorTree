using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>Order in which links are tried. Only the highest priority with at least one valid link is used.</summary>
    public enum LinkPriority { Low, BelowNormal, Normal, AboveNormal, High }

    /// <summary>
    /// A branch from one entry to another. It is taken only when its own <see cref="conditions"/> AND the target entry's
    /// conditions hold: put per-branch rules here (shown on the edge), and rules for every way in on the entry.
    /// </summary>
    [Serializable]
    public class DialogueLink
    {
        [Tooltip("Conversation of the destination entry (the same conversation for ordinary links).")]
        public int conversationId;
        public int entryId;
        public LinkPriority priority = LinkPriority.Normal;
        [Tooltip("All must hold for this branch (in addition to the target entry's own conditions).")]
        [SerializeReference] public List<DialogueCondition> conditions = new List<DialogueCondition>();
    }

    /// <summary>
    /// One node of a conversation: a single line spoken by <see cref="actorId"/>. A line of the player actor is shown
    /// as a choice (<see cref="menuText"/>, or <see cref="text"/> when empty). A passthrough entry shows nothing and
    /// only routes: its conditions pick a branch and its actions run.
    /// </summary>
    [Serializable]
    public class DialogueEntry
    {
        public int id;
        [Tooltip("Label in the editor only.")]
        public string title;
        public int actorId;
        [Tooltip("The line. Plain text, or connected to a table to translate it.")]
        public LocalizableString text = new LocalizableString();
        [Tooltip("Choice label when this is a player response. Empty = use Text.")]
        public LocalizableString menuText = new LocalizableString();
        [Tooltip("Emotion of the speaker for this line. Empty = the actor's default emotion.")]
        public string emotion;
        [Tooltip("No line is shown: the entry only runs its actions and continues.")]
        public bool isPassthrough;

        [Tooltip("All must be true for this entry to be reachable.")]
        [SerializeReference] public List<DialogueCondition> conditions = new List<DialogueCondition>();
        [Tooltip("Run in order when the entry is entered.")]
        [SerializeReference] public List<DialogueAction> actions = new List<DialogueAction>();
        public List<DialogueLink> links = new List<DialogueLink>();

        [Tooltip("Graph editor position.")]
        public Vector2 position;
    }

    [Serializable]
    public class Conversation
    {
        public const int StartEntryId = 0;

        public int id;
        public string title;
        [TextArea] public string description;
        public List<DialogueEntry> entries = new List<DialogueEntry>();
        [SerializeField] private int m_NextEntryId = 1;

        [NonSerialized] private Dictionary<int, DialogueEntry> m_ById;

        public DialogueEntry StartEntry => FindEntry(StartEntryId);

        public DialogueEntry FindEntry(int entryId)
        {
            if (m_ById == null)
            {
                m_ById = new Dictionary<int, DialogueEntry>(entries.Count);
                foreach (var e in entries)
                    if (e != null && !m_ById.ContainsKey(e.id)) m_ById[e.id] = e;
            }
            m_ById.TryGetValue(entryId, out var entry);
            return entry;
        }

        /// <summary>Creates the START entry (a passthrough with id 0). Called once when the conversation is created.</summary>
        public DialogueEntry EnsureStartEntry()
        {
            var start = StartEntry;
            if (start != null) return start;
            start = new DialogueEntry { id = StartEntryId, title = "START", isPassthrough = true };
            entries.Insert(0, start);
            InvalidateCache();
            return start;
        }

        public DialogueEntry AddEntry(int actorId, string entryTitle = null)
        {
            var entry = new DialogueEntry { id = m_NextEntryId++, actorId = actorId, title = entryTitle };
            entries.Add(entry);
            InvalidateCache();
            return entry;
        }

        /// <summary>Adds an entry with a known id (importers keep the source tool's ids).</summary>
        public DialogueEntry AddEntry(int actorId, int entryId, string entryTitle)
        {
            if (FindEntry(entryId) != null)
            {
                Debug.LogError($"[Dialogue] Conversation '{title}': entry id {entryId} already exists.");
                return null;
            }
            var entry = new DialogueEntry { id = entryId, actorId = actorId, title = entryTitle };
            entries.Add(entry);
            m_NextEntryId = Math.Max(m_NextEntryId, entryId + 1);
            InvalidateCache();
            return entry;
        }

        /// <summary>
        /// Removes an entry and the links in THIS conversation that pointed at it. START cannot be removed.
        /// Use <see cref="DialogueDatabase.RemoveEntry"/>, which also cleans links from other conversations.
        /// </summary>
        internal bool RemoveEntry(int entryId)
        {
            if (entryId == StartEntryId) return false;
            var entry = FindEntry(entryId);
            if (entry == null) return false;
            entries.Remove(entry);
            foreach (var e in entries)
                e.links.RemoveAll(l => l.conversationId == id && l.entryId == entryId);
            InvalidateCache();
            return true;
        }

        public DialogueLink Link(DialogueEntry from, DialogueEntry to, LinkPriority priority = LinkPriority.Normal)
        {
            var link = new DialogueLink { conversationId = id, entryId = to.id, priority = priority };
            from.links.Add(link);
            return link;
        }

        public void InvalidateCache() => m_ById = null;
    }
}
