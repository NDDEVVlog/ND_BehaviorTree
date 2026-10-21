using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Editor.Theme;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// Finds every <see cref="ND_BTPlugin"/> subclass on domain load, registers it, and exposes the contributions
    /// to the core sites that use them (canvas / node / toolbar menus, <see cref="GraphValidator"/>, the Plugins window).
    /// Synchronous: everything is registered before any editor window is created after a reload.
    /// </summary>
    [InitializeOnLoad]
    public static class ND_BTPluginRegistry
    {
        private const string LogTag = "[ND_BTPluginRegistry]";

        public sealed class Entry
        {
            public ND_BTPlugin Plugin;
            public ND_BTPluginContext Context;
            /// <summary>Why <see cref="ND_BTPlugin.Register"/> failed (the plugin then contributes nothing), else null.</summary>
            public string Error;
            public bool Installed => Error == null;
        }

        private static readonly List<Entry> s_Entries = new List<Entry>();

        /// <summary>Raised after a plugin was installed or uninstalled.</summary>
        public static event Action Changed;

        static ND_BTPluginRegistry()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<ND_BTPlugin>().Where(IsAutoInstalled).OrderBy(t => t.FullName))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"{LogTag} Plugin '{type.FullName}' needs a public parameterless constructor; it was not loaded.");
                    continue;
                }
                Install((ND_BTPlugin)Activator.CreateInstance(type));
            }
        }

        /// <summary>True for plugin types the domain-load scan installs: concrete, not generic, not [ND_BTManualInstall].</summary>
        public static bool IsAutoInstalled(Type type) =>
            !type.IsAbstract && !type.IsGenericTypeDefinition && !type.IsDefined(typeof(ND_BTManualInstallAttribute), false);

        public static IReadOnlyList<Entry> Entries => s_Entries;

        private static IEnumerable<ND_BTPluginContext> Active => s_Entries.Where(e => e.Installed).Select(e => e.Context);

        public static IEnumerable<ND_BTPluginMenuItem> CanvasMenuItems => Active.SelectMany(c => c.CanvasItems);
        public static IEnumerable<ND_BTPluginMenuItem> ToolbarItems => Active.SelectMany(c => c.ToolbarItems);
        public static IEnumerable<ND_BTPluginRule> ValidationRules => Active.SelectMany(c => c.Rules);
        public static IEnumerable<ND_BTLiveSectionEntry> LiveSectionEntries => Active.SelectMany(c => c.LiveSections);
        public static IEnumerable<ND_BTPluginMenuItem> NodeMenuItems(Node node) =>
            node == null ? Enumerable.Empty<ND_BTPluginMenuItem>() : Active.SelectMany(c => c.NodeItems).Where(i => i.NodeType.IsInstanceOfType(node));
        public static IEnumerable<(ND_BTPlugin Plugin, NodeAppearancePack Pack)> AppearancePacks =>
            Active.SelectMany(c => c.AppearancePacks.Select(p => (c.Plugin, p)));
        public static IEnumerable<ND_BTPluginMenuItem> NodePropertyButtons(Node node) =>
            node == null ? Enumerable.Empty<ND_BTPluginMenuItem>() : Active.SelectMany(c => c.PropertyButtons).Where(i => i.NodeType.IsInstanceOfType(node));

        /// <summary>
        /// Registers a plugin (done automatically for every subclass; call it yourself only for plugins created at
        /// runtime, e.g. in tests). Returns false if it is already installed or its Register threw (logged; partial
        /// contributions are removed).
        /// </summary>
        public static bool Install(ND_BTPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            if (s_Entries.Any(e => e.Plugin.GetType() == plugin.GetType()))
            {
                Debug.LogWarning($"{LogTag} {nameof(Install)} rejected: a '{plugin.GetType().FullName}' plugin is already registered.");
                return false;
            }

            var entry = new Entry { Plugin = plugin, Context = new ND_BTPluginContext(plugin) };
            s_Entries.Add(entry);
            try
            {
                plugin.Register(entry.Context);
            }
            catch (Exception e)
            {
                Remove(entry.Context);
                entry.Error = e.Message;
                Debug.LogError($"{LogTag} Plugin '{plugin.DisplayName}' ({plugin.GetType().FullName}) failed to register and was disabled: {e}");
                Changed?.Invoke();
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a plugin and everything it registered. Returns false if it was not registered.</summary>
        public static bool Uninstall(ND_BTPlugin plugin)
        {
            var entry = s_Entries.FirstOrDefault(e => e.Plugin == plugin);
            if (entry == null)
            {
                Debug.LogWarning($"{LogTag} {nameof(Uninstall)} rejected: plugin '{plugin?.DisplayName}' is not registered.");
                return false;
            }
            Remove(entry.Context);
            s_Entries.Remove(entry);
            Changed?.Invoke();
            return true;
        }

        private static void Remove(ND_BTPluginContext context)
        {
            foreach (var unhook in context.Unhooks) unhook();
            foreach (var theme in context.Themes) ND_BTThemeCatalog.UnregisterBuiltIn(theme);
            context.Unhooks.Clear();
            context.Themes.Clear();
            context.CanvasItems.Clear();
            context.NodeItems.Clear();
            context.ToolbarItems.Clear();
            context.PropertyButtons.Clear();
            context.AppearancePacks.Clear();
            context.Rules.Clear();
            context.LiveSections.Clear();
            context.LiveUpdates.Clear();
            context.LiveStops.Clear();
        }

        // ---------- used by core sites ----------

        /// <summary>Runs every plugin rule; a rule that throws becomes an Error issue (code "plugin.rule-failed").</summary>
        public static IEnumerable<GraphIssue> RunValidationRules(BehaviorTree tree)
        {
            var issues = new List<GraphIssue>();
            foreach (var rule in ValidationRules.ToList())
            {
                try
                {
                    var found = rule.Check(tree);
                    if (found != null) issues.AddRange(found);
                }
                catch (Exception e)
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, GraphValidator.PluginRuleFailed,
                        $"Validation rule '{rule.Name}' of plugin '{rule.Plugin.DisplayName}' threw: {e.Message}"));
                }
            }
            return issues;
        }

        /// <summary>Builds the args for a canvas-hosted item.</summary>
        public static ND_BTPluginArgs ArgsFor(ND_BTGraphCanvas canvas, Node node = null, Vector2 position = default) => new ND_BTPluginArgs
        {
            RuntimeNode = canvas != null && node != null && canvas.IsLiveDebugging ? canvas.LookupRuntimeNode(node.id) : null,
            Agent = canvas != null && canvas.IsLiveDebugging ? canvas.WatchedAgent : null,
            Canvas = canvas,
            Tree = canvas?.TreeData,
            Graph = canvas?.Graph,
            Node = node,
            Position = position,
            Selection = canvas != null ? canvas.SelectedNodes.Select(v => v.NodeData).ToList() : new List<Node>(),
        };

        /// <summary>Appends <paramref name="items"/> (with a leading separator) to a UI Toolkit menu.</summary>
        public static void AppendTo(DropdownMenu menu, IEnumerable<ND_BTPluginMenuItem> items, Func<ND_BTPluginArgs> args)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            menu.AppendSeparator();
            foreach (var item in list)
            {
                var captured = item;
                menu.AppendAction(captured.Path, _ => Run(captured, args()), _ => IsEnabled(captured, args()) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        public static bool IsEnabled(ND_BTPluginMenuItem item, ND_BTPluginArgs args)
        {
            if (item.Enabled == null) return true;
            try { return item.Enabled(args); }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Enable check of '{item.Path}' (plugin '{item.Plugin.DisplayName}') threw: {e}");
                return false;
            }
        }

        // ---------- Live Debug ----------

        /// <summary>A live hook slower than this (ms) is reported once: it runs ~30 times per second.</summary>
        public const double SlowLiveHookMs = 4.0;
        private static readonly HashSet<string> s_LiveReported = new HashSet<string>();
        private static readonly System.Diagnostics.Stopwatch s_LiveWatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Runs every plugin's OnLiveUpdate hook. Errors and slow hooks are reported once each. No allocations per
        /// call (no closures, messages built only when reporting): this runs ~30 times per second.
        /// </summary>
        public static void RunLiveUpdate(ND_BTLiveContext live)
        {
            for (int e = 0; e < s_Entries.Count; e++)
            {
                var entry = s_Entries[e];
                if (!entry.Installed) continue;
                var hooks = entry.Context.LiveUpdates;
                for (int i = 0; i < hooks.Count; i++)
                {
                    s_LiveWatch.Restart();
                    try { hooks[i](live); }
                    catch (Exception ex) { ReportLiveError(entry.Plugin, "OnLiveUpdate", ex); continue; }
                    CheckSlow(entry.Plugin, "OnLiveUpdate");
                }
            }
        }

        public static void RunLiveStopped()
        {
            for (int e = 0; e < s_Entries.Count; e++)
            {
                var entry = s_Entries[e];
                if (!entry.Installed) continue;
                foreach (var hook in entry.Context.LiveStops)
                {
                    try { hook(); }
                    catch (Exception ex) { ReportLiveError(entry.Plugin, "OnLiveStopped", ex); }
                }
            }
        }

        /// <summary>Updates a plugin section of the Live Debug panel under the same error / time guard.</summary>
        public static void RunSectionUpdate(ND_BTLiveSectionEntry entry, ND_BTLiveSection section, ND_BTLiveContext live)
        {
            s_LiveWatch.Restart();
            try { section.OnLiveUpdate(live); }
            catch (Exception ex) { ReportLiveError(entry.Plugin, "section '" + entry.Title + "'", ex); return; }
            CheckSlow(entry.Plugin, entry.Title);
        }

        public static void RunSectionStopped(ND_BTLiveSectionEntry entry, ND_BTLiveSection section)
        {
            try { section.OnLiveStopped(); }
            catch (Exception ex) { ReportLiveError(entry.Plugin, "section '" + entry.Title + "'", ex); }
        }

        /// <summary>Creates a section; null (logged) if the plugin's factory throws.</summary>
        public static ND_BTLiveSection CreateSection(ND_BTLiveSectionEntry entry)
        {
            try { return entry.Create(); }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Live Debug section '{entry.Title}' of plugin '{entry.Plugin.DisplayName}' could not be created: {e}");
                return null;
            }
        }

        private static void ReportLiveError(ND_BTPlugin plugin, string what, Exception e)
        {
            if (s_LiveReported.Add(plugin.GetType().FullName + "|" + what + "|error"))
                Debug.LogError($"{LogTag} Live Debug {what} of plugin '{plugin.DisplayName}' threw (reported once): {e}");
        }

        private static void CheckSlow(ND_BTPlugin plugin, string what)
        {
            double ms = s_LiveWatch.Elapsed.TotalMilliseconds;
            if (ms <= SlowLiveHookMs) return;
            if (s_LiveReported.Add(plugin.GetType().FullName + "|" + what + "|slow"))
                Debug.LogWarning($"{LogTag} Live Debug {what} of plugin '{plugin.DisplayName}' took {ms:0.0} ms (budget {SlowLiveHookMs} ms; it runs ~30 times per second). Reported once.");
        }

        /// <summary>Runs a menu item; if it changed the graph (new history step), the canvas saves and redraws.</summary>
        public static void Run(ND_BTPluginMenuItem item, ND_BTPluginArgs args)
        {
            int version = args.Graph?.History.Version ?? 0;
            try { item.Action(args); }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Menu item '{item.Path}' (plugin '{item.Plugin.DisplayName}') threw: {e}");
            }
            if (args.Canvas != null && args.Graph != null && args.Graph.History.Version != version)
            {
                args.Canvas.RequestSave();
                args.Canvas.Rebuild();
            }
        }
    }
}
