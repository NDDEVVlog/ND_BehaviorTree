using System.IO;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Import
{
    /// <summary>
    /// ND Dialogue › Import…: brings a Twine story (Twee, or a published Twine 2 HTML file) or an Arcweave project (JSON
    /// export) into a dialogue database as a new conversation, then lists what could not be translated.
    /// </summary>
    public class DialogueImportWindow : EditorWindow
    {
        public enum Format { Twee, TwineHtml, ArcweaveJson }

        private string m_Path = string.Empty;
        private Format m_Format;
        private DialogueDatabase m_Database;
        private readonly DialogueImportOptions m_Options = new DialogueImportOptions();
        private string m_Report = string.Empty;
        private Vector2 m_Scroll;

        [MenuItem("ND Dialogue/Import (Twine, Arcweave)…", false, 100)]
        public static void Open()
        {
            var window = GetWindow<DialogueImportWindow>(true, "Import Dialogue");
            window.minSize = new Vector2(460f, 380f);
            if (Selection.activeObject is DialogueDatabase selected) window.m_Database = selected;
        }

        /// <summary>The format a file's extension suggests.</summary>
        public static Format FormatOf(string path)
        {
            switch (Path.GetExtension(path ?? string.Empty).ToLowerInvariant())
            {
                case ".html": case ".htm": return Format.TwineHtml;
                case ".json": return Format.ArcweaveJson;
                default: return Format.Twee;
            }
        }

        /// <summary>Imports <paramref name="text"/> into <paramref name="database"/> (one undo step) and saves the asset.</summary>
        public static DialogueImportReport Import(DialogueDatabase database, string text, Format format, DialogueImportOptions options)
        {
            Undo.RecordObject(database, "Import Dialogue");
            DialogueImportReport report;
            switch (format)
            {
                case Format.TwineHtml: report = TwineImporter.Import(database, TwineImporter.ParseHtml(text), options); break;
                case Format.ArcweaveJson: report = ArcweaveImporter.Import(database, text, options); break;
                default: report = TwineImporter.Import(database, TwineImporter.ParseTwee(text), options); break;
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
            return report;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Twine: .twee / .tw (Twee 3) or a published .html (Twine 2, Harlowe macros: set, if, unless, else). " +
                                    "Arcweave: the project's JSON export. The story becomes a new conversation; nothing existing is changed.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                m_Path = EditorGUILayout.TextField("File", m_Path);
                if (GUILayout.Button("Browse…", GUILayout.Width(70)))
                {
                    string picked = EditorUtility.OpenFilePanelWithFilters("Story to import", Path.GetDirectoryName(m_Path) ?? string.Empty,
                        new[] { "Twine / Arcweave", "twee,tw,txt,html,htm,json", "All files", "*" });
                    if (!string.IsNullOrEmpty(picked)) { m_Path = picked; m_Format = FormatOf(picked); }
                }
            }
            m_Format = (Format)EditorGUILayout.EnumPopup("Format", m_Format);
            using (new EditorGUILayout.HorizontalScope())
            {
                m_Database = (DialogueDatabase)EditorGUILayout.ObjectField("Into database", m_Database, typeof(DialogueDatabase), false);
                if (GUILayout.Button("New…", GUILayout.Width(70))) m_Database = CreateDatabase() ?? m_Database;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            m_Options.NpcActor = EditorGUILayout.TextField(new GUIContent("Speaker (default)", "Lines without a 'Name:' prefix (and elements without a component)."), m_Options.NpcActor);
            m_Options.PlayerActor = EditorGUILayout.TextField(new GUIContent("Player", "Who says the choices."), m_Options.PlayerActor);
            m_Options.SpeakerPrefixes = EditorGUILayout.Toggle(new GUIContent("'Name: text' = speaker", "A line starting with a name and a colon is said by that character (made if missing)."), m_Options.SpeakerPrefixes);
            m_Options.SingleLinkContinues = EditorGUILayout.Toggle(new GUIContent("A lone link continues", "A passage with one link goes on directly instead of showing a one-item choice."), m_Options.SingleLinkContinues);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(m_Database == null || !File.Exists(m_Path)))
            {
                if (GUILayout.Button("Import", GUILayout.Height(28)))
                {
                    var report = Import(m_Database, File.ReadAllText(m_Path), m_Format, m_Options);
                    m_Report = report.ToString();
                    Debug.Log($"[Dialogue Import] {m_Path}\n{m_Report}", m_Database);
                    if (report.Conversation != null) DialogueEditorWindow.Open(m_Database);
                }
            }
            if (m_Database == null) EditorGUILayout.HelpBox("Choose (or create) the database to import into.", MessageType.None);
            else if (!File.Exists(m_Path)) EditorGUILayout.HelpBox("Choose the file to import.", MessageType.None);

            if (string.IsNullOrEmpty(m_Report)) return;
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            EditorGUILayout.TextArea(m_Report, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private static DialogueDatabase CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Dialogue Database", "Imported_Dialogue", "asset", "Where to create the database");
            if (string.IsNullOrEmpty(path)) return null;
            var database = CreateInstance<DialogueDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            return database;
        }
    }
}
