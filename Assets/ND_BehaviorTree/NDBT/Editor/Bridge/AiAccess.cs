using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Bridge
{
    public enum AiAccessLevel { Off = 0, ReadOnly = 1, ReadWrite = 2 }

    /// <summary>
    /// Password-protected access for the AI bridge (CLI / MCP).
    ///   - The project-wide level (Off / Read-only / Read + Write) lives in ND_BehaviorTreeSetting (shared via VCS).
    ///   - Only a salted PBKDF2 hash of the owner password is stored, never the password.
    ///   - Each user must unlock with the password in each editor session (SessionState); the bridge only runs while
    ///     the level is not Off AND this session is unlocked. There is deliberately no command to unlock remotely.
    /// This is a guardrail against accidental/casual AI access, not security against someone who can edit the
    /// project files.
    /// </summary>
    public static class AiAccess
    {
        private const string UnlockedKey = "ND_BehaviorTree.AiAccess.Unlocked";
        private const int Iterations = 20000;

        public static event Action Changed;

        private static ND_BehaviorTreeSetting Settings => ND_BehaviorTreeSetting.Instance;

        /// <summary>Master switch (ND Behavior Tree settings). While off, all AI UI is hidden and the bridge never runs.</summary>
        public static bool FeatureEnabled => Settings.aiFeaturesEnabled;

        /// <summary>Turns the AI features on/off (called from the settings inspector, which is behind the settings lock).</summary>
        public static void SetFeatureEnabled(bool enabled)
        {
            if (Settings.aiFeaturesEnabled == enabled) return;
            Settings.aiFeaturesEnabled = enabled;
            if (!enabled) SessionState.SetBool(UnlockedKey, false);
            SaveSettings();
            SettingsService.NotifySettingsProviderChanged(); // show/hide the AI Access page
            Changed?.Invoke();
        }

        public static bool HasPassword => !string.IsNullOrEmpty(Settings.aiPasswordHash) && !string.IsNullOrEmpty(Settings.aiPasswordSalt);
        public static bool IsUnlocked => HasPassword && SessionState.GetBool(UnlockedKey, false);
        public static AiAccessLevel Level => Settings.aiAccessLevel;

        /// <summary>The bridge serves requests only when access is enabled and unlocked in this session.</summary>
        public static bool IsActive => FeatureEnabled && Level != AiAccessLevel.Off && IsUnlocked;
        public static bool CanWrite => IsActive && Level == AiAccessLevel.ReadWrite;

        /// <summary>
        /// AI tools in the editor UI (AI menu, Copy for AI, import) are visible only when the feature is enabled AND
        /// unlocked with the owner password in this session. They work without the bridge (copy/paste).
        /// </summary>
        public static bool UiUnlocked => FeatureEnabled && IsUnlocked;
        public static int Port => Mathf.Clamp(Settings.aiBridgePort, 1024, 65535);

        /// <summary>Sets the owner password. If one exists, <paramref name="currentPassword"/> must match it.</summary>
        public static bool SetPassword(string currentPassword, string newPassword, out string error)
        {
            error = null;
            if (HasPassword && !Verify(currentPassword)) error = "Current password is incorrect.";
            else if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6) error = "The new password must have at least 6 characters.";
            if (error != null) return false;

            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            Settings.aiPasswordSalt = Convert.ToBase64String(salt);
            Settings.aiPasswordHash = Hash(newPassword, salt);
            SaveSettings();
            SessionState.SetBool(UnlockedKey, true); // the person who just set it is unlocked
            Changed?.Invoke();
            return true;
        }

        public static bool Unlock(string password)
        {
            if (!Verify(password)) return false;
            SessionState.SetBool(UnlockedKey, true);
            Changed?.Invoke();
            return true;
        }

        public static void Lock()
        {
            SessionState.SetBool(UnlockedKey, false);
            Changed?.Invoke();
        }

        /// <summary>Changing the level requires an unlocked session.</summary>
        public static bool SetLevel(AiAccessLevel level, out string error)
        {
            error = IsUnlocked ? null : "Unlock with the owner password first.";
            if (error != null) return false;
            Settings.aiAccessLevel = level;
            SaveSettings();
            Changed?.Invoke();
            return true;
        }

        public static bool SetPort(int port, out string error)
        {
            error = !IsUnlocked ? "Unlock with the owner password first." : port < 1024 || port > 65535 ? "Port must be between 1024 and 65535." : null;
            if (error != null) return false;
            Settings.aiBridgePort = port;
            SaveSettings();
            Changed?.Invoke();
            return true;
        }

        private static bool Verify(string password)
        {
            if (!HasPassword || string.IsNullOrEmpty(password)) return false;
            var salt = Convert.FromBase64String(Settings.aiPasswordSalt);
            return FixedTimeEquals(Hash(password, salt), Settings.aiPasswordHash);
        }

        private static string Hash(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static void SaveSettings()
        {
            EditorUtility.SetDirty(Settings);
            AssetDatabase.SaveAssetIfDirty(Settings);
        }
    }
}
