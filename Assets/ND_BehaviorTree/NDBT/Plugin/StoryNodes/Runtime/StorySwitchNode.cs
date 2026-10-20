using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.Story
{
    /// <summary>
    /// Control-flow node: reads a blackboard value and runs the matching <see cref="StoryCaseNode"/> branch
    /// (or the Default case). The Case nodes are generated in the editor from <see cref="cases"/> — they are owned
    /// children (<see cref="IOwnedChildren"/>), kept in the same order as the list.
    /// </summary>
    [StorySentence("Switch on {value}")]
    [NodeDescription("Runs the branch whose case matches a blackboard value (or Default).",
        "Edit 'Cases' in the properties: a Case node is generated under the Switch for each entry (+ Default), in the same undo step.\n" +
        "The value is compared as text (enums by name, numbers as written, bools as true/false; 'Ignore Case' for letters).\n" +
        "Returns the branch's result; Failure when nothing matches and there is no Default.")]
    [NodeInfo("Switch", "Story/Switch", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/adaptation.png")]
    public class StorySwitchNode : CompositeNode, IOwnedChildren
    {
        [Tooltip("The value to switch on (any key type; compared as text).")]
        [BlackboardKeyType(typeof(object))] public Key valueKey;

        [Tooltip("One Case node per entry, in this order.")]
        public List<string> cases = new List<string> { "A", "B" };
        [Tooltip("Add a Default case, run when no case matches.")]
        public bool includeDefault = true;
        public bool ignoreCase = true;

        [System.NonSerialized] private Node m_Selected;
        [System.NonSerialized] private bool m_NoMatchLogged;

        protected override void OnEnter() => m_Selected = Select();

        protected override Status OnProcess()
        {
            if (m_Selected == null)
            {
                if (!m_NoMatchLogged)
                {
                    m_NoMatchLogged = true;
                    Debug.LogWarning($"[StorySwitchNode] '{name}': no case matches '{CurrentValueText()}' and there is no Default. (Logged once.)", this);
                }
                return Status.Failure;
            }
            return m_Selected.Process();
        }

        private string CurrentValueText()
        {
            if (valueKey == null || blackboard == null || !blackboard.TryGetKey(valueKey.keyName, out var key)) return string.Empty;
            return Convert.ToString(key.GetValueObject(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>The runtime entry (outermost decorator or the case itself) of the matching case, else Default.</summary>
        private Node Select()
        {
            string value = CurrentValueText();
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Node fallback = null;
            for (int i = 0; i < children.Count; i++)
            {
                if (!(Terminal(children[i]) is StoryCaseNode caseNode)) continue;
                if (caseNode.isDefault) { fallback ??= children[i]; continue; }
                if (string.Equals(caseNode.caseValue, value, comparison)) return children[i];
            }
            return fallback;
        }

        private static Node Terminal(Node entry)
        {
            for (int guard = 0; guard < 64 && entry is DecoratorNode decorator && decorator.child != null; guard++) entry = decorator.child;
            return entry;
        }

        public override void Reset()
        {
            base.Reset();
            m_Selected = null;
        }
    }
}
