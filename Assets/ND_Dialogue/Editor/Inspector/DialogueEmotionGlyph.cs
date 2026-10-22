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
            return s_Glyphs.TryGetValue(emotionId.Trim(), out var glyph) ? glyph : Unknown;
        }
    }
}
