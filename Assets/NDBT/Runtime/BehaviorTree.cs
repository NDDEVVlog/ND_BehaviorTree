// --- START OF FILE BehaviorTree.cs ---

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
        public RootNode rootNode; // Made public for easier access, but set via EditorInit

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

        /// <summary>
        /// Creates a "live" instance of the tree asset. Call this once per agent.
        /// This is crucial for allowing multiple agents to run the same tree logic
        /// independently, each with its own state.
        /// </summary>
        public BehaviorTree Clone()
        {
            // Create a new BehaviorTree instance
            BehaviorTree tree = Instantiate(this);
            tree.m_nodes = new List<Node>();

            // --- START OF FIX ---

            // Step 1: Traverse the entire tree from the root to find ALL nodes.
            // This is more robust than relying on the serialized m_nodes list, which
            // can be incomplete if editor scripts don't add all nodes to it.
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

                // Add standard children to the traversal stack
                foreach (var child in currentNode.GetChildren())
                {
                    nodesToVisit.Push(child);
                }

                // If it's a composite node, also add its decorators and services
                if (currentNode is CompositeNode composite)
                {
                    composite.decorators.ForEach(d => nodesToVisit.Push(d));
                    composite.services.ForEach(s => nodesToVisit.Push(s));
                }
            }

            // --- END OF FIX ---


            // Use a dictionary to map original node GUIDs to their new cloned instances
            var nodeMap = new Dictionary<string, Node>();

            // First pass: Clone all nodes found during traversal and populate the map
            foreach (Node originalNode in allNodesInGraph) // Use our complete list
            {
                Node clone = originalNode.Clone();
                tree.m_nodes.Add(clone);
                nodeMap.Add(originalNode.id, clone);

                // If this is the root node, assign it to the new tree's root
                if (originalNode == this.rootNode)
                {
                    tree.rootNode = clone as RootNode;
                }
            }

            // Second pass: Reconnect all the cloned nodes to each other
            foreach (Node originalNode in allNodesInGraph) // Use our complete list again
            {
                Node clonedNode = nodeMap[originalNode.id];

                // Re-link children for Composite nodes (Sequence, Selector)
                if (originalNode is CompositeNode originalComposite)
                {
                    var clonedComposite = clonedNode as CompositeNode;
                    originalComposite.children.ForEach(child => clonedComposite.AddChild(nodeMap[child.id]));
                    // This will now work because all decorators were found during traversal
                    originalComposite.decorators.ForEach(decorator => clonedComposite.decorators.Add(nodeMap[decorator.id] as DecoratorNode));
                    originalComposite.services.ForEach(service => clonedComposite.services.Add(nodeMap[service.id] as ServiceNode));
                }
                // Re-link children for Auxiliary nodes (Inverter, other decorators)
                else if (originalNode is AuxiliaryNode originalAuxiliary && originalAuxiliary.child != null)
                {
                    var clonedAuxiliary = clonedNode as AuxiliaryNode;
                    clonedAuxiliary.AddChild(nodeMap[originalAuxiliary.child.id]);
                }
                // Re-link child for the Root node
                else if (originalNode is RootNode originalRoot && originalRoot.child != null)
                {
                    var clonedRoot = clonedNode as RootNode;
                    clonedRoot.child = nodeMap[originalRoot.child.id];
                }
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