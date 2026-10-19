using System.Collections.Generic;
using System.Linq;
using System.Text;
using ND_BehaviorTree.Editor.Graph;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Read views of a BehaviorTree for the bridge:
    ///   json    — complete structure (nodes by GUID, ordered children, decorators, services, key links, fields,
    ///             entry points, groups, notes, validation issues).
    ///   outline — compact indented text, cheap to read for an AI:
    ///             Root [a1b2c3d4]
    ///             └─ 1. Selector [e5f6a7b8]  ◆ Inverter  ⟳ Rotate To Target (every 1s)
    /// </summary>
    public static class TreeExporter
    {
        public static Dictionary<string, object> Summary(BehaviorTree tree) => new Dictionary<string, object>
        {
            ["name"] = tree.name,
            ["path"] = BridgeUtil.PathOf(tree),
            ["guid"] = BridgeUtil.GuidOf(tree),
            ["nodes"] = tree.nodes.Count(n => n != null),
            ["blackboard"] = tree.blackboard != null ? BridgeUtil.PathOf(tree.blackboard) : null,
            ["schemaVersion"] = tree.SchemaVersion
        };

        public static Dictionary<string, object> ToJson(BehaviorTree tree)
        {
            var result = Summary(tree);
            result["root"] = tree.rootNode != null ? tree.rootNode.id : null;
            result["entries"] = new Dictionary<string, object>
            {
                ["events"] = tree.nodes.OfType<EventNode>().Select(e => new Dictionary<string, object> { ["id"] = e.id, ["eventName"] = e.eventName }).ToList(),
                ["init"] = tree.nodes.OfType<InitNode>().Select(i => i.id).FirstOrDefault()
            };
            result["blackboardKeys"] = BlackboardJson(tree.blackboard);
            result["nodeList"] = tree.nodes.Where(n => n != null).Select(n => NodeJson(tree, n)).ToList();
            result["groups"] = tree.groups.Select(g => new Dictionary<string, object> { ["id"] = g.id, ["title"] = g.title, ["nodes"] = g.nodeIds }).ToList();
            result["notes"] = tree.notes.Select(n => new Dictionary<string, object> { ["id"] = n.id, ["title"] = n.title, ["content"] = n.content }).ToList();
            result["issues"] = GraphValidator.Validate(tree).Select(i => i.ToString()).ToList();
            return result;
        }

        public static List<object> BlackboardJson(Blackboard blackboard)
        {
            if (blackboard == null) return new List<object>();
            return blackboard.keys.Where(k => k != null).Select(k => (object)new Dictionary<string, object>
            {
                ["name"] = k.keyName,
                ["type"] = KeySlotScanner.FriendlyTypeName(k.GetValueType()),
                ["keyClass"] = k.GetType().Name,
                ["value"] = ValueOf(k)
            }).ToList();
        }

        private static object ValueOf(Key key)
        {
            var prop = new SerializedObject(key).FindProperty("value");
            return prop != null ? BridgeUtil.PropertyToJson(prop, 0) : null;
        }

        public static Dictionary<string, object> NodeJson(BehaviorTree tree, Node node)
        {
            var type = node.GetType();
            var slots = KeySlotScanner.Scan(node);
            var json = new Dictionary<string, object>
            {
                ["id"] = node.id,
                ["shortId"] = BridgeUtil.ShortId(node),
                ["type"] = type.FullName,
                ["kind"] = BridgeUtil.KindOf(type),
                ["title"] = BridgeUtil.Title(type),
                ["label"] = string.IsNullOrEmpty(node.typeName) ? null : node.typeName,
                ["position"] = new List<object> { Mathf.Round(node.position.x), Mathf.Round(node.position.y) },
                ["children"] = node.GetChildren().Where(c => c != null).Select(c => c.id).ToList(),
                ["fields"] = BridgeUtil.FieldsOf(node, slots.Select(s => s.PropertyPath).ToList()),
                ["keys"] = KeysJson(tree, slots)
            };
            if (node.decorators.Count > 0) json["decorators"] = node.decorators.Where(d => d != null).Select(d => AttachmentJson(tree, d)).ToList();
            if (node.services.Count > 0) json["services"] = node.services.Where(s => s != null).Select(s => AttachmentJson(tree, s)).ToList();
            if (node is SubtreeNode sub) json["subtree"] = sub.subtree != null ? BridgeUtil.PathOf(sub.subtree) : null;
            return json;
        }

        private static Dictionary<string, object> AttachmentJson(BehaviorTree tree, Node attachment)
        {
            var slots = KeySlotScanner.Scan(attachment);
            return new Dictionary<string, object>
            {
                ["id"] = attachment.id,
                ["shortId"] = BridgeUtil.ShortId(attachment),
                ["type"] = attachment.GetType().FullName,
                ["title"] = BridgeUtil.Title(attachment.GetType()),
                ["fields"] = BridgeUtil.FieldsOf(attachment, slots.Select(s => s.PropertyPath).ToList()),
                ["keys"] = KeysJson(tree, slots)
            };
        }

        private static Dictionary<string, object> KeysJson(BehaviorTree tree, List<KeySlot> slots)
        {
            var keys = new Dictionary<string, object>();
            foreach (var slot in slots)
            {
                keys[slot.PropertyPath] = new Dictionary<string, object>
                {
                    ["required"] = KeySlotScanner.FriendlyTypeName(slot.RequiredValueType),
                    ["key"] = slot.Current == null ? null : KeySlotScanner.IsLinked(tree, slot.Current) ? slot.Current.keyName : $"= {slot.Current.GetValueObject()}",
                    ["linked"] = KeySlotScanner.IsLinked(tree, slot.Current)
                };
            }
            return keys;
        }

        #region Outline

        public static string ToOutline(BehaviorTree tree)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{tree.name}  ({BridgeUtil.PathOf(tree)})");
            if (tree.blackboard != null)
            {
                var keys = tree.blackboard.keys.Where(k => k != null).Select(k => $"{k.keyName}:{KeySlotScanner.FriendlyTypeName(k.GetValueType())}");
                sb.AppendLine($"Blackboard {tree.blackboard.name}: {string.Join(", ", keys)}");
            }
            else sb.AppendLine("Blackboard: (none)");
            sb.AppendLine();

            var visited = new HashSet<Node>();
            if (tree.rootNode != null) WriteBranch(sb, tree, tree.rootNode, "", "", null, visited);

            foreach (var entry in tree.nodes.Where(n => n is EventNode || n is InitNode))
            {
                sb.AppendLine();
                WriteBranch(sb, tree, entry, "", "", null, visited);
            }

            var loose = tree.nodes.Where(n => n != null && !visited.Contains(n)).ToList();
            if (loose.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Unconnected (will not run):");
                foreach (var node in loose.Where(n => !loose.Any(p => p.GetChildren().Contains(n))))
                    WriteBranch(sb, tree, node, "  ", "  ", null, visited);
            }

            var issues = GraphValidator.Validate(tree);
            if (issues.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Issues ({issues.Count}):");
                foreach (var issue in issues) sb.AppendLine($"  - {issue}");
            }
            return sb.ToString();
        }

        private static void WriteBranch(StringBuilder sb, BehaviorTree tree, Node node, string firstPrefix, string childPrefix, string order, HashSet<Node> visited)
        {
            if (node == null) return;
            sb.Append(firstPrefix).Append(order).Append(Describe(tree, node));
            if (!visited.Add(node)) { sb.AppendLine("  (cycle!)"); return; }
            sb.AppendLine();

            var children = node.GetChildren().Where(c => c != null).ToList();
            bool numbered = node is CompositeNode && children.Count > 1;
            for (int i = 0; i < children.Count; i++)
            {
                bool last = i == children.Count - 1;
                WriteBranch(sb, tree, children[i], childPrefix + (last ? "└─ " : "├─ "), childPrefix + (last ? "   " : "│  "), numbered ? $"{i + 1}. " : null, visited);
            }
        }

        private static string Describe(BehaviorTree tree, Node node)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(node.typeName) ? BridgeUtil.Title(node.GetType()) : node.typeName);
            sb.Append($" [{BridgeUtil.ShortId(node)}]");
            if (node is EventNode ev) sb.Append($" event \"{ev.eventName}\"");
            if (node is InitNode) sb.Append(" (runs once at start)");
            if (node is SubtreeNode sub) sb.Append($" → {(sub.subtree != null ? BridgeUtil.PathOf(sub.subtree) : "(no subtree)")}");

            var keyText = KeySummary(tree, node);
            if (keyText.Length > 0) sb.Append($" {{{keyText}}}");
            foreach (var d in node.decorators.Where(d => d != null))
                sb.Append($"  ◆ {BridgeUtil.Title(d.GetType())} [{BridgeUtil.ShortId(d)}]{Braced(KeySummary(tree, d))}");
            foreach (var s in node.services.Where(s => s != null))
                sb.Append($"  ⟳ {BridgeUtil.Title(s.GetType())} [{BridgeUtil.ShortId(s)}] every {s.interval:0.##}s{Braced(KeySummary(tree, s))}");
            return sb.ToString();
        }

        private static string Braced(string text) => text.Length > 0 ? $" {{{text}}}" : string.Empty;

        private static string KeySummary(BehaviorTree tree, Node node)
        {
            var parts = KeySlotScanner.Scan(node).Select(s =>
                $"{s.PropertyPath}={(s.Current == null ? "∅" : KeySlotScanner.IsLinked(tree, s.Current) ? s.Current.keyName : $"={s.Current.GetValueObject()}")}");
            return string.Join(", ", parts);
        }

        #endregion
    }
}
