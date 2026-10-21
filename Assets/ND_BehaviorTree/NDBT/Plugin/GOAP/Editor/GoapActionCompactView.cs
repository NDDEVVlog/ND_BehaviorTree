using System;
using ND_BehaviorTree.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Editor
{
    /// <summary>
    /// "Compact" look of the GOAP Action (GoapAppearancePack, GoapCompactNode.uxml + GoapCompact.uss). Collapsed, it
    /// shows one digest line ("2 needs · 1 does · cost 3") that turns green / red in Live Debug; the ▸ button in the
    /// title expands the full NEEDS / DOES / COST panel of <see cref="GoapActionView"/>. Expanded state is kept per
    /// node for the editor session.
    /// </summary>
    public class GoapActionCompactView : GoapActionView
    {
        private const string ExpandedKeyPrefix = "ND_BehaviorTree.GOAP.CompactExpanded.";

        private Label m_Digest;
        private Button m_Toggle;
        private bool m_Expanded;

        public GoapActionCompactView(Node node, ViewThemeData themeData) : base(node, themeData) { }

        private GOAPActionNode Action => (GOAPActionNode)NodeData;

        protected override void ApplyThemeAndLayout()
        {
            GoapViewUtil.ApplyDefaultNodeStyle(this);
            base.ApplyThemeAndLayout();
            AddToClassList("goap-compact");
        }

        protected override void PopulateData()
        {
            base.PopulateData();
            m_Expanded = SessionState.GetBool(ExpandedKeyPrefix + NodeData.id, false);

            m_Digest = new Label { pickingMode = PickingMode.Ignore };
            m_Digest.AddToClassList("goap-digest");
            Panel.parent?.Insert(Panel.parent.IndexOf(Panel), m_Digest);

            m_Toggle = GoapViewUtil.NoDrag(new Button(ToggleExpanded) { tooltip = "Show / hide the preconditions, effects and cost." });
            m_Toggle.AddToClassList("goap-compact-toggle");
            this.Q("title")?.Add(m_Toggle);

            ApplyExpanded();
            RefreshDigest();
        }

        public override void RefreshKeyInfo()
        {
            base.RefreshKeyInfo();
            RefreshDigest();
        }

        private void RefreshDigest()
        {
            if (m_Digest == null) return;
            var action = Action;
            string cost = action.costKey != null ? action.costKey.keyName : action.cost.ToString();
            string text = $"{action.preconditions.Count} needs · {action.effects.Count} does · cost {cost}";
            if (m_Digest.text != text) m_Digest.text = text;
        }

        private void ToggleExpanded()
        {
            m_Expanded = !m_Expanded;
            SessionState.SetBool(ExpandedKeyPrefix + NodeData.id, m_Expanded);
            ApplyExpanded();
            // The node changes height, so the bottom port moves: tell the edges once the new layout is known.
            RegisterCallback<GeometryChangedEvent>(OnResized);
        }

        private void OnResized(GeometryChangedEvent evt)
        {
            UnregisterCallback<GeometryChangedEvent>(OnResized);
            SetPosition(ViewPosition);
        }

        private void ApplyExpanded()
        {
            Panel.style.display = m_Expanded ? DisplayStyle.Flex : DisplayStyle.None;
            m_Digest.style.display = m_Expanded ? DisplayStyle.None : DisplayStyle.Flex;
            m_Toggle.text = m_Expanded ? "▾" : "▸";
        }

        public override void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            base.UpdateLiveState(lookup, frame);
            CountLiveConditions(out int met, out int unmet);
            m_Digest.EnableInClassList("goap-digest--met", met > 0 && unmet == 0);
            m_Digest.EnableInClassList("goap-digest--unmet", unmet > 0);
        }

        public override void ClearState()
        {
            base.ClearState();
            m_Digest?.RemoveFromClassList("goap-digest--met");
            m_Digest?.RemoveFromClassList("goap-digest--unmet");
        }
    }
}
