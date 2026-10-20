using System;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>
    /// Extension hooks for plugins that must react to graph edits IN THE SAME undo step (e.g. a Switch generating its
    /// Case nodes). Handlers run inside the edit's <see cref="GraphUndoScope"/>, so whatever they change through the
    /// graph service is undone together with the edit — never as a separate step the user could undo on its own.
    /// </summary>
    public static class GraphEditHooks
    {
        /// <summary>A node was created by the user in the graph window (search popup, menu). Not raised for paste, AI import or bridge batches.</summary>
        public static event Action<BehaviorTreeGraphService, Node> NodeCreatedByUser;

        /// <summary>The Node Properties window applied changes to a node (inside that edit's undo step).</summary>
        public static event Action<BehaviorTreeGraphService, Node> NodePropertiesEdited;

        internal static void RaiseNodeCreatedByUser(BehaviorTreeGraphService graph, Node node) => NodeCreatedByUser?.Invoke(graph, node);
        internal static void RaiseNodePropertiesEdited(BehaviorTreeGraphService graph, Node node) => NodePropertiesEdited?.Invoke(graph, node);
    }
}
