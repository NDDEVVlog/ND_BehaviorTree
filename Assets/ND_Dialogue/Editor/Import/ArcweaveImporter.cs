using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using ND.Dialogue.Editor.Bridge;
using UnityEngine;

namespace ND.Dialogue.Editor.Import
{
    /// <summary>
    /// Imports an Arcweave project (its JSON export) into a dialogue database as one conversation:
    /// <list type="bullet">
    /// <item>each element → a line; its first component (a character) is the speaker; paragraphs → more lines;</item>
    /// <item>a connection with a label → a player choice; without one → the line continues;</item>
    /// <item>branches → a routing entry whose links carry the if / else-if conditions (else = lowest priority);
    /// jumpers → links to their element;</item>
    /// <item>variables → database variables; arcscript assignments in an element (<c>x = 3</c>, <c>x += 1</c>) →
    /// actions. What cannot be read is reported.</item>
    /// </list>
    /// </summary>
    public static class ArcweaveImporter
    {
        private sealed class Node
        {
            public string Id;
            public string Kind; // elements, branches, jumpers
            public DialogueEntry Entry;
            public string JumpTo;
        }

        /// <summary>Adds the project in <paramref name="json"/> to <paramref name="database"/>. Undo is the caller's.</summary>
        public static DialogueImportReport Import(DialogueDatabase database, string json, DialogueImportOptions options = null)
        {
            var report = new DialogueImportReport();
            object parsed;
            try { parsed = DialogueJson.Parse(json); }
            catch (System.Exception e) when (e is System.FormatException || e is System.ArgumentException || e is System.IndexOutOfRangeException) { report.Warn($"The file is not valid JSON ({e.Message})."); return report; }
            if (!(parsed is Dictionary<string, object> root))
            {
                report.Warn("The file is not an Arcweave JSON export.");
                return report;
            }
            var elements = Map(root, "elements");
            var connections = Map(root, "connections");
            var components = Map(root, "components");
            var branches = Map(root, "branches");
            var conditions = Map(root, "conditions");
            var jumpers = Map(root, "jumpers");
            if (elements.Count == 0) { report.Warn("The project has no elements."); return report; }

            var target = new ImportTarget(database, Str(root, "name") ?? "Arcweave Project", options, report);
            var conversation = target.Conversation;
            float scale = target.Options.PositionScale * 0.4f;

            foreach (var kv in Map(root, "variables"))
            {
                if (!(kv.Value is Dictionary<string, object> v)) continue;
                string name = Str(v, "name");
                if (string.IsNullOrEmpty(name)) continue;
                v.TryGetValue("value", out var raw);
                switch (Str(v, "type"))
                {
                    case "boolean": target.Declare(name, VariableValue.Bool(raw is bool b && b)); break;
                    case "integer": target.Declare(name, VariableValue.Int(raw is double d ? (int)d : 0)); break;
                    case "float": target.Declare(name, VariableValue.Float(raw is double f ? (float)f : 0f)); break;
                    default: target.Declare(name, VariableValue.String(raw as string ?? string.Empty)); break;
                }
            }

            var nodes = new Dictionary<string, Node>();
            int column = 0;
            foreach (var kv in elements)
            {
                if (!(kv.Value is Dictionary<string, object> e)) continue;
                string where = $"Element '{PlainText(Str(e, "title")) ?? kv.Key}'";
                var (text, scripts) = SplitScripts(Str(e, "content") ?? string.Empty);
                var paragraphs = Paragraphs(text);
                string speakerName = Ids(e, "components").Select(id => components.TryGetValue(id, out var c) ? Str(c as Dictionary<string, object>, "name") : null).FirstOrDefault(n => !string.IsNullOrEmpty(n));
                Actor actor;
                if (speakerName != null) actor = target.ActorNamed(speakerName, false);
                else
                {
                    (actor, _) = paragraphs.Count > 0 ? target.Speaker(paragraphs[0]) : (target.ActorNamed(target.Options.NpcActor, false), null);
                    if (paragraphs.Count > 0) paragraphs[0] = target.Speaker(paragraphs[0]).text;
                }
                var entry = target.Line(actor, paragraphs.Select(ImportTarget.VariableTags).ToList(), Position(e, column++, scale), PlainText(Str(e, "title")));
                foreach (var script in scripts) Assignments(script, entry, target, where);
                nodes[kv.Key] = new Node { Id = kv.Key, Kind = "elements", Entry = entry };
            }
            foreach (var kv in branches)
                nodes[kv.Key] = new Node { Id = kv.Key, Kind = "branches", Entry = target.Line(target.ActorNamed(target.Options.NpcActor, false), new List<string>(), Position(kv.Value as Dictionary<string, object>, column++, scale), "branch") };
            foreach (var kv in jumpers)
                nodes[kv.Key] = new Node { Id = kv.Key, Kind = "jumpers", JumpTo = Str(kv.Value as Dictionary<string, object>, "elementId") };

            DialogueEntry Resolve(string id)
            {
                for (int guard = 0; guard < 16 && id != null && nodes.TryGetValue(id, out var node); guard++)
                {
                    if (node.Entry != null) return node.Entry;
                    id = node.JumpTo;
                }
                if (id != null) report.Warn($"A connection leads to '{id}', which is not an element, branch or jumper: the branch ends there.");
                return null;
            }

            // Connections out of elements: a label makes a choice.
            foreach (var kv in elements)
            {
                if (!(kv.Value is Dictionary<string, object> e)) continue;
                var from = nodes[kv.Key].Entry;
                var outputs = Ids(e, "outputs").Select(id => connections.TryGetValue(id, out var c) ? c as Dictionary<string, object> : null).Where(c => c != null).ToList();
                for (int i = 0; i < outputs.Count; i++)
                {
                    var to = Resolve(Str(outputs[i], "targetid"));
                    string label = PlainText(Str(outputs[i], "label"));
                    if (string.IsNullOrEmpty(label))
                    {
                        if (to != null) conversation.Link(from, to);
                        continue;
                    }
                    var choice = target.Choice(label, from.position + new Vector2(280f, 110f * i));
                    conversation.Link(from, choice);
                    if (to != null) conversation.Link(choice, to);
                }
            }

            // Branches: if / else-if in order (higher priority first), else last.
            foreach (var kv in branches)
            {
                if (!(kv.Value is Dictionary<string, object> b) || !(b.TryGetValue("conditions", out var c) && c is Dictionary<string, object> branchConditions)) continue;
                var from = nodes[kv.Key].Entry;
                var ordered = new List<(string conditionId, LinkPriority priority)>();
                if (Str(branchConditions, "ifCondition") is string ifId) ordered.Add((ifId, LinkPriority.High));
                foreach (var elseIf in Ids(branchConditions, "elseIfConditions")) ordered.Add((elseIf, LinkPriority.AboveNormal));
                if (Str(branchConditions, "elseCondition") is string elseId) ordered.Add((elseId, LinkPriority.Low));
                foreach (var (conditionId, priority) in ordered)
                {
                    if (!(conditions.TryGetValue(conditionId, out var co) && co is Dictionary<string, object> condition)) continue;
                    var output = connections.TryGetValue(Str(condition, "output") ?? string.Empty, out var oc) ? oc as Dictionary<string, object> : null;
                    var to = output != null ? Resolve(Str(output, "targetid")) : null;
                    if (to == null) continue;
                    var link = conversation.Link(from, to, priority);
                    string script = PlainText(Str(condition, "script"));
                    if (priority != LinkPriority.Low && !string.IsNullOrEmpty(script))
                    {
                        var parsedConditions = target.Conditions(script, "Branch");
                        if (parsedConditions != null) link.conditions.AddRange(parsedConditions);
                    }
                }
            }

            var start = Resolve(Str(root, "startingElement")) ?? nodes.Values.FirstOrDefault(n => n.Kind == "elements")?.Entry;
            if (start != null) conversation.Link(conversation.StartEntry, start);
            else report.Warn("No starting element: START is not linked.");
            return report;
        }

