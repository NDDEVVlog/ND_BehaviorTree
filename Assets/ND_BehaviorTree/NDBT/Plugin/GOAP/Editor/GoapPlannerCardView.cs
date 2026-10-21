using System;
using ND_BehaviorTree.Editor;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Editor
{
    /// <summary>
    /// "Card" look of the GOAP Planner (GoapAppearancePack, GoapCardNode.uxml + GoapCard.uss). Same goals and Live
    /// Debug data as <see cref="GoapPlannerView"/>; the live line shows only the goal, and the plan is drawn below it
    /// as step chips (done / current / next).
    /// </summary>
    public class GoapPlannerCardView : GoapPlannerView
    {
        private VisualElement m_Steps;
        private string m_StepsSignature;

        public GoapPlannerCardView(Node node, ViewThemeData themeData) : base(node, themeData) { }

        protected override void ApplyThemeAndLayout()
        {
            GoapViewUtil.ApplyDefaultNodeStyle(this);
            base.ApplyThemeAndLayout();
            AddToClassList("goap-card");
        }

        protected override void PopulateData()
        {
            base.PopulateData();
            m_Steps = new VisualElement { pickingMode = PickingMode.Ignore };
            m_Steps.AddToClassList("goap-steps");
            m_Steps.style.display = DisplayStyle.None;
            LiveLabel.parent?.Insert(LiveLabel.parent.IndexOf(LiveLabel) + 1, m_Steps);
        }

        public override void UpdateLiveState(Func<string, Node> lookup, int frame)
        {
            base.UpdateLiveState(lookup, frame);
            if (!(lookup(NodeData.id) is GOAPPlannerNode runtime)) return;
            var goal = runtime.CurrentGoal;
            var plan = runtime.CurrentPlan;
            string signature = $"{goal?.name}|{runtime.CurrentStep}|{plan.Count}|{runtime.LastFailure}";
            if (signature == m_StepsSignature) return; // same inputs as the base line: rebuild only when the plan changes
            m_StepsSignature = signature;

            // The base wrote "▶ goal: a → [b] → c"; the chips carry the plan, so the line keeps only the goal.
            if (goal != null) LiveLabel.text = $"▶ {goal.name}";

            m_Steps.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                var chip = new Label(GOAPPlannerNode.Title(plan[i])) { pickingMode = PickingMode.Ignore };
                chip.AddToClassList("goap-step");
                chip.AddToClassList(i < runtime.CurrentStep ? "goap-step--done" : i == runtime.CurrentStep ? "goap-step--current" : "goap-step--next");
                m_Steps.Add(chip);
            }
            m_Steps.style.display = goal != null && plan.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void ClearState()
        {
            base.ClearState();
            m_StepsSignature = null;
            if (m_Steps == null) return;
            m_Steps.Clear();
            m_Steps.style.display = DisplayStyle.None;
        }
    }
}
