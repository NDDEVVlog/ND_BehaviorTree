using System.Collections.Generic;

namespace ND.GraphUI
{
    /// <summary>
    /// <see cref="GraphCanvas"/> with typed bookkeeping: node views by id, edge views, and the selection.
    /// A concrete canvas draws its data with <see cref="RegisterNodeView"/> / <see cref="ConnectViews"/> and
    /// implements the data hooks (<see cref="GraphCanvas.TryConnect"/>, <see cref="GraphCanvas.CommitNodeMove"/>…).
    /// </summary>
    public abstract class GraphCanvas<TNode, TEdge> : GraphCanvas
        where TNode : GraphNodeView
        where TEdge : GraphEdgeView
    {
        protected readonly Dictionary<string, TNode> m_NodeDictionary = new Dictionary<string, TNode>();
        protected readonly List<TEdge> m_Edges = new List<TEdge>();

        public List<TNode> SelectedNodes { get; } = new List<TNode>();

        public override IEnumerable<GraphNodeView> NodeViews => m_NodeDictionary.Values;
        public override IEnumerable<GraphEdgeView> EdgeViews => m_Edges;
        public IReadOnlyCollection<TNode> Nodes => m_NodeDictionary.Values;
        public IReadOnlyList<TEdge> Edges => m_Edges;

        #region Selection

        public override IEnumerable<GraphNodeView> SelectedViews => SelectedNodes;
        public override int SelectionCount => SelectedNodes.Count;
        public override bool IsSelected(GraphNodeView view) => view is TNode node && SelectedNodes.Contains(node);
        public override void AddToSelection(GraphNodeView view) { if (view is TNode node) AddToSelection(node); }
        public override void ToggleSelection(GraphNodeView view) { if (view is TNode node) ToggleSelection(node); }

        public override void ClearSelection()
        {
            foreach (var node in SelectedNodes) node.RemoveFromClassList("selected");
            SelectedNodes.Clear();
        }

        public void AddToSelection(TNode node)
        {
            if (SelectedNodes.Contains(node)) return;
            node.AddToClassList("selected");
            SelectedNodes.Add(node);
        }

        public void ToggleSelection(TNode node)
        {
            if (SelectedNodes.Remove(node)) node.RemoveFromClassList("selected");
            else AddToSelection(node);
        }

        #endregion

        #region Views

        public TNode GetNodeView(string id)
        {
            if (id == null) return null;
            m_NodeDictionary.TryGetValue(id, out var view);
            return view;
        }

        /// <summary>Adds a node view to the canvas (ignored if a view with the same id is already there).</summary>
        protected bool RegisterNodeView(TNode view)
        {
            if (view == null || m_NodeDictionary.ContainsKey(view.Id)) return false;
            m_NodeDictionary.Add(view.Id, view);
            AddElement(view);
            return true;
        }

        /// <summary>Removes a node view, every edge touching its ports, and its selection state.</summary>
        protected void UnregisterNodeView(TNode view)
        {
            if (view == null) return;
            for (int i = m_Edges.Count - 1; i >= 0; i--)
            {
                var edge = m_Edges[i];
                if (edge.FromNode == view || edge.ToNode == view) RemoveEdgeView(edge);
            }
            if (SelectedNodes.Remove(view)) view.RemoveFromClassList("selected");
            m_NodeDictionary.Remove(view.Id);
            RemoveElement(view);
        }

        public sealed override GraphEdgeView CreateEdgeView(GraphPortView output) => CreateEdge(output);

        /// <summary>A new, unconnected edge view starting at <paramref name="output"/> (also used for the drag preview).</summary>
        protected abstract TEdge CreateEdge(GraphPortView output);

        /// <summary>Draws an edge between two port views (views only; the data must already hold the connection).</summary>
        protected TEdge ConnectViews(GraphPortView output, GraphPortView input, bool behindNodes)
        {
            var edge = CreateEdge(output);
            edge.SetInputPort(input);
            output.Connect(edge);
            input.Connect(edge);
            m_Edges.Add(edge);
            if (behindNodes) InsertBehindNodes(edge);
            else AddElement(edge);
            return edge;
        }

        protected void RemoveEdgeView(TEdge edge)
        {
            edge.InputPort?.Disconnect(edge);
            edge.OutputPort?.Disconnect(edge);
            m_Edges.Remove(edge);
            RemoveElement(edge);
            if (edge.IsNodeAnchored) FindNodeEdge(edge.ToNode, edge.FromNode)?.SetLaneOffset(0f); // the reverse edge is alone again
        }

        public TEdge FindEdge(GraphPortView output, GraphPortView input)
        {
            foreach (var edge in m_Edges)
                if (edge.OutputPort == output && edge.InputPort == input) return edge;
            return null;
        }

        /// <summary>Removes every node and edge view (keeps pan/zoom). Selection is cleared.</summary>
        protected void ClearViews()
        {
            foreach (var edge in m_Edges) RemoveElement(edge);
            foreach (var view in m_NodeDictionary.Values) RemoveElement(view);
            m_Edges.Clear();
            m_NodeDictionary.Clear();
            SelectedNodes.Clear();
        }


        #endregion

        #region Node-anchored edges

        /// <summary>Gap between A→B and B→A when both exist.</summary>
        protected virtual float NodeEdgeLaneOffset => 6f;

        public sealed override GraphEdgeView CreateNodeEdgeView(GraphNodeView from) => CreateNodeEdge(from);

        /// <summary>
        /// A new node-anchored edge view from <paramref name="from"/>. Override to enable port-less links
        /// (e.g. <c>new MyEdge(from, theme)</c> using the <see cref="GraphEdgeView(GraphNodeView, EdgeTheme)"/> constructor).
        /// </summary>
        protected virtual TEdge CreateNodeEdge(GraphNodeView from)
        {
            UnityEngine.Debug.LogError($"[GraphUI] {GetType().Name} does not support node links: override {nameof(CreateNodeEdge)}.");
            return null;
        }

        /// <summary>Draws a node-anchored edge (views only; the data must already hold the link). Opposite pairs get separate lanes.</summary>
        protected TEdge ConnectNodeViews(GraphNodeView from, GraphNodeView to)
        {
            var edge = CreateNodeEdge(from);
            if (edge == null) return null;
            edge.SetTargetNode(to);
            m_Edges.Add(edge);
            InsertBehindNodes(edge);
            var reverse = FindNodeEdge(to, from);
            if (reverse != null)
            {
                // Lanes are measured to each edge's left, so the same offset puts them on opposite sides.
                edge.SetLaneOffset(NodeEdgeLaneOffset);
                reverse.SetLaneOffset(NodeEdgeLaneOffset);
            }
            return edge;
        }

        public TEdge FindNodeEdge(GraphNodeView from, GraphNodeView to)
        {
            if (from == null || to == null) return null;
            foreach (var edge in m_Edges)
                if (edge.IsNodeAnchored && edge.FromNode == from && edge.ToNode == to) return edge;
            return null;
        }
        #endregion
    }
}
