using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Plugins
{
    /// <summary>
    /// Finds every <see cref="DialoguePlugin"/> subclass on domain load, registers it, and exposes the contributions to
    /// the editor. A plugin that throws while registering, or from a callback, is reported with its name and does not
    /// break the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class DialoguePluginRegistry
    {
        public sealed class Entry
        {
            public DialoguePlugin Plugin { get; internal set; }
            public DialoguePluginContext Context { get; internal set; }
            public string Error { get; internal set; }
            public bool Installed => Error == null;
        }

        private static readonly List<Entry> s_Entries = new List<Entry>();

        /// <summary>Raised after a plugin is installed or uninstalled (open windows rebuild their menus/tabs).</summary>
        public static event Action Changed;

        static DialoguePluginRegistry()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<DialoguePlugin>().Where(IsAutoInstalled).OrderBy(t => t.FullName))
            {
                DialoguePlugin plugin;
                try { plugin = (DialoguePlugin)Activator.CreateInstance(type); }
                catch (Exception e)
                {
                    Debug.LogError($"[Dialogue Plugins] Cannot create '{type.FullName}' (needs a public parameterless constructor): {e.Message}");
                    continue;
                }
                Install(plugin);
            }
        }

        public static bool IsAutoInstalled(Type type) =>
            !type.IsAbstract && !type.IsGenericTypeDefinition && type.GetCustomAttributes(typeof(DialogueManualInstallAttribute), false).Length == 0;

        public static IReadOnlyList<Entry> Entries => s_Entries;

        private static IEnumerable<DialoguePluginContext> Active => s_Entries.Where(e => e.Installed).Select(e => e.Context);

        public static IEnumerable<DialoguePluginMenuItem> CanvasMenuItems => Active.SelectMany(c => c.CanvasItems);
        public static IEnumerable<DialoguePluginMenuItem> EntryMenuItems => Active.SelectMany(c => c.EntryItems);
        public static IEnumerable<DialoguePluginMenuItem> ToolbarItems => Active.SelectMany(c => c.ToolbarItems);
        public static IEnumerable<DialoguePluginPanel> DatabaseTabs => Active.SelectMany(c => c.Tabs);
        public static IEnumerable<DialoguePluginPanel> InspectorSections => Active.SelectMany(c => c.InspectorSections);
        public static IEnumerable<DialoguePluginRule> ValidationRules => Active.SelectMany(c => c.Rules);

        /// <summary>Registers a plugin (again: an installed plugin is replaced). False (logged) if its Register threw.</summary>
        public static bool Install(DialoguePlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            s_Entries.RemoveAll(e => e.Plugin.GetType() == plugin.GetType());
            var entry = new Entry { Plugin = plugin, Context = new DialoguePluginContext(plugin) };
            try
            {
                plugin.Register(entry.Context);
            }
            catch (Exception e)
            {
                entry.Error = e.Message;
                Debug.LogError($"[Dialogue Plugins] '{plugin.DisplayName}' failed to register and is disabled: {e}");
            }
            s_Entries.Add(entry);
            Changed?.Invoke();
            return entry.Installed;
        }

        public static bool Uninstall(DialoguePlugin plugin)
        {
            int removed = s_Entries.RemoveAll(e => e.Plugin == plugin);
            if (removed > 0) Changed?.Invoke();
            return removed > 0;
        }

        #region Calling plugins safely

        public static bool IsEnabled(DialoguePluginMenuItem item, DialoguePluginArgs args)
        {
            if (item.Enabled == null) return true;
            try { return item.Enabled(args); }
            catch (Exception e)
            {
                Debug.LogError($"[Dialogue Plugins] '{item.Plugin.DisplayName}' enable check for '{item.Path}' threw: {e}");
                return false;
            }
        }

        public static void Run(DialoguePluginMenuItem item, DialoguePluginArgs args)
        {
            try { item.Action(args); }
            catch (Exception e) { Debug.LogError($"[Dialogue Plugins] '{item.Plugin.DisplayName}' menu item '{item.Path}' threw: {e}"); }
        }

        /// <summary>Appends plugin items to a menu, with their enabled state.</summary>
        public static void AppendTo(DropdownMenu menu, IEnumerable<DialoguePluginMenuItem> items, Func<DialoguePluginArgs> args)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            menu.AppendSeparator();
            foreach (var item in list)
            {
                var i = item;
                menu.AppendAction(i.Path, _ => Run(i, args()), _ => IsEnabled(i, args()) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        /// <summary>Builds a plugin panel; a throwing builder yields an error box instead of breaking the window.</summary>
        public static VisualElement BuildPanel(DialoguePluginPanel panel, DialoguePluginArgs args)
        {
            try
            {
                return panel.Build(args) ?? new VisualElement();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Dialogue Plugins] '{panel.Plugin.DisplayName}' panel '{panel.Title}' threw: {e}");
                return new HelpBox($"'{panel.Plugin.DisplayName}' failed to build this panel (see Console).", HelpBoxMessageType.Error);
            }
        }

        public static void RaiseEntryCreated(DialoguePluginArgs args, DialogueEntry entry)
        {
            foreach (var context in Active)
                foreach (var handler in context.EntryCreated)
                {
                    try { handler(args, entry); }
                    catch (Exception e) { Debug.LogError($"[Dialogue Plugins] '{context.Plugin.DisplayName}' OnEntryCreated threw: {e}"); }
                }
        }

        /// <summary>The database's own validation plus every plugin rule (prefixed with the plugin name).</summary>
        public static List<string> Validate(DialogueDatabase database)
        {
            var issues = database.Validate();
            foreach (var rule in ValidationRules)
            {
                try
                {
                    foreach (var issue in rule.Check(database) ?? Enumerable.Empty<string>())
                        issues.Add($"[{rule.Plugin.DisplayName}] {issue}");
                }
                catch (Exception e)
                {
                    issues.Add($"[{rule.Plugin.DisplayName}] rule '{rule.Name}' threw: {e.Message}");
                }
            }
            return issues;
        }

        #endregion
    }
}
