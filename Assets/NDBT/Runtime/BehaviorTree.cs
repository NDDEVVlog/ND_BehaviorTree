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
        [HideInInspector]
        [SerializeField]
        public List<ND_BTConnection> m_connection = new List<ND_BTConnection>();

        // Editor-facing properties
        public List<Node> nodes => m_nodes;
        public List<ND_BTConnection> connections => m_connection;

        // Runtime properties for the cloned tree instance
        private RootNode rootNode;
        private Node.Status treeStatus = Node.Status.Running;

        /// <summary>
        /// Called by an agent's MonoBehaviour to execute the tree logic each frame.
        /// </summary>
        public Node.Status Update()
        {
            if (rootNode == null) return Node.Status.Failure;

            if (treeStatus != Node.Status.Running)
            {
                // Reset the tree for a new run if it's not currently running.
                // This allows the tree to be re-evaluated after succeeding or failing.
                rootNode.Reset();
            }

            treeStatus = rootNode.Process();
            return treeStatus;
        }

        /// <summary>
        /// Creates a "live" instance of the tree asset. Call this once per agent.
        /// </summary>
        public BehaviorTree Clone()
        {
            BehaviorTree tree = Instantiate(this);
            tree.name = this.name + " (Runtime Clone)";

            // Create a dictionary to map the original node GUIDs to their new cloned instances
            var clonedNodes = new Dictionary<string, Node>();
            foreach (var nodeAsset in this.nodes)
            {
                clonedNodes[nodeAsset.id] = nodeAsset.Clone();
            }

            // Use the connection data to build the runtime hierarchy between the cloned nodes
            foreach (var connection in this.connections)
            {
                if (clonedNodes.TryGetValue(connection.outputPort.nodeID, out Node parentNode) &&
                    clonedNodes.TryGetValue(connection.inputPort.nodeID, out Node childNode))
                {
                    if (parentNode is CompositeNode composite)
                    {
                        composite.children.Add(childNode);
                    }
                    else if (parentNode is DecoratorNode decorator)
                    {
                        decorator.child = childNode;
                    }
                    else if (parentNode is RootNode root)
                    {
                        root.child = childNode;
                    }
                }
            }

            // Sort children of composite nodes to respect the visual layout from the graph editor
            var originalNodePositions = this.nodes.ToDictionary(n => n.id, n => n.position);
            foreach (var node in clonedNodes.Values)
            {
                if (node is CompositeNode composite)
                {
                    composite.children.Sort((a, b) =>
                        originalNodePositions[a.id].x.CompareTo(originalNodePositions[b.id].x));
                }
            }

            // Find the root node of the newly created cloned tree
            tree.rootNode = clonedNodes.Values.OfType<RootNode>().FirstOrDefault();
            if (tree.rootNode == null)
            {
                Debug.LogError($"RootNode not found in BehaviorTree asset '{this.name}'. A tree must have a RootNode.", this);
            }

            return tree;
        }
        

        #if UNITY_EDITOR
        /// <summary>
        /// Initializes the BehaviorTree asset in the editor.
        /// Ensures that a RootNode exists. If not, it creates and adds one.
        /// This should be called from the EditorWindow when a tree is loaded.
        /// </summary>
        public void EditorInit()
        {
            // Check if a root node already exists.
            if (this.nodes.OfType<RootNode>().Any())
            {
                return; // A root node already exists, no action needed.
            }

            // If no root node exists, create one.
            Debug.Log($"BehaviorTree '{this.name}' has no RootNode. Creating one.");
            
            RootNode root = ScriptableObject.CreateInstance<RootNode>();
            root.name = "Root";
            // Give it a default position in the graph view.
            root.position = new Rect(250, 100, 150, 100);

            // Add the new root node to the asset file.
            // This is crucial for it to be saved correctly with the main BehaviorTree asset.
            AssetDatabase.AddObjectToAsset(root, this);
            
            // Add the new node to our internal list.
            this.nodes.Add(root);

            // Mark the asset as dirty and save the changes.
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif

    }
}
// --- END OF FILE BehaviorTree.cs ---