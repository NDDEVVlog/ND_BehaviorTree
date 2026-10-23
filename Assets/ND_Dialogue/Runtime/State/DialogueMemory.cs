using System;
using System.Collections.Generic;

namespace ND.Dialogue
{
    /// <summary>
    /// What the conversation runner remembers besides variables: which entries were already shown or chosen
    /// ("seen"). Conditions read it through <see cref="IDialogueMemory"/> (e.g. a line that plays only the first time),
    /// and it is part of a save.
    /// </summary>
    public class DialogueMemory
    {
        private readonly HashSet<long> m_Seen = new HashSet<long>();

        /// <summary>(conversation id, entry id) — raised the first time an entry is seen.</summary>
        public event Action<int, int> EntrySeen;

        private static long Key(int conversationId, int entryId) => ((long)conversationId << 32) | (uint)entryId;

        public bool WasSeen(int conversationId, int entryId) => m_Seen.Contains(Key(conversationId, entryId));

        public void MarkSeen(int conversationId, int entryId)
        {
            if (m_Seen.Add(Key(conversationId, entryId))) EntrySeen?.Invoke(conversationId, entryId);
        }

        public int Count => m_Seen.Count;

        // Route state of entries that pick by Cycle / Shuffle: visits so far, and the targets left in the shuffled bag.
        private readonly Dictionary<long, int> m_Cycles = new Dictionary<long, int>();
        private readonly Dictionary<long, List<long>> m_Bags = new Dictionary<long, List<long>>();

        /// <summary>How many times the entry picked by Cycle before (then counts this visit).</summary>
        public int NextCycle(int conversationId, int entryId)
        {
            long key = Key(conversationId, entryId);
            m_Cycles.TryGetValue(key, out int visits);
            m_Cycles[key] = visits + 1;
            return visits;
        }

        /// <summary>The shuffled bag of the entry: (conversation, entry) keys of the targets not picked yet this round.</summary>
        public List<long> ShuffleBag(int conversationId, int entryId)
        {
            long key = Key(conversationId, entryId);
            if (!m_Bags.TryGetValue(key, out var bag)) m_Bags[key] = bag = new List<long>();
            return bag;
        }

        public static long TargetKey(int conversationId, int entryId) => Key(conversationId, entryId);

        /// <summary>Route state of every entry that has some (for saves).</summary>
        public IEnumerable<(int conversationId, int entryId, int cycle, List<long> bag)> Routes
        {
            get
            {
                var keys = new HashSet<long>(m_Cycles.Keys);
                keys.UnionWith(m_Bags.Keys);
                foreach (long key in keys)
                {
                    m_Cycles.TryGetValue(key, out int cycle);
                    m_Bags.TryGetValue(key, out var bag);
                    yield return ((int)(key >> 32), (int)(uint)key, cycle, bag != null ? new List<long>(bag) : new List<long>());
                }
            }
        }

        /// <summary>Puts back an entry's route state (loading a save).</summary>
        public void RestoreRoute(int conversationId, int entryId, int cycle, IEnumerable<long> bag)
        {
            long key = Key(conversationId, entryId);
            if (cycle > 0) m_Cycles[key] = cycle;
            var list = ShuffleBag(conversationId, entryId);
            list.Clear();
            if (bag != null) list.AddRange(bag);
        }

        /// <summary>Every seen entry as (conversation id, entry id).</summary>
        public IEnumerable<(int conversationId, int entryId)> Seen
        {
            get { foreach (long key in m_Seen) yield return ((int)(key >> 32), (int)(uint)key); }
        }

        public void Clear()
        {
            m_Seen.Clear();
            m_Cycles.Clear();
            m_Bags.Clear();
        }
    }

    /// <summary>
    /// Extra knowledge a condition can use while a conversation runs. The runner hands conditions and actions a
    /// context that implements it; outside a conversation it is not available.
    /// </summary>
    public interface IDialogueMemory
    {
        bool WasSeen(int conversationId, int entryId);
        /// <summary>The conversation being run (for "this conversation" references).</summary>
        int CurrentConversationId { get; }
    }
}
