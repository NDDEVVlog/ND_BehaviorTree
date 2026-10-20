using System;
using System.Collections.Generic;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Editor.Theme;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// Handed to <see cref="ND_BTPlugin.Register"/>. Every contribution is recorded against the plugin, so
    /// <see cref="ND_BTPluginRegistry.Uninstall"/> removes exactly what the plugin added.
    /// </summary>
    public sealed class ND_BTPluginContext
    {
        internal readonly List<string> Themes = new List<string>();
        internal readonly List<Action> Unhooks = new List<Action>();
        internal readonly List<ND_BTPluginMenuItem> CanvasItems = new List<ND_BTPluginMenuItem>();
        internal readonly List<ND_BTPluginMenuItem> NodeItems = new List<ND_BTPluginMenuItem>();
        internal readonly List<ND_BTPluginMenuItem> ToolbarItems = new List<ND_BTPluginMenuItem>();
        internal readonly List<ND_BTPluginRule> Rules = new List<ND_BTPluginRule>();
        internal readonly List<ND_BTLiveSectionEntry> LiveSections = new List<ND_BTLiveSectionEntry>();
        internal readonly List<Action<ND_BTLiveContext>> LiveUpdates = new List<Action<ND_BTLiveContext>>();
        internal readonly List<Action> LiveStops = new List<Action>();

        public ND_BTPlugin Plugin { get; }

        internal ND_BTPluginContext(ND_BTPlugin plugin) { Plugin = plugin; }

        /// <summary>Adds a theme to the toolbar theme switcher (built on demand, so stylesheets are looked up when applied).</summary>
        public void AddTheme(string name, Func<ThemeData> build)
        {
            if (string.IsNullOrWhiteSpace(name) || build == null) throw new ArgumentException($"[{Plugin.DisplayName}] AddTheme needs a name and a builder.");
            ND_BTThemeCatalog.RegisterBuiltIn(name, build);
            Themes.Add(name);
        }

        /// <summary>
        /// Runs after the user creates a <typeparamref name="T"/> in the graph window, inside the same undo step
        /// (so graph changes made here undo together with the creation).
        /// </summary>
        public void OnNodeCreated<T>(Action<BehaviorTreeGraphService, T> handler) where T : Node
        {
            Action<BehaviorTreeGraphService, Node> wrapper = (graph, node) => { if (node is T typed) handler(graph, typed); };
            GraphEditHooks.NodeCreatedByUser += wrapper;
            Unhooks.Add(() => GraphEditHooks.NodeCreatedByUser -= wrapper);
        }

        /// <summary>Runs after a <typeparamref name="T"/>'s properties were edited in Node Properties, inside the same undo step.</summary>
        public void OnNodeEdited<T>(Action<BehaviorTreeGraphService, T> handler) where T : Node
        {
            Action<BehaviorTreeGraphService, Node> wrapper = (graph, node) => { if (node is T typed) handler(graph, typed); };
            GraphEditHooks.NodePropertiesEdited += wrapper;
            Unhooks.Add(() => GraphEditHooks.NodePropertiesEdited -= wrapper);
        }

        /// <summary>Adds an item to the canvas right-click menu ("/" makes submenus). Graph changes should go through <see cref="ND_BTPluginArgs.Graph"/>.</summary>
        public void AddCanvasMenuItem(string path, Action<ND_BTPluginArgs> action, Func<ND_BTPluginArgs, bool> enabled = null) =>
            CanvasItems.Add(new ND_BTPluginMenuItem(Plugin, path, action, enabled, typeof(Node)));

        /// <summary>Adds an item to the right-click menu of nodes of type <typeparamref name="T"/> (and subclasses).</summary>
        public void AddNodeMenuItem<T>(string path, Action<ND_BTPluginArgs> action, Func<ND_BTPluginArgs, bool> enabled = null) where T : Node =>
            NodeItems.Add(new ND_BTPluginMenuItem(Plugin, path, action, enabled, typeof(T)));

        /// <summary>Adds an item to the graph window's "Plugins" toolbar menu (shown only when some plugin adds one).</summary>
        public void AddToolbarItem(string path, Action<ND_BTPluginArgs> action, Func<ND_BTPluginArgs, bool> enabled = null) =>
            ToolbarItems.Add(new ND_BTPluginMenuItem(Plugin, path, action, enabled, typeof(Node)));

        /// <summary>
        /// Adds a check to <see cref="GraphValidator.Validate"/> (graph window warnings, bridge "validate", tests).
        /// Read-only: never modify the tree in a rule. Use a code prefix of your own, e.g. "story.case-empty".
        /// </summary>
        public void AddValidationRule(string name, Func<BehaviorTree, IEnumerable<GraphIssue>> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            Rules.Add(new ND_BTPluginRule(Plugin, name, rule));
        }

        /// <summary>
        /// Adds a foldout section to the Live Debug panel. <paramref name="create"/> is called once per panel;
        /// the section then gets <see cref="ND_BTLiveSection.OnLiveUpdate"/> while an agent is watched.
        /// </summary>
        public void AddLiveDebugSection(string title, Func<ND_BTLiveSection> create)
        {
            if (string.IsNullOrWhiteSpace(title) || create == null) throw new ArgumentException($"[{Plugin.DisplayName}] AddLiveDebugSection needs a title and a factory.");
            LiveSections.Add(new ND_BTLiveSectionEntry(Plugin, title, create));
        }

        /// <summary>Runs ~30 times per second while an agent is watched (keep it cheap: no allocations per call).</summary>
        public void OnLiveUpdate(Action<ND_BTLiveContext> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            LiveUpdates.Add(handler);
        }

        /// <summary>Runs when Live Debug stops (Play mode ended, agent cleared).</summary>
        public void OnLiveStopped(Action handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            LiveStops.Add(handler);
        }
    }

    public sealed class ND_BTLiveSectionEntry
    {
        public readonly ND_BTPlugin Plugin;
        public readonly string Title;
        public readonly Func<ND_BTLiveSection> Create;

        internal ND_BTLiveSectionEntry(ND_BTPlugin plugin, string title, Func<ND_BTLiveSection> create)
        {
            Plugin = plugin; Title = title; Create = create;
        }
    }

    public sealed class ND_BTPluginMenuItem
    {
        public readonly ND_BTPlugin Plugin;
        public readonly string Path;
        public readonly Action<ND_BTPluginArgs> Action;
        public readonly Func<ND_BTPluginArgs, bool> Enabled;
        public readonly Type NodeType;

        internal ND_BTPluginMenuItem(ND_BTPlugin plugin, string path, Action<ND_BTPluginArgs> action, Func<ND_BTPluginArgs, bool> enabled, Type nodeType)
        {
            if (string.IsNullOrWhiteSpace(path) || action == null) throw new ArgumentException($"[{plugin.DisplayName}] A menu item needs a path and an action.");
            Plugin = plugin; Path = path; Action = action; Enabled = enabled; NodeType = nodeType;
        }
    }

    public sealed class ND_BTPluginRule
    {
        public readonly ND_BTPlugin Plugin;
        public readonly string Name;
        public readonly Func<BehaviorTree, IEnumerable<GraphIssue>> Check;

        internal ND_BTPluginRule(ND_BTPlugin plugin, string name, Func<BehaviorTree, IEnumerable<GraphIssue>> check)
        {
            Plugin = plugin; Name = string.IsNullOrWhiteSpace(name) ? check.Method.Name : name; Check = check;
        }
    }
}
