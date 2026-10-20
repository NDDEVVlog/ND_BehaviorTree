using System;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.Editor.Plugins
{
    /// <summary>
    /// What a plugin sees during Live Debug (Play mode, an agent watched in the graph window).
    /// Handed to <see cref="ND_BTLiveSection.OnLiveUpdate"/> and <see cref="ND_BTPluginContext.OnLiveUpdate"/> ~30 times per second.
    /// </summary>
    public sealed class ND_BTLiveContext
    {
        private Func<string, Node> m_Lookup;

        public ND_BTLiveContext(BehaviorTreeRunner agent, BehaviorTree runtime, BehaviorTree asset, int frame, Func<string, Node> lookup) =>
            Set(agent, runtime, asset, frame, lookup);

        /// <summary>Refills the context (the window reuses one instance instead of allocating one per tick).</summary>
        public void Set(BehaviorTreeRunner agent, BehaviorTree runtime, BehaviorTree asset, int frame, Func<string, Node> lookup)
        {
            Agent = agent;
            Runtime = runtime;
            Asset = asset;
            Frame = frame;
            m_Lookup = lookup;
        }

        /// <summary>The watched agent.</summary>
        public BehaviorTreeRunner Agent { get; private set; }
        /// <summary>The runtime tree on screen (the agent's tree, or a subtree clone stepped into).</summary>
        public BehaviorTree Runtime { get; private set; }
        /// <summary>The asset shown in the graph.</summary>
        public BehaviorTree Asset { get; private set; }
        public int Frame { get; private set; }

        /// <summary>The runtime node with this id (node ids are kept by the clone), or null.</summary>
        public Node Lookup(string id) => m_Lookup?.Invoke(id);

        /// <summary>The runtime counterpart of an asset node, or null.</summary>
        public T RuntimeOf<T>(Node assetNode) where T : Node => assetNode == null ? null : Lookup(assetNode.id) as T;
    }

    /// <summary>
    /// A section a plugin adds to the Live Debug panel (<see cref="ND_BTPluginContext.AddLiveDebugSection"/>).
    /// Build the UI once in the constructor; in <see cref="OnLiveUpdate"/> only change what changed
    /// (it runs ~30 times per second: no per-call allocations, set text only when the value differs).
    /// </summary>
    public abstract class ND_BTLiveSection : VisualElement
    {
        public virtual void OnLiveUpdate(ND_BTLiveContext live) { }

        /// <summary>Play mode ended or the watched agent was cleared: reset to an idle look.</summary>
        public virtual void OnLiveStopped() { }
    }
}