        private static void Assignments(string script, DialogueEntry entry, ImportTarget target, string where)
        {
            foreach (var statement in script.Split('\n', ';').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var m = Regex.Match(statement, @"^(?<name>[A-Za-z_]\w*)\s*(?<op>\+=|-=|=)\s*(?<value>.+)$");
                if (!m.Success || statement.StartsWith("if ") || statement.Contains("=="))
                {
                    target.Report.Warn($"{where}: arcscript '{statement}' is not imported.");
                    continue;
                }
                string name = m.Groups["name"].Value, value = m.Groups["value"].Value.Trim();
                if (m.Groups["op"].Value != "=") value = $"{name} {m.Groups["op"].Value[0]} {value}";
                var action = target.Assignment(name, value, where);
                if (action != null) entry.actions.Add(action);
            }
        }

        /// <summary>The text without its <c>&lt;pre&gt;&lt;code&gt;</c> scripts, and the scripts.</summary>
        private static (string text, List<string> scripts) SplitScripts(string html)
        {
            var scripts = new List<string>();
            string text = Regex.Replace(html, @"<pre[^>]*>\s*<code[^>]*>(?<s>.*?)</code>\s*</pre>", m =>
            {
                scripts.Add(WebUtility.HtmlDecode(Regex.Replace(m.Groups["s"].Value, "<[^>]+>", string.Empty)));
                return string.Empty;
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return (text, scripts);
        }

        /// <summary>HTML paragraphs as lines.</summary>
        private static List<string> Paragraphs(string html)
        {
            string text = Regex.Replace(html, @"</p>|<br\s*/?>", "\n\n", RegexOptions.IgnoreCase);
            return ImportTarget.Paragraphs(WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", string.Empty)));
        }

        private static string PlainText(string html)
        {
            if (html == null) return null;
            string text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")).Trim();
            return Regex.Replace(text, @"\s+", " ");
        }

        private static Vector2 Position(Dictionary<string, object> node, int fallbackIndex, float scale)
        {
            if (node != null && node.TryGetValue("x", out var x) && node.TryGetValue("y", out var y) && x is double dx && y is double dy)
                return new Vector2((float)dx, (float)dy) * scale;
            return new Vector2(300f * fallbackIndex, 0f);
        }

        private static Dictionary<string, object> Map(Dictionary<string, object> root, string key) =>
            root.TryGetValue(key, out var value) && value is Dictionary<string, object> map ? map : new Dictionary<string, object>();

        private static string Str(Dictionary<string, object> map, string key) =>
            map != null && map.TryGetValue(key, out var value) ? value as string : null;

        private static IEnumerable<string> Ids(Dictionary<string, object> map, string key) =>
            map != null && map.TryGetValue(key, out var value) && value is List<object> list ? list.OfType<string>() : Enumerable.Empty<string>();
    }
}
