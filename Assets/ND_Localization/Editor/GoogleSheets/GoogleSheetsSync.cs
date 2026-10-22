using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ND.Localization.Editor
{
    /// <summary>
    /// Keeps a <see cref="StringTable"/> in a Google Sheet, in the same columns as the CSV export
    /// (<c>id, key, comment, &lt;locale&gt;…</c>):
    /// <list type="bullet">
    /// <item><b>Pull</b> downloads the sheet tab as CSV (it must be shared "Anyone with the link: Viewer", or published)
    /// and imports it with Undo.</item>
    /// <item><b>Push</b> sends the table as CSV to a small Apps Script web app bound to the sheet (copy it with
    /// <see cref="AppsScript"/>), which overwrites that tab. Google needs that script to write; no API key is stored.</item>
    /// </list>
    /// Requests are asynchronous; the result is logged.
    /// </summary>
    public static class GoogleSheetsSync
    {
        private const string LogTag = "[Localization] Google Sheets:";
        private static readonly Regex s_Id = new Regex(@"/spreadsheets/d/([a-zA-Z0-9_-]+)");
        private static readonly Regex s_Gid = new Regex(@"[#&?]gid=(\d+)");

        /// <summary>Spreadsheet id and tab id ("0" = first tab when the URL has none).</summary>
        public static bool TryParseUrl(string url, out string spreadsheetId, out string gid)
        {
            spreadsheetId = null;
            gid = "0";
            if (string.IsNullOrWhiteSpace(url)) return false;
            var id = s_Id.Match(url);
            if (!id.Success) return false;
            spreadsheetId = id.Groups[1].Value;
            var tab = s_Gid.Match(url);
            if (tab.Success) gid = tab.Groups[1].Value;
            return true;
        }

        public static string CsvExportUrl(string spreadsheetId, string gid) =>
            $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gid}";

        /// <summary>True when the "CSV" is actually Google's sign-in / error page (the sheet is not shared).</summary>
        public static bool LooksLikeHtml(string text)
        {
            string head = text?.TrimStart() ?? string.Empty;
            return head.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Downloads the linked sheet tab and imports it into <paramref name="table"/> (one Undo step).</summary>
        public static void Pull(StringTable table, bool clearEmptyCells, Action<bool> done = null)
        {
            if (!TryParseUrl(table.googleSheet.sheetUrl, out string id, out string gid))
            {
                Debug.LogError($"{LogTag} '{table.DisplayName}' has no valid sheet URL (https://docs.google.com/spreadsheets/d/<id>/edit#gid=<tab>).", table);
                done?.Invoke(false);
                return;
            }
            var request = UnityWebRequest.Get(CsvExportUrl(id, gid));
            request.SendWebRequest().completed += _ =>
            {
                bool ok = false;
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                        Debug.LogError($"{LogTag} pull of '{table.DisplayName}' failed: {request.error} (HTTP {request.responseCode}).", table);
                    else if (LooksLikeHtml(request.downloadHandler.text))
                        Debug.LogError($"{LogTag} the sheet of '{table.DisplayName}' is not readable without signing in. In Google Sheets: Share → General access → " +
                                       "'Anyone with the link' (Viewer), or File → Share → Publish to web.", table);
                    else ok = Import(table, request.downloadHandler.text, clearEmptyCells);
                }
                finally
                {
                    request.Dispose();
                    done?.Invoke(ok);
                }
            };
        }

        /// <summary>Imports CSV text (same columns as the export) with Undo and logs the report. Used by Pull.</summary>
        public static bool Import(StringTable table, string csv, bool clearEmptyCells)
        {
            Undo.RecordObject(table, "Pull Google Sheet");
            var report = LocalizationCsv.Import(table, csv, clearEmptyCells);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
            Debug.Log($"{LogTag} pulled into '{table.DisplayName}': {report}", table);
            foreach (var error in report.errors) Debug.LogError($"{LogTag} pull of '{table.DisplayName}': {error}", table);
            return report.errors.Count == 0;
        }

        /// <summary>Sends the table (all project locales) to the linked Apps Script, which overwrites the sheet tab.</summary>
        public static void Push(StringTable table, Action<bool> done = null)
        {
            var link = table.googleSheet;
            if (!link.CanPush)
            {
                Debug.LogError($"{LogTag} '{table.DisplayName}' has no Push URL. Add the Apps Script (Copy push script) to the sheet, deploy it as a web app and paste its URL.", table);
                done?.Invoke(false);
                return;
            }
            TryParseUrl(link.sheetUrl, out _, out string gid);
            string csv = LocalizationCsv.Export(table, LocaleCodes.For(table));
            string url = $"{link.pushUrl}{(link.pushUrl.Contains("?") ? "&" : "?")}gid={Uri.EscapeDataString(gid)}&secret={Uri.EscapeDataString(link.pushSecret ?? string.Empty)}";
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(csv)) { contentType = "text/csv; charset=utf-8" },
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SendWebRequest().completed += _ =>
            {
                bool ok = false;
                try
                {
                    string answer = request.downloadHandler?.text ?? string.Empty;
                    if (request.result != UnityWebRequest.Result.Success)
                        Debug.LogError($"{LogTag} push of '{table.DisplayName}' failed: {request.error} (HTTP {request.responseCode}). {answer}", table);
                    else if (!answer.TrimStart().StartsWith("ok", StringComparison.OrdinalIgnoreCase))
                        Debug.LogError($"{LogTag} push of '{table.DisplayName}': the script answered '{Shorten(answer)}' (wrong secret, or the web app is not deployed for 'Anyone').", table);
                    else
                    {
                        ok = true;
                        Debug.Log($"{LogTag} pushed '{table.DisplayName}' ({table.Count} keys): {answer.Trim()}", table);
                    }
                }
                finally
                {
                    request.Dispose();
                    done?.Invoke(ok);
                }
            };
        }

        private static string Shorten(string text)
        {
            text = text?.Trim() ?? string.Empty;
            return text.Length <= 160 ? text : text.Substring(0, 157) + "…";
        }

        /// <summary>
        /// Apps Script to paste in the sheet (Extensions → Apps Script), then Deploy → New deployment → Web app,
        /// execute as "Me", access "Anyone". Writes the posted CSV into the tab given by gid, as plain text (keeps long ids exact).
        /// </summary>
        public const string AppsScript =
@"// ND Localization — push target. Deploy: Deploy → New deployment → Web app (Execute as: Me, Who has access: Anyone).
const SECRET = ''; // optional: the same text as 'Push Secret' in Unity

function doPost(e) {
  if (SECRET && e.parameter.secret !== SECRET) return ContentService.createTextOutput('error: wrong secret');
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const gid = Number(e.parameter.gid || 0);
  const sheet = ss.getSheets().find(s => s.getSheetId() === gid) || ss.getSheets()[0];
  const rows = Utilities.parseCsv(e.postData.contents);
  if (rows.length === 0) return ContentService.createTextOutput('error: empty csv');
  const width = Math.max.apply(null, rows.map(r => r.length));
  const data = rows.map(r => r.concat(Array(width - r.length).fill('')));
  sheet.clearContents();
  const range = sheet.getRange(1, 1, data.length, width);
  range.setNumberFormat('@'); // text: ids stay exact
  range.setValues(data);
  return ContentService.createTextOutput('ok: ' + (data.length - 1) + ' rows');
}
";
    }
}
