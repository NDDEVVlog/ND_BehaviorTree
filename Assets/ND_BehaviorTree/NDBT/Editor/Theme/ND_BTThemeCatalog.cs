using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Theme
{
    /// <summary>
    /// Built-in themes plus every <see cref="ND_BTThemePreset"/> asset, and the one-click switch between them.
    /// Applying a theme copies it into the ND Behavior Tree settings (the values every view reads), so the look
    /// persists and stays editable in Project Settings; open graph windows redraw immediately.
    /// </summary>
    public static class ND_BTThemeCatalog
    {
        /// <summary>Raised after a theme was applied (graph windows rebuild their views).</summary>
        public static event Action ThemeChanged;

        public sealed class Entry
        {
            public string Name;
            public Func<ThemeData> Build;   // built-ins are created on demand (stylesheets looked up by name)
            public ND_BTThemePreset Asset;  // custom presets
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        private static ThemeData Make(string name, string uss, string background, string grid, EdgeTheme edge, PortTheme port, string styleKey = null) => new ThemeData
        {
            displayName = name,
            nodeStyle = ND_BTStyles.Find(uss),
            // Per-theme overrides: Attachments_<key>.uss (decorator / service cards), Blackboard_<key>.uss (Blackboard panel).
            attachmentStyle = styleKey != null ? ND_BTStyles.Find("Attachments_" + styleKey) : null,
            blackboardStyle = styleKey != null ? ND_BTStyles.Find("Blackboard_" + styleKey) : null,
            graphBackgroundColor = Hex(background),
            gridColor = Hex(grid),
            edge = edge,
            port = port,
        };

        private static readonly List<Entry> s_BuiltIns = new List<Entry>
        {
            new Entry { Name = "Default Dark", Build = () => Make("Default Dark", "NodeElementUss", "#1F1F1F", "#2E2E2E",
                new EdgeTheme { edgeColor = Hex("#B3B3B3"), edgeThickness = 3f, curve = EdgeCurve.Bezier },
                new PortTheme { inputPortColor = Hex("#66CC66"), outputPortColor = Hex("#CC991A"), portSize = 14f, shape = PortShape.Circle }) },

            new Entry { Name = "Cyberpunk", Build = () => Make("Cyberpunk", "CyberpunkTheme", "#08080F", "#16162A",
                new EdgeTheme { edgeColor = Hex("#00F0FF"), edgeThickness = 2.5f, curve = EdgeCurve.Step, labelBackgroundColor = Hex("#1A0B2ECC"), labelTextColor = Hex("#FF2BD6") },
                new PortTheme { inputPortColor = Hex("#FF2BD6"), outputPortColor = Hex("#00F0FF"), portSize = 14f, shape = PortShape.Diamond }, "Cyberpunk") },

            new Entry { Name = "Light Clear", Build = () => Make("Light Clear", "LightClearUSSTheme", "#E9ECF1", "#D3D8DF",
                new EdgeTheme { edgeColor = Hex("#5B6472"), edgeThickness = 2.5f, curve = EdgeCurve.Bezier, labelBackgroundColor = Hex("#FFFFFFE6"), labelTextColor = Hex("#1F2937") },
                new PortTheme { inputPortColor = Hex("#2563EB"), outputPortColor = Hex("#F59E0B"), portSize = 14f, shape = PortShape.Circle }, "LightClear") },

            new Entry { Name = "Magic", Build = () => Make("Magic", "MagicTheme", "#140F1F", "#251C3A",
                new EdgeTheme { edgeColor = Hex("#B48CFF"), edgeThickness = 3f, curve = EdgeCurve.Bezier, labelBackgroundColor = Hex("#2C2340E6"), labelTextColor = Hex("#FFD166") },
                new PortTheme { inputPortColor = Hex("#FFD166"), outputPortColor = Hex("#B48CFF"), portSize = 14f, shape = PortShape.Circle }, "Magic") },

            new Entry { Name = "Steampunk", Build = () => Make("Steampunk", "SteamPunKTheme", "#1E150D", "#2F2318",
                new EdgeTheme { edgeColor = Hex("#C08A3E"), edgeThickness = 3.5f, curve = EdgeCurve.Straight, labelBackgroundColor = Hex("#3E2F1CE6"), labelTextColor = Hex("#F3D9A4") },
                new PortTheme { inputPortColor = Hex("#D9A441"), outputPortColor = Hex("#9C6B35"), portSize = 15f, shape = PortShape.Square }, "Steampunk") },

            // Meow — drawn cat ports: mouth open while unplugged, "ω" once connected.
            new Entry { Name = "Meow", Build = () => Make("Meow", "NodeElementUss", "#1C1A22", "#2A2733",
                new EdgeTheme { edgeColor = Hex("#F7A8C4"), edgeThickness = 3f, curve = EdgeCurve.Bezier, drawBehindNodes = true, labelBackgroundColor = Hex("#2A2733E6"), labelTextColor = Hex("#FFE3EC") },
                new PortTheme { inputPortColor = Hex("#FFB3C7"), outputPortColor = Hex("#FFD39A"), portSize = 20f, shape = PortShape.Cat }, "Meow") },

            // Meow 2 — Pop Cat pictures: mouth open while a port waits for a connection, closed once it is fed one.
            new Entry { Name = "Meow 2", Build = () => Make("Meow 2", "NodeElementUss", "#1C1A22", "#2A2733",
                new EdgeTheme { edgeColor = Hex("#F7A8C4"), edgeThickness = 3f, curve = EdgeCurve.Bezier, drawBehindNodes = true, labelBackgroundColor = Hex("#2A2733E6"), labelTextColor = Hex("#FFE3EC") },
                new PortTheme { inputPortColor = Hex("#FFB3C7"), outputPortColor = Hex("#FFD39A"), portSize = 22f, shape = PortShape.Image,
                                unpluggedImage = FindTexture("POPCat1"), pluggedImage = FindTexture("POPCat") }, "Meow") },
        };

        /// <summary>A texture found by file name (without extension) anywhere in the project; null (logged) if missing.</summary>
        private static Texture2D FindTexture(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Debug.LogError($"[ND_BTThemeCatalog] Texture '{fileName}' was not found in the project.");
            return null;
        }

        /// <summary>Built-ins first, then custom presets (by name).</summary>
        public static List<Entry> All()
        {
            var list = new List<Entry>(s_BuiltIns);
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ND_BTThemePreset)}"))
            {
                var preset = AssetDatabase.LoadAssetAtPath<ND_BTThemePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset == null) continue;
                string name = string.IsNullOrWhiteSpace(preset.theme.displayName) ? preset.name : preset.theme.displayName;
                list.Add(new Entry { Name = name, Asset = preset });
            }
            return list;
        }

        public static string ActiveName => ND_BehaviorTreeSetting.Instance.activeThemeName;

        /// <summary>
        /// Adds a built-in theme (plugins call this from an [InitializeOnLoad] class). Registering the same name again
        /// replaces the previous entry, so domain reloads do not create duplicates.
        /// </summary>
        public static void RegisterBuiltIn(string name, Func<ThemeData> build)
        {
            s_BuiltIns.RemoveAll(e => e.Name == name);
            s_BuiltIns.Add(new Entry { Name = name, Build = build });
        }

        /// <summary>Removes a theme added with <see cref="RegisterBuiltIn"/> (used when a plugin is uninstalled). The active theme stays applied.</summary>
        public static bool UnregisterBuiltIn(string name) => s_BuiltIns.RemoveAll(e => e.Name == name) > 0;

        public static void Apply(Entry entry)
        {
            if (entry == null) return;
            var data = entry.Asset != null ? entry.Asset.theme : entry.Build();
            Apply(data, entry.Name);
        }

        public static void Apply(ThemeData data, string name)
        {
            var settings = ND_BehaviorTreeSetting.Instance;
            if (data.nodeStyle != null) settings.defaultNodeStyle = data.nodeStyle;
            else Debug.LogWarning($"[ND_BTThemeCatalog] Theme '{name}' has no node stylesheet; the current node style is kept.");
            settings.attachmentStyle = data.attachmentStyle; // null = default decorator / service look
            settings.blackboardTheme.customUSS = data.blackboardStyle; // null = default Blackboard panel look
            settings.graphBackgroundColor = data.graphBackgroundColor;
            settings.gridColor = data.gridColor;
            settings.edgeTheme = data.edge.Copy();
            settings.portTheme = data.port.Copy();
            settings.activeThemeName = name;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            ND_BTViewFactory.InvalidateCache();
            ThemeChanged?.Invoke();
        }

        /// <summary>Saves the current look (whatever the settings hold now) as a new preset asset.</summary>
        public static ND_BTThemePreset SaveCurrentAsPreset(string assetPath, string displayName)
        {
            var settings = ND_BehaviorTreeSetting.Instance;
            var preset = ScriptableObject.CreateInstance<ND_BTThemePreset>();
            preset.theme = new ThemeData
            {
                displayName = displayName,
                nodeStyle = settings.defaultNodeStyle,
                attachmentStyle = settings.attachmentStyle,
                blackboardStyle = settings.blackboardTheme.customUSS,
                graphBackgroundColor = settings.graphBackgroundColor,
                gridColor = settings.gridColor,
                edge = settings.edgeTheme.Copy(),
                port = settings.portTheme.Copy(),
            };
            AssetDatabase.CreateAsset(preset, assetPath);
            AssetDatabase.SaveAssets();
            settings.activeThemeName = displayName;
            EditorUtility.SetDirty(settings);
            return preset;
        }

        /// <summary>Theme menu shared by the graph toolbar and the settings page.</summary>
        public static void FillMenu(GenericMenu menu)
        {
            string active = ActiveName;
            foreach (var entry in All())
            {
                var captured = entry;
                string label = entry.Asset != null ? $"Custom/{entry.Name}" : entry.Name;
                menu.AddItem(new GUIContent(label), entry.Name == active, () => Apply(captured));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Save Current as Preset..."), false, () =>
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Theme Preset", "My Theme", "asset", "Save the current graph look as a theme preset.");
                if (string.IsNullOrEmpty(path)) return;
                SaveCurrentAsPreset(path, System.IO.Path.GetFileNameWithoutExtension(path));
            });
            menu.AddItem(new GUIContent("Edit in Project Settings..."), false, () => SettingsService.OpenProjectSettings("Project/ND Behavior Tree"));
        }
    }
}
