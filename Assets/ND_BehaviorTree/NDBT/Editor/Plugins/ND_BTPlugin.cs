using System.Collections.Generic;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Graph;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// The one entry point of a plugin. Subclass it once in the plugin's Editor assembly (no attribute, no
    /// registration): <see cref="ND_BTPluginRegistry"/> finds every subclass on domain load and calls
    /// <see cref="Register"/>, where the plugin declares what it adds through the context (themes, menu items,
    /// graph-edit reactions, validation rules).
    /// Things that need no registration at all: node types (any concrete <see cref="Node"/> subclass with
    /// [NodeInfo]), node views ([CustomNodeView] on an <see cref="ND_BTNodeView"/> subclass), stylesheets
    /// (<see cref="ND_BTStyles.Find"/> by file name) and test assemblies named "ND_BehaviorTree*.Tests".
    /// </summary>
    public abstract class ND_BTPlugin
    {
        public virtual string DisplayName => GetType().Name;
        public virtual string Version => "1.0.0";
        public virtual string Description => string.Empty;

        /// <summary>Called once per domain load. Declare contributions on <paramref name="context"/>; an exception unregisters the plugin (logged).</summary>
        public abstract void Register(ND_BTPluginContext context);
    }

    /// <summary>What a plugin menu item / toolbar item / enable check receives.</summary>
    public sealed class ND_BTPluginArgs
    {
        /// <summary>The graph canvas (null for toolbar items when no tree is open).</summary>
        public ND_BTGraphCanvas Canvas { get; set; }
        public BehaviorTree Tree { get; set; }
        /// <summary>Mutation API for <see cref="Tree"/>: every change made through it is one undoable graph step.</summary>
        public BehaviorTreeGraphService Graph { get; set; }
        /// <summary>The node that was right-clicked (node menu items only).</summary>
        public Node Node { get; set; }
        /// <summary>Canvas-content position of the click (canvas menu items only), e.g. to place a new node.</summary>
        public Vector2 Position { get; set; }
        /// <summary>Selected nodes at the time of the click.</summary>
        public IReadOnlyList<Node> Selection { get; set; } = new Node[0];
        /// <summary>During Live Debug: the runtime counterpart of <see cref="Node"/> in the watched agent's tree (else null).</summary>
        public Node RuntimeNode { get; set; }
        /// <summary>During Live Debug: the watched agent (else null).</summary>
        public BehaviorTreeRunner Agent { get; set; }
    }
}
