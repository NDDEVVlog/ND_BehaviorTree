using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Applies a batch of graph operations as ONE history step (all or nothing).
    ///
    /// Node references: "root", a node GUID / unique prefix, or a "ref" name declared by a create/add op earlier in
    /// the same batch (written as "$name" or "name").
    ///
    /// Operations (field "op"):
    ///   create            type, ref?, position?[x,y], parent?, fields?{path: value}, label?
    ///   delete            nodes[]
    ///   connect           parent, child
    ///   disconnect        parent, child
    ///   move              node, position[x,y]
    ///   set-field         node, path, value          (or fields{path: value})
    ///   link-key          node, path, key (blackboard key name, or null to clear)
    ///   add-decorator     node, type, ref?, fields?
    ///   add-service       node, type, ref?, fields?
    ///   remove-attachment node, attachment
    ///   move-decorator    node, attachment, index
    ///   sort-children     node
    ///   group             title, nodes[]
    ///   note              title, content?, position?
    ///   set-subtree       node, subtree (tree path/GUID)
    /// On failure everything is rolled back and the error names the failing operation.
    /// dryRun: runs the real code path, reports the result and validation, then rolls back.
    /// </summary>
    public static class ApplyEngine
    {
        public static Dictionary<string, object> Execute(BehaviorTree tree, List<object> ops, bool dryRun, string label)
        {
            if (ops == null || ops.Count == 0) throw new BridgeException("No operations given.");
            var graph = new BehaviorTreeGraphService(tree);
            var history = graph.History;
            var refs = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            var log = new List<object>();
            string error = null;
            int versionBefore = history.Version;

            using (new GraphUndoScope(tree, dryRun ? "AI dry run" : $"AI: {label ?? $"{ops.Count} operation(s)"}"))
            {
                for (int i = 0; i < ops.Count; i++)
                {
                    try
                    {
                        if (!(ops[i] is Dictionary<string, object> op)) throw new BridgeException("Each operation must be an object with an \"op\" field.");
                        log.Add(ApplyOne(graph, tree, op, refs));
                    }
                    catch (BridgeException e)
                    {
                        error = $"Operation #{i + 1} ({OpName(ops[i])}) failed: {e.Message}";
                        break;
                    }
                }
            }

            bool recorded = history.Version != versionBefore;
            var issues = GraphValidator.Validate(tree);
            var created = refs.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.id);
            string preview = dryRun && error == null ? TreeExporter.ToOutline(tree) : null;

            if ((error != null || dryRun) && recorded) history.RollbackLastStep();

            var result = new Dictionary<string, object>
            {
                ["applied"] = error == null && !dryRun,
                ["dryRun"] = dryRun,
                ["operations"] = log,
                ["created"] = created,
                ["errors"] = issues.Count(x => x.Severity == GraphIssueSeverity.Error),
                ["issues"] = issues.Select(x => x.ToString()).ToList()
            };
            if (preview != null) result["preview"] = preview;
            if (error != null)
            {
                result["error"] = error;
                result["rolledBack"] = true;
            }
            return result;
        }

        private static string OpName(object op) => op is Dictionary<string, object> d && d.TryGetValue("op", out var name) ? Convert.ToString(name, CultureInfo.InvariantCulture) : "?";

        private static Node Ref(BehaviorTree tree, Dictionary<string, Node> refs, Dictionary<string, object> op, string key)
        {
            string id = BridgeUtil.Str(op, key);
            string refName = id.StartsWith("$") ? id.Substring(1) : id;
            if (refs.TryGetValue(refName, out var node) && node != null) return node;
            return BridgeUtil.ResolveNode(tree, id);
        }

        private static void Remember(Dictionary<string, Node> refs, Dictionary<string, object> op, Node node)
        {
            string refName = BridgeUtil.Str(op, "ref", required: false);
            if (string.IsNullOrEmpty(refName)) return;
            if (refName.StartsWith("$")) refName = refName.Substring(1);
            if (refs.ContainsKey(refName)) throw new BridgeException($"ref '{refName}' is already used in this batch.");
            refs[refName] = node;
        }

        private static object ApplyOne(BehaviorTreeGraphService graph, BehaviorTree tree, Dictionary<string, object> op, Dictionary<string, Node> refs)
        {
            string name = BridgeUtil.Str(op, "op").ToLowerInvariant().Replace('_', '-');
            switch (name)
            {
                case "create":
                {
                    var type = BridgeUtil.ResolveNodeType(BridgeUtil.Str(op, "type"), BridgeUtil.TypeKind.GraphNode);
                    Node parent = op.ContainsKey("parent") ? Ref(tree, refs, op, "parent") : null;
                    Vector2 fallback = parent != null ? parent.position.position + new Vector2(parent.GetChildren().Count * 220f, 180f) : Vector2.zero;
                    var node = graph.CreateNode(type, BridgeUtil.Vec2(op.TryGetValue("position", out var p) ? p : null, fallback));
                    if (node == null) throw new BridgeException($"Could not create '{type.Name}' (see Unity console).");
                    Remember(refs, op, node);
                    var label = BridgeUtil.Str(op, "label", required: false);
                    if (!string.IsNullOrEmpty(label)) SetFields(node, new Dictionary<string, object> { ["typeName"] = label });
                    SetFields(node, BridgeUtil.Dict(op, "fields"));
                    if (parent != null) Connect(graph, parent, node);
                    return Done(name, node);
                }
                case "delete":
                {
                    var nodes = BridgeUtil.List(op, "nodes").Select(n => Ref(tree, refs, new Dictionary<string, object> { ["id"] = n }, "id")).ToList();
                    if (nodes.Any(n => n is RootNode)) throw new BridgeException("The Root node cannot be deleted.");
                    graph.DeleteNodes(nodes);
                    return new Dictionary<string, object> { ["op"] = name, ["deleted"] = nodes.Count };
                }
                case "connect":
                {
                    var parent = Ref(tree, refs, op, "parent");
                    var child = Ref(tree, refs, op, "child");
                    Connect(graph, parent, child);
                    return Done(name, child);
                }
                case "disconnect":
                {
                    var parent = Ref(tree, refs, op, "parent");
                    var child = Ref(tree, refs, op, "child");
                    if (!graph.Disconnect(parent, child)) throw new BridgeException($"'{parent.name}' and '{child.name}' are not connected.");
                    return Done(name, child);
                }
                case "move":
                {
                    var node = Ref(tree, refs, op, "node");
                    graph.MoveNodes(new Dictionary<Node, Vector2> { { node, BridgeUtil.Vec2(op.TryGetValue("position", out var p) ? p : null, node.position.position) } });
                    return Done(name, node);
                }
                case "set-field":
                {
                    var node = Ref(tree, refs, op, "node");
                    var fields = BridgeUtil.Dict(op, "fields") ?? new Dictionary<string, object> { [BridgeUtil.Str(op, "path")] = op.TryGetValue("value", out var v) ? v : null };
                    SetFields(node, fields);
                    return Done(name, node);
                }
                case "link-key":
                {
                    var node = Ref(tree, refs, op, "node");
                    string path = BridgeUtil.Str(op, "path");
                    string keyName = BridgeUtil.Str(op, "key", required: false);
                    Key key = null;
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        if (tree.blackboard == null) throw new BridgeException($"Tree '{tree.name}' has no blackboard; create keys first (blackboard_key).");
                        key = tree.blackboard.keys.FirstOrDefault(k => k != null && k.keyName == keyName)
                              ?? throw new BridgeException($"No key '{keyName}' on blackboard '{tree.blackboard.name}'. Keys: {string.Join(", ", tree.blackboard.keys.Where(k => k != null).Select(k => k.keyName))}.");
                    }
                    if (!graph.AssignBlackboardKey(node, path, key, out string reason)) throw new BridgeException(reason);
                    return Done(name, node);
                }
                case "add-decorator":
                case "add-service":
                {
                    var owner = Ref(tree, refs, op, "node");
                    bool decorator = name == "add-decorator";
                    var type = BridgeUtil.ResolveNodeType(BridgeUtil.Str(op, "type"), decorator ? BridgeUtil.TypeKind.Decorator : BridgeUtil.TypeKind.Service);
                    string reason;
                    if (decorator ? !graph.CanAttachDecorator(owner, out reason) : !graph.CanAttachService(owner, out reason)) throw new BridgeException(reason);
                    Node attachment = decorator ? (Node)graph.AddDecorator(owner, type) : graph.AddService(owner, type);
                    if (attachment == null) throw new BridgeException($"Could not attach '{type.Name}' to '{owner.name}'.");
                    Remember(refs, op, attachment);
                    SetFields(attachment, BridgeUtil.Dict(op, "fields"));
                    return Done(name, attachment);
                }
                case "remove-attachment":
                {
                    var owner = Ref(tree, refs, op, "node");
                    var attachment = Ref(tree, refs, op, "attachment");
                    bool ok = attachment is DecoratorNode d ? graph.RemoveDecorator(owner, d) : attachment is ServiceNode s && graph.RemoveService(owner, s);
                    if (!ok) throw new BridgeException($"'{attachment.name}' is not attached to '{owner.name}'.");
                    return new Dictionary<string, object> { ["op"] = name, ["removed"] = attachment.id };
                }
                case "move-decorator":
                {
                    var owner = Ref(tree, refs, op, "node");
                    var decorator = Ref(tree, refs, op, "attachment") as DecoratorNode ?? throw new BridgeException("'attachment' must be a decorator.");
                    int index = Convert.ToInt32(op.TryGetValue("index", out var ix) ? ix : 0, CultureInfo.InvariantCulture);
                    if (!graph.MoveDecorator(owner, decorator, index)) throw new BridgeException($"Cannot move decorator to index {index}.");
                    return Done(name, decorator);
                }
                case "sort-children":
                {
                    var node = Ref(tree, refs, op, "node") as CompositeNode ?? throw new BridgeException("'node' must be a composite.");
                    graph.SortChildrenByPosition(node);
                    return Done(name, node);
                }
                case "group":
                {
                    var nodes = BridgeUtil.List(op, "nodes").Select(n => Ref(tree, refs, new Dictionary<string, object> { ["id"] = n }, "id")).ToList();
                    if (nodes.Count == 0) throw new BridgeException("A group needs at least one node.");
                    float xMin = nodes.Min(n => n.position.x) - 30, yMin = nodes.Min(n => n.position.y) - 50;
                    float xMax = nodes.Max(n => n.position.x) + 230, yMax = nodes.Max(n => n.position.y) + 120;
                    var group = graph.CreateGroup(BridgeUtil.Str(op, "title", required: false) ?? "Group", Rect.MinMaxRect(xMin, yMin, xMax, yMax), nodes);
                    return new Dictionary<string, object> { ["op"] = name, ["group"] = group.id };
                }
                case "note":
                {
                    var position = BridgeUtil.Vec2(op.TryGetValue("position", out var p) ? p : null, Vector2.zero);
                    var note = graph.CreateNote(new Rect(position, new Vector2(240, 140)), BridgeUtil.Str(op, "title", required: false) ?? "Note", BridgeUtil.Str(op, "content", required: false) ?? string.Empty);
                    return new Dictionary<string, object> { ["op"] = name, ["note"] = note.id };
                }
                case "set-subtree":
                {
                    var node = Ref(tree, refs, op, "node") as SubtreeNode ?? throw new BridgeException("'node' must be a Run Subtree node.");
                    var subtree = BridgeUtil.ResolveTree(BridgeUtil.Str(op, "subtree"));
                    if (!graph.CanAssignSubtree(node, subtree, out string reason)) throw new BridgeException(reason);
                    graph.AssignSubtree(node, subtree);
                    return Done(name, node);
                }
                default:
                    throw new BridgeException($"Unknown op '{name}'. Supported: create, delete, connect, disconnect, move, set-field, link-key, add-decorator, add-service, remove-attachment, move-decorator, sort-children, group, note, set-subtree.");
            }
        }

        private static void Connect(BehaviorTreeGraphService graph, Node parent, Node child)
        {
            if (!graph.CanConnect(parent, child, out string reason)) throw new BridgeException($"Cannot connect '{parent.name}' → '{child.name}': {reason}.");
            graph.Connect(parent, child);
        }

        private static Dictionary<string, object> Done(string op, Node node) =>
            new Dictionary<string, object> { ["op"] = op, ["node"] = node.id, ["shortId"] = BridgeUtil.ShortId(node) };

        /// <summary>Sets serialized fields by property path (inside the open history transaction).</summary>
        public static void SetFields(Node node, Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0) return;
            var so = new SerializedObject(node);
            foreach (var kvp in fields)
            {
                var prop = so.FindProperty(kvp.Key);
                if (prop == null)
                {
                    var known = BridgeUtil.FieldsOf(node).Keys;
                    throw new BridgeException($"'{node.GetType().Name}' has no field '{kvp.Key}'. Fields: {string.Join(", ", known)}.");
                }
                if (prop.propertyType == SerializedPropertyType.ObjectReference && KeySlotScanner.Scan(node).Any(s => s.PropertyPath == kvp.Key))
                    throw new BridgeException($"'{kvp.Key}' is a blackboard key field: use the link-key operation.");
                BridgeUtil.SetProperty(prop, kvp.Value);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);
        }
    }
}
