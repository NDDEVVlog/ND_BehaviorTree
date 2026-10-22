using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ND.Dialogue
{
    public enum LineCommandKind : byte { Text, RichTag, Pause, Speed, Break, StyleOpen, StyleClose, Fix, Event }

    /// <summary>
    /// One instruction of a line, plain data: what <see cref="LineMarkup.Parse"/> reads from the text and
    /// <see cref="LineTimeline.Build(LineCommand[], float, float)"/> turns into a timeline. No behaviour of its own.
    /// </summary>
    public readonly struct LineCommand
    {
        public readonly LineCommandKind Kind;
        /// <summary>Text: the characters. RichTag: the whole tag. Fix: the new text. Event: the argument. StyleOpen: the group id (err).</summary>
        public readonly string Text;
        /// <summary>Pause: seconds. Speed: characters per second (0 = back to the default). Break: indent.</summary>
        public readonly float Value;
        /// <summary>StyleOpen / StyleClose: the style name. Fix: the group id. Event: the event name.</summary>
        public readonly string Id;

        public LineCommand(LineCommandKind kind, string text = null, float value = 0f, string id = null)
        {
            Kind = kind;
            Text = text;
            Value = value;
            Id = id;
        }

        public override string ToString() => $"{Kind}({Id}{(Text != null ? " \"" + Text + "\"" : string.Empty)}{(Value != 0f ? " " + Value.ToString(CultureInfo.InvariantCulture) : string.Empty)})";
    }

    /// <summary>
    /// Reads the tags of a line into <see cref="LineCommand"/>s. Written in the text itself, so a writer controls the
    /// delivery without touching code:
    /// <list type="bullet">
    /// <item><c>&lt;pause 0.5&gt;</c> (or <c>&lt;wait=0.5&gt;</c>): stop typing for half a second.</item>
    /// <item><c>&lt;speed 15&gt;</c>…<c>&lt;/speed&gt;</c>: characters per second from here (closing: the default again).</item>
    /// <item><c>&lt;br posX="50"&gt;</c>: a new line, indented.</item>
    /// <item>Styles, e.g. <c>&lt;wave&gt;…&lt;/wave&gt;</c>, <c>&lt;shake&gt;</c>, <c>&lt;whisper&gt;</c>, <c>&lt;err id="a"&gt;…&lt;/err&gt;</c> —
    /// see <see cref="LineStyles"/> (add your own there).</item>
    /// <item><c>&lt;fix id="a" text="like"&gt;</c>: the words of <c>&lt;err id="a"&gt;</c> are replaced by "like" at this moment.</item>
    /// <item><c>&lt;event name="door" arg="open"&gt;</c>: a gameplay event when typing reaches this point.</item>
    /// </list>
    /// Any other tag (<c>&lt;b&gt;</c>, <c>&lt;color=red&gt;</c>, <c>&lt;size=120%&gt;</c>…) is kept as rich text.
    /// </summary>
    public static class LineMarkup
    {
        private static readonly Regex s_Token = new Regex(@"<([^<>]+)>|([^<]+)|(<)", RegexOptions.Compiled);
        private static readonly Regex s_Attribute = new Regex(@"(\w+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""']+))", RegexOptions.Compiled);

        /// <summary>The commands of <paramref name="raw"/>; mistakes (unclosed styles, a fix with no error…) go to <paramref name="errors"/>.</summary>
        public static LineCommand[] Parse(string raw, List<string> errors = null)
        {
            var commands = new List<LineCommand>();
            if (string.IsNullOrEmpty(raw)) return commands.ToArray();
            var openStyles = new List<string>();
            var groups = new HashSet<string>();
            foreach (Match m in s_Token.Matches(raw))
            {
                if (m.Groups[2].Success) { commands.Add(new LineCommand(LineCommandKind.Text, m.Groups[2].Value)); continue; }
                if (m.Groups[3].Success) { commands.Add(new LineCommand(LineCommandKind.Text, "<")); continue; }
                Tag(m.Groups[1].Value.Trim(), m.Value, commands, openStyles, groups, errors);
            }
            foreach (var style in openStyles) errors?.Add($"<{style}> is never closed (</{style}>).");
            return commands.ToArray();
        }

        private static void Tag(string inner, string whole, List<LineCommand> commands, List<string> openStyles, HashSet<string> groups, List<string> errors)
        {
            bool closing = inner.StartsWith("/", StringComparison.Ordinal);
            string body = closing ? inner.Substring(1).Trim() : inner;
            int split = body.IndexOfAny(new[] { ' ', '=' });
            string name = (split < 0 ? body : body.Substring(0, split)).ToLowerInvariant();
            string rest = split < 0 ? string.Empty : body.Substring(split + 1).Trim();
            var attributes = Attributes(rest);
            string positional = rest.Length > 0 && rest.IndexOf('=') < 0 ? rest.Trim('"', '\'') : (body.Length > name.Length && body[name.Length] == '=' ? rest.Trim('"', '\'') : null);

            switch (name)
            {
                case "pause":
                case "wait":
                    if (closing) return;
                    if (Number(positional ?? Get(attributes, "s", "seconds", "time"), out float seconds) && seconds >= 0f) commands.Add(new LineCommand(LineCommandKind.Pause, value: seconds));
                    else errors?.Add($"{whole}: write the seconds, e.g. <pause 0.5>.");
                    return;
                case "speed":
                    if (closing) { commands.Add(new LineCommand(LineCommandKind.Speed, value: 0f)); return; }
                    if (Number(positional ?? Get(attributes, "cps", "value"), out float cps) && cps >= 0f) commands.Add(new LineCommand(LineCommandKind.Speed, value: cps));
                    else errors?.Add($"{whole}: write characters per second, e.g. <speed 15>.");
                    return;
                case "br":
                    if (closing) return;
                    Number(Get(attributes, "posx", "indent") ?? positional, out float indent);
                    commands.Add(new LineCommand(LineCommandKind.Break, value: indent));
                    return;
                case "fix":
                {
                    if (closing) return;
                    string id = Get(attributes, "id");
                    if (string.IsNullOrEmpty(id)) { errors?.Add($"{whole}: a fix needs the id of its <err id=\"…\">."); return; }
                    if (!groups.Contains(id)) errors?.Add($"{whole}: no <err id=\"{id}\"> before it.");
                    commands.Add(new LineCommand(LineCommandKind.Fix, Get(attributes, "text") ?? string.Empty, id: id));
                    return;
                }
                case "event":
                {
                    if (closing) return;
                    string eventName = Get(attributes, "name") ?? positional;
                    if (string.IsNullOrEmpty(eventName)) { errors?.Add($"{whole}: write the event name, e.g. <event name=\"door\">."); return; }
                    commands.Add(new LineCommand(LineCommandKind.Event, Get(attributes, "arg") ?? string.Empty, id: eventName));
                    return;
                }
            }

            if (LineStyles.TryGet(name, out _))
            {
                if (closing)
                {
                    int at = openStyles.LastIndexOf(name);
                    if (at < 0) { errors?.Add($"{whole} closes nothing."); return; }
                    openStyles.RemoveAt(at);
                    commands.Add(new LineCommand(LineCommandKind.StyleClose, id: name));
                }
                else
                {
                    string group = Get(attributes, "id");
                    if (!string.IsNullOrEmpty(group)) groups.Add(group);
                    openStyles.Add(name);
                    commands.Add(new LineCommand(LineCommandKind.StyleOpen, group, id: name));
                }
                return;
            }
            commands.Add(new LineCommand(LineCommandKind.RichTag, whole)); // <b>, <color=…>: rich text, shown as is
        }

        private static Dictionary<string, string> Attributes(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in s_Attribute.Matches(text))
                map[m.Groups[1].Value] = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
            return map;
        }

        private static string Get(Dictionary<string, string> map, params string[] keys)
        {
            foreach (var key in keys)
                if (map.TryGetValue(key, out var value)) return value;
            return null;
        }

        private static bool Number(string text, out float value)
        {
            value = 0f;
            return !string.IsNullOrEmpty(text) && float.TryParse(text.Trim().TrimEnd('s').Replace("px", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
