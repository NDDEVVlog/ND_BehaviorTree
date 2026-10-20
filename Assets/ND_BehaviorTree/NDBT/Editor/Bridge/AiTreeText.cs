using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Prompt kit for working with an AI by copy/paste (no bridge needed):
    ///   * <see cref="ToText"/> — the whole tree in the compact "ndbt-tree v1" text format (round-trips).
    ///   * <see cref="ToJsonText"/> — the complete tree JSON (same as the bridge's tree_get).
    ///   * <see cref="NodeCatalog"/> — every node type with its fields, key slots and description.
    ///   * <see cref="Import"/> — reads back text, a tree JSON, or a tree_apply operation list, and applies it
    ///     through <see cref="ApplyEngine"/> (validated, all-or-nothing, one undo step).
    /// </summary>
    public static class AiTreeText
    {
        public const string FormatHeader =
            "# ND Behavior Tree - text format \"ndbt-tree v1\" (paste an edited/generated version back with AI > Import).\n" +
            "# - One node per line. Indent 2 spaces per level: a line is a child of the nearest line above with one less indent.\n" +
            "# - Node line:  TypeName \"Optional label\" {\"field\": value} keys {\"slotPath\": \"BlackboardKey\"}\n" +
            "#   ({fields} and keys {...} are optional JSON objects; omitted fields keep their defaults.)\n" +
            "# - \"@decorator Type ...\" / \"@service Type ...\" lines attach to the node they are indented under (list them before its children).\n" +
            "# - Column 0 entry points: Root (main tree, one child), EventNode {\"eventName\":\"X\"} (runs on SendEvent), InitNode (runs once at start).\n" +
            "# - \"key Name: Type = value\" declares a blackboard key (created on import if missing). Types: Bool, Int, Float, String, Vector3, Transform, GameObject, NavMeshAgent...\n" +
            "# - Composite children run left to right = top to bottom here. Subtree: SubtreeNode {\"subtree\": \"Assets/Path/Tree.asset\"}.\n" +
            "# - Weights (Random Rate / Utility Selector children): {\"weight\": 2}; attach @decorator WeightFromKeyNode keys {\"key\": \"SomeFloat\"} to drive it.";

        #region Export

        public static string ToJsonText(BehaviorTree tree) => MiniJson.Serialize(TreeExporter.ToJson(tree), pretty: true);

        public static string ToText(BehaviorTree tree)
        {
            var sb = new StringBuilder();
            sb.AppendLine(FormatHeader);
            sb.AppendLine($"tree {tree.name}");
            if (tree.blackboard != null)
            {
                foreach (var key in tree.blackboard.keys.Where(k => k != null))
                {
                    var valueProp = new SerializedObject(key).FindProperty("value");
                    object value = valueProp != null ? BridgeUtil.PropertyToJson(valueProp, 0) : null;
                    bool simple = value is bool || value is long || value is double || value is string || value is List<object>;
                    sb.Append($"key {key.keyName}: {KeySlotScanner.FriendlyTypeName(key.GetValueType())}");
                    if (simple && !IsDefaultValue(value)) sb.Append(" = ").Append(MiniJson.Serialize(value));
                    sb.AppendLine();
                }
            }

            var written = new HashSet<Node>();
            if (tree.rootNode != null) WriteNode(sb, tree, tree.rootNode, 0, written);
            foreach (var entry in tree.nodes.Where(n => n is EventNode || n is InitNode)) WriteNode(sb, tree, entry, 0, written);

            var orphans = tree.nodes.Where(n => n != null && !(n is RerouteNode) && !written.Contains(n)).ToList();
            if (orphans.Count > 0)
            {
                sb.AppendLine("# Unconnected nodes (not part of any entry point; not imported):");
                foreach (var orphan in orphans) sb.AppendLine($"#   {TypeToken(orphan.GetType())} \"{WeightGraph.TitleOf(orphan)}\"");
            }
            return sb.ToString();
        }

        private static bool IsDefaultValue(object value) =>
            value == null || (value is bool b && !b) || (value is long l && l == 0) || (value is double d && d == 0) || (value is string s && s.Length == 0)
            || (value is List<object> list && list.All(x => x is double dd && dd == 0));

        private static void WriteNode(StringBuilder sb, BehaviorTree tree, Node node, int depth, HashSet<Node> written)
        {
            if (node == null || !written.Add(node)) return;
            sb.Append(' ', depth * 2).AppendLine(NodeLine(tree, node, node is RootNode ? "Root" : TypeToken(node.GetType())));
            foreach (var decorator in node.decorators.Where(d => d != null))
                sb.Append(' ', (depth + 1) * 2).AppendLine("@decorator " + NodeLine(tree, decorator, TypeToken(decorator.GetType())));
            foreach (var service in node.services.Where(s => s != null))
                sb.Append(' ', (depth + 1) * 2).AppendLine("@service " + NodeLine(tree, service, TypeToken(service.GetType())));
            foreach (var child in node.GetChildren())
            {
                var target = RerouteNode.Resolve(child);
                if (target != null) WriteNode(sb, tree, target, depth + 1, written);
            }
        }

        private static string NodeLine(BehaviorTree tree, Node node, string typeToken)
        {
            var sb = new StringBuilder(typeToken);
            if (!(node is RootNode) && !string.IsNullOrEmpty(node.typeName)) sb.Append(" \"").Append(node.typeName.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            if (!(node is RootNode))
            {
                var fields = NonDefaultFields(node);
                if (fields.Count > 0) sb.Append(' ').Append(MiniJson.Serialize(fields));
            }
            var keys = new Dictionary<string, object>();
            foreach (var slot in KeySlotScanner.Scan(node))
                if (slot.Current != null && KeySlotScanner.IsLinked(tree, slot.Current)) keys[slot.PropertyPath] = slot.Current.keyName;
            if (keys.Count > 0) sb.Append(" keys ").Append(MiniJson.Serialize(keys));
            return sb.ToString();
        }

        private static readonly Dictionary<Type, Dictionary<string, string>> s_Defaults = new Dictionary<Type, Dictionary<string, string>>();

        /// <summary>Serialized fields that differ from a fresh instance (key slots and the label excluded).</summary>
        private static Dictionary<string, object> NonDefaultFields(Node node)
        {
            var type = node.GetType();
            var slotPaths = KeySlotScanner.Scan(node).Select(s => s.PropertyPath).ToList();
            if (!s_Defaults.TryGetValue(type, out var defaults))
            {
                var fresh = (Node)ScriptableObject.CreateInstance(type);
                try
                {
                    defaults = BridgeUtil.FieldsOf(fresh, KeySlotScanner.Scan(fresh).Select(s => s.PropertyPath).ToList())
                        .ToDictionary(kvp => kvp.Key, kvp => MiniJson.Serialize(kvp.Value));
                }
                finally { Object.DestroyImmediate(fresh); }
                s_Defaults[type] = defaults;
            }

            var result = new Dictionary<string, object>();
            foreach (var kvp in BridgeUtil.FieldsOf(node, slotPaths))
            {
                if (kvp.Key == "typeName") continue;
                if (defaults.TryGetValue(kvp.Key, out var def) && def == MiniJson.Serialize(kvp.Value)) continue;
                result[kvp.Key] = kvp.Value is string s && s.Contains("#") && s.StartsWith("Assets/") ? s.Substring(0, s.IndexOf('#')) : kvp.Value;
            }
            return result;
        }

        /// <summary>Short class name, or the full name when another node type shares it.</summary>
        private static string TypeToken(Type type) =>
            BridgeUtil.NodeTypes().Count(t => t.Name == type.Name) > 1 ? type.FullName : type.Name;

        #endregion

        #region Node catalog

        public static string NodeCatalog()
        {
            var types = BridgeUtil.NodeTypes()
                .Where(t => !typeof(RootNode).IsAssignableFrom(t) && !typeof(RerouteNode).IsAssignableFrom(t))
                .Where(t => t.Namespace == null || !t.Namespace.Contains(".Tests"))
                .OrderBy(t => BridgeUtil.KindOf(t)).ThenBy(t => BridgeUtil.Info(t)?.menuItem ?? t.Name)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# ND Behavior Tree node catalog: {types.Count} node types available in this project.");
            sb.AppendLine("# Use the TypeName shown first on each entry in the ndbt-tree text format (below) or in tree_apply operations.");
            string kind = null;
            foreach (var type in types)
            {
                var d = BridgeCommands.DescribeNodeType(type);
                if ((string)d["kind"] != kind)
                {
                    kind = (string)d["kind"];
                    sb.AppendLine().AppendLine($"## {kind} ({(kind == "decorator" ? "attach with @decorator" : kind == "service" ? "attach with @service" : "node line")})");
                }
                sb.Append($"- {TypeToken(type)} ({d["title"]})");
                if (d["description"] is string summary) sb.Append(": ").Append(summary);
                sb.AppendLine();
                if (kind != "decorator" && kind != "service") sb.AppendLine($"    children: {d["children"]}");

                var fields = ((Dictionary<string, object>)d["fields"]).Where(kvp => kvp.Key != "typeName" && kvp.Key != "weight").ToList();
                if (fields.Count > 0)
                    sb.AppendLine("    fields: " + string.Join("; ", fields.Select(kvp =>
                    {
                        var f = (Dictionary<string, object>)kvp.Value;
                        return $"{kvp.Key}: {f["type"]} = {MiniJson.Serialize(f["default"])}";
                    })));
                var slots = (List<object>)d["keySlots"];
                if (slots.Count > 0)
                    sb.AppendLine("    keys: " + string.Join("; ", slots.Cast<Dictionary<string, object>>().Select(s => $"{s["path"]} ({s["requires"]})")));
                if (d["details"] is string details) sb.AppendLine("    details: " + details.Replace("\n", " "));
            }
            sb.AppendLine().AppendLine(FormatHeader);
            return sb.ToString();
        }

        #endregion

        #region Import

        private sealed class NodeSpec
        {
            public Type Type;
            public bool IsRoot;
            public string Label;
            public Dictionary<string, object> Fields = new Dictionary<string, object>();
            public Dictionary<string, string> Keys = new Dictionary<string, string>();
            public readonly List<NodeSpec> Decorators = new List<NodeSpec>();
            public readonly List<NodeSpec> Services = new List<NodeSpec>();
            public readonly List<NodeSpec> Children = new List<NodeSpec>();
            public int Line;
        }

        private sealed class KeySpec { public string Name, Type; public bool HasValue; public object Value; }

        private sealed class Document
        {
            public readonly List<NodeSpec> Entries = new List<NodeSpec>();
            public readonly List<KeySpec> Keys = new List<KeySpec>();
            public List<object> RawOps;
        }

        /// <summary>
        /// Imports text (ndbt-tree v1), a tree JSON (as exported) or a tree_apply operation list.
        /// Text and tree JSON REPLACE the tree's content; an operation list is applied on top of it.
        /// Blackboard keys declared but missing are created (not in a dry run). Returns the ApplyEngine result
        /// (plus "error" when the input cannot be parsed).
        /// </summary>
        public static Dictionary<string, object> Import(BehaviorTree tree, string input, bool dryRun)
        {
            Document doc;
            try { doc = Parse(input); }
            catch (BridgeException e) { return new Dictionary<string, object> { ["error"] = e.Message, ["applied"] = false }; }

            if (doc.RawOps != null) return Finish(tree, ApplyEngine.Execute(tree, doc.RawOps, dryRun, "AI import (operations)"), dryRun);

            if (doc.Entries.Count == 0)
                return new Dictionary<string, object> { ["error"] = "Nothing to import: no Root, EventNode or InitNode line found.", ["applied"] = false };

            var keysToCreate = new List<string>();
            var referenced = new HashSet<string>(AllSpecs(doc).SelectMany(s => s.Keys.Values));
            var existing = new HashSet<string>(tree.blackboard != null ? tree.blackboard.keys.Where(k => k != null).Select(k => k.keyName) : Enumerable.Empty<string>());
            foreach (var key in doc.Keys.Where(k => !existing.Contains(k.Name))) keysToCreate.Add(key.Name);
            var unknown = referenced.Where(k => !existing.Contains(k) && !keysToCreate.Contains(k)).ToList();
            if (unknown.Count > 0)
                return new Dictionary<string, object> { ["error"] = $"Keys used but neither on the blackboard nor declared with a 'key' line: {string.Join(", ", unknown)}.", ["applied"] = false };
            if ((referenced.Count > 0 || keysToCreate.Count > 0) && tree.blackboard == null)
                return new Dictionary<string, object> { ["error"] = $"Tree '{tree.name}' has no blackboard; assign one before importing keys.", ["applied"] = false };

            if (!dryRun && keysToCreate.Count > 0)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("AI import: blackboard keys");
                foreach (var key in doc.Keys.Where(k => keysToCreate.Contains(k.Name)))
                {
                    try { BridgeCommands.AddKey(tree.blackboard, key.Name, key.Type, key.HasValue, key.Value); }
                    catch (BridgeException e) { return new Dictionary<string, object> { ["error"] = $"key {key.Name}: {e.Message}", ["applied"] = false }; }
                }
                AssetDatabase.SaveAssetIfDirty(tree.blackboard);
            }

            // In a dry run the declared keys do not exist yet: skip linking them (they are reported instead).
            var skipKeys = dryRun ? new HashSet<string>(keysToCreate) : new HashSet<string>();
            var ops = BuildOps(tree, doc, skipKeys);
            var result = ApplyEngine.Execute(tree, ops, dryRun, "AI import (replace tree)");
            if (keysToCreate.Count > 0) result[dryRun ? "keysToCreate" : "keysCreated"] = keysToCreate.Cast<object>().ToList();
            return Finish(tree, result, dryRun);
        }

        private static Dictionary<string, object> Finish(BehaviorTree tree, Dictionary<string, object> result, bool dryRun)
        {
            if (!dryRun && !result.ContainsKey("error")) result["saved"] = ND_BehaviorTreeEditorWindow.OnTreeChangedExternally(tree, save: true);
            return result;
        }

        private static IEnumerable<NodeSpec> AllSpecs(Document doc)
        {
            var stack = new Stack<NodeSpec>(doc.Entries);
            while (stack.Count > 0)
            {
                var spec = stack.Pop();
                yield return spec;
                foreach (var s in spec.Decorators.Concat(spec.Services).Concat(spec.Children)) stack.Push(s);
            }
        }

        private static Document Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new BridgeException("The input is empty.");
            string trimmed = StripCodeFence(input.Trim());
            return trimmed.StartsWith("[") || trimmed.StartsWith("{") ? ParseJson(trimmed) : ParseText(trimmed);
        }

        /// <summary>AIs often wrap their answer in ```fences```; take the fenced block.</summary>
        private static string StripCodeFence(string text)
        {
            int start = text.IndexOf("```", StringComparison.Ordinal);
            if (start < 0) return text;
            int lineEnd = text.IndexOf('\n', start);
            int end = lineEnd < 0 ? -1 : text.IndexOf("```", lineEnd, StringComparison.Ordinal);
            return lineEnd < 0 || end < 0 ? text : text.Substring(lineEnd + 1, end - lineEnd - 1).Trim();
        }

        // ---------------------------------------------------------------- text

        private static Document ParseText(string text)
        {
            var doc = new Document();
            var stack = new List<NodeSpec>(); // stack[d] = last node line at depth d
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string raw = lines[n];
                int lineNo = n + 1;
                if (raw.Trim().Length == 0 || raw.TrimStart().StartsWith("#")) continue;
                if (raw.Contains('\t')) throw new BridgeException($"Line {lineNo}: use spaces for indentation, not tabs.");
                int spaces = raw.Length - raw.TrimStart(' ').Length;
                if (spaces % 2 != 0) throw new BridgeException($"Line {lineNo}: indentation must be a multiple of 2 spaces.");
                int depth = spaces / 2;
                string line = raw.Trim();

                if (depth == 0 && line.StartsWith("tree ")) continue;
                if (depth == 0 && line.StartsWith("key ")) { doc.Keys.Add(ParseKeyLine(line, lineNo)); continue; }

                bool isDecorator = line.StartsWith("@decorator "), isService = line.StartsWith("@service ");
                if (isDecorator || isService)
                {
                    if (depth == 0 || stack.Count < depth || stack[depth - 1] == null) throw new BridgeException($"Line {lineNo}: an attachment must be indented under the node it belongs to.");
                    var attachment = ParseNodeLine(line.Substring(isDecorator ? 11 : 9).Trim(), lineNo, isDecorator ? BridgeUtil.TypeKind.Decorator : BridgeUtil.TypeKind.Service);
                    (isDecorator ? stack[depth - 1].Decorators : stack[depth - 1].Services).Add(attachment);
                    continue;
                }

                var spec = ParseNodeLine(line, lineNo, BridgeUtil.TypeKind.GraphNode);
                if (depth == 0) doc.Entries.Add(spec);
                else
                {
                    if (stack.Count < depth || stack[depth - 1] == null) throw new BridgeException($"Line {lineNo}: indented too far (no parent line above at depth {depth - 1}).");
                    stack[depth - 1].Children.Add(spec);
                }
                while (stack.Count <= depth) stack.Add(null);
                stack[depth] = spec;
                for (int i = depth + 1; i < stack.Count; i++) stack[i] = null;
            }
            return doc;
        }

        private static KeySpec ParseKeyLine(string line, int lineNo)
        {
            // key Name: Type [= value]
            string body = line.Substring(4);
            int colon = body.IndexOf(':');
            if (colon <= 0) throw new BridgeException($"Line {lineNo}: expected 'key Name: Type [= value]'.");
            var spec = new KeySpec { Name = body.Substring(0, colon).Trim() };
            string rest = body.Substring(colon + 1).Trim();
            int eq = rest.IndexOf('=');
            spec.Type = (eq < 0 ? rest : rest.Substring(0, eq)).Trim();
            if (eq >= 0)
            {
                spec.HasValue = true;
                string valueText = rest.Substring(eq + 1).Trim();
                try { spec.Value = MiniJson.Parse(valueText); }
                catch (Exception) { spec.Value = valueText; } // bare words are taken as strings
            }
            return spec;
        }

        private static NodeSpec ParseNodeLine(string line, int lineNo, BridgeUtil.TypeKind kind)
        {
            int i = 0;
            string token = ReadToken(line, ref i);
            var spec = new NodeSpec { Line = lineNo };
            if (kind == BridgeUtil.TypeKind.GraphNode && string.Equals(token, "Root", StringComparison.OrdinalIgnoreCase)) spec.IsRoot = true;
            else
            {
                try { spec.Type = BridgeUtil.ResolveNodeType(token, kind); }
                catch (BridgeException e) { throw new BridgeException($"Line {lineNo}: {e.Message}"); }
            }

            SkipSpaces(line, ref i);
            if (i < line.Length && line[i] == '"') spec.Label = ReadQuoted(line, ref i, lineNo);
            SkipSpaces(line, ref i);
            if (i < line.Length && line[i] == '{') spec.Fields = ReadObject(line, ref i, lineNo);
            SkipSpaces(line, ref i);
            if (line.Substring(i).StartsWith("keys"))
            {
                i += 4;
                SkipSpaces(line, ref i);
                if (i >= line.Length || line[i] != '{') throw new BridgeException($"Line {lineNo}: 'keys' must be followed by a JSON object.");
                foreach (var kvp in ReadObject(line, ref i, lineNo)) spec.Keys[kvp.Key] = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture);
            }
            SkipSpaces(line, ref i);
            if (i < line.Length) throw new BridgeException($"Line {lineNo}: unexpected text '{line.Substring(i)}'.");
            return spec;
        }

        private static string ReadToken(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '"' && s[i] != '{') i++;
            return s.Substring(start, i - start);
        }

        private static void SkipSpaces(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static string ReadQuoted(string s, ref int i, int lineNo)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) i++;
                sb.Append(s[i++]);
            }
            if (i >= s.Length) throw new BridgeException($"Line {lineNo}: unterminated label (missing closing quote).");
            i++;
            return sb.ToString();
        }

        /// <summary>Reads a balanced {...} JSON object (braces inside strings are ignored).</summary>
        private static Dictionary<string, object> ReadObject(string s, ref int i, int lineNo)
        {
            int start = i, depth = 0;
            bool inString = false;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { i++; break; }
            }
            if (depth != 0) throw new BridgeException($"Line {lineNo}: unbalanced braces in a JSON object.");
            try
            {
                return MiniJson.Parse(s.Substring(start, i - start)) as Dictionary<string, object>
                       ?? throw new BridgeException($"Line {lineNo}: expected a JSON object.");
            }
            catch (BridgeException) { throw; }
            catch (Exception e) { throw new BridgeException($"Line {lineNo}: invalid JSON ({e.Message})."); }
        }

        // ---------------------------------------------------------------- JSON

        private static Document ParseJson(string json)
        {
            object parsed;
            try { parsed = MiniJson.Parse(json); }
            catch (Exception e) { throw new BridgeException($"Invalid JSON: {e.Message}"); }

            var doc = new Document();
            if (parsed is List<object> list) { doc.RawOps = list; return doc; }
            if (!(parsed is Dictionary<string, object> obj)) throw new BridgeException("Expected a JSON object or an array of operations.");
            if (obj.TryGetValue("ops", out var ops) && ops is List<object> opList) { doc.RawOps = opList; return doc; }
            if (!obj.TryGetValue("nodeList", out var nodesValue) || !(nodesValue is List<object> nodes))
                throw new BridgeException("Unknown JSON: expected a tree JSON (with \"nodeList\", as exported) or a list of tree_apply operations.");

            var byId = nodes.OfType<Dictionary<string, object>>().Where(n => n.ContainsKey("id")).ToDictionary(n => Convert.ToString(n["id"], CultureInfo.InvariantCulture));
            if (obj.TryGetValue("blackboardKeys", out var keysValue) && keysValue is List<object> keyList)
                foreach (var k in keyList.OfType<Dictionary<string, object>>())
                    doc.Keys.Add(new KeySpec { Name = Convert.ToString(k["name"], CultureInfo.InvariantCulture), Type = Convert.ToString(k["type"], CultureInfo.InvariantCulture), HasValue = k.TryGetValue("value", out var v) && v != null, Value = k.TryGetValue("value", out var v2) ? v2 : null });

            string rootId = obj.TryGetValue("root", out var r) ? Convert.ToString(r, CultureInfo.InvariantCulture) : null;
            if (rootId != null && byId.TryGetValue(rootId, out var rootJson)) doc.Entries.Add(FromJson(rootJson, byId, new HashSet<string>()));
            foreach (var n in byId.Values.Where(n => n.TryGetValue("kind", out var kind) && (Equals(kind, "event") || Equals(kind, "init"))))
                doc.Entries.Add(FromJson(n, byId, new HashSet<string>()));
            return doc;
        }

        private static NodeSpec FromJson(Dictionary<string, object> json, Dictionary<string, Dictionary<string, object>> byId, HashSet<string> visited)
        {
            string type = Convert.ToString(json["type"], CultureInfo.InvariantCulture);
            var spec = new NodeSpec();
            if (type.EndsWith(nameof(RootNode))) spec.IsRoot = true;
            else spec.Type = BridgeUtil.ResolveNodeType(type, json.TryGetValue("kind", out var k) && Equals(k, "decorator") ? BridgeUtil.TypeKind.Decorator
                                                         : Equals(k, "service") ? BridgeUtil.TypeKind.Service : BridgeUtil.TypeKind.Any);
            spec.Label = json.TryGetValue("label", out var label) ? label as string : null;
            if (json.TryGetValue("fields", out var fields) && fields is Dictionary<string, object> f)
                spec.Fields = f.Where(kvp => kvp.Key != "typeName").ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (json.TryGetValue("subtree", out var subtree) && subtree is string path) spec.Fields["subtree"] = path;
            if (json.TryGetValue("keys", out var keys) && keys is Dictionary<string, object> keyDict)
                foreach (var kvp in keyDict)
                    if (kvp.Value is Dictionary<string, object> slot && Equals(slot.GetValueOrDefault("linked"), true) && slot.GetValueOrDefault("key") is string keyName)
                        spec.Keys[kvp.Key] = keyName;

            foreach (var listName in new[] { "decorators", "services" })
                if (json.TryGetValue(listName, out var attachments) && attachments is List<object> list)
                    foreach (var a in list.OfType<Dictionary<string, object>>())
                    {
                        var copy = new Dictionary<string, object>(a) { ["kind"] = listName == "decorators" ? "decorator" : "service" };
                        (listName == "decorators" ? spec.Decorators : spec.Services).Add(FromJson(copy, byId, visited));
                    }

            if (json.TryGetValue("children", out var children) && children is List<object> childIds)
                foreach (var id in childIds.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture)))
                {
                    var childJson = ResolveRerouteJson(id, byId);
                    if (childJson == null || !visited.Add(Convert.ToString(childJson["id"], CultureInfo.InvariantCulture))) continue;
                    spec.Children.Add(FromJson(childJson, byId, visited));
                }
            return spec;
        }

        /// <summary>Reroutes are editor-only: follow them to the real child.</summary>
        private static Dictionary<string, object> ResolveRerouteJson(string id, Dictionary<string, Dictionary<string, object>> byId)
        {
            for (int guard = 0; guard < 64 && id != null && byId.TryGetValue(id, out var json); guard++)
            {
                if (!Convert.ToString(json["type"], CultureInfo.InvariantCulture).EndsWith(nameof(RerouteNode))) return json;
                id = json.TryGetValue("children", out var c) && c is List<object> l && l.Count > 0 ? Convert.ToString(l[0], CultureInfo.InvariantCulture) : null;
            }
            return null;
        }

        // ---------------------------------------------------------------- operations

        private const float ColumnWidth = 260f, RowHeight = 210f;

        private static List<object> BuildOps(BehaviorTree tree, Document doc, HashSet<string> skipKeys)
        {
            var ops = new List<object>();
            var existing = tree.nodes.Where(n => n != null && !(n is RootNode)).Select(n => (object)n.id).ToList();
            if (existing.Count > 0) ops.Add(new Dictionary<string, object> { ["op"] = "delete", ["nodes"] = existing });

            Vector2 rootPos = tree.rootNode != null ? tree.rootNode.position.position : Vector2.zero;
            int counter = 0;
            var root = doc.Entries.FirstOrDefault(e => e.IsRoot);
            if (root != null)
            {
                foreach (var d in root.Decorators) AddAttachment(ops, "root", d, "add-decorator", ref counter, skipKeys);
                foreach (var s in root.Services) AddAttachment(ops, "root", s, "add-service", ref counter, skipKeys);
                float x = rootPos.x - (Leaves(root) - 1) * ColumnWidth / 2f;
                foreach (var child in root.Children)
                {
                    EmitNode(ops, child, "root", ref x, rootPos.y + RowHeight, ref counter, skipKeys);
                }
            }

            // Events and Init go to the left of the main tree.
            float left = rootPos.x - (root != null ? Leaves(root) : 1) * ColumnWidth / 2f - ColumnWidth * 1.5f;
            foreach (var entry in doc.Entries.Where(e => !e.IsRoot))
            {
                float width = Leaves(entry) * ColumnWidth;
                float x = left - width + ColumnWidth;
                EmitNode(ops, entry, null, ref x, rootPos.y, ref counter, skipKeys);
                left -= width + ColumnWidth * 0.5f;
            }
            return ops;
        }

        private static int Leaves(NodeSpec spec) => spec.Children.Count == 0 ? 1 : spec.Children.Sum(Leaves);

        /// <summary>Creates the node centred over its children (children laid out left to right from <paramref name="x"/>).</summary>
        private static void EmitNode(List<object> ops, NodeSpec spec, string parentRef, ref float x, float y, ref int counter, HashSet<string> skipKeys)
        {
            string myRef = "n" + (++counter);
            float myX = x + (Leaves(spec) - 1) * ColumnWidth / 2f;
            var fields = new Dictionary<string, object>(spec.Fields);
            fields.TryGetValue("subtree", out var subtree);
            fields.Remove("subtree");

            var create = new Dictionary<string, object> { ["op"] = "create", ["type"] = spec.Type.FullName, ["ref"] = myRef, ["position"] = new List<object> { myX, y } };
            if (parentRef != null) create["parent"] = parentRef == "root" ? "root" : "$" + parentRef;
            if (!string.IsNullOrEmpty(spec.Label)) create["label"] = spec.Label;
            if (fields.Count > 0) create["fields"] = fields;
            ops.Add(create);
            AddKeyLinks(ops, myRef, spec, skipKeys);
            if (subtree is string path && path.Length > 0) ops.Add(new Dictionary<string, object> { ["op"] = "set-subtree", ["node"] = "$" + myRef, ["subtree"] = path.Split('#')[0] });
            foreach (var d in spec.Decorators) AddAttachment(ops, "$" + myRef, d, "add-decorator", ref counter, skipKeys);
            foreach (var s in spec.Services) AddAttachment(ops, "$" + myRef, s, "add-service", ref counter, skipKeys);

            if (spec.Children.Count == 0) { x += ColumnWidth; return; }
            foreach (var child in spec.Children) EmitNode(ops, child, myRef, ref x, y + RowHeight, ref counter, skipKeys);
        }

        private static void AddAttachment(List<object> ops, string ownerRef, NodeSpec spec, string op, ref int counter, HashSet<string> skipKeys)
        {
            string myRef = "a" + (++counter);
            var add = new Dictionary<string, object> { ["op"] = op, ["node"] = ownerRef, ["type"] = spec.Type.FullName, ["ref"] = myRef };
            if (spec.Fields.Count > 0) add["fields"] = spec.Fields;
            ops.Add(add);
            AddKeyLinks(ops, myRef, spec, skipKeys);
        }

        private static void AddKeyLinks(List<object> ops, string myRef, NodeSpec spec, HashSet<string> skipKeys)
        {
            foreach (var kvp in spec.Keys)
                if (!skipKeys.Contains(kvp.Value))
                    ops.Add(new Dictionary<string, object> { ["op"] = "link-key", ["node"] = "$" + myRef, ["path"] = kvp.Key, ["key"] = kvp.Value });
        }

        #endregion
    }
}
