using System.Collections.Generic;
using System.Linq;

namespace ND_BehaviorTree.Editor.Graph
{
    public enum GraphIssueSeverity { Warning, Error }

    public readonly struct GraphIssue
    {
        public readonly GraphIssueSeverity Severity;
        public readonly string Code;
        public readonly string Message;
        public readonly Node Node;

        public GraphIssue(GraphIssueSeverity severity, string code, string message, Node node = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Node = node;
        }

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    /// <summary>
    /// Read-only structural validation of a BehaviorTree. Never mutates the tree.
    /// </summary>
    public static class GraphValidator
    {
        public const string RootMissing = "root.missing";
        public const string RootMultiple = "root.multiple";
        public const string RootNotInTree = "root.not-in-tree";
        public const string NodeNull = "node.null";
        public const string NodeUnreachable = "node.unreachable";
        public const string ChildNull = "child.null";
        public const string ChildNotInTree = "child.not-in-tree";
        public const string ChildMultipleParents = "child.multiple-parents";
        public const string ServiceNull = "service.null";
        public const string Cycle = "graph.cycle";
        public const string OrderNotByPosition = "order.not-by-position";
        public const string DecoratorStandalone = "decorator.standalone";
        public const string DecoratorNull = "decorator.null";
        public const string DecoratorHasChild = "decorator.has-child";
        public const string SubtreeMissing = "subtree.missing";
        public const string SubtreeRecursive = "subtree.recursive";
        public const string EventNameEmpty = "event.name-empty";
        public const string EventDuplicateName = "event.duplicate-name";
        public const string EventNoChild = "event.no-child";
        public const string InitMultiple = "init.multiple";
        public const string AttachmentListedAsNode = "attachment.listed-as-node";
        public const string InitNoChild = "init.no-child";
        public const string RerouteNoChild = "reroute.no-child";
        public const string WeightUnused = "weight.unused";
        public const string WeightModifierUnused = "weight.modifier-unused";
        public const string WeightNeverPicked = "weight.never-picked";

        public static List<GraphIssue> Validate(BehaviorTree tree)
        {
            var issues = new List<GraphIssue>();
            if (tree == null)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, RootMissing, "Tree is null."));
                return issues;
            }

