using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// The variables and quests a conversation reads (entry and link conditions) and writes (actions), found through
    /// the <see cref="DialogueVariableAttribute"/> / <see cref="DialogueQuestAttribute"/> fields of every condition and
    /// action — custom ones and nested lists ("any of") included.
    /// </summary>
    public static class DialogueValueUses
    {
        public sealed class Use
        {
            /// <summary>The variable, or "quest name" (the form <see cref="DialogueTestSession.SetValue"/> takes).</summary>
            public string Key;
            public string Name;
            public bool IsQuest;
            public readonly SortedSet<int> ReadBy = new SortedSet<int>();
            public readonly SortedSet<int> WrittenBy = new SortedSet<int>();
        }

        private const int MaxDepth = 8;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Every value <paramref name="conversation"/> uses, variables first, each sorted by name.</summary>
        public static List<Use> Collect(Conversation conversation)
        {
            var uses = new Dictionary<string, Use>();
            if (conversation == null) return new List<Use>();
            foreach (var entry in conversation.entries)
            {
                if (entry == null) continue;
                foreach (var c in entry.conditions) Visit(c, entry.id, false, uses, 0);
                foreach (var link in entry.links)
                    foreach (var c in link.conditions) Visit(c, entry.id, false, uses, 0);
                foreach (var a in entry.actions) Visit(a, entry.id, true, uses, 0);
            }
            return uses.Values.OrderBy(u => u.IsQuest).ThenBy(u => u.Name).ToList();
        }

        private static void Visit(object node, int entryId, bool write, Dictionary<string, Use> uses, int depth)
        {
            if (node == null || depth > MaxDepth) return;
            for (var type = node.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(Fields))
                {
                    object value = field.GetValue(node);
                    if (value is string name)
                    {
                        bool variable = field.IsDefined(typeof(DialogueVariableAttribute), true);
                        bool quest = field.IsDefined(typeof(DialogueQuestAttribute), true);
                        if ((variable || quest) && !string.IsNullOrEmpty(name)) Note(uses, name, quest, entryId, write);
                    }
                    else if (value is DialogueCondition || value is DialogueAction) Visit(value, entryId, write, uses, depth + 1);
                    else if (value is IList list)
                        foreach (var item in list)
                            if (item is DialogueCondition || item is DialogueAction) Visit(item, entryId, write, uses, depth + 1);
                }
            }
        }

        private static void Note(Dictionary<string, Use> uses, string name, bool quest, int entryId, bool write)
        {
            string key = quest ? "quest " + name : name;
            if (!uses.TryGetValue(key, out var use)) uses[key] = use = new Use { Key = key, Name = name, IsQuest = quest };
            (write ? use.WrittenBy : use.ReadBy).Add(entryId);
        }
    }
}
