using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ND.Dialogue
{
    /// <summary>One command of an entry's sequence: <c>Name(arg, arg)@seconds</c>.</summary>
    public sealed class SequenceCommand
    {
        public string Name;
        public string[] Args;
        /// <summary>Seconds after the line starts (0 = at once).</summary>
        public float At;

        public string Arg(int index) => Args != null && index < Args.Length ? Args[index] : null;

        public override string ToString() => $"{Name}({string.Join(", ", Args ?? new string[0])}){(At > 0f ? "@" + At.ToString(CultureInfo.InvariantCulture) : string.Empty)}";
    }

    /// <summary>
    /// Parses an entry's sequence: commands separated by <c>;</c> or new lines, each <c>Name(arg, arg)</c> with an
    /// optional <c>@seconds</c> start time. Arguments are trimmed; quote one ("a, b") to keep commas. <c>//</c> starts a
    /// comment to the end of the line. Example: <c>Camera(CloseUp); Anim(Malfoy, Blush)@0.5; Wait(2); Continue()@2</c>.
    /// </summary>
    public static class DialogueSequence
    {
        /// <summary>The commands, and a message per malformed one (those are skipped).</summary>
        public static List<SequenceCommand> Parse(string text, List<string> errors = null)
        {
            var commands = new List<SequenceCommand>();
            if (string.IsNullOrWhiteSpace(text)) return commands;
            foreach (var raw in SplitCommands(StripComments(text)))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                if (TryParseCommand(part, out var command, out string error)) commands.Add(command);
                else errors?.Add($"'{part}': {error}");
            }
            return commands;
        }

        private static string StripComments(string text)
        {
            var sb = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                int comment = IndexOutsideQuotes(line, "//");
                sb.Append(comment >= 0 ? line.Substring(0, comment) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static int IndexOutsideQuotes(string text, string token)
        {
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"') quoted = !quoted;
                else if (!quoted && string.CompareOrdinal(text, i, token, 0, token.Length) == 0) return i;
            }
            return -1;
        }

        /// <summary>Splits on ';' and new lines that are outside quotes and parentheses.</summary>
        private static IEnumerable<string> SplitCommands(string text)
        {
            var sb = new StringBuilder();
            bool quoted = false;
            int depth = 0;
            foreach (char ch in text)
            {
                if (ch == '"') quoted = !quoted;
                else if (!quoted && ch == '(') depth++;
                else if (!quoted && ch == ')') depth--;
                if (!quoted && depth <= 0 && (ch == ';' || ch == '\n'))
                {
                    yield return sb.ToString();
                    sb.Clear();
                    depth = 0;
                    continue;
                }
                sb.Append(ch);
            }
            yield return sb.ToString();
        }

        private static bool TryParseCommand(string part, out SequenceCommand command, out string error)
        {
            command = null;
            int open = part.IndexOf('(');
            int close = part.LastIndexOf(')');
            if (open <= 0 || close < open) { error = "write it as Name(arguments)"; return false; }
            string name = part.Substring(0, open).Trim();
            foreach (char ch in name)
                if (!char.IsLetterOrDigit(ch) && ch != '_') { error = $"'{name}' is not a command name"; return false; }

            float at = 0f;
            string rest = part.Substring(close + 1).Trim();
            if (rest.Length > 0)
            {
                if (rest[0] != '@' || !float.TryParse(rest.Substring(1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out at) || at < 0f)
                {
                    error = $"'{rest}' after the command: only @seconds (e.g. @1.5) may follow";
                    return false;
                }
            }
            command = new SequenceCommand { Name = name, Args = SplitArgs(part.Substring(open + 1, close - open - 1)), At = at };
            error = null;
            return true;
        }

        private static string[] SplitArgs(string inside)
        {
            var args = new List<string>();
            if (string.IsNullOrWhiteSpace(inside)) return args.ToArray();
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (char ch in inside)
            {
                if (ch == '"') { quoted = !quoted; continue; }
                if (ch == ',' && !quoted)
                {
                    args.Add(sb.ToString().Trim());
                    sb.Clear();
                    continue;
                }
                sb.Append(ch);
            }
            args.Add(sb.ToString().Trim());
            return args.ToArray();
        }
    }
}
