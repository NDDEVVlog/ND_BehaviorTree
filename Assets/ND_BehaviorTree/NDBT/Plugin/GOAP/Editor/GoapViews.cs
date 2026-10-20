using System;
using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor;
using ND_BehaviorTree.Editor.CustomGraph;
using UnityEditor;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Editor
{
    internal static class GoapViewUtil
    {
        public static StyleSheet Styles => ND_BTStyles.Find("GoapNodes");

        /// <summary>A label container inserted right under the node's title row.</summary>
        public static VisualElement AddSummary(VisualElement view)
        {
            var summary = new VisualElement { pickingMode = PickingMode.Ignore };
            summary.AddToClassList("goap-summary");
            var title = view.Q("title");
            title?.parent?.Insert(title.parent.IndexOf(title) + 1, summary);
            return summary;
        }

        public static void SetLines(VisualElement summary, string[] lines)
        {
            if (summary.childCount == lines.Length && lines.Select((l, i) => ((Label)summary[i]).text == l).All(x => x)) return;
            summary.Clear();
            foreach (var line in lines) summary.Add(new Label(line) { pickingMode = PickingMode.Ignore });
        }
    }

    /// <summary>
    /// GOAP action: its preconditions (NEEDS), effects (DOES) and cost, one row each, under the title.
    /// In Live Debug every precondition row shows ✓ / ✗ against the agent's runtime blackboard.
    /// </summary>
    [CustomNodeView(typeof(GOAPActionNode))]
    public class GoapActionView : ND_BTNodeView
    {
        private VisualElement m_Panel;
        private string m_Signature;
        private readonly List<(KeyCondition condition, VisualElement row, Label mark)> m_ConditionRows = new List<(KeyCondition, VisualElement, Label)>();

        public GoapActionView(Node node, ViewThemeData themeData) : base(node, themeData) { }

        private GOAPActionNode Action => (GOAPActionNode)NodeData;

        protected override void ApplyThemeAndLayout()
        {
            base.ApplyThemeAndLayout();
            ND_BTStyles.Apply(this, GoapViewUtil.Styles);
            AddToClassList("goap-action");
        }

        protected override void PopulateData()
        {
            base.PopulateData();
            m_Panel = GoapViewUtil.AddSummary(this);
            m_Panel.AddToClassList("goap-panel");
            Rebuild();
        }

        public override void RefreshKeyInfo()
        {
            base.RefreshKeyInfo();
            Rebuild();
        }

        /// <summary>Rebuilds the rows only when the action's data changed.</summary>
        private void Rebuild()
        {
            var action = Action;
            string signature = $"{EditorUtility.GetDirtyCount(action)}|{string.Join(";", action.preconditions)}|{string.Join(";", action.effects)}|{action.cost}|{action.costKey?.keyName}|{action.applyEffects}";
            if (signature == m_Signature) return;
            m_Signature = signature;

            m_Panel.Clear();
            m_ConditionRows.Clear();

            Section("NEEDS");
            if (action.preconditions.Count == 0) Row("always possible", "goap-row--none");
            foreach (var condition in action.preconditions)
            {
                var (row, mark) = Row(condition.ToString(), condition.key == null ? "goap-row--error" : "goap-row--need", withMark: true);
                m_ConditionRows.Add((condition, row, mark));
            }

            Section(action.applyEffects ? "DOES" : "DOES (written by the child)");
            if (action.effects.Count == 0) Row("nothing", "goap-row--none");
            foreach (var effect in action.effects) Row(effect.ToString(), effect.key == null ? "goap-row--error" : "goap-row--effect");

            var cost = new Label(action.costKey != null ? $"COST  = {action.costKey.keyName}" : $"COST  {action.cost}") { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("goap-cost");
            m_Panel.Add(cost);
        }

        private void Section(string title)
        {
            var label = new Label(title) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("goap-section");
            m_Panel.Add(label);
        }

        private (VisualElement row, Label mark) Row(string text, string cssClass, bool withMark = false)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("goap-row");
            row.AddToClassList(cssClass);
            Label mark = null;
            if (withMark)
            {
                mark = new Label("•") { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("goap-row__mark");
                row.Add(mark);
            }
            row.Add(new Label(text) { pickingMode = PickingMode.Ignore });
            m_Panel.Add(row);
            return (row, mark);
        }

        public override void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            base.UpdateLiveState(lookup, frame);
            var blackboard = lookup(NodeData.id)?.blackboard;
            foreach (var (condition, row, mark) in m_ConditionRows)
            {
                bool known = condition.TryEvaluate(blackboard, out bool met);
                string text = !known ? "•" : met ? "✓" : "✗";
                if (mark.text != text) mark.text = text; // only touch the label when the state flips
                row.EnableInClassList("goap-row--met", known && met);
                row.EnableInClassList("goap-row--unmet", known && !met);
            }
        }

        public override void ClearState()
        {
            base.ClearState();
            foreach (var (_, row, mark) in m_ConditionRows)
            {
                if (mark.text != "•") mark.text = "•";
                row.RemoveFromClassList("goap-row--met");
                row.RemoveFromClassList("goap-row--unmet");
            }
        }
    }

    /// <summary>GOAP planner: its goals; in Live Debug, the goal being pursued and the plan with the current step.</summary>
    [CustomNodeView(typeof(GOAPPlannerNode))]
    public class GoapPlannerView : ND_BTNodeView
    {
        private VisualElement m_Summary;
        private Label m_Live;
        private string m_LiveSignature;

        public GoapPlannerView(Node node, ViewThemeData themeData) : base(node, themeData) { }

        protected override void ApplyThemeAndLayout()
        {
            base.ApplyThemeAndLayout();
            ND_BTStyles.Apply(this, GoapViewUtil.Styles);
            AddToClassList("goap-planner");
        }

        protected override void PopulateData()
        {
            base.PopulateData();
            m_Summary = GoapViewUtil.AddSummary(this);
            m_Live = new Label { pickingMode = PickingMode.Ignore };
            m_Live.AddToClassList("goap-live");
            m_Live.style.display = DisplayStyle.None;
            m_Summary.parent?.Insert(m_Summary.parent.IndexOf(m_Summary) + 1, m_Live);
            Refresh();
        }

        public override void RefreshKeyInfo()
        {
            base.RefreshKeyInfo();
            Refresh();
        }

        private void Refresh()
        {
            var planner = (GOAPPlannerNode)NodeData;
            GoapViewUtil.SetLines(m_Summary, planner.goals.Count == 0
                ? new[] { "no goals: add some in the properties" }
                : planner.goals.OrderByDescending(g => g.priority)
                    .Select(g => $"goal  {g.name}  [{(g.priorityKey != null ? g.priorityKey.keyName : g.priority.ToString())}]: {(g.conditions.Count == 0 ? "—" : string.Join(", ", g.conditions))}")
                    .ToArray());
        }

        public override void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            base.UpdateLiveState(lookup, frame);
            if (!(lookup(NodeData.id) is GOAPPlannerNode runtime)) return;
            var goal = runtime.CurrentGoal;
            var plan = runtime.CurrentPlan;
            string signature = $"{goal?.name}|{runtime.CurrentStep}|{plan.Count}|{runtime.LastFailure}";
            if (signature == m_LiveSignature) return; // text only changes when the plan does
            m_LiveSignature = signature;
            string text = goal != null
                ? $"▶ {goal.name}: {string.Join(" → ", plan.Select((a, i) => i == runtime.CurrentStep ? $"[{GOAPPlannerNode.Title(a)}]" : GOAPPlannerNode.Title(a)))}"
                : runtime.LastFailure != null ? $"✕ {runtime.LastFailure}" : "✓ every goal holds";
            m_Live.text = text;
            m_Live.EnableInClassList("goap-live--failed", goal == null && runtime.LastFailure != null);
            m_Live.style.display = DisplayStyle.Flex;
        }

        public override void ClearState()
        {
            base.ClearState();
            m_LiveSignature = null;
            if (m_Live != null) m_Live.style.display = DisplayStyle.None;
        }
    }
}
