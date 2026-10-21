using UnityEngine;
using UnityEngine.UIElements;

namespace ND.GraphUI
{
    /// <summary>
    /// Handles the drag-to-connect gesture, from a port (port-anchored edges) or from a node's link handle
    /// (node-anchored edges). Connection rules and data changes belong to the canvas
    /// (<see cref="GraphCanvas.CanConnect"/> / <see cref="GraphCanvas.TryConnect"/>,
    /// <see cref="GraphCanvas.CanConnectNodes"/> / <see cref="GraphCanvas.TryConnectNodes"/>); this class only draws
    /// the temporary edge and reports the drop.
    /// </summary>
    public class GraphConnectionManager
    {
        private readonly GraphCanvas m_Canvas;
        private GraphPortView m_StartPort;
        private GraphNodeView m_StartNode;
        private GraphEdgeView m_TempEdge;
        private int m_PointerId = -1;
        private GraphPortView m_HoverPort;

        public bool IsConnecting => m_TempEdge != null;

        public GraphConnectionManager(GraphCanvas canvas)
        {
            m_Canvas = canvas;
            m_Canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            m_Canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void StartConnection(GraphPortView port, Vector2 mousePosition, int pointerId)
        {
            var edge = m_Canvas.CreateEdgeView(port);
            if (edge == null) return;
            m_StartPort = port;
            Begin(edge, mousePosition, pointerId);
            m_Canvas.SetConnectionCandidates(port);
        }

        /// <summary>Starts dragging a node-anchored link from <paramref name="node"/> (its link handle was pressed).</summary>
        public void StartNodeConnection(GraphNodeView node, Vector2 mousePosition, int pointerId)
        {
            var edge = m_Canvas.CreateNodeEdgeView(node);
            if (edge == null) return; // the canvas logged why
            m_StartNode = node;
            Begin(edge, mousePosition, pointerId);
            m_Canvas.SetNodeLinkCandidates(node);
        }

        private void Begin(GraphEdgeView edge, Vector2 mousePosition, int pointerId)
        {
            m_PointerId = pointerId;
            m_TempEdge = edge;
            m_Canvas.AddElement(m_TempEdge);
            m_TempEdge.UpdateTempPosition(mousePosition);
            m_Canvas.CapturePointer(pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_TempEdge == null) return;
            m_TempEdge.UpdateTempPosition(evt.position);
            if (m_StartPort == null) return;
            var hovered = FindPortUnderMouse(evt.position);
            if (hovered == m_HoverPort) return;
            m_HoverPort?.SetDragHover(false);
            m_HoverPort = hovered;
            m_HoverPort?.SetDragHover(true);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (m_TempEdge == null) return;
            m_Canvas.RemoveElement(m_TempEdge);

            if (m_StartPort != null)
            {
                var targetPort = FindPortUnderMouse(evt.position);
                m_Canvas.SetConnectionCandidates(null);
                m_HoverPort?.SetDragHover(false);
                m_HoverPort = null;
                if (TryGetOrientedPorts(m_StartPort, targetPort, out var output, out var input))
                    m_Canvas.TryConnect(output, input);
            }
            else if (m_StartNode != null)
            {
                var targetNode = FindNodeUnderMouse(evt.position);
                m_Canvas.SetNodeLinkCandidates(null);
                if (targetNode != null && targetNode != m_StartNode)
                    m_Canvas.TryConnectNodes(m_StartNode, targetNode);
            }

            m_TempEdge = null;
            m_StartPort = null;
            m_StartNode = null;
            if (m_PointerId >= 0 && m_Canvas.HasPointerCapture(m_PointerId)) m_Canvas.ReleasePointer(m_PointerId);
            m_PointerId = -1;
        }

        /// <summary>Orders a drag (either way round) as output → input; false for same-direction or same-port drops.</summary>
        public static bool TryGetOrientedPorts(GraphPortView start, GraphPortView end, out GraphPortView output, out GraphPortView input)
        {
            output = input = null;
            if (start == null || end == null || start == end || start.Direction == end.Direction) return false;
            output = start.Direction == PortDirection.Output ? start : end;
            input = start.Direction == PortDirection.Input ? start : end;
            return true;
        }

        private GraphPortView FindPortUnderMouse(Vector2 screenPosition)
        {
            var element = m_Canvas.panel?.Pick(screenPosition);
            return element as GraphPortView ?? element?.GetFirstAncestorOfType<GraphPortView>();
        }

        private GraphNodeView FindNodeUnderMouse(Vector2 screenPosition)
        {
            var element = m_Canvas.panel?.Pick(screenPosition);
            return element as GraphNodeView ?? element?.GetFirstAncestorOfType<GraphNodeView>();
        }
    }
}
