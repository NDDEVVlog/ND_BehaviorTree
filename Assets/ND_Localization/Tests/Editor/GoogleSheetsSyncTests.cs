using ND.Localization.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Localization.Tests
{
    public class GoogleSheetsSyncTests
    {
        [Test]
        public void TryParseUrl_ReadsTheSpreadsheetAndTab()
        {
            Assert.IsTrue(GoogleSheetsSync.TryParseUrl("https://docs.google.com/spreadsheets/d/1AbC-d_E/edit#gid=12345", out string id, out string gid));
            Assert.AreEqual("1AbC-d_E", id);
            Assert.AreEqual("12345", gid);

            Assert.IsTrue(GoogleSheetsSync.TryParseUrl("https://docs.google.com/spreadsheets/d/XYZ/edit", out id, out gid));
            Assert.AreEqual("0", gid, "no gid: the first tab");
            Assert.IsFalse(GoogleSheetsSync.TryParseUrl("https://example.com/sheet", out _, out _));
            Assert.AreEqual("https://docs.google.com/spreadsheets/d/XYZ/export?format=csv&gid=7", GoogleSheetsSync.CsvExportUrl("XYZ", "7"));
        }

        [Test]
        public void LooksLikeHtml_DetectsTheSignInPage()
        {
            Assert.IsTrue(GoogleSheetsSync.LooksLikeHtml("  <!DOCTYPE html><html>…"));
            Assert.IsFalse(GoogleSheetsSync.LooksLikeHtml("id,key,comment,en\n1,a,,Hello"));
        }

        [Test]
        public void Import_PullsTheSheetCsv_IntoTheTable_AsOneUndoStep()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                Assert.IsTrue(GoogleSheetsSync.Import(table, "key,comment,en,vi\ngreet,,Hello,Xin chào\n", clearEmptyCells: false));
                Assert.IsTrue(table.TryGetId("greet", out long id));
                Assert.IsTrue(table.TryGetValue(id, "vi", out string vi));
                Assert.AreEqual("Xin chào", vi);

                Undo.PerformUndo();
                table.InvalidateCache();
                Assert.IsFalse(table.TryGetId("greet", out _), "undo removes the pulled rows");
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(table);
            }
        }
    }
}
