using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTConnectionManager
    {
        private ND_BTGraphCanvas m_Canvas;
        private ND_BTPortView m_StartPort;
        private ND_BTEdgeView m_TempEdge;

        public ND_BTConnectionManager(ND_BTGraphCanvas canvas)
        {
            m_Canvas = canvas;
            m_Canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            m_Canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void StartConnection(ND_BTPortView port, Vector2 mousePosition)
        {
            m_StartPort = port;
            m_TempEdge = new ND_BTEdgeView(port);
            m_Canvas.AddElement(m_TempEdge);
            m_TempEdge.UpdateTempPosition(mousePosition);
            m_Canvas.CapturePointer(1);
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
            if (m_TempEdge != null)
            {
                ND_BTPortView targetPort = FindPortUnderMouse(evt.position);

                if (IsValidConnection(m_StartPort, targetPort))
                {
                    CreateDataConnection(m_StartPort, targetPort);
                }
                else
                {
                    m_Canvas.RemoveElement(m_TempEdge);
                }

                m_TempEdge = null;
                m_StartPort = null;
                m_Canvas.ReleasePointer(1);
            }
        }

        private bool IsValidConnection(ND_BTPortView start, ND_BTPortView end)
        {
            if (end == null || start == end) return false;
            if (start.Direction == end.Direction) return false;
            if (start.ParentNodeView == end.ParentNodeView) return false;
            if (end.Direction == BTPortDirection.Input && end.Connections.Count > 0 && end.Capacity == BTPortCapacity.Single) return false;
            if (start.Direction == BTPortDirection.Output && start.Connections.Count > 0 && start.Capacity == BTPortCapacity.Single) return false;
            return true;
        }

        private void CreateDataConnection(ND_BTPortView start, ND_BTPortView end)
        {
            ND_BTPortView outputPort = start.Direction == BTPortDirection.Output ? start : end;
            ND_BTPortView inputPort = start.Direction == BTPortDirection.Input ? start : end;

            Undo.RecordObject(m_Canvas.TreeData, "Connect Nodes");
            
            outputPort.ParentNodeView.NodeData.AddChild(inputPort.ParentNodeView.NodeData);
            
            EditorUtility.SetDirty(m_Canvas.TreeData);
            
            m_Canvas.RemoveElement(m_TempEdge);
            m_Canvas.AddEdgeVisuals(outputPort, inputPort);
        }

        private ND_BTPortView FindPortUnderMouse(Vector2 screenPosition)
        {
            var element = m_Canvas.panel.Pick(screenPosition);
            return element?.GetFirstAncestorOfType<ND_BTPortView>() ?? element as ND_BTPortView;
        }
    }
}