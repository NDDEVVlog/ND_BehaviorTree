using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Bridge
{
    /// <summary>
    /// Project Settings → ND Behavior Tree → AI Access: owner password, per-session unlock, access level, port,
    /// bridge status and ready-to-paste MCP config. Also reachable from ND_BehaviorTree → AI Access.
    /// </summary>
    public static class AiAccessSettingsProvider
    {
        public const string Path = ND_BTSettingsProvider.GeneralPath + "/AI Access";

        private static string s_Password = string.Empty, s_NewPassword = string.Empty, s_Confirm = string.Empty, s_Message;
        private static MessageType s_MessageType;
        private static int s_PortField = -1;

        /// <summary>Opened from the ND Behavior Tree settings page (button shown only when AI features are enabled).</summary>
        public static void Open() => SettingsService.OpenProjectSettings(Path);

        /// <summary>The page exists only while AI features are enabled in the ND Behavior Tree settings.</summary>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            if (!AiAccess.FeatureEnabled) return null;
            return new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "AI Access",
                keywords = new HashSet<string>(new[] { "AI", "MCP", "CLI", "Bridge", "Password", "Claude" }),
                guiHandler = _ => Draw()
            };
        }

        private static void Draw()
        {
            EditorGUILayout.HelpBox(
                "Lets an AI assistant (Claude Code via MCP, or the ndbt CLI) read and edit behavior trees through the editor.\n" +
                "Off by default. Changing the level needs the owner password, and every user must unlock it each editor session. " +
                "This is a guardrail, not security against someone who can edit the project files.", MessageType.Info);

            if (s_Message != null) EditorGUILayout.HelpBox(s_Message, s_MessageType);
            EditorGUILayout.Space();

            if (!AiAccess.HasPassword) DrawSetFirstPassword();
            else if (!AiAccess.IsUnlocked) DrawUnlock();
            else DrawUnlocked();

            EditorGUILayout.Space(12);
            DrawStatus();
        }

        private static void DrawSetFirstPassword()
        {
            EditorGUILayout.LabelField("Set the owner password", EditorStyles.boldLabel);
            s_NewPassword = EditorGUILayout.PasswordField("New password", s_NewPassword);
            s_Confirm = EditorGUILayout.PasswordField("Confirm", s_Confirm);
            if (GUILayout.Button("Set Owner Password", GUILayout.Height(24)))
            {
                if (s_NewPassword != s_Confirm) Report("Passwords do not match.", MessageType.Error);
                else if (AiAccess.SetPassword(null, s_NewPassword, out var error)) { Report("Owner password set. This session is unlocked.", MessageType.Info); ClearFields(); }
                else Report(error, MessageType.Error);
            }
        }

        private static void DrawUnlock()
        {
            EditorGUILayout.LabelField($"Locked — project level: {AiAccess.Level}", EditorStyles.boldLabel);
            s_Password = EditorGUILayout.PasswordField("Owner password", s_Password);
            if (GUILayout.Button("Unlock for this session", GUILayout.Height(24)))
            {
                if (AiAccess.Unlock(s_Password)) { Report("Unlocked for this editor session.", MessageType.Info); ClearFields(); }
                else Report("Incorrect password.", MessageType.Error);
            }
        }

        private static void DrawUnlocked()
        {
            EditorGUILayout.LabelField("Unlocked (this session)", EditorStyles.boldLabel);

            var level = (AiAccessLevel)EditorGUILayout.EnumPopup(new GUIContent("Access level", "Off: no AI access. Read-only: inspect trees, validate, watch agents. Read + Write: also create and edit trees."), AiAccess.Level);
            if (level != AiAccess.Level && !AiAccess.SetLevel(level, out var levelError)) Report(levelError, MessageType.Error);

            if (s_PortField < 0) s_PortField = AiAccess.Port;
            using (new EditorGUILayout.HorizontalScope())
            {
                s_PortField = EditorGUILayout.IntField("Port (localhost)", s_PortField);
                using (new EditorGUI.DisabledScope(s_PortField == AiAccess.Port))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)) && !AiAccess.SetPort(s_PortField, out var portError)) Report(portError, MessageType.Error);
                }
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lock now")) { AiAccess.Lock(); Report("Locked. The bridge stopped.", MessageType.Info); }
                if (GUILayout.Button("Copy MCP config")) { EditorGUIUtility.systemCopyBuffer = McpConfig(); Report("MCP config copied: paste it into .mcp.json (see tools/ndbt/README.md).", MessageType.Info); }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Change owner password", EditorStyles.boldLabel);
            s_Password = EditorGUILayout.PasswordField("Current", s_Password);
            s_NewPassword = EditorGUILayout.PasswordField("New", s_NewPassword);
            s_Confirm = EditorGUILayout.PasswordField("Confirm", s_Confirm);
            if (GUILayout.Button("Change Password"))
            {
                if (s_NewPassword != s_Confirm) Report("Passwords do not match.", MessageType.Error);
                else if (AiAccess.SetPassword(s_Password, s_NewPassword, out var error)) { Report("Password changed.", MessageType.Info); ClearFields(); }
                else Report(error, MessageType.Error);
            }
        }

        private static void DrawStatus()
        {
            EditorGUILayout.LabelField("Bridge", EditorStyles.boldLabel);
            string state = BridgeServer.IsRunning ? $"Running on 127.0.0.1:{BridgeServer.RunningPort} ({AiAccess.Level})"
                : AiAccess.Level == AiAccessLevel.Off ? "Stopped (access level is Off)"
                : !AiAccess.IsUnlocked ? "Stopped (locked in this session)"
                : "Stopped";
            EditorGUILayout.LabelField("State", state);
            if (!string.IsNullOrEmpty(BridgeServer.LastError)) EditorGUILayout.HelpBox(BridgeServer.LastError, MessageType.Error);
            EditorGUILayout.LabelField("Connection file", "Library/NDBT/bridge.json", EditorStyles.miniLabel);
        }

        /// <summary>.mcp.json snippet for Claude Code (project scope).</summary>
        public static string McpConfig()
        {
            string project = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            return "{\n  \"mcpServers\": {\n    \"ndbt\": {\n      \"command\": \"dotnet\",\n" +
                   $"      \"args\": [\"{project}/tools/ndbt/bin/ndbt.dll\", \"mcp\", \"--project\", \"{project}\"]\n" +
                   "    }\n  }\n}";
        }

        private static void Report(string message, MessageType type)
        {
            s_Message = message;
            s_MessageType = type;
        }

        private static void ClearFields() => s_Password = s_NewPassword = s_Confirm = string.Empty;
    }
}
