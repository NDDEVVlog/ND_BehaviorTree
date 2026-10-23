using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ND.Dialogue
{
    /// <summary>A span of visible characters with a motion (indices as TextMeshPro counts them: tags excluded).</summary>
    public readonly struct LineEffectRange
    {
        public readonly int Start;
        public readonly int Length;
        public readonly LineEffect Effect;

        public LineEffectRange(int start, int length, LineEffect effect) { Start = start; Length = length; Effect = effect; }
    }

    /// <summary>
    /// What a line shows at one moment: the text so far (rich text), its motions, and whether it is done. Reused frame
    /// to frame without allocating: <see cref="Builder"/> holds the text (TextMeshPro takes it with SetText), and the
    /// <see cref="Text"/> string is only made when asked for after a change.
    /// </summary>
    public sealed class LineFrame
    {
        private StringBuilder m_Builder = new StringBuilder(64);
        private StringBuilder m_Next = new StringBuilder(64);
        private string m_Text = string.Empty;
        private bool m_ForceChanged;

        /// <summary>The text so far (rich text), without allocating; do not modify it.</summary>
        public StringBuilder Builder => m_Builder;
        /// <summary>The text so far as a string (made once per change).</summary>
        public string Text => m_Text ??= m_Builder.ToString();
        /// <summary>The text differs from the previous <see cref="LineTimeline.Compose"/> into this frame.</summary>
        public bool Changed { get; private set; }
        /// <summary>Visible characters in <see cref="Text"/> (rich tags excluded, line breaks included).</summary>
        public int VisibleCharacters;
        public readonly List<LineEffectRange> Effects = new List<LineEffectRange>();
        public bool Done;

        /// <summary>Empties the frame for a new line: the next Compose counts as <see cref="Changed"/> whatever it shows.</summary>
        public void Reset()
        {
            m_Builder.Clear();
            m_Text = string.Empty;
            Effects.Clear();
            VisibleCharacters = 0;
            Done = false;
            m_ForceChanged = true;
        }

        /// <summary>The buffer <see cref="LineTimeline.Compose"/> writes the new text into.</summary>
        internal StringBuilder BeginText()
        {
            m_Next.Clear();
            return m_Next;
        }

        /// <summary>Keeps the new text if it differs from the current one (no allocation when it does not).</summary>
        internal void EndText()
        {
            Changed = m_ForceChanged || !SameText(m_Builder, m_Next);
            m_ForceChanged = false;
            if (!Changed) return;
            (m_Builder, m_Next) = (m_Next, m_Builder);
            m_Text = null;
        }

        private static bool SameText(StringBuilder a, StringBuilder b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>A gameplay event a line fires when typing reaches it (<c>&lt;event name="…"&gt;</c>).</summary>
    public readonly struct LineEvent
    {
        public readonly float Time;
        public readonly string Name;
        public readonly string Argument;

        public LineEvent(float time, string name, string argument) { Time = time; Name = name; Argument = argument; }
    }

    /// <summary>
    /// A line as a timeline: built once from its commands (<see cref="Build(string, float, float)"/>), then asked what
    /// it shows at any time (<see cref="Compose"/>) and which events lie between two times (<see cref="EventsBetween"/>).
    /// Pure data and functions: the game's TextMeshPro typewriter and the editor's Test Dialogue run the same
    /// timeline, so they show the same thing.
    /// </summary>
    public sealed class LineTimeline
    {
        public const float DefaultCharactersPerSecond = 40f;
        /// <summary>Time a &lt;fix&gt; holds before typing goes on (the swap is seen).</summary>
        public const float FixHold = 0.3f;

        private enum PieceKind : byte { Text, Rich, Break }

        private struct Piece
        {
            public PieceKind Kind;
            public string Text;
            public string Open;
            public string Close;
            public LineEffect Effect;
            public string Group;
            public float Start;
            public float Interval;
            public float HideAt;
        }

        private readonly List<Piece> m_Pieces = new List<Piece>();
        private readonly List<LineEvent> m_Events = new List<LineEvent>();

        /// <summary>When the last character (or pause, fix, event) is reached.</summary>
        public float Duration { get; private set; }
        public IReadOnlyList<LineEvent> Events => m_Events;

        /// <summary>Parses <paramref name="raw"/> (see <see cref="LineMarkup"/>) and builds its timeline.</summary>
        /// <param name="fitSeconds">&gt; 0: stretch or squeeze the whole line to last this long (e.g. its voice clip).</param>
        public static LineTimeline Build(string raw, float charactersPerSecond = DefaultCharactersPerSecond, float fitSeconds = 0f) =>
            Build(LineMarkup.Parse(raw), charactersPerSecond, fitSeconds);

        public static LineTimeline Build(LineCommand[] commands, float charactersPerSecond = DefaultCharactersPerSecond, float fitSeconds = 0f)
        {
            var timeline = new LineTimeline();
            float baseInterval = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;
            float interval = baseInterval, clock = 0f;
            var styles = new List<(string name, string group)>();
            foreach (var command in commands ?? Array.Empty<LineCommand>())
            {
                switch (command.Kind)
                {
                    case LineCommandKind.Text:
                    {
                        if (string.IsNullOrEmpty(command.Text)) break;
                        var (open, close, effect, group) = Styled(styles);
                        timeline.m_Pieces.Add(new Piece { Kind = PieceKind.Text, Text = command.Text, Open = open, Close = close, Effect = effect, Group = group, Start = clock, Interval = interval, HideAt = float.PositiveInfinity });
                        clock += command.Text.Length * interval;
                        break;
                    }
                    case LineCommandKind.RichTag:
                        timeline.m_Pieces.Add(new Piece { Kind = PieceKind.Rich, Text = command.Text, HideAt = float.PositiveInfinity });
                        break;
                    case LineCommandKind.Pause:
                        clock += command.Value;
                        break;
                    case LineCommandKind.Speed:
                        interval = command.Value > 0f ? 1f / command.Value : baseInterval;
                        break;
                    case LineCommandKind.Break:
                        // The indent tag is made once here, not on every Compose.
                        timeline.m_Pieces.Add(new Piece { Kind = PieceKind.Break, Start = clock, HideAt = float.PositiveInfinity,
                            Open = command.Value > 0f ? "<indent=" + command.Value.ToString(CultureInfo.InvariantCulture) + "px>" : null });
                        break;
                    case LineCommandKind.StyleOpen:
                        styles.Add((command.Id, command.Text));
                        break;
                    case LineCommandKind.StyleClose:
                        for (int i = styles.Count - 1; i >= 0; i--)
                            if (styles[i].name == command.Id) { styles.RemoveAt(i); break; }
                        break;
                    case LineCommandKind.Fix:
                        timeline.Fix(command.Id, command.Text, clock);
                        clock += FixHold;
                        break;
                    case LineCommandKind.Event:
                        timeline.m_Events.Add(new LineEvent(clock, command.Id, command.Text));
                        break;
                }
            }
            timeline.Duration = clock;
            if (fitSeconds > 0f && clock > 0f) timeline.Scale(fitSeconds / clock);
            return timeline;
        }

        /// <summary>The rich text, motion and error group of text written inside <paramref name="styles"/> (outermost first).</summary>
        private static (string open, string close, LineEffect effect, string group) Styled(List<(string name, string group)> styles)
        {
            if (styles.Count == 0) return (string.Empty, string.Empty, LineEffect.None, null);
            var open = new StringBuilder();
            var close = new StringBuilder();
            var effect = LineEffect.None;
            string group = null;
            foreach (var (name, styleGroup) in styles)
            {
                if (!LineStyles.TryGet(name, out var style)) continue;
                open.Append(style.RichOpen);
                close.Insert(0, style.RichClose);
                if (style.Effect != LineEffect.None) effect = style.Effect;
                if (!string.IsNullOrEmpty(styleGroup)) group = styleGroup;
            }
            return (open.ToString(), close.ToString(), effect, group);
        }

        /// <summary>From <paramref name="time"/>, the words of error group <paramref name="group"/> give way to <paramref name="text"/> (fix style, shown at once).</summary>
        private void Fix(string group, string text, float time)
        {
            int last = -1;
            for (int i = 0; i < m_Pieces.Count; i++)
            {
                if (m_Pieces[i].Group != group || m_Pieces[i].Kind != PieceKind.Text) continue;
                var piece = m_Pieces[i];
                piece.HideAt = Math.Min(piece.HideAt, time);
                m_Pieces[i] = piece;
                last = i;
            }
            if (last < 0) return; // Parse reported it
            LineStyles.TryGet(LineStyles.Fix, out var fix);
            m_Pieces.Insert(last + 1, new Piece { Kind = PieceKind.Text, Text = text, Open = fix.RichOpen, Close = fix.RichClose, Effect = fix.Effect, Start = time, Interval = 0f, HideAt = float.PositiveInfinity });
        }

        private void Scale(float factor)
        {
            for (int i = 0; i < m_Pieces.Count; i++)
            {
                var p = m_Pieces[i];
                p.Start *= factor;
                p.Interval *= factor;
                if (!float.IsPositiveInfinity(p.HideAt)) p.HideAt *= factor;
                m_Pieces[i] = p;
            }
            for (int i = 0; i < m_Events.Count; i++) m_Events[i] = new LineEvent(m_Events[i].Time * factor, m_Events[i].Name, m_Events[i].Argument);
            Duration *= factor;
        }

        /// <summary>
        /// What the line shows <paramref name="time"/> seconds after it started (∞ = all of it). Pass the same
        /// <paramref name="frame"/> every frame: it then allocates nothing (see <see cref="LineFrame.Changed"/>).
        /// </summary>
        public LineFrame Compose(float time, LineFrame frame = null)
        {
            frame ??= new LineFrame();
            frame.Effects.Clear();
            var sb = frame.BeginText();
            int visible = 0;
            bool indented = false;
            foreach (var piece in m_Pieces)
            {
                if (!float.IsPositiveInfinity(piece.HideAt) && piece.HideAt <= time) continue; // ∞ = never hidden, even at time ∞ (Final)
                switch (piece.Kind)
                {
                    case PieceKind.Rich:
                        sb.Append(piece.Text);
                        break;
                    case PieceKind.Break:
                        if (piece.Start > time) break;
                        if (indented) { sb.Append("</indent>"); indented = false; }
                        sb.Append('\n');
                        visible++;
                        if (piece.Open != null)
                        {
                            sb.Append(piece.Open);
                            indented = true;
                        }
                        break;
                    default:
                    {
                        int shown = Shown(piece, time);
                        if (shown == 0) break;
                        sb.Append(piece.Open).Append(piece.Text, 0, shown).Append(piece.Close);
                        if (piece.Effect != LineEffect.None) frame.Effects.Add(new LineEffectRange(visible, shown, piece.Effect));
                        visible += shown;
                        break;
                    }
                }
            }
            if (indented) sb.Append("</indent>");
            frame.EndText();
            frame.VisibleCharacters = visible;
            frame.Done = time >= Duration;
            return frame;
        }

        private static int Shown(Piece piece, float time)
        {
            if (time < piece.Start) return 0;
            if (piece.Interval <= 0f || float.IsPositiveInfinity(time)) return piece.Text.Length;
            return Math.Min(piece.Text.Length, (int)Math.Floor((time - piece.Start) / piece.Interval) + 1);
        }

        /// <summary>The events reached after <paramref name="from"/> and up to <paramref name="to"/> (in order).</summary>
        public void EventsBetween(float from, float to, List<LineEvent> output)
        {
            foreach (var e in m_Events)
                if (e.Time > from && e.Time <= to) output.Add(e);
        }

        /// <summary>The finished line as rich text (all tags turned into what they show): for plain renderers, logs and exports.</summary>
        public static string Final(string raw) => string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0 ? raw ?? string.Empty : Build(raw, 0f).Compose(float.PositiveInfinity).Text;
    }
}
