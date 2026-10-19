// --- MODIFIED FILE: BehaviorTree.cs ---

using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(menuName = "ND_BehaviorTree/Trees")]
    public class BehaviorTree : ScriptableObject
    {
        // Serialized data for the editor
        [SerializeField]
        private List<Node> m_nodes = new List<Node>();
        public List<Node> nodes => m_nodes;

        [SerializeField]
        public RootNode rootNode;
        [SerializeField]
        public Blackboard blackboard;

        public GameObject Self;

        /// <summary>
        /// Serialized data-format version. Assets saved before versioning load as 0.
        /// Editor migrations upgrade old assets on open (see ND_BehaviorTree.Editor.Graph.BehaviorTreeMigrator).
        /// 1 = decorators attached to nodes (Node.decorators) instead of standalone graph nodes.
        /// 2 = attached services/decorators are no longer also listed in m_nodes (legacy duplicates removed).
        /// </summary>
        public const int CurrentSchemaVersion = 2;
        [SerializeField, HideInInspector] private int m_SchemaVersion = 0;
        public int SchemaVersion { get => m_SchemaVersion; set => m_SchemaVersion = value; }

        [Header("Editor annotations (never read at runtime)")]
        [Tooltip("Node groups drawn in the graph editor. Edit them in the graph (right-click selected nodes → Group Selected Nodes).")]
        [FormerlySerializedAs("m_Groups")]
        public List<GraphGroup> groups = new List<GraphGroup>();

        [Tooltip("Sticky notes drawn in the graph editor. Edit them in the graph (right-click canvas → Create Note).")]
        [FormerlySerializedAs("m_Notes")]
        public List<GraphNote> notes = new List<GraphNote>();

        // Runtime state for a cloned tree
        private Node.Status treeStatus = Node.Status.Running;
        [System.NonSerialized] private Node m_RootEntry;

        // Event runtime state (only set on runtime clones; null on assets).
        [System.NonSerialized] private List<EventNode> m_EventNodes;
        [System.NonSerialized] private Dictionary<EventNode, Node> m_EventEntries;
        [System.NonSerialized] private List<EventNode> m_PendingEvents;
        [System.NonSerialized] private List<EventNode> m_RunningEvents;

        // Init runtime state (runtime clones only).
        [System.NonSerialized] private InitNode m_InitNode;
        [System.NonSerialized] private Node m_InitEntry;
        [System.NonSerialized] private bool m_InitDone;
        [System.NonSerialized] private bool m_StoppedByInitFailure;

        /// <summary>Status returned by the last Update (Running until the first completion).</summary>
        public Node.Status LastStatus => treeStatus;

        /// <summary>True once the optional Init branch has finished (always true when the tree has no InitNode).</summary>
        public bool IsInitialized => m_InitDone || m_InitEntry == null;

        public Node.Status Update()
        {
            if (rootNode == null) return Node.Status.Failure;

            if (!IsInitialized)
            {
                if (RunInitialization() == Node.Status.Running) return Node.Status.Running;
            }
            if (m_StoppedByInitFailure) return Node.Status.Failure;

            if (TickEvents()) return Node.Status.Running; // an interrupting event is running: main tree paused

            // If the tree was not running, it means it finished on the last frame (Success/Failure).
            // Reset the main tree (cascades through children, attached decorators and services) before running again.
            if (treeStatus != Node.Status.Running)
            {
                MainEntry.Reset();
            }

            treeStatus = MainEntry.Process();
            return treeStatus;
        }

        /// <summary>The root's outermost attached decorator (set by Clone), or the root itself.</summary>
        private Node MainEntry => m_RootEntry != null ? m_RootEntry : rootNode;

        /// <summary>
        /// Ticks the optional Init branch once (see InitNode for the contract). Called by BehaviorTreeRunner in Start and
        /// by Update until initialization finishes. Returns Success when there is nothing (left) to initialize.
        /// </summary>
        public Node.Status RunInitialization()
        {
            if (IsInitialized) return m_StoppedByInitFailure ? Node.Status.Failure : Node.Status.Success;

            var status = m_InitEntry.Process();
            if (status == Node.Status.Running) return status;

            m_InitDone = true;
            if (status == Node.Status.Failure)
            {
                if (m_InitNode.stopTreeOnFailure)
                {
                    m_StoppedByInitFailure = true;
                    Debug.LogError($"[BehaviorTree] Init branch of '{name}' failed and stopTreeOnFailure is on: the tree will not run.", this);
                }
                else
                {
                    Debug.LogWarning($"[BehaviorTree] Init branch of '{name}' failed; starting the main tree anyway.", this);
                }
            }
            return status;
        }

        #region Events

        /// <summary>
        /// Requests the EventNode(s) named <paramref name="eventName"/> to run. Call on the RUNTIME tree
        /// (BehaviorTreeRunner.RuntimeTree), from the main thread. The event starts on the next Update.
        /// Returns false if no EventNode has that name.
        /// </summary>
        public bool SendEvent(string eventName)
        {
            if (m_EventNodes == null)
            {
                Debug.LogError($"[BehaviorTree] SendEvent('{eventName}') called on asset '{name}'. Send events to the runtime clone (BehaviorTreeRunner.SendEvent / RuntimeTree).", this);
                return false;
            }

            bool found = false;
            foreach (var ev in m_EventNodes)
            {
                if (ev.eventName != eventName) continue;
                found = true;
                if (m_PendingEvents.Contains(ev)) continue;
                if (m_RunningEvents.Contains(ev) && !ev.restartIfRunning) continue; // already running: ignore
                m_PendingEvents.Add(ev);
            }

            if (!found) Debug.LogWarning($"[BehaviorTree] SendEvent: no EventNode named '{eventName}' in '{name}'.", this);
            return found;
        }

        public bool IsEventRunning(string eventName)
        {
            if (m_RunningEvents == null) return false;
            foreach (var ev in m_RunningEvents) if (ev.eventName == eventName) return true;
            return false;
        }

        /// <summary>Starts pending events and ticks running ones. Returns true if the main tree must stay paused.</summary>
        private bool TickEvents()
        {
            if (m_EventNodes == null || (m_PendingEvents.Count == 0 && m_RunningEvents.Count == 0)) return false;

            foreach (var ev in m_PendingEvents)
            {
                if (m_RunningEvents.Contains(ev)) m_EventEntries[ev].Reset(); // restartIfRunning
                else m_RunningEvents.Add(ev);

                if (ev.interruptMainTree)
                {
                    MainEntry.Reset();
                    treeStatus = Node.Status.Running;
                }
            }
            m_PendingEvents.Clear();

            bool pauseMain = false;
            for (int i = 0; i < m_RunningEvents.Count; i++)
            {
                var ev = m_RunningEvents[i];
                var entry = m_EventEntries[ev];
                if (entry.Process() == Node.Status.Running)
                {
                    if (ev.interruptMainTree) pauseMain = true;
                }
                else
                {
                    entry.Reset();
                    m_RunningEvents.RemoveAt(i--);
                }
            }
            return pauseMain;
        }

        #endregion

        /// <summary>
        /// Creates a deep, runtime-safe clone of the behavior tree asset.
        /// This is crucial so that each agent running this tree has its own instance
        /// with its own state (e.g., which node is 'Running').
        /// </summary>
        /// <returns>A new BehaviorTree instance ready for execution.</returns>
        public BehaviorTree Clone() => CloneInternal(new HashSet<BehaviorTree>(), null);

        /// <summary>
        /// Resets every runtime node (used when a SubtreeNode is interrupted) without triggering a second reset
        /// on the next Update.
        /// </summary>
        internal void ResetRuntimeState()
        {
            foreach (var node in m_nodes) node?.Reset();
            treeStatus = Node.Status.Running;
            m_PendingEvents?.Clear();
            m_RunningEvents?.Clear();
        }

        /// <param name="cloneChain">Trees currently being cloned up the subtree call chain (recursion guard).</param>
        /// <param name="sharedBlackboard">When non-null, the clone uses this runtime blackboard instead of cloning its own.</param>
        internal BehaviorTree CloneInternal(HashSet<BehaviorTree> cloneChain, Blackboard sharedBlackboard)
        {
            BehaviorTree tree = Instantiate(this);
            tree.m_nodes = new List<Node>();

            // --- 1. Gom tất cả các node từ cây gốc ---
            // (Phần này giữ nguyên, không cần thay đổi)
            var allNodesInGraph = new List<Node>();
            var nodesToVisit = new Stack<Node>();
            if (this.rootNode != null)
            {
                nodesToVisit.Push(this.rootNode);
            }
            // Event and Init branches are not reachable from the root; they are entry points of their own.
            foreach (var node in m_nodes)
            {
                if (node is EventNode || node is InitNode) nodesToVisit.Push(node);
            }

            while (nodesToVisit.Count > 0)
            {
                Node currentNode = nodesToVisit.Pop();
                if (currentNode == null || allNodesInGraph.Contains(currentNode))
                {
                    continue;
                }
                
                allNodesInGraph.Add(currentNode);
                
                foreach (var child in currentNode.GetChildren())
                {
                    nodesToVisit.Push(child);
                }
                
                if (!(currentNode is ServiceNode))
                {
                    currentNode.services.ForEach(s => nodesToVisit.Push(s));
                }
            }
            
            // --- 2. Clone từng node và tạo map ---
            // (Phần này giữ nguyên, không cần thay đổi)
            // Reroute nodes are editor-only routing points: they are not cloned, and Entry() skips over them.
            allNodesInGraph.RemoveAll(n => n is RerouteNode);

            var nodeMap = new Dictionary<string, Node>();
            foreach (Node originalNode in allNodesInGraph)
            {
                Node clone = originalNode.Clone();
                tree.m_nodes.Add(clone);
                nodeMap.Add(originalNode.id, clone);
                if (originalNode == this.rootNode)
                {
                    tree.rootNode = clone as RootNode;
                }
            }

            // --- 2b. Build attached-decorator chains ---
            // A parent must enter a node through its outermost decorator:
            // parent -> decorators[0] -> ... -> decorators[n-1] -> node.
            var entryMap = new Dictionary<string, Node>();
            foreach (Node originalNode in allNodesInGraph)
            {
                Node clonedNode = nodeMap[originalNode.id];
                clonedNode.decorators = new List<DecoratorNode>();

                Node inner = clonedNode;
                for (int i = originalNode.decorators.Count - 1; i >= 0; i--)
                {
                    var originalDecorator = originalNode.decorators[i];
                    if (originalDecorator == null) continue;
                    var clonedDecorator = (DecoratorNode)originalDecorator.Clone();
                    clonedDecorator.child = inner;
                    clonedNode.decorators.Insert(0, clonedDecorator);
                    tree.m_nodes.Add(clonedDecorator);
                    inner = clonedDecorator;
                }
                entryMap[originalNode.id] = inner;
            }

            // The runtime node a parent enters for this child: reroutes resolved, then the outermost decorator.
            Node Entry(Node originalChild)
            {
                var target = RerouteNode.Resolve(originalChild);
                return target != null ? entryMap[target.id] : null;
            }
            if (this.rootNode != null) tree.m_RootEntry = Entry(this.rootNode);

            tree.m_EventNodes = new List<EventNode>();
            tree.m_EventEntries = new Dictionary<EventNode, Node>();
            tree.m_PendingEvents = new List<EventNode>();
            tree.m_RunningEvents = new List<EventNode>();

            var originalInits = allNodesInGraph.OfType<InitNode>().ToList();
            if (originalInits.Count > 1)
                Debug.LogError($"[BehaviorTree] '{name}' has {originalInits.Count} Init nodes; only '{originalInits[0].name}' will run.", this);
            if (originalInits.Count > 0)
            {
                tree.m_InitNode = (InitNode)nodeMap[originalInits[0].id];
                tree.m_InitEntry = Entry(originalInits[0]);
            }
            foreach (var originalEvent in allNodesInGraph.OfType<EventNode>())
            {
                var clonedEvent = (EventNode)nodeMap[originalEvent.id];
                tree.m_EventNodes.Add(clonedEvent);
                tree.m_EventEntries[clonedEvent] = Entry(originalEvent);
            }

            // --- 3. Kết nối lại các node đã clone ---
            foreach (Node originalNode in allNodesInGraph)
            {
                Node clonedNode = nodeMap[originalNode.id];

                // Services can be attached to any node.
                clonedNode.services = new List<ServiceNode>();
                foreach (var service in originalNode.services)
                {
                    if (service != null) clonedNode.services.Add((ServiceNode)nodeMap[service.id]);
                }

                // Reconnect children generically through the node's own child API, in list order.
                // (Composites append; single-child nodes such as Root, Event, legacy decorators and GOAP actions replace.)
                foreach (var child in originalNode.GetChildren())
                {
                    var entry = child != null ? Entry(child) : null; // null: a reroute that leads nowhere
                    if (entry != null) clonedNode.AddChild(entry);
                }
            }
            
            // --- 4. Clone blackboard và gán owner tree ---
            if (sharedBlackboard != null)
            {
                tree.blackboard = sharedBlackboard;
            }
            else if (this.blackboard != null)
            {
                tree.blackboard = this.blackboard.Clone();
            }

            foreach (var clonedNode in tree.nodes)
            {
                clonedNode.ownerTree = tree;
            }

            // --- 5. Build isolated runtime clones for subtree references ---
            cloneChain.Add(this);
            foreach (var subtreeNode in tree.nodes.OfType<SubtreeNode>())
            {
                subtreeNode.BuildRuntime(tree, cloneChain);
            }
            cloneChain.Remove(this);

            return tree;
        }

#if UNITY_EDITOR
        public void EditorInit()
        {
            if (this.nodes.OfType<RootNode>().Any()) return;
            Debug.Log($"BehaviorTree '{this.name}' has no RootNode. Creating one.");
            RootNode root = ScriptableObject.CreateInstance<RootNode>();
            root.name = "Root";
            root.position = new Rect(250, 100, 150, 100);
            AssetDatabase.AddObjectToAsset(root, this);
            m_nodes.Add(root);
            this.rootNode = root;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        
        public Node FindNode(string guid)
        {
            return nodes.FirstOrDefault(n => n.id == guid);
        }
#endif
    }
}