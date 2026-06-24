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
        protected Vector2 m_CurrentMousePosition;
        
        protected ND_BTSearchProvider m_SearchProvider;
        protected ND_BehaviorTreeEditorWindow m_Window;

        public List<ND_BTNodeView> SelectedNodes { get; private set; } = new List<ND_BTNodeView>();
        private VisualElement m_SelectionBox;
        private bool m_IsBoxSelecting;
        private Vector2 m_BoxSelectStartPos;
        private static List<Node> s_Clipboard = new List<Node>();

        public ND_BTGraphCanvas(SerializedObject serializedObject, ND_BehaviorTreeEditorWindow window)
        {
            TreeData = serializedObject.targetObject as BehaviorTree;
            SerializedTree = serializedObject;
            m_Window = window;

            focusable = true; 
            tabIndex = 0;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = ND_BehaviorTreeSetting.Instance.graphBackgroundColor;

            ContentContainer = new VisualElement { style = { transformOrigin = new TransformOrigin(0, 0) } };
            hierarchy.Add(ContentContainer);

            m_SelectionBox = new VisualElement();
            m_SelectionBox.style.position = Position.Absolute;
            m_SelectionBox.style.backgroundColor = new Color(0.1f, 0.5f, 1f, 0.2f);
            m_SelectionBox.style.borderLeftColor = m_SelectionBox.style.borderRightColor = 
            m_SelectionBox.style.borderTopColor = m_SelectionBox.style.borderBottomColor = new Color(0.1f, 0.5f, 1f, 1f);
            m_SelectionBox.style.borderLeftWidth = m_SelectionBox.style.borderRightWidth = 
            m_SelectionBox.style.borderTopWidth = m_SelectionBox.style.borderBottomWidth = 1f;
            m_SelectionBox.style.display = DisplayStyle.None;
            ContentContainer.Add(m_SelectionBox);

            ConnectionManager = new ND_BTConnectionManager(this);
            m_SearchProvider = ScriptableObject.CreateInstance<ND_BTSearchProvider>();

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => { m_IsPanning = false; m_IsBoxSelecting = false; });
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            generateVisualContent += DrawGrid;
            
            DrawExistingData();
        }

        public void ClearSelection()
        {
            foreach (var node in SelectedNodes) node.RemoveFromClassList("selected");
            SelectedNodes.Clear();
        }

        public void AddToSelection(ND_BTNodeView node)
        {
            if (!SelectedNodes.Contains(node))
            {
                node.AddToClassList("selected");
                SelectedNodes.Add(node);
            }
        }

        public void ToggleSelection(ND_BTNodeView node)
        {
            if (SelectedNodes.Contains(node))
            {
                node.RemoveFromClassList("selected");
                SelectedNodes.Remove(node);
            }
            else AddToSelection(node);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                DeleteSelectedNodes();
                evt.StopPropagation();
            }
            else if (evt.actionKey && evt.keyCode == KeyCode.C)
            {
                CopySelection();
                evt.StopPropagation();
            }
            else if (evt.actionKey && evt.keyCode == KeyCode.V)
            {
                PasteSelection(ContentContainer.WorldToLocal(m_CurrentMousePosition));
                evt.StopPropagation();
            }
            else if (evt.actionKey && evt.keyCode == KeyCode.D)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
        }

        public void CopySelection()
        {
            if (SelectedNodes.Count == 0) return;
            s_Clipboard.Clear();
            foreach (var view in SelectedNodes) s_Clipboard.Add(view.NodeData);
        }

        public void PasteSelection(Vector2 targetLocalPosition)
        {
            if (s_Clipboard.Count == 0) return;
            
            s_Clipboard.RemoveAll(n => n == null);
            if (s_Clipboard.Count == 0) return;

            ClearSelection();
            Undo.RecordObject(TreeData, "Paste Nodes");

            Vector2 originalCenter = CalculateClipboardCenter();
            Dictionary<Node, Node> originalToNewMap = CloneNodesWithOffset(targetLocalPosition, originalCenter);
            
            RebuildPastedHierarchy(originalToNewMap);
            DrawPastedVisuals(originalToNewMap);

            EditorUtility.SetDirty(TreeData);
            AssetDatabase.SaveAssets();
        }

        private Vector2 CalculateClipboardCenter()
        {
            Vector2 center = Vector2.zero;
            foreach (var node in s_Clipboard)
            {
                center += node.position.position;
            }
            return center / s_Clipboard.Count;
        }

        private Dictionary<Node, Node> CloneNodesWithOffset(Vector2 targetPosition, Vector2 originalCenter)
        {
            Dictionary<Node, Node> originalToNew = new Dictionary<Node, Node>();

            foreach (var origNode in s_Clipboard)
            {
                Node cloneNode = UnityEngine.Object.Instantiate(origNode);
                cloneNode.name = origNode.name;
                cloneNode.SetNewID(Guid.NewGuid().ToString());
                
                Vector2 offset = origNode.position.position - originalCenter;
                Rect newPos = cloneNode.position;
                newPos.position = targetPosition + offset;
                cloneNode.SetPosition(newPos);

                CloneNodeServices(cloneNode as CompositeNode);

                AssetDatabase.AddObjectToAsset(cloneNode, TreeData);
                TreeData.nodes.Add(cloneNode);
                originalToNew.Add(origNode, cloneNode);
            }

            return originalToNew;
        }

        private void CloneNodeServices(CompositeNode compositeNode)
        {
            if (compositeNode == null) return;

            List<ServiceNode> newServices = new List<ServiceNode>();
            foreach (var service in compositeNode.services)
            {
                if (service == null) continue;
                ServiceNode cloneService = UnityEngine.Object.Instantiate(service);
                cloneService.name = service.name;
                cloneService.SetNewID(Guid.NewGuid().ToString());
                AssetDatabase.AddObjectToAsset(cloneService, TreeData);
                newServices.Add(cloneService);
            }
            compositeNode.services = newServices;
        }

        private void RebuildPastedHierarchy(Dictionary<Node, Node> originalToNew)
        {
            foreach (var kvp in originalToNew)
            {
                Node orig = kvp.Key;
                Node clone = kvp.Value;

                if (clone is CompositeNode comp) comp.children.Clear();
                if (clone is AuxiliaryNode aux) aux.SetChild(null);

                foreach (var origChild in orig.GetChildren())
                {
                    if (originalToNew.TryGetValue(origChild, out Node cloneChild))
                    {
                        clone.AddChild(cloneChild);
                    }
                }
                EditorUtility.SetDirty(clone);
            }
        }

        private void DrawPastedVisuals(Dictionary<Node, Node> originalToNew)
        {
            foreach (var clone in originalToNew.Values)
            {
                AddNodeVisuals(clone);
                var view = GetNodeView(clone.id);
                if (view != null) AddToSelection(view);
            }

            foreach (var clone in originalToNew.Values)
            {
                var parentView = GetNodeView(clone.id);
                if (parentView?.OutputPort == null) continue;

                foreach (var child in clone.GetChildren())
                {
                    var childView = GetNodeView(child.id);
                    if (childView?.InputPort != null)
                        AddEdgeVisuals(parentView.OutputPort, childView.InputPort);
                }
            }
        }

        public void DuplicateSelection()
        {
            CopySelection();
            PasteSelection(ContentContainer.WorldToLocal(m_CurrentMousePosition));
        }

        public void DeleteSelectedNodes()
        {
            if (SelectedNodes.Count == 0) return;
            var nodesToDelete = SelectedNodes.ToList();
            ClearSelection();
            foreach (var node in nodesToDelete) DeleteNode(node);
        }

        protected virtual void DrawExistingData()
        {
            TreeData.EditorInit();
            TreeData.nodes.RemoveAll(n => n == null);

            foreach (var node in TreeData.nodes.ToList())
            {
                if (node is CompositeNode comp)
                {
                    comp.children.RemoveAll(c => c == null);
                    comp.services.RemoveAll(s => s == null);
                }
                else if (node is AuxiliaryNode aux)
                {
                    if (aux.child == null) aux.SetChild(null);
                }
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

                var parentData = edge.OutputPort?.ParentNodeView.NodeData;
                var childData = edge.InputPort?.ParentNodeView.NodeData;

                if (parentData != null && childData != null)
                {
                    parentData.RemoveChild(childData);
                    EditorUtility.SetDirty(parentData);
                }

                m_Edges.Remove(edge);
                RemoveElement(edge);
            }

            if (nodeView.NodeData is CompositeNode composite)
            {
                foreach (var service in composite.services.ToList())
                {
                    if (service != null) Undo.DestroyObjectImmediate(service);
                }
                composite.services.Clear();
            }

            TreeData.nodes.Remove(nodeView.NodeData);
            Undo.DestroyObjectImmediate(nodeView.NodeData);
            m_NodeDictionary.Remove(nodeView.NodeData.id);
            RemoveElement(nodeView);
            
            EditorUtility.SetDirty(TreeData);
            AssetDatabase.SaveAssets();
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

        public virtual void OpenServiceSearch(ND_BTNodeView targetNode, Vector2 screenPosition)
        {
            if (m_SearchProvider == null) m_SearchProvider = ScriptableObject.CreateInstance<ND_BTSearchProvider>();
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
                
                evt.menu.AppendSeparator();
                
                if (SelectedNodes.Count > 0)
                {
                    evt.menu.AppendAction("Copy (Ctrl+C)", a => CopySelection());
                    evt.menu.AppendAction("Duplicate (Ctrl+D)", a => DuplicateSelection());
                    evt.menu.AppendAction("Delete (Del)", a => DeleteSelectedNodes());
                }
                
                if (s_Clipboard.Count > 0)
                {
                    evt.menu.AppendAction("Paste (Ctrl+V)", a => PasteSelection(localPos));
                }
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
            Focus(); 
            
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                m_IsPanning = true;
                m_LastMousePosition = evt.position;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
            else if (evt.button == 0 && evt.target == this)
            {
                if (!evt.actionKey && !evt.shiftKey) ClearSelection();
                
                m_IsBoxSelecting = true;
                m_BoxSelectStartPos = ContentContainer.WorldToLocal(evt.position);
                
                m_SelectionBox.style.display = DisplayStyle.Flex;
                m_SelectionBox.style.left = m_BoxSelectStartPos.x;
                m_SelectionBox.style.top = m_BoxSelectStartPos.y;
                m_SelectionBox.style.width = 0;
                m_SelectionBox.style.height = 0;
                
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            m_CurrentMousePosition = evt.position; 

            if (m_IsPanning && this.HasPointerCapture(evt.pointerId))
            {
                m_PanOffset += (Vector2)evt.position - m_LastMousePosition;
                m_LastMousePosition = evt.position;
                ApplyTransform();
                evt.StopPropagation();
            }
            else if (m_IsBoxSelecting && this.HasPointerCapture(evt.pointerId))
            {
                Vector2 currentPos = ContentContainer.WorldToLocal(evt.position);
                Vector2 min = Vector2.Min(m_BoxSelectStartPos, currentPos);
                Vector2 max = Vector2.Max(m_BoxSelectStartPos, currentPos);
                
                m_SelectionBox.style.left = min.x;
                m_SelectionBox.style.top = min.y;
                m_SelectionBox.style.width = max.x - min.x;
                m_SelectionBox.style.height = max.y - min.y;
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
            else if (m_IsBoxSelecting && this.HasPointerCapture(evt.pointerId))
            {
                m_IsBoxSelecting = false;
                m_SelectionBox.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);

                Rect selectionRect = new Rect(
                    m_SelectionBox.style.left.value.value,
                    m_SelectionBox.style.top.value.value,
                    m_SelectionBox.style.width.value.value,
                    m_SelectionBox.style.height.value.value
                );

                foreach (var kvp in m_NodeDictionary)
                {
                    if (selectionRect.Overlaps(kvp.Value.layout))
                    {
                        AddToSelection(kvp.Value);
                    }
                }
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