using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Story.Editor
{
    /// <summary>
    /// Keeps a Switch's Case nodes in line with its <see cref="StorySwitchNode.cases"/> list (+ Default):
    /// creates missing cases, renames in place (the branch below stays connected), removes extra cases (their branches
    /// stay on the graph, unconnected) and restores the list order.
    /// <see cref="StoryNodesPlugin"/> runs it inside the edit that changed the Switch (creation in the graph window,
    /// Node Properties edits), so undo reverts the edit and its cases together.
    /// </summary>
    public static class StorySwitchCases
    {
        private readonly struct Wanted
        {
            public readonly string Value;
            public readonly bool IsDefault;
            public Wanted(string value, bool isDefault) { Value = value; IsDefault = isDefault; }
        }

        private static List<Wanted> Desired(StorySwitchNode storySwitch)
        {
            var comparer = storySwitch.ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var list = storySwitch.cases.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(comparer)
                .Select(c => new Wanted(c, false)).ToList();
            if (storySwitch.includeDefault) list.Add(new Wanted(null, true));
            return list;
        }

        private static bool Matches(StoryCaseNode node, Wanted wanted, StringComparison comparison) =>
            node.isDefault == wanted.IsDefault && (wanted.IsDefault || string.Equals(node.caseValue, wanted.Value, comparison));

        /// <summary>
        /// Appends a case with a fresh name ("Case 1", "Case 2"…) and creates its Case node: one undo step.
        /// (The list's "+" in the properties window duplicates the last entry, which is skipped as a duplicate.)
        /// </summary>
        public static string AddCase(BehaviorTreeGraphService graph, StorySwitchNode storySwitch)
        {
            var comparer = storySwitch.ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var used = new HashSet<string>(storySwitch.cases.Where(c => c != null).Select(c => c.Trim()), comparer);
            int n = storySwitch.cases.Count + 1;
            while (used.Contains($"Case {n}")) n++;
            string name = $"Case {n}";

            using (new GraphUndoScope(graph.Tree, "Add Switch Case"))
            {
                storySwitch.cases.Add(name);
                EditorUtility.SetDirty(storySwitch);
                Sync(graph, storySwitch);
            }
            return name;
        }

        /// <summary>Entries that produce no case: blank, or a repeat of an earlier entry (as compared by the Switch).</summary>
        public static List<string> IgnoredEntries(StorySwitchNode storySwitch)
        {
            var comparer = storySwitch.ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var seen = new HashSet<string>(comparer);
            var ignored = new List<string>();
            foreach (var entry in storySwitch.cases)
            {
                if (string.IsNullOrWhiteSpace(entry)) ignored.Add("(empty)");
                else if (!seen.Add(entry.Trim())) ignored.Add(entry.Trim());
            }
            return ignored;
        }

        /// <summary>True when the Switch's children are exactly the wanted cases, in order.</summary>
        public static bool InSync(StorySwitchNode storySwitch)
        {
            var wanted = Desired(storySwitch);
            var comparison = storySwitch.ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (storySwitch.children.Count != wanted.Count) return false;
            for (int i = 0; i < wanted.Count; i++)
                if (!(storySwitch.children[i] is StoryCaseNode node) || !Matches(node, wanted[i], comparison)) return false;
            return true;
        }

        /// <summary>Brings the Case nodes in line with the list. Returns true if anything changed (one undo step, nested in the caller's).</summary>
        public static bool Sync(BehaviorTreeGraphService graph, StorySwitchNode storySwitch)
        {
            if (storySwitch == null || InSync(storySwitch)) return false;
            var comparison = storySwitch.ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var wanted = Desired(storySwitch);

            using (new GraphUndoScope(graph.Tree, "Sync Switch Cases"))
            {
                // Only Case nodes belong under a Switch: anything else is detached (kept on the graph).
                foreach (var stray in storySwitch.children.Where(c => !(c is StoryCaseNode)).ToList())
                {
                    Debug.LogWarning($"[StorySwitchCases] '{storySwitch.name}': '{stray.name}' is not a Case node and was detached from the Switch.");
                    graph.Disconnect(storySwitch, stray);
                }

                var existing = storySwitch.children.OfType<StoryCaseNode>().ToList();
                var result = new StoryCaseNode[wanted.Count];

                // 1. Exact matches keep their branch.
                for (int i = 0; i < wanted.Count; i++)
                {
                    var match = existing.FirstOrDefault(c => Matches(c, wanted[i], comparison));
                    if (match == null) continue;
                    result[i] = match;
                    existing.Remove(match);
                }

                // 2. Renamed cases: reuse a leftover case of the same kind (prefer the same position), so its branch stays.
                for (int i = 0; i < wanted.Count; i++)
                {
                    if (result[i] != null) continue;
                    var reuse = existing.Where(c => c.isDefault == wanted[i].IsDefault)
                        .OrderBy(c => Math.Abs(storySwitch.children.IndexOf(c) - i)).FirstOrDefault();
                    if (reuse == null) continue;
                    reuse.caseValue = wanted[i].Value;
                    reuse.typeName = string.Empty;
                    EditorUtility.SetDirty(reuse);
                    result[i] = reuse;
                    existing.Remove(reuse);
                }

                // 3. Cases still missing are created under the Switch.
                for (int i = 0; i < wanted.Count; i++)
                {
                    if (result[i] != null) continue;
                    var position = storySwitch.position.position + new Vector2((i - (wanted.Count - 1) / 2f) * 220f, 150f);
                    var created = (StoryCaseNode)graph.CreateNode(typeof(StoryCaseNode), position);
                    if (created == null) return false; // CreateNode logged why
                    created.caseValue = wanted[i].Value;
                    created.isDefault = wanted[i].IsDefault;
                    EditorUtility.SetDirty(created);
                    graph.Connect(storySwitch, created, allowOwned: true);
                    result[i] = created;
                }

                // 4. Leftovers are removed (their branches stay on the graph, unconnected); then restore the order.
                if (existing.Count > 0) graph.DeleteNodes(existing);
                for (int i = 0; i < result.Length; i++) graph.MoveChild(storySwitch, result[i], i);
            }
            return true;
        }
    }
}
