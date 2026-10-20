using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.CustomGraph
{
    /// <summary>
    /// The graph view: pan/zoom, selection, and input. It never mutates graph data directly;
    /// every change goes through <see cref="BehaviorTreeGraphService"/> and the view redraws after Undo/Redo.
    /// </summary>
    public class ND_BTGraphCanvas : VisualElement
    {
        public BehaviorTree TreeData { get; private set; }
        public SerializedObject SerializedTree { get; private set; }
        public BehaviorTreeGraphService Graph { get; private set; }
        public VisualElement ContentContainer { get; private set; }
        public ND_BTConnectionManager ConnectionManager { get; private set; }

        protected Dictionary<string, ND_BTNodeView> m_NodeDictionary = new Dictionary<string, ND_BTNodeView>();
        protected List<ND_BTEdgeView> m_Edges = new List<ND_BTEdgeView>();
        protected List<ND_BTGroupView> m_GroupViews = new List<ND_BTGroupView>();
        protected List<ND_BTNoteView> m_NoteViews = new List<ND_BTNoteView>();

        protected Vector2 m_PanOffset = Vector2.zero;
        protected float m_ZoomFactor = 1.0f;
        protected bool m_IsPanning;
        protected Vector2 m_LastMousePosition;
        protected Vector2 m_CurrentMousePosition;

        protected ND_BehaviorTreeEditorWindow m_Window;

        public List<ND_BTNodeView> SelectedNodes { get; private set; } = new List<ND_BTNodeView>();
        private VisualElement m_SelectionBox;
        private bool m_IsBoxSelecting;
        private Vector2 m_BoxSelectStartPos;

        // Detached snapshot shared across canvases so copy/paste works between trees.
        private static GraphClipboard s_Clipboard;

        public ND_BTGraphCanvas(SerializedObject serializedObject, ND_BehaviorTreeEditorWindow window)
        {
            TreeData = serializedObject.targetObject as BehaviorTree;
            SerializedTree = serializedObject;
            Graph = new BehaviorTreeGraphService(TreeData);
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

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerDownEvent>(OnEdgeDoubleClick, TrickleDown.TrickleDown); // before groups/notes under the edge
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(evt => { m_IsPanning = false; m_IsBoxSelecting = false; });
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<AttachToPanelEvent>(evt => { Undo.undoRedoPerformed -= OnUndoRedo; Undo.undoRedoPerformed += OnUndoRedo; });
            RegisterCallback<DetachFromPanelEvent>(evt => Undo.undoRedoPerformed -= OnUndoRedo);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            generateVisualContent += DrawGrid;

            DrawExistingData();
            ReportValidationIssues();
        }

        #region Selection

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

        #endregion

        #region Drawing

        /// <summary>Rebuilds all views from data, keeping pan/zoom and selection.</summary>
        public void Rebuild()
        {
            var selectedIds = new HashSet<string>(SelectedNodes.Where(v => v.NodeData != null).Select(v => v.NodeData.id));
            foreach (var edge in m_Edges) RemoveElement(edge);
            foreach (var view in m_NodeDictionary.Values) RemoveElement(view);
            foreach (var view in m_GroupViews) RemoveElement(view);
            foreach (var view in m_NoteViews) RemoveElement(view);
            m_Edges.Clear();
            m_NodeDictionary.Clear();
            m_GroupViews.Clear();
            m_NoteViews.Clear();
            SelectedNodes.Clear();

            DrawExistingData();

            foreach (var id in selectedIds)
            {
                var view = GetNodeView(id);
                if (view != null) AddToSelection(view);
            }
        }

        protected virtual void DrawExistingData()
        {
            BehaviorTreeMigrator.MigrateIfNeeded(TreeData);
            TreeData.EditorInit();
            TreeData.nodes.RemoveAll(n => n == null);

            foreach (var node in TreeData.nodes.ToList())
            {
                if (node is CompositeNode comp) comp.children.RemoveAll(c => c == null);
                node.services.RemoveAll(s => s == null);
                AddNodeVisuals(node);
            }

            DrawEdgesFor(TreeData.nodes);
            RefreshExecutionOrder();
            DrawAnnotations();
            RefreshWeightLabels();
            ApplyFocusOnRunMarkers();
        }

        /// <summary>Groups go behind everything, notes above groups but behind nodes and edges.</summary>
        private void DrawAnnotations()
        {
            int index = 0;
            foreach (var group in TreeData.groups)
            {
                var view = new ND_BTGroupView(group, this);
                m_GroupViews.Add(view);
                ContentContainer.Insert(index++, view);
            }
            foreach (var note in TreeData.notes)
            {
                var view = new ND_BTNoteView(note, this);
                m_NoteViews.Add(view);
                ContentContainer.Insert(index++, view);
            }
        }

        private void DrawEdgesFor(IEnumerable<Node> parents)
        {
            foreach (var node in parents)
            {
                var parentView = GetNodeView(node.id);
                if (parentView?.OutputPort == null) continue;

                foreach (var child in node.GetChildren())
                {
                    var childView = child != null ? GetNodeView(child.id) : null;
                    if (childView?.InputPort != null)
                        AddEdgeVisuals(parentView.OutputPort, childView.InputPort);
                }
            }
        }

        /// <summary>Shows each child's execution index (1-based) inside its composite parent.</summary>
        public void RefreshExecutionOrder()
        {
            foreach (var view in m_NodeDictionary.Values) view.SetExecutionIndex(-1);
            foreach (var composite in TreeData.nodes.OfType<CompositeNode>())
            {
                for (int i = 0; i < composite.children.Count; i++)
                {
                    var child = composite.children[i];
                    if (child == null) continue;
                    GetNodeView(child.id)?.SetExecutionIndex(i);
                    // Through a reroute, the node it leads to shows the same index.
                    if (child is RerouteNode)
                    {
                        var target = RerouteNode.Resolve(child);
                        if (target != null) GetNodeView(target.id)?.SetExecutionIndex(i);
                    }
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

        private void RemoveEdgeVisuals(ND_BTEdgeView edge)
        {
            edge.InputPort?.Disconnect(edge);
            edge.OutputPort?.Disconnect(edge);
            m_Edges.Remove(edge);
            RemoveElement(edge);
        }

        public ND_BTNodeView GetNodeView(string id)
        {
            m_NodeDictionary.TryGetValue(id, out var view);
            return view;
        }

        /// <summary>Unity Undo/Redo anywhere in the editor (scene, other assets): rebuild only if it touched this tree.</summary>
        private void OnUndoRedo()
        {
            if (TreeData == null) return;
            int signature = ComputeChangeSignature();
            if (signature == m_LastSignature) return;
            m_LastSignature = signature;
            SerializedTree.Update();
            Rebuild();
        }

        private int m_LastSignature;
        private int m_LastInfoSignature;

        /// <summary>
        /// Cheap fingerprint of everything the graph displays: dirty counters of the tree, nodes, attachments,
        /// blackboard and keys, plus subtree asset names. EditorUtility.GetDirtyCount increments on every change, so
        /// an unchanged signature means nothing needs redrawing. No allocations.
        /// </summary>
        public int ComputeChangeSignature()
        {
            unchecked
            {
                int h = 17;
                void Mix(UnityEngine.Object o) => h = h * 31 + (o != null ? EditorUtility.GetDirtyCount(o) * 397 ^ o.GetInstanceID() : 0);

                Mix(TreeData);
                var nodes = TreeData.nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    Mix(n);
                    if (n == null) continue;
                    for (int d = 0; d < n.decorators.Count; d++) Mix(n.decorators[d]);
                    for (int s = 0; s < n.services.Count; s++) Mix(n.services[s]);
                    if (n is SubtreeNode sub && sub.subtree != null) h = h * 31 + sub.subtree.name.GetHashCode();
                }
                var bb = TreeData.blackboard;
                Mix(bb);
                if (bb != null) for (int k = 0; k < bb.keys.Count; k++) Mix(bb.keys[k]);
                return h;
            }
        }

        // Last report per tree this session: re-opening / redrawing a tree does not re-log the same issues.
        private static readonly Dictionary<int, string> s_LastValidationReport = new Dictionary<int, string>();

        /// <summary>Logs all validation issues of this tree as ONE console entry, only when they changed since the last report.</summary>
        private void ReportValidationIssues()
        {
            var issues = GraphValidator.Validate(TreeData);
            string report = string.Join("\n  - ", issues.Select(i => i.ToString()));
            int key = TreeData.GetInstanceID();
            if (s_LastValidationReport.TryGetValue(key, out var last) && last == report) return;
            s_LastValidationReport[key] = report;
            if (issues.Count == 0) return;

            string message = $"[ND_BehaviorTree] '{TreeData.name}' has {issues.Count} validation issue(s):\n  - {report}";
            if (issues.Any(i => i.Severity == GraphIssueSeverity.Error)) Debug.LogError(message, TreeData);
            else Debug.LogWarning(message, TreeData);
        }

        #region Debounced save

        private const long SaveDelayMs = 1000;
        private IVisualElementScheduledItem m_PendingSave;
        private bool m_SaveRequested;

        /// <summary>
        /// Called after every edit.
        /// Live Save on: writes the asset ~1 s after the last edit (one disk write per burst of edits).
        /// Live Save off: only marks the window as having unsaved changes; the user saves with Save / Ctrl+S.
        /// </summary>
        public void RequestSave()
        {
            if (m_Window != null && !m_Window.LiveSave)
            {
                m_Window.SetUnsavedChanges(true);
                return;
            }
            m_SaveRequested = true;
            if (m_PendingSave == null) m_PendingSave = schedule.Execute(FlushPendingSave);
            m_PendingSave.ExecuteLater(SaveDelayMs);
        }

        /// <summary>Writes a pending Live Save now (no-op when nothing is pending or Live Save is off).</summary>
        public void FlushPendingSave()
        {
            m_PendingSave?.Pause();
            if (!m_SaveRequested) return;
            m_SaveRequested = false;
            if (TreeData != null) Graph.Save();
        }

        /// <summary>Manual save (Save button / Ctrl+S / closing with unsaved changes).</summary>
        public void SaveNow()
        {
            m_PendingSave?.Pause();
            m_SaveRequested = false;
            if (TreeData != null)
            {
                EditorUtility.SetDirty(TreeData);
                Graph.Save();
            }
            m_Window?.SetUnsavedChanges(false);
        }

        #endregion

        #endregion

        #region Graph operations (all routed through the service)

        public void CopySelection()
        {
            var nodes = SelectedNodes.Select(v => v.NodeData).Where(n => n != null && !(n is RootNode)).ToList();
            if (nodes.Count == 0) return;
            s_Clipboard?.Dispose();
            s_Clipboard = GraphClipboard.Capture(TreeData, nodes);
        }

        public void PasteSelection(Vector2 targetLocalPosition)
        {
            if (s_Clipboard == null || s_Clipboard.IsEmpty) return;

            var pasted = Graph.Paste(s_Clipboard, targetLocalPosition);
            RequestSave();

            ClearSelection();
            foreach (var node in pasted) AddNodeVisuals(node);
            DrawEdgesFor(pasted);
            foreach (var node in pasted)
            {
                var view = GetNodeView(node.id);
                if (view != null) AddToSelection(view);
            }
            RefreshExecutionOrder();
        }

        public void DuplicateSelection()
        {
            CopySelection();
            PasteSelection(ContentContainer.WorldToLocal(m_CurrentMousePosition));
        }

        public void DeleteSelectedNodes()
        {
            if (SelectedNodes.Count == 0) return;
            DeleteNodes(SelectedNodes.ToList());
        }

        public virtual void DeleteNode(ND_BTNodeView nodeView) => DeleteNodes(new List<ND_BTNodeView> { nodeView });

        private void DeleteNodes(List<ND_BTNodeView> views)
        {
            ClearSelection();
            var deletable = views.Where(v => !(v.NodeData is RootNode)).ToList();
            var ids = deletable.Select(v => v.NodeData.id).ToList();
            bool deletesReroute = deletable.Any(v => v.NodeData is RerouteNode);
            if (Graph.DeleteNodes(deletable.Select(v => v.NodeData)) == 0) return;
            RequestSave();

            // A deleted reroute re-links its parent to the node behind it: redraw so the bridged edge appears.
            if (deletesReroute)
            {
                Rebuild();
                return;
            }

            foreach (var id in ids)
            {
                var view = GetNodeView(id);
                if (view == null) continue;
                foreach (var edge in m_Edges.Where(e => e.InputPort == view.InputPort || e.OutputPort == view.OutputPort).ToList())
                    RemoveEdgeVisuals(edge);
                m_NodeDictionary.Remove(id);
                RemoveElement(view);
            }
            RefreshExecutionOrder();
        }

        /// <summary>
        /// While a connection is dragged from <paramref name="start"/>, every port that could receive it glows
        /// (same rules as the graph service). Null clears the highlight.
        /// </summary>
        public void SetConnectionCandidates(ND_BTPortView start)
        {
            foreach (var view in m_NodeDictionary.Values)
            {
                view.InputPort?.SetCandidate(false);
                view.OutputPort?.SetCandidate(false);
                if (start == null || view == start.ParentNodeView) continue;

                var startNode = start.ParentNodeView.NodeData;
                if (start.Direction == BTPortDirection.Output)
                    view.InputPort?.SetCandidate(Graph.CanConnect(startNode, view.NodeData, out _));
                else
                    view.OutputPort?.SetCandidate(Graph.CanConnect(view.NodeData, startNode, out _));
            }
        }

        private const float EdgeHitTolerance = 8f;

        /// <summary>Double-click on an edge (not on a node or port): insert a reroute ("extended node") there.</summary>
        private void OnEdgeDoubleClick(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.clickCount != 2) return;
            if (evt.target is VisualElement target && (target is ND_BTNodeView || target is ND_BTPortView
                || target.GetFirstAncestorOfType<ND_BTNodeView>() != null)) return;

            Vector2 local = ContentContainer.WorldToLocal(evt.position);
            ND_BTEdgeView best = null;
            float bestDistance = EdgeHitTolerance / Mathf.Max(0.1f, m_ZoomFactor);
            foreach (var edge in m_Edges)
            {
                float distance = edge.DistanceTo(local);
                if (distance < bestDistance) { bestDistance = distance; best = edge; }
            }
            if (best == null) return;

            InsertReroute(best, local);
            evt.StopImmediatePropagation();
        }

        public void InsertReroute(ND_BTEdgeView edge, Vector2 localPosition)
        {
            var parent = edge.OutputPort?.ParentNodeView?.NodeData;
            var child = edge.InputPort?.ParentNodeView?.NodeData;
            if (parent == null || child == null) return;
            if (Graph.InsertReroute(parent, child, localPosition - ND_BTRerouteView.HalfSize) == null) return;
            RequestSave();
            Rebuild();
        }

        public bool TryConnect(ND_BTPortView output, ND_BTPortView input)
        {
            if (!Graph.Connect(output.ParentNodeView.NodeData, input.ParentNodeView.NodeData)) return false;
            RequestSave();
            AddEdgeVisuals(output, input);
            RefreshExecutionOrder();
            return true;
        }

        /// <summary>Removes the edge entering <paramref name="input"/>.</summary>
        public void DisconnectInput(ND_BTPortView input)
        {
            var edge = input.Connections.FirstOrDefault();
            if (edge?.OutputPort == null) return;
            if (!Graph.Disconnect(edge.OutputPort.ParentNodeView.NodeData, input.ParentNodeView.NodeData)) return;
            RequestSave();
            RemoveEdgeVisuals(edge);
            RefreshExecutionOrder();
        }

        /// <summary>Called by node views when a drag ends: one undo step, parents re-sorted left-to-right.</summary>
        public void CommitNodeMove(IEnumerable<ND_BTNodeView> movedViews)
        {
            var positions = movedViews.Where(v => v.NodeData != null).ToDictionary(v => v.NodeData, v => v.ViewPosition);
            Graph.MoveNodes(positions);
            RequestSave();
            RefreshExecutionOrder();
            foreach (var groupView in m_GroupViews) groupView.Refresh(); // membership counts may have changed
        }

        /// <summary>Pans/zooms so the Root node is centred at 100% zoom.</summary>
        public void FocusOnRoot()
        {
            var rootView = TreeData.rootNode != null ? GetNodeView(TreeData.rootNode.id) : null;
            if (rootView == null) return;
            FrameRect(NodeContentRect(rootView), 1f);
        }

        /// <summary>F key: frame the selected nodes (zoom to fit, max 100%), or the Root when nothing is selected.</summary>
        public void FrameSelectionOrRoot()
        {
            if (SelectedNodes.Count == 0) { FocusOnRoot(); return; }
            var bounds = SelectedNodes.Select(NodeContentRect).Aggregate((a, b) => Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));
            float padding = 80f;
            float zoom = Mathf.Min(1f, layout.width / (bounds.width + padding), layout.height / (bounds.height + padding));
            FrameRect(bounds, Mathf.Clamp(zoom, 0.2f, 1f));
        }

        private static Rect NodeContentRect(ND_BTNodeView view)
        {
            var r = view.layout;
            if (float.IsNaN(r.width) || r.width <= 0) r = new Rect(view.ViewPosition, new Vector2(200, 80));
            return r;
        }

        private void FrameRect(Rect contentRect, float zoom)
        {
            m_ZoomFactor = zoom;
            Vector2 viewCenter = new Vector2(layout.width, layout.height) * 0.5f;
            m_PanOffset = viewCenter - contentRect.center * m_ZoomFactor;
            ApplyTransform();
        }

        /// <summary>Asks for a folder/name, then moves the selected branch into a new subtree asset.</summary>
        public void ConvertSelectionToSubtree()
        {
            var selection = SelectedNodes.Select(v => v.NodeData).Where(n => n != null).ToList();
            if (selection.Count == 0) return;

            string treeFolder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(TreeData))?.Replace('\\', '/') ?? "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Create Subtree", $"{TreeData.name}_Subtree", "asset",
                "Choose the folder and file name for the new subtree asset.", treeFolder);
            if (string.IsNullOrEmpty(path)) return; // cancelled

            var subtreeNode = Graph.ConvertToSubtree(selection, path, out string error);
            if (subtreeNode == null)
            {
                EditorUtility.DisplayDialog("Cannot create subtree", error, "OK");
                return;
            }
            RequestSave();
            ClearSelection();
            Rebuild();
            var view = GetNodeView(subtreeNode.id);
            if (view != null) AddToSelection(view);
            Debug.Log($"[ND_BehaviorTree] Moved {selection.Count} node(s) into subtree '{path}'.", subtreeNode.subtree);
        }

        #region Live debug

        private BehaviorTree m_LiveRuntime;
        private Dictionary<string, Node> m_LiveLookup = new Dictionary<string, Node>();
        private Func<string, Node> m_LookupDelegate;

        private Node LookupRuntime(string id) => id != null && m_LiveLookup.TryGetValue(id, out var n) ? n : null;

        /// <summary>True while the window is showing a runtime tree (Play mode + watched agent).</summary>
        public bool IsLiveDebugging => m_Window != null && m_Window.IsLiveDebugging;

        /// <summary>Called every editor update in Play mode with the runtime tree that matches this graph.</summary>
        private static readonly Unity.Profiling.ProfilerMarker s_LiveMarker = new Unity.Profiling.ProfilerMarker("ND_BehaviorTree.LiveDebug");
        private static readonly Unity.Profiling.ProfilerMarker s_RefreshMarker = new Unity.Profiling.ProfilerMarker("ND_BehaviorTree.RefreshInfo");

        public void UpdateLiveDebug(BehaviorTree runtime)
        {
            using (s_LiveMarker.Auto()) UpdateLiveDebugInternal(runtime);
        }

        private void UpdateLiveDebugInternal(BehaviorTree runtime)
        {
            if (runtime != m_LiveRuntime)
            {
                m_LiveRuntime = runtime;
                m_LiveLookup = LiveDebugUtility.BuildLookup(runtime);
            }
            if (m_LookupDelegate == null) m_LookupDelegate = LookupRuntime; // one delegate, not one per node per tick

            int frame = Time.frameCount;
            foreach (var view in m_NodeDictionary.Values) view.UpdateLiveState(m_LookupDelegate, frame);
            foreach (var edge in m_Edges)
            {
                // Reroutes do not exist at runtime: an edge into a reroute shows the state of the node it leads to.
                var childData = RerouteNode.Resolve(edge.InputPort?.ParentNodeView?.NodeData);
                edge.SetLiveState(childData != null ? LiveDebugUtility.GetEdgeState(LookupRuntime(childData.id), frame) : LiveState.Inactive);
            }
            UpdateLiveScores();
            UpdateFocusOnRun(frame);
        }

        public void ClearLiveDebug()
        {
            m_LiveRuntime = null;
            m_LiveLookup.Clear();
            LiveDebugUtility.ClearCache();
            foreach (var view in m_NodeDictionary.Values) view.ClearState();
            foreach (var edge in m_Edges) edge.SetLiveState(LiveState.Inactive);
            m_WasRunning.Clear();
            RefreshWeightLabels(); // live scores back to the static weights
        }

        /// <summary>
        /// Periodic refresh of labels, key slots and attachment summaries. Does nothing (and allocates nothing)
        /// unless something the graph displays changed since the last refresh.
        /// </summary>
        public void RefreshInfoLabels(bool force = false)
        {
            using (s_RefreshMarker.Auto()) RefreshInfoLabelsInternal(force);
        }

        private void RefreshInfoLabelsInternal(bool force)
        {
            int signature = ComputeChangeSignature();
            if (!force && signature == m_LastInfoSignature) return;
            m_LastInfoSignature = signature;
            m_LastSignature = signature;

            bool live = IsLiveDebugging; // live mode owns the subtree label ("▶ running path")
            foreach (var view in m_NodeDictionary.Values)
            {
                if (!live) view.RefreshSubtreeLabel();
                view.RefreshKeyInfo();
            }
            RefreshWeightLabels();
        }

        #region Weights (badges + edge labels)

        private static readonly List<float> s_WeightScratch = new List<float>();

        /// <summary>
        /// Under a weighted composite: each child gets a weight badge, and each edge shows the child's chance (Random Rate)
        /// or weight (Utility). Everything else shows nothing. Called when the graph changes (signature-gated).
        /// </summary>
        public void RefreshWeightLabels()
        {
            foreach (var view in m_NodeDictionary.Values) view.SetWeightBadge(null, null);
            foreach (var edge in m_Edges) edge.SetWeightLabel(null, null);

            foreach (var parent in TreeData.nodes)
            {
                if (!(parent is IWeightedComposite weighted)) continue;
                var slots = parent.GetChildren();

                s_WeightScratch.Clear();
                foreach (var slot in slots) s_WeightScratch.Add(RerouteNode.Resolve(slot)?.weight ?? 0f);
                float[] chances = null;
                float total = 0f;
                if (weighted.WeightUsage == WeightUsage.Chance)
                {
                    chances = s_WeightScratch.ToArray();
                    var mode = parent is RandomRateSelectorNode rrs ? rrs.weightingMode : RandomRateSelectorNode.WeightingMode.HigherIsBetter;
                    RandomRateSelectorNode.ComputeChances(chances, mode);
                    foreach (var c in chances) total += c;
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    var target = RerouteNode.Resolve(slots[i]);
                    if (target == null) continue;
                    string modifiers = WeightGraph.ModifierText(target);
                    string weightText = $"w {target.weight:0.##}" + (modifiers != null ? " " + modifiers : string.Empty);
                    GetNodeView(target.id)?.SetWeightBadge(weightText,
                        $"Weight in '{WeightGraph.TitleOf(parent)}'" + (modifiers != null ? $" — changed at runtime by {modifiers}." : "."));

                    string edgeText, edgeTooltip;
                    if (chances != null)
                    {
                        edgeText = total > 0f ? $"{chances[i] / total * 100f:0.#}%" : "0%";
                        if (modifiers != null) edgeText += "*";
                        edgeTooltip = modifiers != null ? "Chance from the base weights; Weight From Key changes it at runtime." : "Chance to be picked on each activation.";
                    }
                    else
                    {
                        edgeText = weightText;
                        edgeTooltip = "Base utility score (Live Debug shows the current score).";
                    }
                    FindEdge(parent, slots[i])?.SetWeightLabel(edgeText, edgeTooltip);
                }
            }
        }

        private ND_BTEdgeView FindEdge(Node parent, Node child)
        {
            foreach (var edge in m_Edges)
                if (edge.OutputPort?.ParentNodeView?.NodeData == parent && edge.InputPort?.ParentNodeView?.NodeData == child) return edge;
            return null;
        }

        /// <summary>Live: Utility Selector edges show each child's current score (text rebuilt only when it changes).</summary>
        private void UpdateLiveScores()
        {
            foreach (var parent in TreeData.nodes)
            {
                if (!(parent is UtilitySelectorNode)) continue;
                if (!(LookupRuntime(parent.id) is UtilitySelectorNode runtime)) continue;
                var slots = parent.GetChildren();
                int runtimeIndex = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (RerouteNode.Resolve(slots[i]) == null) continue; // not part of the runtime tree
                    float score = runtime.GetLastScore(runtimeIndex);
                    bool chosen = runtime.RunningIndex == runtimeIndex;
                    runtimeIndex++;
                    FindEdge(parent, slots[i])?.SetLiveScore(score, chosen);
                }
            }
        }

        #endregion

        #region Live Debug: focus when running / ping agent

        private readonly HashSet<string> m_FocusOnRun = new HashSet<string>();
        private readonly HashSet<string> m_WasRunning = new HashSet<string>();
        private bool m_FocusLoaded;
        private double m_LastAutoFocus;
        private const double AutoFocusCooldown = 0.75;

        private string FocusSessionKey => "NDBT.FocusOnRun." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(TreeData));

        public bool HasFocusOnRun => m_FocusOnRun.Count > 0;
        public bool IsFocusOnRun(Node node) => node != null && m_FocusOnRun.Contains(node.id);

        public void ToggleFocusOnRun(Node node)
        {
            if (node == null) return;
            bool on = m_FocusOnRun.Add(node.id);
            if (!on) m_FocusOnRun.Remove(node.id);
            GetNodeView(node.id)?.SetFocusOnRunMarker(on);
            SaveFocusOnRun();
        }

        public void ClearFocusOnRun()
        {
            foreach (var id in m_FocusOnRun) GetNodeView(id)?.SetFocusOnRunMarker(false);
            m_FocusOnRun.Clear();
            m_WasRunning.Clear();
            SaveFocusOnRun();
        }

        private void SaveFocusOnRun() => SessionState.SetString(FocusSessionKey, string.Join("\n", m_FocusOnRun));

        /// <summary>Restores the markers (kept for the editor session) after the graph is (re)drawn.</summary>
        private void ApplyFocusOnRunMarkers()
        {
            if (!m_FocusLoaded)
            {
                m_FocusLoaded = true;
                foreach (var id in SessionState.GetString(FocusSessionKey, string.Empty).Split('\n'))
                    if (!string.IsNullOrEmpty(id)) m_FocusOnRun.Add(id);
            }
            m_FocusOnRun.RemoveWhere(id => GetNodeView(id) == null); // deleted nodes
            foreach (var id in m_FocusOnRun) GetNodeView(id).SetFocusOnRunMarker(true);
        }

        /// <summary>Centres the graph on a node (keeps a readable zoom) and flashes it.</summary>
        public void FocusNode(ND_BTNodeView view)
        {
            if (view == null) return;
            FrameRect(NodeContentRect(view), Mathf.Clamp(m_ZoomFactor, 0.6f, 1f));
            view.AddToClassList("nd-flash");
            view.schedule.Execute(() => view.RemoveFromClassList("nd-flash")).StartingIn(700);
        }

        /// <summary>Highlights the watched agent in the Hierarchy and moves the Scene view to it (selection is not changed).</summary>
        public void PingWatchedAgent()
        {
            var runner = m_Window != null ? m_Window.WatchedRunner : null;
            if (runner == null) return;
            EditorGUIUtility.PingObject(runner.gameObject);
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) sceneView.LookAt(runner.transform.position);
        }

        /// <summary>Live tick: a marked node that just started running is framed, and the agent pinged (throttled).</summary>
        private void UpdateFocusOnRun(int frame)
        {
            if (m_FocusOnRun.Count == 0) return;
            foreach (var id in m_FocusOnRun)
            {
                bool running = LiveDebugUtility.GetState(LookupRuntime(id), frame) == LiveState.Running;
                if (!running)
                {
                    m_WasRunning.Remove(id);
                    continue;
                }
                if (!m_WasRunning.Add(id)) continue; // still the same run
                double now = EditorApplication.timeSinceStartup;
                if (now - m_LastAutoFocus < AutoFocusCooldown) continue;
                m_LastAutoFocus = now;
                FocusNode(GetNodeView(id));
                PingWatchedAgent();
            }
        }

        #endregion

        /// <summary>Links (or clears, with null) a blackboard key on a node or attachment field, then refreshes the views.</summary>
        public void AssignKey(Node target, string propertyPath, Key key)
        {
            if (!Graph.AssignBlackboardKey(target, propertyPath, key, out string reason))
            {
                ShowNotification(reason);
                return;
            }
            RequestSave();
            foreach (var view in m_NodeDictionary.Values) view.RefreshKeyInfo();
        }

        private void ShowNotification(string message)
        {
            if (m_Window != null) m_Window.ShowNotification(new GUIContent(message), 2.5);
        }

        public void StepIntoSubtreeLive(SubtreeNode assetNode)
        {
            if (assetNode == null || m_LiveRuntime == null) return;
            if (!(m_LiveLookup.TryGetValue(assetNode.id, out var runtimeNode) && runtimeNode is SubtreeNode runtimeSubtree) || runtimeSubtree.RuntimeSubtree == null)
            {
                Debug.LogWarning($"[ND_BehaviorTree] Step Into: '{assetNode.name}' has no runtime subtree on the watched agent (missing or recursive reference).");
                return;
            }
            m_Window.StepIntoSubtree(assetNode.subtree, runtimeSubtree.RuntimeSubtree, assetNode.name);
        }

        #endregion

        #region Groups, notes, subtrees

        public void CreateGroup(Vector2 localPosition)
        {
            var members = SelectedNodes.Select(v => v.NodeData).Where(n => n != null).ToList();
            Rect rect;
            if (SelectedNodes.Count > 0)
            {
                rect = SelectedNodes.Select(NodeContentRect).Aggregate((a, b) => Rect.MinMaxRect(
                    Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));
                rect = Rect.MinMaxRect(rect.xMin - 20, rect.yMin - 44, rect.xMax + 20, rect.yMax + 20);
            }
            else rect = new Rect(localPosition, new Vector2(320, 220));

            var group = Graph.CreateGroup("Group", rect, members);
            RequestSave();
            var view = new ND_BTGroupView(group, this);
            m_GroupViews.Add(view);
            ContentContainer.Insert(0, view);
        }

        public void CreateNote(Vector2 localPosition)
        {
            var note = Graph.CreateNote(new Rect(localPosition, new Vector2(220, 140)));
            RequestSave();
            var view = new ND_BTNoteView(note, this);
            m_NoteViews.Add(view);
            ContentContainer.Insert(m_GroupViews.Count, view);
        }

        public void CommitGroupMove(GraphGroup group, Vector2 newPosition)
        {
            Graph.MoveGroup(group, newPosition);
            RequestSave();
            RefreshExecutionOrder();
        }

        public void CommitGroupResize(GraphGroup group, Rect rect)
        {
            Graph.ResizeGroup(group, rect);
            RequestSave();
        }

        public void CommitGroupEdit(GraphGroup group, string title, Color color)
        {
            if (Graph.UpdateGroup(group, title, color)) RequestSave();
            m_GroupViews.FirstOrDefault(v => v.Group == group)?.Refresh();
        }

        public void DeleteGroup(GraphGroup group)
        {
            if (!Graph.DeleteGroup(group)) return;
            RequestSave();
            var view = m_GroupViews.FirstOrDefault(v => v.Group == group);
            if (view == null) return;
            m_GroupViews.Remove(view);
            RemoveElement(view);
        }

        public void CommitNoteEdit(GraphNote note, string title, string content, Color color, Rect rect)
        {
            if (Graph.UpdateNote(note, title, content, color, rect)) RequestSave();
            m_NoteViews.FirstOrDefault(v => v.Note == note)?.Refresh();
        }

        public void DeleteNote(GraphNote note)
        {
            if (!Graph.DeleteNote(note)) return;
            RequestSave();
            var view = m_NoteViews.FirstOrDefault(v => v.Note == note);
            if (view == null) return;
            m_NoteViews.Remove(view);
            RemoveElement(view);
        }

        public void OpenSubtree(SubtreeNode node)
        {
            if (node?.subtree == null) return;
            ND_BehaviorTreeEditorWindow.Open(node.subtree);
        }

        /// <summary>Creates a new tree asset (with a Root) next to this tree and assigns it to the node.</summary>
        public void CreateSubtreeAsset(SubtreeNode node)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(TreeData))?.Replace('\\', '/') ?? "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{TreeData.name}_Subtree.asset");

            var subtree = ScriptableObject.CreateInstance<BehaviorTree>();
            subtree.SchemaVersion = BehaviorTree.CurrentSchemaVersion;
            AssetDatabase.CreateAsset(subtree, path);
            subtree.EditorInit();

            if (Graph.AssignSubtree(node, subtree)) RequestSave();
            GetNodeView(node.id)?.RefreshSubtreeLabel();
            EditorGUIUtility.PingObject(subtree);
            Debug.Log($"[ND_BehaviorTree] Created subtree asset '{path}' for '{node.name}'.", subtree);
        }

        #endregion

        public void SortChildren(CompositeNode composite)
        {
            if (Graph.SortChildrenByPosition(composite)) RequestSave();
            RefreshExecutionOrder();
        }

        public virtual void AddNewNodeFromSearch(Type nodeType, Vector2 localPosition)
        {
            var node = Graph.CreateNode(nodeType, localPosition);
            if (node == null) return;
            RequestSave();
            AddNodeVisuals(node);
        }

        public virtual void OpenServiceSearch(ND_BTNodeView targetNode, Vector2 screenPosition)
        {
            ND_BTSearchPopup.Show(screenPosition, "Add Service", ND_BTSearchProvider.BuildNodeEntries(NodeSearchKind.Service),
                data => AddServiceToNode((Type)data, targetNode));
        }

        public virtual void OpenDecoratorSearch(ND_BTNodeView targetNode, Vector2 screenPosition)
        {
            ND_BTSearchPopup.Show(screenPosition, "Add Decorator", ND_BTSearchProvider.BuildNodeEntries(NodeSearchKind.Decorator),
                data => AddDecoratorToNode((Type)data, targetNode));
        }

        public virtual void AddDecoratorToNode(Type decoratorType, ND_BTNodeView ownerView)
        {
            if (Graph.AddDecorator(ownerView.NodeData, decoratorType) == null) return;
            RequestSave();
            ownerView.DrawDecorators();
        }

        public virtual void RemoveDecorator(Node owner, DecoratorNode decorator)
        {
            if (!Graph.RemoveDecorator(owner, decorator)) return;
            RequestSave();
            GetNodeView(owner.id)?.DrawDecorators();
        }

        public virtual void MoveDecorator(Node owner, DecoratorNode decorator, int newIndex)
        {
            if (!Graph.MoveDecorator(owner, decorator, newIndex)) return;
            RequestSave();
            GetNodeView(owner.id)?.DrawDecorators();
        }

        public virtual void AddServiceToNode(Type serviceType, ND_BTNodeView parentView)
        {
            if (Graph.AddService(parentView.NodeData, serviceType) == null) return;
            RequestSave();
            parentView.DrawServices();
        }

        public virtual void RemoveService(Node parentNode, ServiceNode service)
        {
            if (!Graph.RemoveService(parentNode, service)) return;
            RequestSave();
            GetNodeView(parentNode.id)?.DrawServices();
        }

        #endregion

        #region Input

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Typing in a note/rename field must not trigger graph shortcuts.
            if ((evt.target as VisualElement)?.GetFirstOfType<TextField>() != null) return;

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
            else if (!evt.actionKey && evt.keyCode == KeyCode.F)
            {
                FrameSelectionOrRoot();
                evt.StopPropagation();
            }
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            Vector2 screenPos = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            Vector2 localPos = ContentContainer.WorldToLocal(evt.mousePosition);

            evt.menu.AppendAction("Create Node...", a =>
                ND_BTSearchPopup.Show(screenPos, "Create Node", ND_BTSearchProvider.BuildNodeEntries(NodeSearchKind.GraphNode),
                    data => AddNewNodeFromSearch((Type)data, localPos)));

            evt.menu.AppendAction("Create Event", a => AddNewNodeFromSearch(typeof(EventNode), localPos));
            evt.menu.AppendAction("Create Init Node (runs once at start)", a => AddNewNodeFromSearch(typeof(InitNode), localPos),
                TreeData.nodes.Any(n => n is InitNode) ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction(SelectedNodes.Count > 0 ? "Group Selected Nodes" : "Create Group", a => CreateGroup(localPos));
            evt.menu.AppendAction("Create Note", a => CreateNote(localPos));
            if (SelectedNodes.Count > 0)
                evt.menu.AppendAction("Convert Selection to Subtree...", a => ConvertSelectionToSubtree());
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Focus Root (F)", a => FocusOnRoot());

            evt.menu.AppendSeparator();

            if (SelectedNodes.Count > 0)
            {
                evt.menu.AppendAction("Copy (Ctrl+C)", a => CopySelection());
                evt.menu.AppendAction("Duplicate (Ctrl+D)", a => DuplicateSelection());
                evt.menu.AppendAction("Delete (Del)", a => DeleteSelectedNodes());
            }

            if (s_Clipboard != null && !s_Clipboard.IsEmpty)
            {
                evt.menu.AppendAction("Paste (Ctrl+V)", a => PasteSelection(localPos));
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

        #endregion
    }
}
