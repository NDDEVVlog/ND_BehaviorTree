using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Bridge
{
    /// <summary>
    /// Password-protected access for the dialogue AI bridge (same model as ND_BehaviorTree's AI Access):
    ///   - The master switch and level live in <see cref="DialogueProjectSettings"/> (shared via VCS).
    ///   - Only a salted PBKDF2 hash of the owner password is stored, never the password.
    ///   - Each user unlocks with the password in each editor session (SessionState); the bridge runs only while the
    ///     feature is on, the level is not Off, and this session is unlocked. Nothing can unlock it remotely.
    /// A guardrail against accidental AI edits, not security against someone who can edit the project files.
    /// </summary>
    public static class DialogueAiAccess
    {
        private const string UnlockedKey = "ND.Dialogue.AiAccess.Unlocked";
        private const int Iterations = 20000;

        public static event Action Changed;

        private static DialogueProjectSettings Settings => DialogueProjectSettings.instance;

        public static bool FeatureEnabled => Settings.aiFeaturesEnabled;

        public static void SetFeatureEnabled(bool enabled)
        {
            if (Settings.aiFeaturesEnabled == enabled) return;
            Settings.aiFeaturesEnabled = enabled;
            if (!enabled) SessionState.SetBool(UnlockedKey, false);
            Settings.SaveNow();
            SettingsService.NotifySettingsProviderChanged(); // show/hide the AI Bridge page
            Changed?.Invoke();
        }

        public static bool HasPassword => !string.IsNullOrEmpty(Settings.aiPasswordHash) && !string.IsNullOrEmpty(Settings.aiPasswordSalt);
        public static bool IsUnlocked => HasPassword && SessionState.GetBool(UnlockedKey, false);
        public static DialogueAiAccessLevel Level => Settings.aiAccessLevel;
        public static bool IsActive => FeatureEnabled && Level != DialogueAiAccessLevel.Off && IsUnlocked;
        public static bool CanWrite => IsActive && Level == DialogueAiAccessLevel.ReadWrite;
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
            Settings.SaveNow();
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

        public static bool SetLevel(DialogueAiAccessLevel level, out string error)
        {
            error = IsUnlocked ? null : "Unlock with the owner password first.";
            if (error != null) return false;
            Settings.aiAccessLevel = level;
            Settings.SaveNow();
            Changed?.Invoke();
            return true;
        }

        public static bool SetPort(int port, out string error)
        {
            error = !IsUnlocked ? "Unlock with the owner password first." : port < 1024 || port > 65535 ? "Port must be between 1024 and 65535." : null;
            if (error != null) return false;
            Settings.aiBridgePort = port;
            Settings.SaveNow();
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
    }
}
