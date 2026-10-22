using ND.Localization;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Reads and writes an entry's texts. Text is plain until it is connected to a table (<see cref="Localize"/>):
    /// plain text is the same in every locale; connected text is read/written per locale in the table, under a key
    /// derived from ids (<c>c3.e7</c>, <c>c3.e7.menu</c>, <c>c3.e7.l2</c> for its second line) so renaming a
    /// conversation never breaks it.
    /// </summary>
    public static class DialogueTextEditing
    {
        public static string KeyFor(Conversation conversation, DialogueEntry entry, bool menu) =>
            $"c{conversation.id}.e{entry.id}" + (menu ? ".menu" : string.Empty);

        /// <summary>Key of line <paramref name="lineIndex"/> (0 = the entry's text).</summary>
        public static string KeyForLine(Conversation conversation, DialogueEntry entry, int lineIndex) =>
            lineIndex == 0 ? KeyFor(conversation, entry, false) : $"c{conversation.id}.e{entry.id}.l{lineIndex + 1}";

        /// <summary>Connected: the text for <paramref name="localeCode"/> (no fallback) or null. Plain: the plain text, or null when empty.</summary>
        public static string Read(LocalizableString text, string localeCode)
        {
            if (text == null) return null;
            if (!text.IsLocalized) return string.IsNullOrEmpty(text.Text) ? null : text.Text;
            return text.Table.TryGetValue(text.EntryId, localeCode, out string value) ? value : null;
        }

        /// <summary>Writes the entry's text or choice label (plain, or the locale's translation when connected). One undo step.</summary>
        public static bool Write(DialogueDatabase database, Conversation conversation, DialogueEntry entry, bool menu, string localeCode, string value) =>
            Write(database, menu ? entry.menuText : entry.text, localeCode, value, menu ? "Edit Menu Text" : "Edit Dialogue Text");

        /// <summary>Writes line <paramref name="lineIndex"/> of the entry (0 = its text). One undo step.</summary>
        public static bool WriteLine(DialogueDatabase database, DialogueEntry entry, int lineIndex, string localeCode, string value) =>
            Write(database, entry.LineText(lineIndex), localeCode, value, "Edit Dialogue Text");

        public static bool Write(DialogueDatabase database, LocalizableString text, string localeCode, string value, string undoLabel)
        {
            if (text == null) return false;
            if (!text.IsLocalized)
            {
                Undo.RecordObject(database, undoLabel);
                text.Text = value;
                EditorUtility.SetDirty(database);
                return true;
            }
            if (string.IsNullOrEmpty(localeCode))
            {
                Debug.LogError("[Dialogue Editor] No preview locale selected: create a Locale asset first.");
                return false;
            }
            var table = text.Table;
            Undo.RecordObject(table, undoLabel);
            if (string.IsNullOrEmpty(value)) table.ClearValue(text.EntryId, localeCode);
            else table.SetValue(text.EntryId, localeCode, value);
            EditorUtility.SetDirty(table);
            return true;
        }

        /// <summary>
        /// Connects plain text to the database's text table: creates (or reuses) the key, stores the plain text as the
        /// <paramref name="localeCode"/> translation, and links the entry. Empty plain text is left alone. One undo step.
        /// </summary>
        public static bool Localize(DialogueDatabase database, Conversation conversation, DialogueEntry entry, bool menu, string localeCode) =>
            Localize(database, menu ? entry.menuText : entry.text, KeyFor(conversation, entry, menu), localeCode);

        /// <summary>Like <see cref="Localize(DialogueDatabase, Conversation, DialogueEntry, bool, string)"/> for line <paramref name="lineIndex"/>.</summary>
        public static bool LocalizeLine(DialogueDatabase database, Conversation conversation, DialogueEntry entry, int lineIndex, string localeCode) =>
            Localize(database, entry.LineText(lineIndex), KeyForLine(conversation, entry, lineIndex), localeCode);

        public static bool Localize(DialogueDatabase database, LocalizableString text, string key, string localeCode)
        {
            if (text == null || text.IsLocalized || string.IsNullOrEmpty(text.Text)) return false;
            var table = database.textTable;
            if (table == null)
            {
                Debug.LogError($"[Dialogue Editor] '{database.name}' has no Text Table (Settings tab): cannot localize.", database);
                return false;
            }
            if (string.IsNullOrEmpty(localeCode))
            {
                Debug.LogError("[Dialogue Editor] No locale to store the text under: create a Locale asset first.");
                return false;
            }

            Undo.RecordObjects(new Object[] { database, table }, "Localize Dialogue Text");
            if (!table.TryGetId(key, out long id)) id = table.AddEntry(key);
            if (id == LocalizationTable.InvalidId) return false;
            table.SetValue(id, localeCode, text.Text);
            text.SetReference(table, id);
            EditorUtility.SetDirty(database);
            EditorUtility.SetDirty(table);
            return true;
        }

        /// <summary>Localizes every plain line (all lines of each entry) and choice label. Returns how many texts were connected.</summary>
        public static int LocalizeAll(DialogueDatabase database, string localeCode)
        {
            int count = 0;
            foreach (var conversation in database.conversations)
            {
                if (conversation == null) continue;
                foreach (var entry in conversation.entries)
                {
                    if (entry == null) continue;
                    if (Localize(database, conversation, entry, menu: false, localeCode)) count++;
                    if (Localize(database, conversation, entry, menu: true, localeCode)) count++;
                    for (int i = 1; i <= entry.moreLines.Count; i++)
                        if (LocalizeLine(database, conversation, entry, i, localeCode)) count++;
                }
            }
            return count;
        }

        /// <summary>Plain texts (lines and choice labels) still waiting to be localized.</summary>
        public static int CountPlain(DialogueDatabase database)
        {
            int count = 0;
            foreach (var conversation in database.conversations)
            {
                if (conversation == null) continue;
                foreach (var entry in conversation.entries)
                {
                    if (entry == null) continue;
                    if (IsPlain(entry.text)) count++;
                    if (IsPlain(entry.menuText)) count++;
                    foreach (var page in entry.moreLines) if (IsPlain(page?.text)) count++;
                }
            }
            return count;
        }

        private static bool IsPlain(LocalizableString text) => text != null && !text.IsLocalized && !string.IsNullOrEmpty(text.Text);
    }
}
