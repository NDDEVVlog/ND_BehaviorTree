using System;
using UnityEngine;

namespace ND.Localization
{
    [CreateAssetMenu(fileName = "StringTable", menuName = "ND Localization/String Table", order = 10)]
    public class StringTable : LocalizationTable<string>
    {
        [Tooltip("Optional: the Google Sheet this table is kept in (the editor pulls / pushes it). Not used at runtime.")]
        public GoogleSheetLink googleSheet = new GoogleSheetLink();
    }

    /// <summary>Where a table lives in Google Sheets (editor sync only; nothing here is read at runtime).</summary>
    [Serializable]
    public class GoogleSheetLink
    {
        [Tooltip("The sheet's URL (the tab's gid is kept). Pull needs it shared as 'Anyone with the link: Viewer' or published.")]
        public string sheetUrl;

        [Tooltip("Optional: URL of the Apps Script web app that writes the sheet (Push). The editor can copy the script for you.")]
        public string pushUrl;

        [Tooltip("Optional: shared secret checked by the Apps Script before it writes.")]
        public string pushSecret;

        public bool HasSheet => !string.IsNullOrWhiteSpace(sheetUrl);
        public bool CanPush => !string.IsNullOrWhiteSpace(pushUrl);
    }
}
