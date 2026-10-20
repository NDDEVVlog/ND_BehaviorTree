using System.Collections.Generic;
using System.Linq;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Execution-order contract (editor side).
    ///
    /// Runtime: a composite runs its children strictly in <see cref="CompositeNode.children"/> list order.
    /// Runtime code never reads node positions.
    ///
    /// Editor: the list is kept sorted left-to-right by node X position. The sort is applied by
    /// <see cref="BehaviorTreeGraphService"/> at explicit commit points only:
    ///   - when a child is connected,
    ///   - when a node drag ENDS (not while dragging),
    ///   - when nodes are pasted,
    ///   - when the user runs "Sort Children Left-to-Right".
    /// Each commit is a single undoable operation. Ties on X keep their previous relative order (stable sort).
    /// Assets saved before this contract may hold a different order; they are reported by
    /// <see cref="GraphValidator"/> and are NOT silently re-sorted on open.
    /// </summary>
    public static class ExecutionOrder
    {
        /// <summary>
        /// Sorts a composite's children left-to-right, except for <see cref="IOwnedChildren"/> composites (Block rows,
        /// Switch cases), whose order is authored explicitly. Returns true if the order changed.
        /// </summary>
        public static bool SortChildren(CompositeNode composite) =>
            composite != null && !(composite is IOwnedChildren) && SortByPosition(composite.children);

        /// <summary>Sorts in place. Returns true if the order changed. Null entries move to the end.</summary>
        public static bool SortByPosition(List<Node> children)
        {
            if (children == null || children.Count < 2) return false;

            var sorted = children
                .Select((node, index) => (node, index))
                .OrderBy(t => t.node == null ? float.PositiveInfinity : t.node.position.x)
                .ThenBy(t => t.index)
                .Select(t => t.node)
                .ToList();

            if (sorted.SequenceEqual(children)) return false;

            children.Clear();
            children.AddRange(sorted);
            return true;
        }

        public static bool IsSortedByPosition(IReadOnlyList<Node> children)
        {
            if (children == null) return true;
            for (int i = 1; i < children.Count; i++)
            {
                if (children[i - 1] == null || children[i] == null) continue;
                if (children[i - 1].position.x > children[i].position.x) return false;
            }
            return true;
        }
    }
}
