using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ND.Dialogue.Editor.Bridge;
using UnityEngine;

namespace ND.Dialogue.Editor.Import
{
    /// <summary>A Twine story as read from Twee 3 text or a published Twine 2 HTML file.</summary>
    public sealed class TwineStory
    {
        public sealed class Passage
        {
            public string Name;
            public string[] Tags = new string[0];
            public string Text = string.Empty;
            public Vector2 Position;
        }

        public string Name = "Twine Story";
        public string Start;
        public readonly List<Passage> Passages = new List<Passage>();

        public Passage Find(string name) => Passages.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Imports a Twine story into a dialogue database as one conversation:
    /// <list type="bullet">
    /// <item>each passage → a line (paragraphs → more lines; "Name: text" sets the speaker), empty ones route only;</item>
    /// <item>each link (<c>[[text]]</c>, <c>[[text-&gt;Target]]</c>, <c>[[Target&lt;-text]]</c>, <c>[[text|Target]]</c>) → a
    /// player choice leading to its passage (a lone link just continues);</item>
    /// <item>Harlowe <c>(set: $x to 1)</c> / <c>(set: $x to $x + 1)</c> → actions; <c>(if: $x is 1)[ [[link]] ]</c> →
    /// conditions on those links; <c>$x</c> in text → <c>[var=x]</c>. Other macros are left out and reported.</item>
    /// </list>
    /// </summary>
    public static class TwineImporter
    {
        private static readonly Regex s_Header = new Regex(@"^::\s*(?<name>[^\[\{]+?)\s*(\[(?<tags>[^\]]*)\])?\s*(?<meta>\{.*\})?\s*$");
        private static readonly Regex s_Link = new Regex(@"\[\[(.+?)\]\]");
        private static readonly Regex s_Set = new Regex(@"\(set:\s*\$(?<name>\w+)\s+to\s+(?<value>[^)]+)\)", RegexOptions.IgnoreCase);
        private static readonly Regex s_Macro = new Regex(@"\((?<name>[\w-]+):[^()]*\)");
        /// <summary>Marks a link taken out of a conditional hook (its place in the passage is kept).</summary>
        private const char HookMarker = '\u0001';

        #region Parsing

        /// <summary>Reads Twee 3 (also older Twee: <c>:: Name [tags]</c> headers).</summary>
        public static TwineStory ParseTwee(string twee)
        {
            var story = new TwineStory();
            TwineStory.Passage current = null;
            var body = new StringBuilder();
            void Flush()
            {
                if (current == null) return;
                current.Text = body.ToString().Trim();
                body.Clear();
                if (current.Name == "StoryTitle") story.Name = current.Text;
                else if (current.Name == "StoryData")
                {
                    var data = TryParseJson(current.Text);
                    if (data != null && data.TryGetValue("start", out var start)) story.Start = start as string;
                }
                else story.Passages.Add(current);
            }
            foreach (var line in (twee ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                var m = line.StartsWith("::") ? s_Header.Match(line) : Match.Empty;
                if (!m.Success)
                {
                    if (current != null) body.Append(line).Append('\n');
                    continue;
                }
                Flush();
                current = new TwineStory.Passage
                {
                    Name = m.Groups["name"].Value.Trim(),
                    Tags = m.Groups["tags"].Value.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries),
                };
                if (m.Groups["meta"].Success && TryParseJson(m.Groups["meta"].Value) is Dictionary<string, object> meta &&
                    meta.TryGetValue("position", out var position) && position is string p) current.Position = ParsePosition(p);
            }
            Flush();
            if (story.Start == null && story.Passages.Count > 0) story.Start = story.Find("Start")?.Name ?? story.Passages[0].Name;
            return story;
        }

