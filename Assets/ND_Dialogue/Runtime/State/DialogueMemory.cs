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

        /// <summary>Every seen entry as (conversation id, entry id).</summary>
        public IEnumerable<(int conversationId, int entryId)> Seen
        {
            get { foreach (long key in m_Seen) yield return ((int)(key >> 32), (int)(uint)key); }
        }

        public void Clear() => m_Seen.Clear();
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
