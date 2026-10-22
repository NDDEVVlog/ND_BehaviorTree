using System.Collections.Generic;

namespace ND.Dialogue
{
    /// <summary>Per-character motion a renderer applies (TextMeshPro moves the glyphs; plain text renderers ignore it).</summary>
    public enum LineEffect : byte { None, Wave, Shake }

    /// <summary>What a style tag looks like: rich text around its characters and an optional motion.</summary>
    public readonly struct LineStyle
    {
        public readonly string RichOpen;
        public readonly string RichClose;
        public readonly LineEffect Effect;

        public LineStyle(string richOpen, string richClose, LineEffect effect = LineEffect.None)
        {
            RichOpen = richOpen ?? string.Empty;
            RichClose = richClose ?? string.Empty;
            Effect = effect;
        }
    }

    /// <summary>
    /// The style tags a line may use, as data: name → rich text + effect. Every renderer (the game's dialogue UI, the
    /// editor's Test Dialogue) reads the same table, so a style looks the same everywhere. Add your own with
    /// <see cref="Register"/> (e.g. at startup: <c>LineStyles.Register("angry", new LineStyle("&lt;color=#f44&gt;", "&lt;/color&gt;", LineEffect.Shake))</c>).
    /// </summary>
    public static class LineStyles
    {
        /// <summary>The style a <c>&lt;fix&gt;</c> gives its new words.</summary>
        public const string Fix = "fix";

        private static readonly Dictionary<string, LineStyle> s_Styles = new Dictionary<string, LineStyle>
        {
            ["wave"] = new LineStyle(null, null, LineEffect.Wave),
            ["shake"] = new LineStyle(null, null, LineEffect.Shake),
            ["err"] = new LineStyle("<color=#FF6B6B><s>", "</s></color>", LineEffect.Shake),
            [Fix] = new LineStyle("<color=#7CFFB2>", "</color>"),
            ["whisper"] = new LineStyle("<size=80%><color=#9AA3B2>", "</color></size>"),
            ["shout"] = new LineStyle("<size=125%><b>", "</b></size>", LineEffect.Shake),
        };

        public static bool TryGet(string name, out LineStyle style) => s_Styles.TryGetValue(name ?? string.Empty, out style);

        /// <summary>Adds or replaces a style tag (lower-case name).</summary>
        public static void Register(string name, LineStyle style) => s_Styles[name.ToLowerInvariant()] = style;

        public static IEnumerable<string> Names => s_Styles.Keys;
    }
}
