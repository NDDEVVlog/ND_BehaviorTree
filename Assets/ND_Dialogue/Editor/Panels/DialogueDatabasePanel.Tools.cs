using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor
{
    /// <summary>The Tools tab: find and replace, statistics, exports (see <see cref="DialogueTools"/>), and the importer.</summary>
    public partial class DialogueDatabasePanel
    {
        private const int MaxResults = 200;
        private string m_FindText = string.Empty;
        private string m_ReplaceText = string.Empty;
        private bool m_MatchCase;

        private void BuildTools()
        {
            string locale = Args().Locale;
            Intro($"Work on every conversation at once. Texts are read in the preview locale ({(string.IsNullOrEmpty(locale) ? "none" : locale)}); plain text is the same in every locale.");
            BuildFindReplace(locale);
            BuildStatistics(locale);
            BuildExports(locale);
        }

        private void BuildFindReplace(string locale)
        {
            var group = Group("Find and replace", "In every line and choice label. Click a result to show it on the graph.");
            var find = new TextField("Find") { value = m_FindText };
            var replace = new TextField("Replace with") { value = m_ReplaceText };
            var matchCase = new Toggle("Match case") { value = m_MatchCase };
            var results = new VisualElement { name = "find-results" };
            void Search()
            {
                results.Clear();
                if (string.IsNullOrEmpty(m_FindText)) return;
                var hits = DialogueTools.Find(m_Database, m_FindText, m_MatchCase, locale);
                results.Add(new Label(hits.Count == 0 ? "No match." : $"{hits.Count} text(s){(hits.Count > MaxResults ? $" — the first {MaxResults} shown" : string.Empty)}:") { name = "find-count" });
                foreach (var hit in hits.Take(MaxResults))
                {
                    var h = hit;
                    var row = new Button(() => EntryRequested?.Invoke(h.Conversation.id, h.Entry.id)) { text = $"{h.Where}:  {Snippet(h.Value, m_FindText)}", tooltip = h.Value };
                    row.AddToClassList("dlg-tools__hit");
                    results.Add(row);
                }
            }
            find.RegisterValueChangedCallback(evt => { m_FindText = evt.newValue; Search(); });
            replace.RegisterValueChangedCallback(evt => m_ReplaceText = evt.newValue);
            matchCase.RegisterValueChangedCallback(evt => { m_MatchCase = evt.newValue; Search(); });
            var replaceAll = new Button(() =>
            {
                var hits = DialogueTools.Find(m_Database, m_FindText, m_MatchCase, locale);
                if (hits.Count == 0) return;
                if (!EditorUtility.DisplayDialog("Replace all", $"Replace '{m_FindText}' with '{m_ReplaceText}' in {hits.Count} text(s)? (One undo step.)", "Replace", "Cancel")) return;
                int changed = DialogueTools.ReplaceAll(m_Database, m_FindText, m_ReplaceText, m_MatchCase, locale);
                Debug.Log($"[Dialogue Tools] Replaced '{m_FindText}' in {changed} text(s) of '{m_Database.name}'.", m_Database);
                DatabaseChanged?.Invoke();
                Rebuild();
            }) { text = "Replace all" };
            group.Add(find);
            group.Add(replace);
            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row2.Add(matchCase);
            row2.Add(new VisualElement { style = { flexGrow = 1 } });
            row2.Add(replaceAll);
            group.Add(row2);
            group.Add(results);
            Search();
            m_Content.Add(group);
        }

        private static string Snippet(string text, string query)
        {
            int at = text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase);
            int start = Mathf.Max(0, at - 30);
            string snippet = text.Substring(start, Mathf.Min(text.Length - start, 90)).Replace('\n', ' ');
            return (start > 0 ? "…" : string.Empty) + snippet + (start + 90 < text.Length ? "…" : string.Empty);
        }

        private void BuildStatistics(string locale)
        {
            var stats = DialogueTools.Statistics(m_Database, locale);
            var group = Group("Statistics", $"{stats.Lines} lines · {stats.Choices} choices · {stats.Words} words");
            group.Add(Table("Conversation", stats.Conversations));
            group.Add(Table("Speaker", stats.Speakers));
            string translation = stats.Missing.Count == 0 && stats.Plain == 0 ? $"Every text has a {locale} translation." :
                $"{stats.Missing.Count} connected text(s) without a {locale} translation, {stats.Plain} plain text(s) not connected to a table.";
            group.Add(new Label(translation) { name = "translation-status", style = { whiteSpace = WhiteSpace.Normal, marginTop = 4 } });
            foreach (var missing in stats.Missing.Take(30))
            {
                var m = missing;
                var row = new Button(() => EntryRequested?.Invoke(m.Conversation.id, m.Entry.id)) { text = $"missing: {m.Where}" };
                row.AddToClassList("dlg-tools__hit");
                group.Add(row);
            }
            m_Content.Add(group);
        }

        private static VisualElement Table(string title, System.Collections.Generic.IEnumerable<DialogueTools.Stats.Row> rows)
        {
            var table = new VisualElement();
            table.AddToClassList("dlg-tools__table");
            table.Add(TableRow(title, "lines", "choices", "words", true));
            foreach (var row in rows) table.Add(TableRow(row.Name, row.Lines.ToString(), row.Choices.ToString(), row.Words.ToString(), false));
            return table;
        }

        private static VisualElement TableRow(string name, string lines, string choices, string words, bool header)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.AddToClassList(header ? "dlg-tools__row--header" : "dlg-tools__row");
            row.Add(new Label(name) { style = { flexGrow = 1, flexBasis = 0 } });
            foreach (var cell in new[] { lines, choices, words }) row.Add(new Label(cell) { style = { width = 70, unityTextAlign = TextAnchor.MiddleRight } });
            return row;
        }

        private void BuildExports(string locale)
        {
            var group = Group("Export and import", "Export every conversation for translators, voice actors or a read-through; import Twine or Arcweave stories.");
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(new Button(() => Export("csv", DialogueTools.ExportCsv(m_Database, locale))) { text = "Export CSV…", tooltip = "One row per line and choice label: conversation, entry, line, speaker, emotion, kind, text." });
            row.Add(new Button(() => Export("txt", DialogueTools.ExportScreenplay(m_Database, locale))) { text = "Export screenplay…", tooltip = "Every conversation as a readable script." });
            row.Add(new Button(Import.DialogueImportWindow.Open) { text = "Import Twine / Arcweave…" });
            group.Add(row);
            m_Content.Add(group);
        }

        private void Export(string extension, string content)
        {
            string path = EditorUtility.SaveFilePanel("Export dialogue", string.Empty, $"{m_Database.name}.{extension}", extension);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, content, new System.Text.UTF8Encoding(true)); // BOM: spreadsheets read UTF-8 correctly
            Debug.Log($"[Dialogue Tools] Exported '{m_Database.name}' to {path}.", m_Database);
            EditorUtility.RevealInFinder(path);
        }
    }
}
