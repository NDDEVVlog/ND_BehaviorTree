using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Bridge
{
    /// <summary>
    /// Project Settings → ND Dialogue (general page; the AI bridge switch sits folded under "Advanced") and, only once
    /// that switch is on, Project Settings → ND Dialogue → AI Bridge (owner password, per-session unlock, level, port,
    /// status). Mirrors ND_BehaviorTree's AI Access so both bridges work the same way.
    /// </summary>
    public static class DialogueSettingsProvider
    {
        public const string BridgePath = DialogueProjectSettings.SettingsPath + "/AI Bridge";
        private const string AdvancedFoldoutKey = "ND.Dialogue.Settings.AdvancedOpen";

        private static string s_Password = string.Empty, s_NewPassword = string.Empty, s_Confirm = string.Empty, s_Message;
        private static MessageType s_MessageType;
        private static int s_PortField = -1;

        [MenuItem("Window/ND Dialogue/Project Settings")]
        public static void Open() => SettingsService.OpenProjectSettings(DialogueProjectSettings.SettingsPath);

        [SettingsProvider]
        public static SettingsProvider CreateGeneral() => new SettingsProvider(DialogueProjectSettings.SettingsPath, SettingsScope.Project)
        {
            label = "ND Dialogue",
            keywords = new HashSet<string>(new[] { "Dialogue", "Conversation", "Bridge" }),
            guiHandler = _ => DrawGeneral()
        };

        /// <summary>The bridge page exists only while the AI bridge is switched on (General → Advanced).</summary>
        [SettingsProvider]
        public static SettingsProvider CreateBridge()
        {
            if (!DialogueAiAccess.FeatureEnabled) return null;
            return new SettingsProvider(BridgePath, SettingsScope.Project)
            {
                label = "AI Bridge",
                keywords = new HashSet<string>(new[] { "AI", "MCP", "CLI", "Bridge", "Password", "Claude" }),
                guiHandler = _ => DrawBridge()
            };
        }

        private static void DrawGeneral()
        {
            EditorGUILayout.HelpBox("Dialogue databases are edited in Window → ND Dialogue → Dialogue Editor. Each database keeps its own " +
                                    "text table, world source and runner options (its Settings section).", MessageType.None);
            EditorGUILayout.Space(8);

            bool open = SessionState.GetBool(AdvancedFoldoutKey, false);
            open = EditorGUILayout.Foldout(open, "Advanced", true);
            SessionState.SetBool(AdvancedFoldoutKey, open);
            if (!open) return;

            EditorGUI.indentLevel++;
            bool enabled = EditorGUILayout.Toggle(new GUIContent("Enable AI Bridge",
                "Lets an AI assistant / the ndbt CLI read and edit dialogue databases through the editor, once unlocked with the owner password. Off by default."),
                DialogueAiAccess.FeatureEnabled);
            if (enabled != DialogueAiAccess.FeatureEnabled) DialogueAiAccess.SetFeatureEnabled(enabled);
            if (DialogueAiAccess.FeatureEnabled && GUILayout.Button("Open AI Bridge settings →")) SettingsService.OpenProjectSettings(BridgePath);
            EditorGUI.indentLevel--;
        }

        private static void DrawBridge()
        {
            EditorGUILayout.HelpBox(
                "Lets an AI assistant (Claude Code via MCP, or the ndbt CLI with dlg- commands) read and edit dialogue databases through the editor.\n" +
                "Off by default. Changing the level needs the owner password, and every user must unlock it each editor session. " +
                "This is a guardrail, not security against someone who can edit the project files.", MessageType.Info);
            if (s_Message != null) EditorGUILayout.HelpBox(s_Message, s_MessageType);
            EditorGUILayout.Space();

            if (!DialogueAiAccess.HasPassword) DrawSetFirstPassword();
            else if (!DialogueAiAccess.IsUnlocked) DrawUnlock();
            else DrawUnlocked();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Bridge", EditorStyles.boldLabel);
            string state = DialogueBridgeServer.IsRunning ? $"Running on 127.0.0.1:{DialogueBridgeServer.RunningPort} ({DialogueAiAccess.Level})"
                : DialogueAiAccess.Level == DialogueAiAccessLevel.Off ? "Stopped (access level is Off)"
                : !DialogueAiAccess.IsUnlocked ? "Stopped (locked in this session)"
                : "Stopped";
            EditorGUILayout.LabelField("State", state);
            if (!string.IsNullOrEmpty(DialogueBridgeServer.LastError)) EditorGUILayout.HelpBox(DialogueBridgeServer.LastError, MessageType.Error);
            EditorGUILayout.LabelField("Connection file", "Library/NDDialogue/bridge.json", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Try it", "dotnet tools/ndbt/bin/ndbt.dll dlg-help", EditorStyles.miniLabel);
        }

        private static void DrawSetFirstPassword()
        {
            EditorGUILayout.LabelField("Set the owner password", EditorStyles.boldLabel);
            s_NewPassword = EditorGUILayout.PasswordField("New password", s_NewPassword);
            s_Confirm = EditorGUILayout.PasswordField("Confirm", s_Confirm);
            if (GUILayout.Button("Set Owner Password", GUILayout.Height(24)))
            {
                if (s_NewPassword != s_Confirm) Report("Passwords do not match.", MessageType.Error);
                else if (DialogueAiAccess.SetPassword(null, s_NewPassword, out var error)) { Report("Owner password set. This session is unlocked.", MessageType.Info); ClearFields(); }
                else Report(error, MessageType.Error);
            }
        }

        private static void DrawUnlock()
        {
            EditorGUILayout.LabelField($"Locked — project level: {DialogueAiAccess.Level}", EditorStyles.boldLabel);
            s_Password = EditorGUILayout.PasswordField("Owner password", s_Password);
            if (GUILayout.Button("Unlock for this session", GUILayout.Height(24)))
            {
                if (DialogueAiAccess.Unlock(s_Password)) { Report("Unlocked for this editor session.", MessageType.Info); ClearFields(); }
                else Report("Incorrect password.", MessageType.Error);
            }
        }

        private static void DrawUnlocked()
        {
            EditorGUILayout.LabelField("Unlocked (this session)", EditorStyles.boldLabel);
            var level = (DialogueAiAccessLevel)EditorGUILayout.EnumPopup(new GUIContent("Access level",
                "Off: no AI access. Read-only: inspect databases, conversations, validate. Read + Write: also create and edit lines, links, characters."), DialogueAiAccess.Level);
            if (level != DialogueAiAccess.Level && !DialogueAiAccess.SetLevel(level, out var levelError)) Report(levelError, MessageType.Error);

            if (s_PortField < 0) s_PortField = DialogueAiAccess.Port;
            using (new EditorGUILayout.HorizontalScope())
            {
                s_PortField = EditorGUILayout.IntField("Port (localhost)", s_PortField);
                using (new EditorGUI.DisabledScope(s_PortField == DialogueAiAccess.Port))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)) && !DialogueAiAccess.SetPort(s_PortField, out var portError)) Report(portError, MessageType.Error);
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Lock now")) { DialogueAiAccess.Lock(); Report("Locked. The bridge stopped.", MessageType.Info); }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Change owner password", EditorStyles.boldLabel);
            s_Password = EditorGUILayout.PasswordField("Current", s_Password);
            s_NewPassword = EditorGUILayout.PasswordField("New", s_NewPassword);
            s_Confirm = EditorGUILayout.PasswordField("Confirm", s_Confirm);
            if (GUILayout.Button("Change Password"))
            {
                if (s_NewPassword != s_Confirm) Report("Passwords do not match.", MessageType.Error);
                else if (DialogueAiAccess.SetPassword(s_Password, s_NewPassword, out var error)) { Report("Password changed.", MessageType.Info); ClearFields(); }
                else Report(error, MessageType.Error);
            }
        }

        private static void Report(string message, MessageType type)
        {
            s_Message = message;
            s_MessageType = type;
        }

        private static void ClearFields() => s_Password = s_NewPassword = s_Confirm = string.Empty;
    }
}
