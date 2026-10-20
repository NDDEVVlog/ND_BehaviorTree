using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor
{
    public sealed class ND_BehaviorTreeSetting : ScriptableObject
    {
        [Header("Security")]
        public bool isLockEnabled = true;
        public string enableSettingPassword = "SubcribeToNDDEVGAMEandSORA";

        [Header("Core Configuration")]
        public NodeEditorConfig activeConfig;

        [Header("Global UI Templates")]
        public VisualTreeAsset defaultNodeUXML;
        public StyleSheet defaultNodeStyle;
        public StyleSheet graphViewStyle;

        [Header("Graph Theme")]
        public Color graphBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
        public Color gridColor = new Color(0.18f, 0.18f, 0.18f, 1.0f);

        [Header("Graph History")]
        [Tooltip("How many graph edits can be undone per tree (Ctrl+Z in the Behavior Tree window). Oldest steps are dropped.")]
        [Range(1, 1000)]
        public int historyCapacity = 100;

        // Master switch for everything AI-related (AI Access page, Copy for AI, the bridge). Off by default; toggled in
        // the ND Behavior Tree settings page (behind the settings lock).
        [HideInInspector] public bool aiFeaturesEnabled;

        // AI access (CLI / MCP bridge). Edited only on the "AI Access" settings page, behind the owner password.
        [HideInInspector] public Bridge.AiAccessLevel aiAccessLevel = Bridge.AiAccessLevel.Off;
        [HideInInspector] public string aiPasswordSalt;
        [HideInInspector] public string aiPasswordHash;
        [HideInInspector] public int aiBridgePort = 39877;

        [Header("Component Themes")]
        [Tooltip("Name of the theme last applied from the Theme menu (display only; the values below are what is used).")]
        public string activeThemeName = "Default Dark";
        [Tooltip("Decorator / service cards and key slots: stylesheet applied on top of ND_NodeAttachments.uss (set by the theme). Empty = default look.")]
        public StyleSheet attachmentStyle;
        public EdgeTheme edgeTheme = new EdgeTheme();
        public PortTheme portTheme = new PortTheme();
        public BlackboardTheme blackboardTheme = new BlackboardTheme();

        private const string LogTag = "[ND_BehaviorTreeSetting]";
        private const string DefaultFileName = "ND_BehaviorTree_Settings.asset";
        private static ND_BehaviorTreeSetting _instance;
        private static ND_BehaviorTreeSetting _transientDefaults;

        /// <summary>
        /// Settings are located by asset TYPE anywhere in the project, so moving the package folder keeps working.
        /// A new default asset is created only when no settings asset exists at all, and never while the
        /// AssetDatabase is importing or compiling (it can report 0 assets then, which would create duplicates).
        /// </summary>
        public static ND_BehaviorTreeSetting Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var paths = AssetDatabase.FindAssets($"t:{nameof(ND_BehaviorTreeSetting)}")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToList();

                string path = ResolveSettingsPath(paths, out string warning);
                if (warning != null) Debug.LogWarning($"{LogTag} {warning}");

                if (path != null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<ND_BehaviorTreeSetting>(path);
                    if (_instance != null) return _instance;
                    Debug.LogError($"{LogTag} Settings asset at '{path}' could not be loaded.");
                }

                if (EditorApplication.isUpdating || EditorApplication.isCompiling)
                {
                    // Transient in-memory defaults; never saved, retried on the next access.
                    if (_transientDefaults == null) _transientDefaults = CreateInstance<ND_BehaviorTreeSetting>();
                    return _transientDefaults;
                }

                _instance = CreateDefaultAsset();
                return _instance;
            }
        }

        /// <summary>
        /// Picks the settings asset to use from every path found in the project.
        /// 0 → null. 1 → that path. More than one → the first by ordinal path order, with a warning listing all of them.
        /// </summary>
        public static string ResolveSettingsPath(IReadOnlyList<string> foundPaths, out string warning)
        {
            warning = null;
            if (foundPaths == null || foundPaths.Count == 0) return null;
            if (foundPaths.Count == 1) return foundPaths[0];

            var ordered = foundPaths.OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            warning = $"Found {ordered.Count} settings assets; using '{ordered[0]}'. Delete the others: {string.Join(", ", ordered.Skip(1))}";
            return ordered[0];
        }

        private static ND_BehaviorTreeSetting CreateDefaultAsset()
        {
            var settings = CreateInstance<ND_BehaviorTreeSetting>();
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(settings));
            string dir = string.IsNullOrEmpty(scriptPath) ? "Assets" : Path.GetDirectoryName(scriptPath).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{DefaultFileName}");

            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"{LogTag} No settings asset found in the project. Created defaults at '{path}'. Assign UXML/USS templates there.", settings);
            return settings;
        }
    }
}
