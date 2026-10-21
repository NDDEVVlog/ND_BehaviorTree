using System.Collections.Generic;
using System.Text;

namespace ND.Localization.Editor
{
    /// <summary>
    /// CSV round-trip for <see cref="StringTable"/>. Columns: <c>id, key, comment, &lt;locale code&gt;…</c>.
    /// The <c>id</c> column keeps references stable when a translator renames a key; it may be omitted in hand-written files.
    /// </summary>
    public static class LocalizationCsv
    {
        public const string IdColumn = "id";
        public const string KeyColumn = "key";
        public const string CommentColumn = "comment";

        public class ImportReport
        {
            public int added;
            public int renamed;
            public int valuesWritten;
            public int valuesCleared;
            public readonly List<string> errors = new List<string>();

            public override string ToString() =>
                $"added {added}, renamed {renamed}, values written {valuesWritten}, cleared {valuesCleared}, errors {errors.Count}";
        }

        public static string Export(StringTable table, IReadOnlyList<string> localeCodes)
        {
            var sb = new StringBuilder();
            var header = new List<string> { IdColumn, KeyColumn, CommentColumn };
            header.AddRange(localeCodes);
            WriteRow(sb, header);

            var row = new List<string>();
            foreach (var entry in table.Entries)
            {
                row.Clear();
                row.Add(entry.id.ToString());
                row.Add(entry.key);
                row.Add(entry.comment ?? string.Empty);
                foreach (var code in localeCodes)
                    row.Add(table.TryGetValue(entry.id, code, out string v) ? v : string.Empty);
                WriteRow(sb, row);
            }
            return sb.ToString();
        }

        /// <param name="clearEmptyCells">true: an empty locale cell removes that translation; false: empty cells are skipped.</param>
        public static ImportReport Import(StringTable table, string csv, bool clearEmptyCells)
        {
            var report = new ImportReport();
            var rows = Parse(csv);
            if (rows.Count == 0)
            {
                report.errors.Add("File is empty.");
                return report;
            }

            var header = rows[0];
            int idCol = header.IndexOf(IdColumn);
            int keyCol = header.IndexOf(KeyColumn);
            int commentCol = header.IndexOf(CommentColumn);
            if (keyCol < 0)
            {
                report.errors.Add($"Header has no '{KeyColumn}' column.");
                return report;
            }
            var localeCols = new List<(int col, string code)>();
            for (int c = 0; c < header.Count; c++)
                if (c != idCol && c != keyCol && c != commentCol && !string.IsNullOrWhiteSpace(header[c]))
                    localeCols.Add((c, header[c].Trim()));

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue; // blank line
                string key = Cell(row, keyCol).Trim();
                if (key.Length == 0)
                {
                    report.errors.Add($"Row {r + 1}: empty key.");
                    continue;
                }

                long id = ResolveOrCreate(table, row, idCol, key, r, report);
                if (id == LocalizationTable.InvalidId) continue;

                table.TryGetEntry(id, out var entry);
                if (commentCol >= 0) entry.comment = Cell(row, commentCol);

                foreach (var (col, code) in localeCols)
                {
                    string value = Cell(row, col);
                    if (value.Length > 0)
                    {
                        table.SetValue(id, code, value);
                        report.valuesWritten++;
                    }
                    else if (clearEmptyCells && table.ClearValue(id, code))
                    {
                        report.valuesCleared++;
                    }
                }
            }
            return report;
        }

        private static long ResolveOrCreate(StringTable table, List<string> row, int idCol, string key, int r, ImportReport report)
        {
            long fileId = LocalizationTable.InvalidId;
            if (idCol >= 0)
            {
                string idText = Cell(row, idCol).Trim();
                if (idText.Length > 0 && !long.TryParse(idText, out fileId))
                {
                    report.errors.Add($"Row {r + 1}: id '{idText}' is not a number.");
                    return LocalizationTable.InvalidId;
                }
            }

            if (fileId > LocalizationTable.InvalidId && table.Contains(fileId))
            {
                table.TryGetKey(fileId, out string oldKey);
                if (oldKey == key) return fileId;
                if (!table.RenameEntry(fileId, key))
                {
                    report.errors.Add($"Row {r + 1}: cannot rename '{oldKey}' to '{key}' (key already used).");
                    return LocalizationTable.InvalidId;
                }
                report.renamed++;
                return fileId;
            }

            if (table.TryGetId(key, out long byKey)) return byKey;

            long added = fileId > LocalizationTable.InvalidId ? table.AddEntry(key, fileId) : table.AddEntry(key);
            if (added == LocalizationTable.InvalidId)
            {
                report.errors.Add($"Row {r + 1}: could not add key '{key}'.");
                return LocalizationTable.InvalidId;
            }
            report.added++;
            return added;
        }

        private static string Cell(List<string> row, int col) => col >= 0 && col < row.Count ? row[col] : string.Empty;

        // ---- RFC 4180 ----

        public static void WriteRow(StringBuilder sb, IReadOnlyList<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                string cell = cells[i] ?? string.Empty;
                bool quote = cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || cell.StartsWith(" ") || cell.EndsWith(" ");
                if (!quote)
                {
                    sb.Append(cell);
                    continue;
                }
                sb.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
            }
            sb.Append("\r\n");
        }

        public static List<List<string>> Parse(string csv)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csv)) return rows;

            int i = 0;
            if (csv[0] == '﻿') i = 1; // UTF-8 BOM written by Excel
            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(ch);
                    continue;
                }

                switch (ch)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(ch);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
