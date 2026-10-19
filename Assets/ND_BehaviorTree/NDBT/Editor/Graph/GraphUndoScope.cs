using System;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// One completed user operation = one step in the tree's <see cref="GraphHistory"/>.
    /// Scopes nest: only the outermost one records a step. Edits made inside must NOT use Unity's Undo
    /// (the graph has its own history; mixing both would make Ctrl+Z ambiguous).
    /// </summary>
    public sealed class GraphUndoScope : IDisposable
    {
        private readonly GraphHistory m_History;

        /// <param name="coalesceKey">Consecutive steps with the same key within GraphHistory.CoalesceSeconds merge into one.</param>
        public GraphUndoScope(BehaviorTree tree, string operationName, string coalesceKey = null)
        {
            m_History = GraphHistory.For(tree);
            m_History.Begin(operationName, coalesceKey);
        }

        public void Dispose() => m_History.End();
    }
}
