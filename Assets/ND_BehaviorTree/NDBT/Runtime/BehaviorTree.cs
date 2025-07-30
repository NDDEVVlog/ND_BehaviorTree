// --- MODIFIED FILE: BehaviorTree.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

        // Runtime state for a cloned tree
        private Node.Status treeStatus = Node.Status.Running;

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

            // --- 1. Gom tất cả các node từ cây gốc ---
            // (Phần này giữ nguyên, không cần thay đổi)
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
                
                foreach (var child in currentNode.GetChildren())
                {
                    nodesToVisit.Push(child);
                }
                
                if (currentNode is CompositeNode composite)
                {
                    composite.services.ForEach(s => nodesToVisit.Push(s));
                }
            }
            
            // --- 2. Clone từng node và tạo map ---
            // (Phần này giữ nguyên, không cần thay đổi)
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

            // --- 3. Kết nối lại các node đã clone ---
            // (*** PHẦN QUAN TRỌNG CẦN SỬA ĐỔI NẰM Ở ĐÂY ***)
            foreach (Node originalNode in allNodesInGraph)
            {
                Node clonedNode = nodeMap[originalNode.id];

                // Reconnect children for Composite Nodes
                if (originalNode is CompositeNode originalComposite)
                {
                    var clonedComposite = clonedNode as CompositeNode;
                    originalComposite.children.ForEach(child => clonedComposite.AddChild(nodeMap[child.id]));
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
                // **********************************************************
                // *** THÊM KHỐI LỆNH NÀY VÀO ĐỂ SỬA LỖI ***
                // Reconnect the child for GOAPActionNode
                else if (originalNode is GOAP.GOAPActionNode originalGoapAction && originalGoapAction.child != null)
                {
                    var clonedGoapAction = clonedNode as GOAP.GOAPActionNode;
                    // Tìm bản sao của node con trong map và gán lại
                    clonedGoapAction.child = nodeMap[originalGoapAction.child.id];
                }
                // **********************************************************
            }
            
            // --- 4. Clone blackboard và gán owner tree ---
            // (Phần này giữ nguyên, không cần thay đổi)
            if (this.blackboard != null)
            {
                tree.blackboard = this.blackboard.Clone();
            }
            
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