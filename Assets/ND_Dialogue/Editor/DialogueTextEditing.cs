using ND.Localization;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Reads and writes an entry's text. Text is plain until it is connected to a table (<see cref="Localize"/>):
    /// plain text is the same in every locale; connected text is read/written per locale in the table, under a key
    /// derived from ids (<c>c3.e7</c>, <c>c3.e7.menu</c>) so renaming a conversation never breaks it.
    /// </summary>
    public static class DialogueTextEditing
    {
        public static string KeyFor(Conversation conversation, DialogueEntry entry, bool menu) =>
            $"c{conversation.id}.e{entry.id}" + (menu ? ".menu" : string.Empty);

        /// <summary>Connected: the text for <paramref name="localeCode"/> (no fallback) or null. Plain: the plain text, or null when empty.</summary>
        public static string Read(LocalizableString text, string localeCode)
        {
            if (text == null) return null;
            if (!text.IsLocalized) return string.IsNullOrEmpty(text.Text) ? null : text.Text;
            return text.Table.TryGetValue(text.EntryId, localeCode, out string value) ? value : null;
        }

        /// <summary>Writes the text (plain, or the locale's translation when connected). One undo step.</summary>
        public static bool Write(DialogueDatabase database, Conversation conversation, DialogueEntry entry, bool menu, string localeCode, string value)
        {
            var text = menu ? entry.menuText : entry.text;
            if (!text.IsLocalized)
            {
                Undo.RecordObject(database, menu ? "Edit Menu Text" : "Edit Dialogue Text");
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
            Undo.RecordObject(table, menu ? "Edit Menu Text" : "Edit Dialogue Text");
            if (string.IsNullOrEmpty(value)) table.ClearValue(text.EntryId, localeCode);
            else table.SetValue(text.EntryId, localeCode, value);
            EditorUtility.SetDirty(table);
            return true;
        }

        /// <summary>
        /// Connects plain text to the database's text table: creates (or reuses) the key, stores the plain text as the
        /// <paramref name="localeCode"/> translation, and links the entry. Empty plain text is left alone. One undo step.
        /// </summary>
        public static bool Localize(DialogueDatabase database, Conversation conversation, DialogueEntry entry, bool menu, string localeCode)
        {
            var text = menu ? entry.menuText : entry.text;
            if (text.IsLocalized || string.IsNullOrEmpty(text.Text)) return false;
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
            string key = KeyFor(conversation, entry, menu);
            if (!table.TryGetId(key, out long id)) id = table.AddEntry(key);
            if (id == LocalizationTable.InvalidId) return false;
            table.SetValue(id, localeCode, text.Text);
            text.SetReference(table, id);
            EditorUtility.SetDirty(database);
            EditorUtility.SetDirty(table);
            return true;
        }

        /// <summary>Localizes every plain line and choice label of the database. Returns how many texts were connected.</summary>
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
                }
            }
            return count;
        }
    }
}
