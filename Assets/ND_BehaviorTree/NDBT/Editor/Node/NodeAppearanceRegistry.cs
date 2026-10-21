using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Plugins;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    public enum NodeAppearanceSource { Plugin, Config, Project }

    /// <summary>A pack the graph knows about, where it came from, and whether it is used.</summary>
    public sealed class NodeAppearancePackInfo
    {
        public NodeAppearancePack Pack;
        public NodeAppearanceSource Source;
        /// <summary>The plugin that registered the pack (Source == Plugin), else null.</summary>
        public string PluginName;
        /// <summary>Switched on in <see cref="NodeEditorConfig"/> (on unless listed there as off).</summary>
        public bool Enabled;
        /// <summary>Why the pack is not used although enabled (its owner plugin is not installed), else null.</summary>
        public string InactiveReason;
        public bool Active => Enabled && InactiveReason == null;
    }

    /// <summary>
    /// Collects the <see cref="NodeAppearancePack"/>s (plugin-registered, loaded in the active
    /// <see cref="NodeEditorConfig"/>, found in the project) and picks the mapping for a node type.
    /// Cached; <see cref="ND_BTViewFactory.InvalidateCache"/> resets it (asset changes and plugin installs do so).
    /// </summary>
    [InitializeOnLoad]
    public static class NodeAppearanceRegistry
    {
        private const string LogTag = "[NodeAppearanceRegistry]";
        private static List<NodeAppearancePackInfo> s_Packs;
        private static readonly HashSet<string> s_ReportedTies = new HashSet<string>();

        static NodeAppearanceRegistry()
        {
            // Lives as long as the domain, like the registry it listens to.
            ND_BTPluginRegistry.Changed += ND_BTViewFactory.InvalidateCache;
        }

        internal static void Invalidate() => s_Packs = null;

        /// <summary>Every known pack (used or not), for the Node Appearance settings page.</summary>
        public static IReadOnlyList<NodeAppearancePackInfo> Packs => s_Packs ??= Collect(ND_BehaviorTreeSetting.Instance.activeConfig);

        /// <summary>
        /// Builds the pack list for <paramref name="config"/>. A null config means: every pack in the project, all on.
        /// Order: plugin packs, config packs, project packs (a pack appears once, under its first source).
        /// </summary>
        public static List<NodeAppearancePackInfo> Collect(NodeEditorConfig config)
        {
            var result = new List<NodeAppearancePackInfo>();
            var seen = new HashSet<NodeAppearancePack>();

            void Add(NodeAppearancePack pack, NodeAppearanceSource source, string pluginName)
            {
                if (pack == null || !seen.Add(pack)) return;
                result.Add(new NodeAppearancePackInfo
                {
                    Pack = pack,
                    Source = source,
                    PluginName = pluginName,
                    Enabled = config == null || config.IsEnabled(pack),
                    InactiveReason = OwnerMissing(pack),
                });
            }

            foreach (var (plugin, pack) in ND_BTPluginRegistry.AppearancePacks) Add(pack, NodeAppearanceSource.Plugin, plugin.DisplayName);
            if (config != null)
                foreach (var entry in config.packs) Add(entry.pack, NodeAppearanceSource.Config, null);
            if (config == null || config.autoDiscoverPacks)
                foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(NodeAppearancePack)}"))
                    Add(AssetDatabase.LoadAssetAtPath<NodeAppearancePack>(AssetDatabase.GUIDToAssetPath(guid)), NodeAppearanceSource.Project, null);
            return result;
        }

        private static string OwnerMissing(NodeAppearancePack pack)
        {
            if (pack.ownerPlugin == null) return null;
            var ownerType = pack.ownerPlugin.GetClass();
            if (ownerType == null || !typeof(ND_BTPlugin).IsAssignableFrom(ownerType))
                return $"owner '{pack.ownerPlugin.name}' is not an {nameof(ND_BTPlugin)} script";
            if (ND_BTPluginRegistry.Entries.Any(e => e.Installed && e.Plugin.GetType() == ownerType)) return null;
            return $"plugin '{ownerType.Name}' is not installed";
        }

        public static bool TryResolve(Type nodeType, out NodeAppearancePack pack, out NodeEditorMapping mapping) =>
            TryResolve(nodeType, Packs, out pack, out mapping);

        /// <summary>
        /// The mapping for <paramref name="nodeType"/> among the active <paramref name="packs"/>: the most specific
        /// node type (walking up base classes) wins, then the higher pack priority. Equal priority: the first pack by
        /// asset path wins, with a warning (once per type). Within a pack, its first matching mapping counts.
        /// </summary>
        public static bool TryResolve(Type nodeType, IReadOnlyList<NodeAppearancePackInfo> packs, out NodeAppearancePack pack, out NodeEditorMapping mapping)
        {
            var ordered = packs.Where(p => p.Active).Select(p => p.Pack).OrderBy(SortKey, StringComparer.Ordinal).ToList();
            for (Type current = nodeType; current != null; current = current.BaseType)
            {
                pack = null;
                mapping = null;
                NodeAppearancePack tiedWith = null;
                foreach (var candidate in ordered)
                {
                    var match = candidate.mappings?.FirstOrDefault(m => Matches(m, current));
                    if (match == null) continue;
                    if (pack == null || candidate.priority > pack.priority) { pack = candidate; mapping = match; tiedWith = null; }
                    else if (candidate.priority == pack.priority && tiedWith == null) tiedWith = candidate;
                }
                if (pack == null) continue;

                if (tiedWith != null && s_ReportedTies.Add($"{current.FullName}|{pack.GetInstanceID()}|{tiedWith.GetInstanceID()}"))
                    Debug.LogWarning($"{LogTag} Packs '{pack.name}' and '{tiedWith.name}' both map {current.Name} with priority {pack.priority}; using '{pack.name}' (first by asset path). Give one a higher priority.", tiedWith);
                return true;
            }
            pack = null;
            mapping = null;
            return false;
        }

        private static string SortKey(NodeAppearancePack pack)
        {
            string path = AssetDatabase.GetAssetPath(pack);
            return string.IsNullOrEmpty(path) ? pack.name : path;
        }

        /// <summary>Matches by exact Type (from the MonoScript), else by the stored type name (full, or the legacy short name).</summary>
        private static bool Matches(NodeEditorMapping mapping, Type type)
        {
            if (mapping == null) return false;
            if (mapping.nodeScript != null) return mapping.nodeScript.GetClass() == type;
            return !string.IsNullOrEmpty(mapping.nodeTypeFullName)
                && (mapping.nodeTypeFullName == type.FullName || mapping.nodeTypeFullName == type.Name);
        }
    }

    /// <summary>Packs, stylesheets and UXML can be added, edited or deleted at any time: drop the cached looks.</summary>
    internal sealed class NodeAppearanceAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Any(IsAppearanceAsset)) ND_BTViewFactory.InvalidateCache();
        }

        private static bool IsAppearanceAsset(string path) =>
            path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase);
    }
}
