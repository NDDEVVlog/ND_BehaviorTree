// --- MODIFIED FILE: BehaviorTree.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree
{
    [CreateAssetMenu(menuName = "BehaviourTree/Trees")]
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

        // Runtime state for a cloned tree
        private Node.Status treeStatus = Node.Status.Running;

        public GameObject self;

        public Node.Status Update()
        {
            if (rootNode == null) return Node.Status.Failure;

            // If the tree was not running, it means it finished on the last frame (Success/Failure).
            // We should reset all nodes to their initial state before processing again.
            if (treeStatus != Node.Status.Running)
            {
                // Note: Resetting the root should cascade to all children.
                // However, a full reset ensures a clean state if the tree was modified.
                nodes.ForEach(n => n.Reset());
            }

            treeStatus = rootNode.Process();
            return treeStatus;
        }

        /// <summary>
        /// Creates a deep, runtime-safe clone of the behavior tree asset.
        /// This is crucial so that each agent running this tree has its own instance
        /// with its own state (e.g., which node is 'Running').
        /// </summary>
        /// <returns>A new BehaviorTree instance ready for execution.</returns>
        public BehaviorTree Clone()
        {
            BehaviorTree tree = Instantiate(this);
            tree.m_nodes = new List<Node>();

            // --- 1. Traverse the original tree to find all unique nodes ---
            // This includes the main execution graph (via GetChildren) and attached services.
            var allNodesInGraph = new List<Node>();
            var nodesToVisit = new Stack<Node>();
            if (this.rootNode != null)
            {
                nodesToVisit.Push(this.rootNode);
            }

            while (nodesToVisit.Count > 0)
            {
                Node currentNode = nodesToVisit.Pop();
                if (currentNode == null || allNodesInGraph.Contains(currentNode))
                {
                    continue;
                }
                
                allNodesInGraph.Add(currentNode);
                
                // Add all children from the main execution path (this now includes decorators)
                foreach (var child in currentNode.GetChildren())
                {
                    nodesToVisit.Push(child);
                }
                
                // Separately add attached services, as they are not in the main GetChildren() path.
                if (currentNode is CompositeNode composite)
                {
                    composite.services.ForEach(s => nodesToVisit.Push(s));
                }
            }
            
            // --- 2. Clone each unique node and map originals to clones ---
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

            // --- 3. Reconnect the cloned nodes to form the correct graph structure ---
            foreach (Node originalNode in allNodesInGraph)
            {
                Node clonedNode = nodeMap[originalNode.id];

                // Reconnect children for Composite Nodes
                if (originalNode is CompositeNode originalComposite)
                {
                    var clonedComposite = clonedNode as CompositeNode;
                    // Reconnect main children (Actions, other Composites, and now Decorators)
                    originalComposite.children.ForEach(child => clonedComposite.AddChild(nodeMap[child.id]));
                    // Reconnect attached services
                    originalComposite.services.ForEach(service => clonedComposite.services.Add(nodeMap[service.id] as ServiceNode));
                }
                // Reconnect the single child for Auxiliary Nodes (which is the base for Decorator)
                else if (originalNode is AuxiliaryNode originalAuxiliary && originalAuxiliary.child != null)
                {
                    var clonedAuxiliary = clonedNode as AuxiliaryNode;
                    clonedAuxiliary.AddChild(nodeMap[originalAuxiliary.child.id]);
                }
                // Reconnect the child for the Root Node
                else if (originalNode is RootNode originalRoot && originalRoot.child != null)
                {
                    var clonedRoot = clonedNode as RootNode;
                    clonedRoot.child = nodeMap[originalRoot.child.id];
                }
            }
            
            // --- 4. Clone the blackboard and assign the owner tree reference ---

            // Clone the blackboard instance so the runtime tree has its own state.
            if (this.blackboard != null)
            {
                tree.blackboard = this.blackboard.Clone();
            }

            // Assign the owner tree to all cloned nodes. This allows each node
            // to access the runtime tree's properties, most importantly, its blackboard.
            foreach (var clonedNode in tree.nodes)
            {
                clonedNode.ownerTree = tree;
            }

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