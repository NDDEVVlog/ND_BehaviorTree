using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor
{
    public enum DialogueAiAccessLevel { Off = 0, ReadOnly = 1, ReadWrite = 2 }

    /// <summary>
    /// Project-wide ND Dialogue settings, stored in ProjectSettings/NDDialogueSettings.asset (shared through version
    /// control). Today it only holds the AI bridge switches, which are edited on the settings pages — never directly.
    /// </summary>
    [FilePath("ProjectSettings/NDDialogueSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class DialogueProjectSettings : ScriptableSingleton<DialogueProjectSettings>
    {
        public const string SettingsPath = "Project/ND Dialogue";

        // Master switch for the AI bridge. Off by default; toggled under "Advanced" on the ND Dialogue settings page.
        [SerializeField] internal bool aiFeaturesEnabled;
        [SerializeField] internal DialogueAiAccessLevel aiAccessLevel = DialogueAiAccessLevel.Off;
        [SerializeField] internal string aiPasswordSalt;
        [SerializeField] internal string aiPasswordHash;
        [SerializeField] internal int aiBridgePort = 39878;

        internal void SaveNow() => Save(true);
    }
}
