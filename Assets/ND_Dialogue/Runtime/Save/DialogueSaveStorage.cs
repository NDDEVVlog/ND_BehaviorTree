using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>Where save slots are kept. Implement it to use your game's own save system (cloud, one big file…).</summary>
    public interface IDialogueSaveStorage
    {
        bool Write(string slot, string json);
        bool TryRead(string slot, out string json);
        bool Exists(string slot);
        bool Delete(string slot);
    }

    public enum DialogueSaveLocation { File, PlayerPrefs }

    /// <summary>One JSON file per slot under <c>persistentDataPath/NDDialogue/Saves</c> (or a folder you give).</summary>
    public class FileSaveStorage : IDialogueSaveStorage
    {
        public string Folder { get; }

        public FileSaveStorage(string folder = null)
        {
            Folder = folder ?? Path.Combine(Application.persistentDataPath, "NDDialogue", "Saves");
        }

        public string PathOf(string slot) => Path.Combine(Folder, SlotNames.Sanitize(slot) + ".json");

        public bool Write(string slot, string json)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                // Write next to the file, then swap: a crash mid-write never leaves a half save.
                string path = PathOf(slot), temp = path + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[DialogueSave] Cannot write slot '{slot}' to {Folder}: {e.Message}");
                return false;
            }
        }

        public bool TryRead(string slot, out string json)
        {
            json = null;
            string path = PathOf(slot);
            if (!File.Exists(path)) return false;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[DialogueSave] Cannot read slot '{slot}' ({path}): {e.Message}");
                return false;
            }
        }

        public bool Exists(string slot) => File.Exists(PathOf(slot));

        public bool Delete(string slot)
        {
            string path = PathOf(slot);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    /// <summary>Slots in PlayerPrefs under <c>&lt;prefix&gt;&lt;slot&gt;</c> (small saves, WebGL).</summary>
    public class PlayerPrefsSaveStorage : IDialogueSaveStorage
    {
        public string Prefix { get; }

        public PlayerPrefsSaveStorage(string prefix = "NDDialogue.Save.") { Prefix = prefix; }

        private string Key(string slot) => Prefix + SlotNames.Sanitize(slot);

        public bool Write(string slot, string json)
        {
            PlayerPrefs.SetString(Key(slot), json);
            PlayerPrefs.Save();
            return true;
        }

        public bool TryRead(string slot, out string json)
        {
            json = PlayerPrefs.GetString(Key(slot), null);
            return PlayerPrefs.HasKey(Key(slot));
        }

        public bool Exists(string slot) => PlayerPrefs.HasKey(Key(slot));

        public bool Delete(string slot)
        {
            if (!PlayerPrefs.HasKey(Key(slot))) return false;
            PlayerPrefs.DeleteKey(Key(slot));
            PlayerPrefs.Save();
            return true;
        }
    }

    internal static class SlotNames
    {
        /// <summary>Letters, digits, '-' and '_' only (a slot becomes a file name / key).</summary>
        public static string Sanitize(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot)) return "default";
            var sb = new StringBuilder(slot.Length);
            foreach (char c in slot.Trim()) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }
    }
}
