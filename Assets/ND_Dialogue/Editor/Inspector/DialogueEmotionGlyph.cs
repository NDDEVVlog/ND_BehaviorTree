using System.Collections.Generic;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// An emoji per emotion id, so lines read at a glance in the editor (popup, node rows). Matched by common names in
    /// English and Vietnamese, case-insensitive; anything else gets a neutral dot. Editor display only.
    /// </summary>
    public static class DialogueEmotionGlyph
    {
        public const string Unknown = "•";

        private static readonly Dictionary<string, string> s_Glyphs = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Neutral"] = "😐", ["Normal"] = "😐", ["Default"] = "😐", ["Binh thuong"] = "😐",
            ["Happy"] = "😊", ["Joy"] = "😊", ["Smile"] = "😊", ["Vui"] = "😊",
            ["Laugh"] = "😆", ["Excited"] = "🤩",
            ["Angry"] = "😠", ["Mad"] = "😠", ["Gian"] = "😠", ["Furious"] = "😡",
            ["Sad"] = "😢", ["Cry"] = "😭", ["Buon"] = "😢",
            ["Surprised"] = "😲", ["Shock"] = "😱", ["Shocked"] = "😱",
            ["Scared"] = "😨", ["Afraid"] = "😨", ["Fear"] = "😨",
            ["Embarrassed"] = "😳", ["Blush"] = "😳", ["Flustered"] = "😳", ["Shy"] = "😊",
            ["Tsun"] = "😤", ["Pout"] = "😤", ["Huff"] = "😤", ["Annoyed"] = "😒",
            ["Smug"] = "😏", ["Proud"] = "😏", ["Confident"] = "😎",
            ["Love"] = "😍", ["Dere"] = "🥰",
            ["Thinking"] = "🤔", ["Confused"] = "😕", ["Tired"] = "😩", ["Bored"] = "😑",
        };

        public static string For(string emotionId)
        {
            if (string.IsNullOrEmpty(emotionId)) return Unknown;
            string id = emotionId.Trim();
            if (s_Glyphs.TryGetValue(id, out var glyph)) return glyph;
            // A variant of a known emotion ("Older_Angry", "Ball Happy"): the last word decides.
            int split = id.LastIndexOfAny(new[] { '_', ' ', '-' });
            return split >= 0 && s_Glyphs.TryGetValue(id.Substring(split + 1), out glyph) ? glyph : Unknown;
        }
    }
}
