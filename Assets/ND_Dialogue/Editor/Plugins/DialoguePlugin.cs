using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Editor.Plugins
{
    /// <summary>
    /// The one entry point of a dialogue editor plugin. Subclass it once in the plugin's Editor assembly (no attribute,
    /// no registration): <see cref="DialoguePluginRegistry"/> finds every subclass on domain load and calls
    /// <see cref="Register"/>, where the plugin declares what it adds through the context (menus, toolbar items,
    /// sidebar tabs, inspector sections, validation rules, reactions to new entries).
    /// Things that need no registration: new conditions/actions (any concrete <see cref="DialogueCondition"/> /
    /// <see cref="DialogueAction"/> subclass appears in the inspector's add menus), new schema sources
    /// (<see cref="DialogueSchemaSource"/> subclasses) and test assemblies named "ND.*.Tests".
    /// </summary>
    public abstract class DialoguePlugin
    {
        public virtual string DisplayName => GetType().Name;
        public virtual string Version => "1.0.0";
        public virtual string Description => string.Empty;

        /// <summary>Called once per domain load. Declare contributions on <paramref name="context"/>; an exception unregisters the plugin (logged).</summary>
        public abstract void Register(DialoguePluginContext context);
    }

    /// <summary>Put on a <see cref="DialoguePlugin"/> that is installed only through <see cref="DialoguePluginRegistry.Install"/> (tests): the domain-load scan skips it.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DialogueManualInstallAttribute : Attribute { }

    /// <summary>What a plugin callback receives: where the user is in the editor.</summary>
    public sealed class DialoguePluginArgs
    {
        public DialogueEditorWindow Window { get; set; }
        public DialogueGraphCanvas Canvas { get; set; }
        public DialogueDatabase Database { get; set; }
        /// <summary>The conversation shown on the canvas (may be null).</summary>
        public Conversation Conversation { get; set; }
        /// <summary>The right-clicked / inspected entry (entry menu items and inspector sections), else null.</summary>
        public DialogueEntry Entry { get; set; }
        /// <summary>Canvas-content position of the click (canvas menu items).</summary>
        public Vector2 Position { get; set; }
        public IReadOnlyList<DialogueEntry> Selection { get; set; } = Array.Empty<DialogueEntry>();
        /// <summary>The preview locale code of the window.</summary>
        public string Locale { get; set; }

        /// <summary>
        /// Changes the database as one undo step and redraws the editor. Do every data change of a plugin through this
        /// (or record Undo yourself and call <see cref="DialogueEditorWindow.NotifyDatabaseChanged"/>).
        /// </summary>
        public void Edit(string undoLabel, Action change)
        {
            if (Database == null || change == null) return;
            Undo.RecordObject(Database, undoLabel);
            change();
            EditorUtility.SetDirty(Database);
            if (Window != null) Window.NotifyDatabaseChanged();
            else Canvas?.Reload();
        }
    }
}
