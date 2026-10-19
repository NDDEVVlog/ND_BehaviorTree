using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree
{
    /// <summary>
    /// Runs another BehaviorTree asset as a reusable subtree.
    ///
    /// Runtime contract:
    ///   - Isolation: every SubtreeNode instance in every agent gets its OWN clone of the subtree (built when the
    ///     owner tree is cloned), so two references to the same asset never share node state.
    ///   - Blackboard: the subtree uses the owner tree's runtime blackboard (keys resolved by name). If the owner
    ///     has no blackboard, the subtree keeps a clone of its own.
    ///   - Status: returns the subtree's status each tick (Success / Failure / Running).
    ///   - Completion: after Success/Failure the next entry restarts the subtree from its root.
    ///   - Interruption/Reset: resetting this node resets every node of the subtree clone.
    ///   - Missing reference or recursion (A → B → A): no runtime clone is built; the node logs an error once
    ///     and returns Failure.
    /// </summary>
    [NodeInfo("Run Subtree", "Subtree/Run Subtree", true, false)]
    public class SubtreeNode : Node
    {
        [Tooltip("Reusable BehaviorTree asset to run. It uses this tree's blackboard; its keys are resolved by name.")]
        public BehaviorTree subtree;

        [NonSerialized] private BehaviorTree m_Runtime;
        [NonSerialized] private string m_BuildError;
        [NonSerialized] private bool m_ErrorLogged;

        /// <summary>The isolated runtime clone (null in assets, or when the reference is missing/recursive).</summary>
        public BehaviorTree RuntimeSubtree => m_Runtime;

        internal void BuildRuntime(BehaviorTree ownerRuntime, HashSet<BehaviorTree> cloneChain)
        {
            m_Runtime = null;
            m_BuildError = null;
            m_ErrorLogged = false;

            if (subtree == null)
            {
                m_BuildError = "no subtree asset is assigned";
                return;
            }
            if (cloneChain.Contains(subtree))
            {
                m_BuildError = $"subtree '{subtree.name}' is already being executed higher up (recursive reference)";
                Debug.LogError($"[SubtreeNode] '{name}': {m_BuildError}. The node will fail.", this);
                m_ErrorLogged = true;
                return;
            }
            m_Runtime = subtree.CloneInternal(cloneChain, ownerRuntime.blackboard);
        }

        protected override void OnEnter()
        {
            if (m_Runtime != null) m_Runtime.Self = ownerTree != null ? ownerTree.Self : null;
        }

        protected override Status OnProcess()
        {
            if (m_Runtime == null)
            {
                if (!m_ErrorLogged)
                {
                    Debug.LogError($"[SubtreeNode] '{name}' cannot run: {m_BuildError ?? "the owner tree was not cloned (run trees through BehaviorTree.Clone)"}.", this);
                    m_ErrorLogged = true;
                }
                return Status.Failure;
            }
            return m_Runtime.Update();
        }

        public override void Reset()
        {
            base.Reset();
            if (m_Runtime != null) m_Runtime.ResetRuntimeState();
        }
    }
}
