using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ND.Dialogue.Editor.Import
{
    /// <summary>What an import made and what it could not translate (shown to the user, never silently dropped).</summary>
    public sealed class DialogueImportReport
    {
        public Conversation Conversation;
        public int Entries;
        public int Choices;
        public readonly List<string> CreatedActors = new List<string>();
        public readonly List<string> CreatedVariables = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public void Warn(string message)
        {
            if (!Warnings.Contains(message)) Warnings.Add(message);
        }

        public override string ToString()
        {
            var lines = new List<string> { $"Imported '{Conversation?.title}': {Entries} entries, {Choices} choices." };
            if (CreatedActors.Count > 0) lines.Add("New characters: " + string.Join(", ", CreatedActors));
            if (CreatedVariables.Count > 0) lines.Add("New variables: " + string.Join(", ", CreatedVariables));
            lines.AddRange(Warnings.Select(w => "⚠ " + w));
            return string.Join("\n", lines);
        }
    }

    public sealed class DialogueImportOptions
    {
        /// <summary>Speaker of lines without a "Name:" prefix (made if missing).</summary>
        public string NpcActor = "NPC";
        /// <summary>Speaker of the choices (made if missing, as the player).</summary>
        public string PlayerActor = "Player";
        /// <summary>"Malfoy: Hello." makes Malfoy the speaker (made if missing).</summary>
        public bool SpeakerPrefixes = true;
        /// <summary>A passage with a single link continues to it directly (no one-item choice menu).</summary>
        public bool SingleLinkContinues = true;
        /// <summary>Source pixels → graph units.</summary>
        public float PositionScale = 2.6f;
    }

    /// <summary>Helpers shared by the importers: actors, variables, conditions and texts in a target database.</summary>
    internal sealed class ImportTarget
    {
        private static readonly Regex s_Speaker = new Regex(@"^\s*([A-Z][\w' .\-]{0,23}):\s+(.+)$", RegexOptions.Singleline);
        private static readonly Regex s_Comparison = new Regex(@"^\s*\$?(?<name>[A-Za-z_][\w.]*)\s*(?<op>is not|isnt|is|==|!=|>=|<=|>|<|=)\s*(?<value>.+?)\s*$", RegexOptions.IgnoreCase);

        public readonly DialogueDatabase Database;
        public readonly DialogueImportOptions Options;
        public readonly DialogueImportReport Report;
        public readonly Conversation Conversation;

        public ImportTarget(DialogueDatabase database, string title, DialogueImportOptions options, DialogueImportReport report)
        {
            Database = database;
            Options = options ?? new DialogueImportOptions();
            Report = report;
            string unique = title;
            for (int i = 2; database.FindConversation(unique) != null; i++) unique = $"{title} ({i})";
            Conversation = database.AddConversation(unique);
            report.Conversation = Conversation;
        }

        public Actor ActorNamed(string name, bool player)
        {
            var actor = Database.FindActor(name);
            if (actor != null) return actor;
            actor = Database.AddActor(name, player);
            if (actor != null) Report.CreatedActors.Add(name);
            return actor;
        }

        /// <summary>Splits "Name: text" when prefixes are on and the name looks like a speaker.</summary>
        public (Actor actor, string text) Speaker(string text)
        {
            if (Options.SpeakerPrefixes)
            {
                var m = s_Speaker.Match(text);
                if (m.Success) return (ActorNamed(m.Groups[1].Value.Trim(), false), m.Groups[2].Value.Trim());
            }
            return (ActorNamed(Options.NpcActor, false), text);
        }

        public DialogueEntry Line(Actor actor, IReadOnlyList<string> lines, Vector2 position, string title)
        {
            var entry = Conversation.AddEntry(actor.id, title);
            entry.position = position;
            if (lines.Count == 0) entry.isPassthrough = true;
            else
            {
                entry.text.Text = lines[0];
                foreach (var more in lines.Skip(1)) entry.moreLines.Add(new DialoguePage { text = { Text = more } });
            }
            Report.Entries++;
            return entry;
        }

        public DialogueEntry Choice(string text, Vector2 position)
        {
            var entry = Conversation.AddEntry(ActorNamed(Options.PlayerActor, true).id);
            entry.text.Text = text;
            entry.position = position;
            Report.Choices++;
            return entry;
        }

        /// <summary>The value's literal as typed text: true/false, a number or a quoted string.</summary>
        public static VariableValue Literal(string raw)
        {
            raw = raw.Trim();
            if (bool.TryParse(raw, out bool b)) return VariableValue.Bool(b);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return VariableValue.Int(i);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return VariableValue.Float(f);
            return VariableValue.String(raw.Trim('"', '\''));
        }

        /// <summary>Declares a variable the source uses (typed by its first value); a type clash is reported.</summary>
        public void Declare(string name, VariableValue initial)
        {
            var existing = Database.FindVariable(name);
            if (existing == null)
            {
                Database.AddVariable(name, initial);
                Report.CreatedVariables.Add(name);
            }
            else if (existing.initialValue.type != initial.type && !(existing.initialValue.IsNumber && initial.IsNumber))
                Report.Warn($"Variable '{name}' is {existing.initialValue.type} in the database but the source treats it as {initial.type}.");
        }

        /// <summary>
        /// A source condition ("$gold is 3", "x > 2 and y", "a or b") as dialogue conditions (all must hold). Null when
        /// it cannot be read (reported; the branch then has no condition).
        /// </summary>
        public List<DialogueCondition> Conditions(string expression, string where)
        {
            var result = new List<DialogueCondition>();
            foreach (var part in Regex.Split(expression, @"\s+and\s+|\s*&&\s*", RegexOptions.IgnoreCase))
            {
                var any = Regex.Split(part, @"\s+or\s+|\s*\|\|\s*", RegexOptions.IgnoreCase);
                var conditions = new List<DialogueCondition>();
                foreach (var single in any)
                {
                    var condition = Comparison(single);
                    if (condition == null)
                    {
                        Report.Warn($"{where}: cannot read the condition '{expression.Trim()}' — the branch has no condition.");
                        return null;
                    }
                    conditions.Add(condition);
                }
                if (conditions.Count == 1) result.Add(conditions[0]);
                else
                {
                    var or = new AnyCondition();
                    or.conditions.AddRange(conditions);
                    result.Add(or);
                }
            }
            return result;
        }

        private VariableCondition Comparison(string text)
        {
            text = text.Trim().Trim('(', ')').Trim();
            bool negated = text.StartsWith("not ", System.StringComparison.OrdinalIgnoreCase) || text.StartsWith("!");
            if (negated) text = text.TrimStart('!').Substring(text.StartsWith("not ", System.StringComparison.OrdinalIgnoreCase) ? 4 : 0).Trim();
            var m = s_Comparison.Match(text);
            if (!m.Success)
            {
                // A bare flag: "$met" = "$met is true".
                if (!Regex.IsMatch(text, @"^\$?[A-Za-z_][\w.]*$")) return null;
                string flag = text.TrimStart('$');
                Declare(flag, VariableValue.Bool(false));
                return new VariableCondition { variable = flag, op = negated ? CompareOp.NotEqual : CompareOp.Equal, value = "true" };
            }
            string name = m.Groups["name"].Value;
            var value = Literal(m.Groups["value"].Value);
            Declare(name, value.type == VariableType.Bool ? VariableValue.Bool(false) : value.type == VariableType.String ? VariableValue.String(string.Empty) : value.type == VariableType.Float ? VariableValue.Float(0f) : VariableValue.Int(0));
            CompareOp op;
            switch (m.Groups["op"].Value.ToLowerInvariant())
            {
                case "is not": case "isnt": case "!=": op = CompareOp.NotEqual; break;
                case ">": op = CompareOp.Greater; break;
                case "<": op = CompareOp.Less; break;
                case ">=": op = CompareOp.GreaterOrEqual; break;
                case "<=": op = CompareOp.LessOrEqual; break;
                default: op = CompareOp.Equal; break;
            }
            if (negated) op = op == CompareOp.Equal ? CompareOp.NotEqual : op == CompareOp.NotEqual ? CompareOp.Equal : op;
            return new VariableCondition { variable = name, op = op, value = value.type == VariableType.String ? value.stringValue : value.ToString() };
        }

        /// <summary>"x to 3" / "x = x + 1" / "x += 1" as an action (null, reported, when it cannot be read).</summary>
        public SetVariableAction Assignment(string name, string expression, string where)
        {
            expression = expression.Trim();
            var add = Regex.Match(expression, $@"^\$?{Regex.Escape(name)}\s*([+-])\s*(.+)$");
            if (add.Success)
            {
                var delta = Literal(add.Groups[2].Value);
                if (!delta.IsNumber) { Report.Warn($"{where}: cannot read '{name} = {expression}'."); return null; }
                Declare(name, delta.type == VariableType.Float ? VariableValue.Float(0f) : VariableValue.Int(0));
                string amount = add.Groups[1].Value == "-" ? "-" + add.Groups[2].Value.Trim() : add.Groups[2].Value.Trim();
                return new SetVariableAction { variable = name, mode = SetVariableAction.Mode.Add, value = amount };
            }
            if (Regex.IsMatch(expression, @"[\$+\-*/()]") && !Regex.IsMatch(expression, @"^-?\d+(\.\d+)?$"))
            {
                Report.Warn($"{where}: cannot read '{name} = {expression}' (only values and 'x + n' are imported).");
                return null;
            }
            var value = Literal(expression);
            Declare(name, value.type == VariableType.String ? VariableValue.String(string.Empty) : value);
            return new SetVariableAction { variable = name, value = value.type == VariableType.String ? value.stringValue : value.ToString() };
        }

        /// <summary>"$gold" in a line → "[var=gold]" (shows the value).</summary>
        public static string VariableTags(string text) => Regex.Replace(text, @"\$([A-Za-z_]\w*)", "[var=$1]");

        /// <summary>Paragraphs (blank-line separated), each trimmed and on one line; empty ones dropped.</summary>
        public static List<string> Paragraphs(string text) =>
            Regex.Split(text.Replace("\r", string.Empty), @"\n\s*\n").Select(p => Regex.Replace(p, @"\s*\n\s*", " ").Trim()).Where(p => p.Length > 0).ToList();
    }
}
