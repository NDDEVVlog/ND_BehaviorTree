using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    public class ND_BTGraphCanvas : VisualElement
    {
        public BehaviorTree TreeData { get; private set; }
        public SerializedObject SerializedTree { get; private set; }
        public VisualElement ContentContainer { get; private set; }
        public ND_BTConnectionManager ConnectionManager { get; private set; }
        
        protected Dictionary<string, ND_BTNodeView> m_NodeDictionary = new Dictionary<string, ND_BTNodeView>();
        protected List<ND_BTEdgeView> m_Edges = new List<ND_BTEdgeView>();
        
        protected Vector2 m_PanOffset = Vector2.zero;
        protected float m_ZoomFactor = 1.0f;
        protected bool m_IsPanning;
        protected Vector2 m_LastMousePosition;
        
        protected ND_BTSearchProvider m_SearchProvider;
        protected ND_BehaviorTreeEditorWindow m_Window;

        public ND_BTGraphCanvas(SerializedObject serializedObject, ND_BehaviorTreeEditorWindow window)
        {
            TreeData = serializedObject.targetObject as BehaviorTree;
            SerializedTree = serializedObject;
            m_Window = window;

            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = ND_BehaviorTreeSetting.Instance.graphBackgroundColor;

            ContentContainer = new VisualElement { style = { transformOrigin = new TransformOrigin(0, 0) } };
            hierarchy.Add(ContentContainer);

            ConnectionManager = new ND_BTConnectionManager(this);
            m_SearchProvider = ScriptableObject.CreateInstance<ND_BTSearchProvider>();

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => m_IsPanning = false);
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            generateVisualContent += DrawGrid;
            
            DrawExistingData();
        }

        protected virtual void DrawExistingData()
        {
            TreeData.EditorInit();
            foreach (var node in TreeData.nodes.ToList())
            {
                if (node == null) { TreeData.nodes.Remove(null); continue; }
                AddNodeVisuals(node);
            }

            foreach (var node in TreeData.nodes)
            {
                var parentView = GetNodeView(node.id);
                if (parentView?.OutputPort == null) continue;

                foreach (var child in node.GetChildren())
                {
                    var childView = GetNodeView(child.id);
                    if (childView?.InputPort != null)
                        AddEdgeVisuals(parentView.OutputPort, childView.InputPort);
                }
            }
        }

        public void AddElement(VisualElement element) => ContentContainer.Add(element);
        public void RemoveElement(VisualElement element) { if (ContentContainer.Contains(element)) ContentContainer.Remove(element); }

        public virtual void AddNodeVisuals(Node nodeData)
        {
            if (m_NodeDictionary.ContainsKey(nodeData.id)) return;
            
            // SỬ DỤNG FACTORY ĐỂ TẠO VIEW CHUẨN XÁC DỰA TRÊN CONFIG
            var nodeView = ND_BTViewFactory.CreateNodeView(nodeData);
            
            m_NodeDictionary.Add(nodeData.id, nodeView);
            AddElement(nodeView);
        }

        public virtual void AddEdgeVisuals(ND_BTPortView output, ND_BTPortView input)
        {
            var edge = CreateEdgeView(output);
            edge.SetInputPort(input);
            output.Connect(edge);
            input.Connect(edge);
            m_Edges.Add(edge);
            AddElement(edge);
        }

        protected virtual ND_BTEdgeView CreateEdgeView(ND_BTPortView output)
        {
            return new ND_BTEdgeView(output);
        }

        public ND_BTNodeView GetNodeView(string id)
        {
            m_NodeDictionary.TryGetValue(id, out var view);
            return view;
        }

        public virtual void DeleteNode(ND_BTNodeView nodeView)
        {
            Undo.RecordObject(TreeData, "Delete Node");
            var connectedEdges = m_Edges.Where(e => e.InputPort == nodeView.InputPort || e.OutputPort == nodeView.OutputPort).ToList();
            
            foreach (var edge in connectedEdges)
            {
                edge.InputPort?.Disconnect(edge);
                edge.OutputPort?.Disconnect(edge);
                edge.OutputPort?.ParentNodeView.NodeData.RemoveChild(edge.InputPort?.ParentNodeView.NodeData);
                m_Edges.Remove(edge);
                RemoveElement(edge);
            }

            TreeData.nodes.Remove(nodeView.NodeData);
            Undo.DestroyObjectImmediate(nodeView.NodeData);
            m_NodeDictionary.Remove(nodeView.NodeData.id);
            RemoveElement(nodeView);
            EditorUtility.SetDirty(TreeData);
        }

        public virtual void AddNewNodeFromSearch(Type nodeType, Vector2 localPosition)
        {
            Undo.RecordObject(TreeData, "Added Node");
            Node nodeData = (Node)ScriptableObject.CreateInstance(nodeType);
            nodeData.name = nodeType.Name;
            nodeData.SetPosition(new Rect(localPosition, Vector2.zero));
            
            AssetDatabase.AddObjectToAsset(nodeData, TreeData);
            TreeData.nodes.Add(nodeData);
            AddNodeVisuals(nodeData);
            
            EditorUtility.SetDirty(TreeData);
            AssetDatabase.SaveAssets();
        }

        public virtual void OpenServiceSearch(ND_BTNodeView targetNode)
        {
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            m_SearchProvider.InitializeServiceSearch(this, targetNode);
            SearchWindow.Open(new SearchWindowContext(screenPosition, 300, 200), m_SearchProvider);
        }

        public virtual void AddServiceToNode(Type serviceType, ND_BTNodeView parentView)
        {
            if (!(parentView.NodeData is CompositeNode comp)) return;
            Undo.RecordObject(TreeData, "Added Service");
            
            ServiceNode service = (ServiceNode)ScriptableObject.CreateInstance(serviceType);
            service.name = serviceType.Name;
            AssetDatabase.AddObjectToAsset(service, TreeData);
            comp.AddService(service);
            
            EditorUtility.SetDirty(TreeData);
            AssetDatabase.SaveAssets();
            parentView.DrawServices();
        }

        public virtual void RemoveService(CompositeNode parentNode, ServiceNode service)
        {
            Undo.RecordObject(TreeData, "Remove Service");
            parentNode.RemoveService(service);
            Undo.DestroyObjectImmediate(service);
            EditorUtility.SetDirty(TreeData);
            AssetDatabase.SaveAssets();
            GetNodeView(parentNode.id)?.DrawServices();
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target == this)
            {
                Vector2 screenPos = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                Vector2 localPos = ContentContainer.WorldToLocal(evt.mousePosition);
                evt.menu.AppendAction("Create Node...", a => {
                    m_SearchProvider.InitializeNodeSearch(this, localPos);
                    SearchWindow.Open(new SearchWindowContext(screenPos, 300, 200), m_SearchProvider);
                });
            }
        }

        protected virtual void OnWheel(WheelEvent evt)
        {
            float zoomDelta = evt.delta.y > 0 ? -0.05f : 0.05f;
            float newZoom = Mathf.Clamp(m_ZoomFactor + zoomDelta, 0.2f, 3.0f);
            m_PanOffset = evt.localMousePosition - (evt.localMousePosition - m_PanOffset) * (newZoom / m_ZoomFactor);
            m_ZoomFactor = newZoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        protected virtual void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                m_IsPanning = true;
                m_LastMousePosition = evt.position;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_IsPanning && this.HasPointerCapture(evt.pointerId))
            {
                m_PanOffset += (Vector2)evt.position - m_LastMousePosition;
                m_LastMousePosition = evt.position;
                ApplyTransform();
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (m_IsPanning && this.HasPointerCapture(evt.pointerId))
            {
                m_IsPanning = false;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        protected void ApplyTransform()
        {
            ContentContainer.style.translate = new Translate(m_PanOffset.x, m_PanOffset.y, 0);
            ContentContainer.style.scale = new Scale(new Vector3(m_ZoomFactor, m_ZoomFactor, 1));
            MarkDirtyRepaint();
        }

        protected virtual void DrawGrid(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            painter.strokeColor = ND_BehaviorTreeSetting.Instance.gridColor;
            painter.lineWidth = 1.0f;
            float gs = 25f * m_ZoomFactor;
            float ox = m_PanOffset.x % gs;
            float oy = m_PanOffset.y % gs;

            painter.BeginPath();
            for (float x = ox; x < layout.width; x += gs) { painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, layout.height)); }
            for (float y = oy; y < layout.height; y += gs) { painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(layout.width, y)); }
            painter.Stroke();
        }
    }
}