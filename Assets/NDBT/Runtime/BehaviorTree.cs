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

        public Node.Status Update()
        {
            if (rootNode == null) return Node.Status.Failure;

            if (treeStatus != Node.Status.Running)
            {
                rootNode.Reset();
            }

            treeStatus = rootNode.Process();
            return treeStatus;
        }

        public BehaviorTree Clone()
        {
            BehaviorTree tree = Instantiate(this);
            tree.m_nodes = new List<Node>();

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
                    composite.decorators.ForEach(d => nodesToVisit.Push(d));
                    composite.services.ForEach(s => nodesToVisit.Push(s));
                }
            }

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

            foreach (Node originalNode in allNodesInGraph)
            {
                Node clonedNode = nodeMap[originalNode.id];
                if (originalNode is CompositeNode originalComposite)
                {
                    var clonedComposite = clonedNode as CompositeNode;
                    originalComposite.children.ForEach(child => clonedComposite.AddChild(nodeMap[child.id]));
                    originalComposite.decorators.ForEach(decorator => clonedComposite.decorators.Add(nodeMap[decorator.id] as DecoratorNode));
                    originalComposite.services.ForEach(service => clonedComposite.services.Add(nodeMap[service.id] as ServiceNode));
                }
                else if (originalNode is AuxiliaryNode originalAuxiliary && originalAuxiliary.child != null)
                {
                    var clonedAuxiliary = clonedNode as AuxiliaryNode;
                    clonedAuxiliary.AddChild(nodeMap[originalAuxiliary.child.id]);
                }
                else if (originalNode is RootNode originalRoot && originalRoot.child != null)
                {
                    var clonedRoot = clonedNode as RootNode;
                    clonedRoot.child = nodeMap[originalRoot.child.id];
                }
            }
            
            // --- MODIFICATION START ---

            // Now that the graph structure is cloned, clone the blackboard instance.
            // This ensures that the runtime tree has its own blackboard state. This will be the
            // default blackboard, which can be replaced by the BehaviorTreeRunner.
            if (this.blackboard != null)
            {
                tree.blackboard = this.blackboard.Clone();
            }

            // Finally, assign the owner tree to all cloned nodes. This allows each node
            // to access the runtime tree's properties, most importantly, its blackboard.
            foreach (var clonedNode in tree.nodes)
            {
                clonedNode.ownerTree = tree;
            }
            
            // --- MODIFICATION END ---

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