        /// <summary>Reads a published (or archived) Twine 2 HTML file: <c>&lt;tw-storydata&gt;</c> and its <c>&lt;tw-passagedata&gt;</c>.</summary>
        public static TwineStory ParseHtml(string html)
        {
            var story = new TwineStory();
            var data = Regex.Match(html ?? string.Empty, @"<tw-storydata\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase);
            string startPid = null;
            if (data.Success)
            {
                story.Name = Attribute(data.Groups["attrs"].Value, "name") ?? story.Name;
                startPid = Attribute(data.Groups["attrs"].Value, "startnode");
            }
            foreach (Match m in Regex.Matches(html ?? string.Empty, @"<tw-passagedata\b(?<attrs>[^>]*)>(?<text>.*?)</tw-passagedata>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string attrs = m.Groups["attrs"].Value;
                var passage = new TwineStory.Passage
                {
                    Name = Attribute(attrs, "name") ?? "Untitled",
                    Tags = (Attribute(attrs, "tags") ?? string.Empty).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries),
                    Text = WebUtility.HtmlDecode(m.Groups["text"].Value).Trim(),
                    Position = ParsePosition(Attribute(attrs, "position") ?? "0,0"),
                };
                story.Passages.Add(passage);
                if (startPid != null && Attribute(attrs, "pid") == startPid) story.Start = passage.Name;
            }
            if (story.Start == null && story.Passages.Count > 0) story.Start = story.Passages[0].Name;
            return story;
        }

        private static Dictionary<string, object> TryParseJson(string json)
        {
            try { return DialogueJson.Parse(json) as Dictionary<string, object>; }
            catch (System.FormatException) { return null; } // Twine metadata is optional: a bad one only loses the position or start
        }

        private static string Attribute(string attrs, string name)
        {
            var m = Regex.Match(attrs, $@"\b{name}\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? WebUtility.HtmlDecode(m.Groups["v"].Value) : null;
        }

        private static Vector2 ParsePosition(string text)
        {
            var parts = text.Split(',');
            return parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                   float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ? new Vector2(x, y) : Vector2.zero;
        }

        #endregion

        #region Import

        private sealed class Link
        {
            public string Label;
            public string Target;
            public List<DialogueCondition> Conditions;
        }

        /// <summary>Adds <paramref name="story"/> to <paramref name="database"/> as a new conversation. Undo is the caller's (record the database first).</summary>
        public static DialogueImportReport Import(DialogueDatabase database, TwineStory story, DialogueImportOptions options = null)
        {
            var report = new DialogueImportReport();
            var target = new ImportTarget(database, story.Name, options, report);
            var conversation = target.Conversation;
            float scale = target.Options.PositionScale;
            var entries = new Dictionary<string, DialogueEntry>();
            var links = new Dictionary<string, List<Link>>();

            foreach (var passage in story.Passages)
            {
                if (passage.Tags.Any(t => t == "script" || t == "stylesheet" || t == "widget")) { report.Warn($"Passage '{passage.Name}' ({string.Join(" ", passage.Tags)}) is not dialogue: skipped."); continue; }
                string where = $"Passage '{passage.Name}'";
                var actions = new List<DialogueAction>();
                string text = s_Set.Replace(passage.Text, m =>
                {
                    var action = target.Assignment(m.Groups["name"].Value, m.Groups["value"].Value.Replace("$", string.Empty), where);
                    if (action != null) actions.Add(action);
                    return string.Empty;
                });
                var passageLinks = new List<Link>();
                var conditional = new List<Link>();
                text = ExtractHooks(text, conditional, target, where); // leaves [[\u0001n]] where its links were, so the order stays
                text = s_Link.Replace(text, m =>
                {
                    string inner = m.Groups[1].Value;
                    passageLinks.Add(inner.Length > 1 && inner[0] == HookMarker ? conditional[int.Parse(inner.Substring(1))] : ParseLink(inner));
                    return string.Empty;
                });
                text = s_Macro.Replace(text, m =>
                {
                    report.Warn($"Macro '({m.Groups["name"].Value}:)' is not imported (first seen in {where}).");
                    return string.Empty;
                });
                var paragraphs = ImportTarget.Paragraphs(ImportTarget.VariableTags(text));
                var (actor, first) = paragraphs.Count > 0 ? target.Speaker(paragraphs[0]) : (target.ActorNamed(target.Options.NpcActor, false), null);
                if (first != null) paragraphs[0] = first;
                var entry = target.Line(actor, paragraphs, passage.Position * scale, passage.Name);
                entry.actions.AddRange(actions);
                entries[passage.Name] = entry;
                links[passage.Name] = passageLinks;
            }

            foreach (var passage in story.Passages)
            {
                if (!entries.TryGetValue(passage.Name, out var from)) continue;
                var passageLinks = links[passage.Name];
                bool direct = passageLinks.Count == 1 && passageLinks[0].Conditions == null && target.Options.SingleLinkContinues;
                for (int i = 0; i < passageLinks.Count; i++)
                {
                    var link = passageLinks[i];
                    entries.TryGetValue(link.Target, out var to);
                    if (to == null) report.Warn($"Passage '{passage.Name}' links to '{link.Target}', which does not exist: the branch ends there.");
                    if (direct)
                    {
                        if (to != null) conversation.Link(from, to);
                        continue;
                    }
                    var choice = target.Choice(link.Label, from.position + new Vector2(280f, 110f * i));
                    var toChoice = conversation.Link(from, choice);
                    if (link.Conditions != null) toChoice.conditions.AddRange(link.Conditions);
                    if (to != null) conversation.Link(choice, to);
                }
            }

            if (story.Start != null && entries.TryGetValue(story.Start, out var start))
            {
                conversation.Link(conversation.StartEntry, start);
                conversation.StartEntry.position = start.position - new Vector2(200f, 0f);
            }
            else report.Warn($"Start passage '{story.Start}' not found: START is not linked.");
            return report;
        }

        private static Link ParseLink(string inner)
        {
            int arrow = inner.LastIndexOf("->", System.StringComparison.Ordinal);
            if (arrow >= 0) return new Link { Label = inner.Substring(0, arrow).Trim(), Target = inner.Substring(arrow + 2).Trim() };
            int back = inner.IndexOf("<-", System.StringComparison.Ordinal);
            if (back >= 0) return new Link { Target = inner.Substring(0, back).Trim(), Label = inner.Substring(back + 2).Trim() };
            int bar = inner.LastIndexOf('|');
            if (bar >= 0) return new Link { Label = inner.Substring(0, bar).Trim(), Target = inner.Substring(bar + 1).Trim() };
            return new Link { Label = inner.Trim(), Target = inner.Trim() };
        }

        /// <summary>
        /// Takes out <c>(if: …)[hook]</c>, <c>(unless: …)[hook]</c> and <c>(else:)[hook]</c>: links inside a hook get its
        /// condition; text inside a hook is dropped (reported) — a line cannot change with conditions.
        /// </summary>
        private static string ExtractHooks(string text, List<Link> links, ImportTarget target, string where)
        {
            var result = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                var m = Regex.Match(text.Substring(i), @"^\((?<kind>if|unless|else-if|else):\s*(?<cond>[^)]*)\)\s*\[", RegexOptions.IgnoreCase);
                if (!(text[i] == '(' && m.Success))
                {
                    result.Append(text[i]);
                    i++;
                    continue;
                }
                int open = i + m.Length - 1;
                int close = MatchingBracket(text, open);
                if (close < 0) { target.Report.Warn($"{where}: an unclosed hook '['."); result.Append(text.Substring(i)); break; }
                string hook = text.Substring(open + 1, close - open - 1);
                string kind = m.Groups["kind"].Value.ToLowerInvariant();
                List<DialogueCondition> conditions = null;
                if (kind == "else") target.Report.Warn($"{where}: (else:) is imported without a condition (its links are always offered).");
                else
                {
                    string expression = m.Groups["cond"].Value.Replace("$", string.Empty);
                    if (kind == "unless") expression = "not " + expression;
                    if (kind == "else-if") target.Report.Warn($"{where}: (else-if:) is imported as its own condition (the 'else' part is lost).");
                    conditions = target.Conditions(expression, where);
                }
                var markers = new StringBuilder();
                string rest = s_Link.Replace(hook, lm =>
                {
                    var link = ParseLink(lm.Groups[1].Value);
                    link.Conditions = conditions;
                    markers.Append("[[").Append(HookMarker).Append(links.Count).Append("]]");
                    links.Add(link);
                    return string.Empty;
                });
                result.Append(markers);
                if (rest.Trim().Length > 0) target.Report.Warn($"{where}: text inside a conditional hook is not imported ('{rest.Trim()}').");
                i = close + 1;
            }
            return result.ToString();
        }

        /// <summary>Index of the ']' closing the '[' at <paramref name="open"/>, skipping [[links]]; -1 if none.</summary>
        private static int MatchingBracket(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '[') depth++;
                else if (text[i] == ']' && --depth == 0) return i;
            }
            return -1;
        }

        #endregion
    }
}
