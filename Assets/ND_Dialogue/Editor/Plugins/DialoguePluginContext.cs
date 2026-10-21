using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Plugins
{
    /// <summary>
    /// Handed to <see cref="DialoguePlugin.Register"/>. Every contribution is recorded against the plugin, so
    /// <see cref="DialoguePluginRegistry.Uninstall"/> removes exactly what the plugin added.
    /// </summary>
    public sealed class DialoguePluginContext
    {
        internal readonly List<DialoguePluginMenuItem> CanvasItems = new List<DialoguePluginMenuItem>();
        internal readonly List<DialoguePluginMenuItem> EntryItems = new List<DialoguePluginMenuItem>();
        internal readonly List<DialoguePluginMenuItem> ToolbarItems = new List<DialoguePluginMenuItem>();
        internal readonly List<DialoguePluginPanel> Tabs = new List<DialoguePluginPanel>();
        internal readonly List<DialoguePluginPanel> InspectorSections = new List<DialoguePluginPanel>();
        internal readonly List<DialoguePluginRule> Rules = new List<DialoguePluginRule>();
        internal readonly List<Action<DialoguePluginArgs, DialogueEntry>> EntryCreated = new List<Action<DialoguePluginArgs, DialogueEntry>>();

        public DialoguePlugin Plugin { get; }

        internal DialoguePluginContext(DialoguePlugin plugin) { Plugin = plugin; }

        /// <summary>Adds an item to the canvas background right-click menu ("/" makes submenus).</summary>
        public void AddCanvasMenuItem(string path, Action<DialoguePluginArgs> action, Func<DialoguePluginArgs, bool> enabled = null) =>
            CanvasItems.Add(new DialoguePluginMenuItem(Plugin, path, action, enabled));

        /// <summary>Adds an item to an entry's right-click menu (<see cref="DialoguePluginArgs.Entry"/> is the entry).</summary>
        public void AddEntryMenuItem(string path, Action<DialoguePluginArgs> action, Func<DialoguePluginArgs, bool> enabled = null) =>
            EntryItems.Add(new DialoguePluginMenuItem(Plugin, path, action, enabled));

        /// <summary>Adds an item to the window's "Plugins" toolbar menu (shown only when some plugin adds one).</summary>
        public void AddToolbarItem(string path, Action<DialoguePluginArgs> action, Func<DialoguePluginArgs, bool> enabled = null) =>
            ToolbarItems.Add(new DialoguePluginMenuItem(Plugin, path, action, enabled));

        /// <summary>
        /// Adds a tab to the database sidebar (next to Characters, Variables…). <paramref name="build"/> is called each
        /// time the tab is shown or the database changes; return the tab's content.
        /// </summary>
        public void AddDatabaseTab(string title, Func<DialoguePluginArgs, VisualElement> build) =>
            Tabs.Add(new DialoguePluginPanel(Plugin, title, build));

        /// <summary>Adds a section at the bottom of the entry inspector (<see cref="DialoguePluginArgs.Entry"/> is the entry).</summary>
        public void AddInspectorSection(string title, Func<DialoguePluginArgs, VisualElement> build) =>
            InspectorSections.Add(new DialoguePluginPanel(Plugin, title, build));

        /// <summary>Adds a check to the editor's Validate (read-only: never modify the database in a rule).</summary>
        public void AddValidationRule(string name, Func<DialogueDatabase, IEnumerable<string>> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            Rules.Add(new DialoguePluginRule(Plugin, name, rule));
        }

        /// <summary>Runs after the user creates an entry in the editor, inside the same undo step (set defaults, add actions…).</summary>
        public void OnEntryCreated(Action<DialoguePluginArgs, DialogueEntry> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EntryCreated.Add(handler);
        }
    }

    public sealed class DialoguePluginMenuItem
    {
        public readonly DialoguePlugin Plugin;
        public readonly string Path;
        public readonly Action<DialoguePluginArgs> Action;
        public readonly Func<DialoguePluginArgs, bool> Enabled;

        internal DialoguePluginMenuItem(DialoguePlugin plugin, string path, Action<DialoguePluginArgs> action, Func<DialoguePluginArgs, bool> enabled)
        {
            if (string.IsNullOrWhiteSpace(path) || action == null) throw new ArgumentException($"[{plugin.DisplayName}] A menu item needs a path and an action.");
            Plugin = plugin; Path = path; Action = action; Enabled = enabled;
        }
    }

    public sealed class DialoguePluginPanel
    {
        public readonly DialoguePlugin Plugin;
        public readonly string Title;
        public readonly Func<DialoguePluginArgs, VisualElement> Build;

        internal DialoguePluginPanel(DialoguePlugin plugin, string title, Func<DialoguePluginArgs, VisualElement> build)
        {
            if (string.IsNullOrWhiteSpace(title) || build == null) throw new ArgumentException($"[{plugin.DisplayName}] A panel needs a title and a builder.");
            Plugin = plugin; Title = title; Build = build;
        }
    }

    public sealed class DialoguePluginRule
    {
        public readonly DialoguePlugin Plugin;
        public readonly string Name;
        public readonly Func<DialogueDatabase, IEnumerable<string>> Check;

        internal DialoguePluginRule(DialoguePlugin plugin, string name, Func<DialogueDatabase, IEnumerable<string>> check)
        {
            Plugin = plugin; Name = string.IsNullOrWhiteSpace(name) ? check.Method.Name : name; Check = check;
        }
    }
}
