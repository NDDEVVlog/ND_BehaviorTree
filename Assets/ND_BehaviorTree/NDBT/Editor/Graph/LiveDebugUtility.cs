using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Graph
{
    public enum LiveState { Inactive, Running, Succeeded, Failed }

    /// <summary>
    /// Derives what the live-debug view shows from a runtime tree, using Node.debugLastFrame (set in Node.Process).
    ///   Running   = status Running and ticked this frame or the previous one.
    ///   Succeeded / Failed = finished within the last <see cref="RecentFrames"/> frames (fades out afterwards).
    ///   Inactive  = not ticked recently (including Running nodes that were abandoned without being reset).
    /// </summary>
    public static class LiveDebugUtility
    {
        public const int RecentFrames = 20;

        // Runtime structure does not change during Play, but Root/Event/Init/decorator GetChildren() allocate a new
        // list per call. Cache each node's children once; cleared whenever a new lookup is built.
        private static readonly Dictionary<Node, List<Node>> s_Children = new Dictionary<Node, List<Node>>();

        /// <summary>Drops cached runtime structure (call when live debugging stops).</summary>
        public static void ClearCache() => s_Children.Clear();

        private static List<Node> ChildrenOf(Node node)
        {
            if (!s_Children.TryGetValue(node, out var children))
            {
                children = node.GetChildren();
                s_Children[node] = children;
            }
            return children;
        }

        /// <summary>Runtime nodes by id (first wins). Runtime clones keep the asset node ids.</summary>
        public static Dictionary<string, Node> BuildLookup(BehaviorTree runtime)
        {
            s_Children.Clear();
            var lookup = new Dictionary<string, Node>();
            if (runtime == null) return lookup;
            foreach (var node in runtime.nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id) && !lookup.ContainsKey(node.id)) lookup.Add(node.id, node);
            }
            return lookup;
        }

        public static LiveState GetState(Node runtimeNode, int frame)
        {
            if (runtimeNode == null || runtimeNode.debugLastFrame < 0) return LiveState.Inactive;
            int age = frame - runtimeNode.debugLastFrame;
            if (runtimeNode.status == Node.Status.Running) return age <= 1 ? LiveState.Running : LiveState.Inactive;
            if (age > RecentFrames) return LiveState.Inactive;
            if (runtimeNode.status == Node.Status.Success) return LiveState.Succeeded;
            if (runtimeNode.status == Node.Status.Failure) return LiveState.Failed;
            return LiveState.Inactive;
        }

        /// <summary>
        /// State of the edge entering <paramref name="runtimeChild"/>. The parent enters the child through its attached
        /// decorators, so a decorator that ran (and maybe blocked the child) also lights the edge.
        /// </summary>
        public static LiveState GetEdgeState(Node runtimeChild, int frame)
        {
            if (runtimeChild == null) return LiveState.Inactive;
            Node latest = runtimeChild;
            foreach (var decorator in runtimeChild.decorators)
            {
                if (decorator != null && decorator.debugLastFrame > latest.debugLastFrame) latest = decorator;
            }
            return GetState(latest, frame);
        }

        /// <summary>
        /// Human-readable path to the deepest running node, following SubtreeNodes into their runtime clones,
        /// e.g. "Patrol ▸ Move To Transform". Null when nothing is running.
        /// </summary>
        public static string DescribeActivePath(BehaviorTree runtime, int frame, int maxDepth = 8)
        {
            s_ChainBuffer.Clear();
            GetActiveChain(runtime, frame, s_ChainBuffer, maxDepth);
            return DescribeChain(s_ChainBuffer);
        }

        private static readonly List<Node> s_ChainBuffer = new List<Node>();

        /// <summary>
        /// Fills <paramref name="chain"/> with the deepest running node at each subtree level (no allocations).
        /// Callers compare chains between ticks and build text only when the chain changes.
        /// </summary>
        public static void GetActiveChain(BehaviorTree runtime, int frame, List<Node> chain, int maxDepth = 8)
        {
            var current = runtime;
            for (int depth = 0; current != null && depth < maxDepth; depth++)
            {
                var leaf = FindActiveLeaf(current, frame);
                if (leaf == null) break;
                chain.Add(leaf);
                current = (leaf as SubtreeNode)?.RuntimeSubtree;
            }
        }

        /// <summary>"Patrol ▸ Move To Transform", or null for an empty chain.</summary>
        public static string DescribeChain(List<Node> chain)
        {
            if (chain.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0) sb.Append(" ▸ ");
                sb.Append(DisplayName(chain[i]));
            }
            return sb.ToString();
        }

        /// <summary>The running node none of whose children is running (decorators are skipped).</summary>
        public static Node FindActiveLeaf(BehaviorTree runtime, int frame)
        {
            if (runtime == null) return null;
            foreach (var node in runtime.nodes)
            {
                if (node == null || node is DecoratorNode || node is ServiceNode) continue;
                if (GetState(node, frame) != LiveState.Running) continue;

                bool childRunning = false;
                var children = ChildrenOf(node);
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child != null && GetState(child, frame) == LiveState.Running) { childRunning = true; break; }
                }
                if (!childRunning) return node;
            }
            return null;
        }

        // GetCustomAttribute creates a new attribute object on every call: cache the title per type.
        private static readonly Dictionary<System.Type, string> s_Titles = new Dictionary<System.Type, string>();

        private static string DisplayName(Node node)
        {
            if (!string.IsNullOrEmpty(node.typeName)) return node.typeName;
            var type = node.GetType();
            if (!s_Titles.TryGetValue(type, out var title))
            {
                var info = System.Attribute.GetCustomAttribute(type, typeof(NodeInfoAttribute)) as NodeInfoAttribute;
                title = info?.title ?? type.Name;
                s_Titles[type] = title;
            }
            return title;
        }
    }
}