            var nodes = tree.nodes;
            var nodeSet = new HashSet<Node>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null)
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, NodeNull, $"nodes[{i}] is null (deleted or missing script)."));
                else
                    nodeSet.Add(nodes[i]);
            }

            var roots = nodeSet.OfType<RootNode>().ToList();
            if (tree.rootNode == null)
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, RootMissing, "Tree has no rootNode assigned."));
            else if (!nodeSet.Contains(tree.rootNode))
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, RootNotInTree, "rootNode is not in the tree's node list.", tree.rootNode));
            if (roots.Count > 1)
            {
                foreach (var extra in roots.Where(r => r != tree.rootNode))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, RootMultiple, $"Extra RootNode '{extra.name}' ({extra.id}). Only tree.rootNode executes.", extra));
            }

            var parentCount = new Dictionary<Node, int>();
            foreach (var node in nodeSet)
            {
                var children = node.GetChildren();
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null)
                    {
                        issues.Add(new GraphIssue(GraphIssueSeverity.Error, ChildNull, $"'{node.name}' has a null child at index {i}.", node));
                        continue;
                    }
                    if (!nodeSet.Contains(child))
                        issues.Add(new GraphIssue(GraphIssueSeverity.Error, ChildNotInTree, $"'{node.name}' references child '{child.name}' that is not in this tree.", node));
                    parentCount.TryGetValue(child, out int c);
                    parentCount[child] = c + 1;
                }

                if (node is DecoratorNode)
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, DecoratorStandalone,
                        $"'{node.name}' is a legacy standalone decorator that could not be migrated. Re-attach it with 'Add Decorator' on the target node.", node));

                if (node.decorators.Any(d => d == null))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, DecoratorNull, $"'{node.name}' has a null decorator entry.", node));
                foreach (var decorator in node.decorators.Where(d => d != null && d.child != null))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, DecoratorHasChild,
                        $"Attached decorator '{decorator.name}' on '{node.name}' has its own child '{decorator.child.name}'; attached decorators must not.", node));

                if (node is SubtreeNode subtreeNode)
                {
                    if (subtreeNode.subtree == null)
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning, SubtreeMissing, $"'{node.name}' has no subtree asset assigned; it will fail at runtime.", node));
                    else if (subtreeNode.subtree == tree || SubtreeReaches(subtreeNode.subtree, tree))
                        issues.Add(new GraphIssue(GraphIssueSeverity.Error, SubtreeRecursive, $"'{node.name}' runs '{subtreeNode.subtree.name}', which runs '{tree.name}' again (infinite recursion).", node));
                }

                if (node.services.Any(s => s == null))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, ServiceNull, $"'{node.name}' has a null/missing service entry.", node));
                foreach (var attachment in node.services.Cast<Node>().Concat(node.decorators).Where(a => a != null && nodeSet.Contains(a)))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, AttachmentListedAsNode,
                        $"'{attachment.name}' is attached to '{node.name}' and also listed as a graph node. Reopen the tree to migrate it.", attachment));

                if (node is CompositeNode composite)
                {
                    if (!ExecutionOrder.IsSortedByPosition(composite.children))
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning, OrderNotByPosition,
                            $"Children of '{node.name}' do not run left-to-right. Runtime uses list order; use 'Sort Children Left-to-Right' to align it.", node));
                }
            }

            foreach (var kvp in parentCount.Where(p => p.Value > 1))
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, ChildMultipleParents, $"'{kvp.Key.name}' has {kvp.Value} parents.", kvp.Key));

            foreach (var node in FindNodesOnCycles(nodeSet))
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, Cycle, $"'{node.name}' is part of a cycle.", node));

            var events = nodeSet.OfType<EventNode>().ToList();
            foreach (var ev in events)
            {
                if (string.IsNullOrWhiteSpace(ev.eventName))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, EventNameEmpty, $"Event node '{ev.name}' has no event name; it can never be sent.", ev));
                if (ev.child == null)
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, EventNoChild, $"Event '{ev.eventName}' has no child; sending it does nothing.", ev));
            }
            foreach (var dup in events.Where(e => !string.IsNullOrWhiteSpace(e.eventName)).GroupBy(e => e.eventName).Where(g => g.Count() > 1))
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, EventDuplicateName, $"{dup.Count()} event nodes are named '{dup.Key}'; SendEvent starts all of them.", dup.First()));

            var inits = nodeSet.OfType<InitNode>().ToList();
            if (inits.Count > 1)
                foreach (var extra in inits.Skip(1))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, InitMultiple, $"Only one Init node is allowed; '{extra.name}' will not run.", extra));
            foreach (var init in inits.Where(i => i.child == null))
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, InitNoChild, "The Init node has no child; it does nothing.", init));
            foreach (var reroute in tree.nodes.OfType<RerouteNode>().Where(r => r.child == null))
                issues.Add(new GraphIssue(GraphIssueSeverity.Warning, RerouteNoChild, $"Reroute '{reroute.name}' has no child; the branch through it does nothing.", reroute));

            ValidateWeights(tree, issues);

            if (tree.rootNode != null)
            {
                // Entry points: the root, every event node and the init node.
                var reachable = CollectReachable(tree.rootNode);
                foreach (var ev in events) reachable.UnionWith(CollectReachable(ev));
                foreach (var init in inits) reachable.UnionWith(CollectReachable(init));
                foreach (var node in nodeSet.Where(n => !reachable.Contains(n) && !(n is RootNode)))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, NodeUnreachable, $"'{node.name}' is not reachable from the root or an event and will not run.", node));
            }

            return issues;
        }

        /// <summary>True if running <paramref name="from"/> would (directly or through nested subtrees) run <paramref name="target"/>.</summary>
        public static bool SubtreeReaches(BehaviorTree from, BehaviorTree target)
        {
            var visited = new HashSet<BehaviorTree>();
            var stack = new Stack<BehaviorTree>();
            stack.Push(from);
            while (stack.Count > 0)
            {
                var tree = stack.Pop();
                if (tree == null || !visited.Add(tree)) continue;
                if (tree == target) return true;
                foreach (var s in tree.nodes.OfType<SubtreeNode>()) stack.Push(s.subtree);
            }
            return false;
        }

        /// <summary>Weights only matter under a weighted composite (Random Rate / Utility Selector).</summary>
        private static void ValidateWeights(BehaviorTree tree, List<GraphIssue> issues)
        {
            foreach (var node in tree.nodes)
            {
                if (node == null || node is RerouteNode) continue;
                var parent = WeightGraph.FindLogicalParent(tree, node, out _);
                bool weighted = parent is IWeightedComposite;
                bool hasModifier = node.decorators.Any(d => d is IWeightModifier);

                if (!weighted && !WeightGraph.IsUnsetWeight(node.weight))
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, WeightUnused,
                        $"'{WeightGraph.TitleOf(node)}' has weight {node.weight:0.##}, but its parent '{WeightGraph.TitleOf(parent)}' runs children left to right and ignores it.", node));
                if (!weighted && hasModifier)
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, WeightModifierUnused,
                        $"'{WeightGraph.TitleOf(node)}' has a weight modifier, but its parent '{WeightGraph.TitleOf(parent)}' does not pick children by weight.", node));
                if (weighted && node.weight <= 0f && !hasModifier)
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, WeightNeverPicked,
                        $"'{WeightGraph.TitleOf(node)}' has weight {node.weight:0.##} under '{WeightGraph.TitleOf(parent)}', so it is never picked.", node));
            }
        }

        public static HashSet<Node> CollectReachable(Node start)
        {
            var visited = new HashSet<Node>();
            var stack = new Stack<Node>();
            if (start != null) stack.Push(start);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n == null || !visited.Add(n)) continue;
                foreach (var c in n.GetChildren()) stack.Push(c);
            }
            return visited;
        }

        private static List<Node> FindNodesOnCycles(HashSet<Node> nodeSet)
        {
            // Iterative DFS with colors: 0 = unvisited, 1 = on stack, 2 = done.
            var color = new Dictionary<Node, int>();
            var onCycle = new HashSet<Node>();
            foreach (var start in nodeSet)
            {
                if (color.ContainsKey(start)) continue;
                var path = new List<Node>();
                var stack = new Stack<(Node node, int childIndex)>();
                stack.Push((start, 0));
                color[start] = 1;
                path.Add(start);

                while (stack.Count > 0)
                {
                    var (node, idx) = stack.Pop();
                    var children = node.GetChildren();
                    if (idx < children.Count)
                    {
                        stack.Push((node, idx + 1));
                        var child = children[idx];
                        if (child == null || !nodeSet.Contains(child)) continue;
                        color.TryGetValue(child, out int c);
                        if (c == 0)
                        {
                            color[child] = 1;
                            path.Add(child);
                            stack.Push((child, 0));
                        }
                        else if (c == 1)
                        {
                            for (int i = path.IndexOf(child); i < path.Count; i++) onCycle.Add(path[i]);
                        }
                    }
                    else
                    {
                        color[node] = 2;
                        path.RemoveAt(path.Count - 1);
                    }
                }
            }
            return onCycle.ToList();
        }
    }
}
