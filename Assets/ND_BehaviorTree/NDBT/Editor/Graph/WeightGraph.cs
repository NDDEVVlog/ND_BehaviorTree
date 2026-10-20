using System.Reflection;
using System.Text;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Graph
{
    /// <summary>Editor-side questions about weights and node descriptions (looks through reroutes like the runtime does).</summary>
    public static class WeightGraph
    {
        /// <summary>
        /// The node whose children list leads to <paramref name="node"/> (directly or through reroutes).
        /// <paramref name="slot"/> is the entry in that list (the node itself or the first reroute).
        /// </summary>
        public static Node FindLogicalParent(BehaviorTree tree, Node node, out Node slot)
        {
            slot = null;
            if (tree == null || node == null || node is RerouteNode) return null;
            foreach (var candidate in tree.nodes)
            {
                if (candidate == null || candidate is RerouteNode) continue;
                foreach (var child in candidate.GetChildren())
                {
                    if (child == null || RerouteNode.Resolve(child) != node) continue;
                    slot = child;
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>The weighted composite parent of <paramref name="node"/>, or null when its weight is not used.</summary>
        public static IWeightedComposite WeightedParentOf(BehaviorTree tree, Node node) =>
            FindLogicalParent(tree, node, out _) as IWeightedComposite;

        /// <summary>The old default (priority 0) and the new default (1) both mean "not set".</summary>
        public static bool IsUnsetWeight(float weight) => weight == 0f || Mathf.Approximately(weight, 1f);

        /// <summary>"×Menace +Fear" from the node's attached weight modifiers, or null.</summary>
        public static string ModifierText(Node node)
        {
            if (node == null) return null;
            StringBuilder sb = null;
            foreach (var decorator in node.decorators)
            {
                if (!(decorator is IWeightModifier modifier)) continue;
                sb ??= new StringBuilder();
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(modifier.Describe());
            }
            return sb?.ToString();
        }

        public static NodeDescriptionAttribute DescriptionOf(System.Type type) =>
            type?.GetCustomAttribute<NodeDescriptionAttribute>(false);

        /// <summary>Node title for messages: display name, else the NodeInfo title, else the type name.</summary>
        public static string TitleOf(Node node)
        {
            if (node == null) return "(none)";
            if (!string.IsNullOrEmpty(node.typeName)) return node.typeName;
            return node.GetType().GetCustomAttribute<NodeInfoAttribute>()?.title ?? node.GetType().Name;
        }
    }
}
