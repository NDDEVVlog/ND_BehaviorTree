using System;
using System.Collections.Generic;
using ND.Localization;
using UnityEngine;
using UnityEngine.Serialization;

namespace ND.Dialogue
{
    /// <summary>Order in which links are tried. Only the highest priority with at least one valid link is used.</summary>
    public enum LinkPriority { Low, BelowNormal, Normal, AboveNormal, High }

    /// <summary>
    /// How an entry picks ONE next line when several NPC lines (or passthroughs) are valid in its highest priority group,
    /// like the composites of ND Behavior Tree. Player responses are never picked: they still make the choice menu.
    /// </summary>
    public enum RouteMode
    {
        /// <summary>The first valid link (by link order).</summary>
        FirstValid,
        /// <summary>Any valid link, each as likely (Random Selector).</summary>
        Random,
        /// <summary>A valid link with probability proportional to its weight (Random Rate + Weight).</summary>
        WeightedRandom,
        /// <summary>Each valid link in turn, one per visit, then again from the first.</summary>
        Cycle,
        /// <summary>Every valid link once in random order before any repeats (a shuffled bag).</summary>
        Shuffle,
        /// <summary>The valid link with the highest weight, e.g. read from a variable (Utility Selector).</summary>
        Utility
    }

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
        [Tooltip("Editor only: points the edge bends through on the graph.")]
        [HideInInspector] public List<Vector2> bends = new List<Vector2>();
        [Tooltip("Used when the entry picks by Weighted Random (relative chance; 0 or less = never) or Utility (score; the highest wins).")]
        public float weight = 1f;
        [Tooltip("Optional: the weight is this number variable (or attribute) times Weight, read when the entry picks.")]
        [DialogueVariable] public string weightVariable;
    }

    /// <summary>One more line of the same speaker inside an entry, with its own emotion (shown after the previous one on Continue).</summary>
    [Serializable]
    public class DialoguePage
    {
        [Tooltip("The line. Plain text, or connected to a table to translate it.")]
        public LocalizableString text = new LocalizableString();
        [Tooltip("Emotion for this line. Empty = the entry's emotion.")]
        public string emotion;
        [Tooltip("Voice-over of this line (empty = none). The localized one wins when set.")]
        public AudioClip voice;
        [Tooltip("Voice-over per language (an Asset Table entry). Empty = Voice.")]
        public LocalizedAudioClip localizedVoice = new LocalizedAudioClip();
        [Tooltip("Voice from an audio middleware (e.g. an FMOD event), played by a matching Dialogue Voice Player.")]
        [SerializeReference] public DialogueVoiceCue voiceCue;
    }

    /// <summary>
    /// One node of a conversation: what <see cref="actorId"/> says — <see cref="text"/>, then each of
    /// <see cref="moreLines"/> in turn (Continue steps through them) — before the links are followed. A line of the
    /// player actor is shown as a choice (<see cref="menuText"/>, or <see cref="text"/> when empty). A passthrough
    /// entry shows nothing and only routes: its conditions pick a branch and its actions run.
    /// <para>Derive from it for kinds of entries with their own data or behaviour (<c>class ShopEntry : DialogueEntry</c>):
    /// conversations store entries polymorphically, the editor's "New…" menu lists every subclass, and
    /// <see cref="OnEnter"/> runs after the actions. Keep subclasses [Serializable] with a parameterless constructor.</para>
    /// </summary>
    [Serializable]
    public class DialogueEntry
    {
        public int id;
        [Tooltip("Label in the editor only.")]
        public string title;
        public int actorId;
        [Tooltip("The (first) line. Plain text, or connected to a table to translate it.")]
        public LocalizableString text = new LocalizableString();
        [Tooltip("Choice label when this is a player response. Empty = use Text.")]
        public LocalizableString menuText = new LocalizableString();
        [Tooltip("Emotion of the speaker for this line. Empty = the actor's default emotion.")]
        public string emotion;
        [Tooltip("Voice-over of the (first) line (empty = none). The localized one wins when set.")]
        public AudioClip voice;
        [Tooltip("Voice-over per language (an Asset Table entry). Empty = Voice.")]
        public LocalizedAudioClip localizedVoice = new LocalizedAudioClip();
        [Tooltip("Voice of the (first) line from an audio middleware (e.g. an FMOD event), played by a matching Dialogue Voice Player.")]
        [SerializeReference] public DialogueVoiceCue voiceCue;
        [Tooltip("More lines the same speaker says here, one after another, each with its own emotion.")]
        public List<DialoguePage> moreLines = new List<DialoguePage>();
        [Tooltip("No line is shown: the entry only runs its actions and continues.")]
        public bool isPassthrough;
        [Tooltip("Sequence played when the entry starts (camera, animation, sound…): Name(args)@seconds, separated by ; or new lines. Needs a Dialogue Sequencer in the scene. E.g. Anim(speaker, Blush); Camera(CloseUp)@0.5")]
        [TextArea(1, 6)] public string sequence;
        [Tooltip("How ONE next line is picked when several NPC lines are valid: first, random, weighted, in turn, shuffled or by highest weight. Player responses still make the menu.")]
        public RouteMode routeMode = RouteMode.FirstValid;
        [Tooltip("Hub (with Passthrough): merges links. It is never entered: a link into it counts as a link to each of its targets (its own links' conditions and priorities apply). Use it when many lines lead to the same replies.")]
        public bool isHub;

        /// <summary>A hub is never entered: links into it stand for links to each of its targets (see <see cref="isHub"/>).</summary>
        public bool IsHub => isHub && isPassthrough && id != Conversation.StartEntryId;

        [Tooltip("All must be true for this entry to be reachable.")]
        [SerializeReference] public List<DialogueCondition> conditions = new List<DialogueCondition>();
        [Tooltip("Run in order when the entry is entered.")]
        [SerializeReference] public List<DialogueAction> actions = new List<DialogueAction>();
        public List<DialogueLink> links = new List<DialogueLink>();

        [Tooltip("Graph editor position.")]
        public Vector2 position;

        /// <summary>How many lines are shown: 0 for passthrough, else the first line plus <see cref="moreLines"/>.</summary>
        public int LineCount => isPassthrough ? 0 : 1 + (moreLines?.Count ?? 0);

        /// <summary>Text of line <paramref name="index"/> (0 = <see cref="text"/>).</summary>
        public LocalizableString LineText(int index) => index == 0 ? text : moreLines[index - 1]?.text;

        /// <summary>Voice of line <paramref name="index"/> (0 = the first): its localized clip when set, else its clip; null = none.</summary>
        public AudioClip LineVoice(int index, Func<LocalizedAudioClip, AudioClip> resolveLocalized = null)
        {
            var page = index == 0 ? null : moreLines[index - 1];
            var localized = index == 0 ? localizedVoice : page?.localizedVoice;
            if (localized != null && !localized.IsEmpty) return resolveLocalized != null ? resolveLocalized(localized) : localized.Resolve();
            return index == 0 ? voice : page?.voice;
        }

        /// <summary>Middleware voice cue of line <paramref name="index"/> (0 = the first), or null.</summary>
        public DialogueVoiceCue LineVoiceCue(int index) => index == 0 ? voiceCue : moreLines[index - 1]?.voiceCue;

        /// <summary>Emotion of line <paramref name="index"/>: its own, else the entry's.</summary>
        public string LineEmotion(int index)
        {
            if (index == 0) return emotion;
            var page = moreLines[index - 1];
            return page != null && !string.IsNullOrEmpty(page.emotion) ? page.emotion : emotion;
        }

        /// <summary>Shown at the top of the entry inspector for subclasses that work differently.</summary>
        public virtual string EditorNote => null;

        /// <summary>Called when the runner enters this entry, after its actions. Override in subclasses.</summary>
        public virtual void OnEnter(IDialogueContext context) { }
    }

    [Serializable]
    public class Conversation : ISerializationCallbackReceiver
    {
        public const int StartEntryId = 0;
        /// <summary>Serialized name of the entry list (for SerializedProperty paths).</summary>
        public const string EntriesField = nameof(m_Entries);

        public int id;
        public string title;
        [TextArea] public string description;
        [Tooltip("A bark conversation: a Dialogue Barker says ONE of its START lines in a bubble while the game goes on (no choices, nothing waits). Off = a normal conversation, played in the dialogue UI.")]
        public bool isBark;
        [SerializeReference] private List<DialogueEntry> m_Entries = new List<DialogueEntry>();
        // Databases saved before entries could be subclassed stored them by value under "entries"; moved on load.
        [SerializeField, HideInInspector, FormerlySerializedAs("entries")] private List<DialogueEntry> m_LegacyEntries = new List<DialogueEntry>();
        [SerializeField] private int m_NextEntryId = 1;

        [NonSerialized] private Dictionary<int, DialogueEntry> m_ById;

        /// <summary>Every entry (START first). Entries may be subclasses of <see cref="DialogueEntry"/>.</summary>
        public List<DialogueEntry> entries => m_Entries;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_LegacyEntries == null || m_LegacyEntries.Count == 0) return;
            if (m_Entries == null) m_Entries = new List<DialogueEntry>();
            if (m_Entries.Count == 0) m_Entries.AddRange(m_LegacyEntries);
            m_LegacyEntries.Clear();
            m_ById = null;
        }

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

        public DialogueEntry AddEntry(int actorId, string entryTitle = null) => AddEntry(typeof(DialogueEntry), actorId, entryTitle);

        /// <summary>Adds an entry of a subclass (<c>AddEntry&lt;ShopEntry&gt;(merchant.id)</c>) with the next id.</summary>
        public T AddEntry<T>(int actorId, string entryTitle = null) where T : DialogueEntry, new() => (T)AddEntry(typeof(T), actorId, entryTitle);

        /// <summary>Adds an entry of <paramref name="entryType"/> (a <see cref="DialogueEntry"/> with a parameterless constructor) with the next id.</summary>
        public DialogueEntry AddEntry(Type entryType, int actorId, string entryTitle = null)
        {
            if (entryType == null || !typeof(DialogueEntry).IsAssignableFrom(entryType) || entryType.IsAbstract || entryType.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogError($"[Dialogue] Conversation '{title}': '{entryType?.FullName}' is not a concrete DialogueEntry with a parameterless constructor.");
                return null;
            }
            var entry = (DialogueEntry)Activator.CreateInstance(entryType);
            entry.id = m_NextEntryId++;
            entry.actorId = actorId;
            entry.title = entryTitle;
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
