using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// Handles the drag-to-connect gesture. Connection rules and data changes live in the graph service;
    /// this class only draws the temporary edge and asks the canvas to connect.
    /// </summary>
    public class ND_BTConnectionManager
    {
        private ND_BTGraphCanvas m_Canvas;
        private ND_BTPortView m_StartPort;
        private ND_BTEdgeView m_TempEdge;
        private int m_PointerId = -1;

        public ND_BTConnectionManager(ND_BTGraphCanvas canvas)
        {
            m_Canvas = canvas;
            m_Canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            m_Canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void StartConnection(ND_BTPortView port, Vector2 mousePosition, int pointerId)
        {
            m_StartPort = port;
            m_PointerId = pointerId;
            m_TempEdge = new ND_BTEdgeView(port);
            m_Canvas.AddElement(m_TempEdge);
            m_TempEdge.UpdateTempPosition(mousePosition);
            m_Canvas.CapturePointer(pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_TempEdge != null)
            {
                m_TempEdge.UpdateTempPosition(evt.position);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (m_TempEdge == null) return;

            ND_BTPortView targetPort = FindPortUnderMouse(evt.position);
            m_Canvas.RemoveElement(m_TempEdge);

            if (TryGetOrientedPorts(m_StartPort, targetPort, out var output, out var input))
            {
                m_Canvas.TryConnect(output, input);
            }

            m_TempEdge = null;
            m_StartPort = null;
            if (m_PointerId >= 0 && m_Canvas.HasPointerCapture(m_PointerId)) m_Canvas.ReleasePointer(m_PointerId);
            m_PointerId = -1;
        }

        private static bool TryGetOrientedPorts(ND_BTPortView start, ND_BTPortView end, out ND_BTPortView output, out ND_BTPortView input)
        {
            output = input = null;
            if (end == null || start == end || start.Direction == end.Direction) return false;
            output = start.Direction == BTPortDirection.Output ? start : end;
            input = start.Direction == BTPortDirection.Input ? start : end;
            return true;
        }

        private ND_BTPortView FindPortUnderMouse(Vector2 screenPosition)
        {
            var element = m_Canvas.panel.Pick(screenPosition);
            return element as ND_BTPortView ?? element?.GetFirstAncestorOfType<ND_BTPortView>();
        }
    }
}
