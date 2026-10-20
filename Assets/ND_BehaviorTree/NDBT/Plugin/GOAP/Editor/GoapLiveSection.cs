using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Plugins;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Editor
{
    /// <summary>
    /// "GOAP" section of the Live Debug panel: for every GOAP Planner in the watched agent's tree — the goal being
    /// pursued and the plan (current step in brackets), every goal with its live priority and ✓ / ✗, why the last plan
    /// failed, the recent planning history (why it replanned) and a "Check goals now" button.
    /// Text is rebuilt only when the planner's state changes; goal marks are compared before being set.
    /// </summary>
    public sealed class GoapLiveSection : ND_BTLiveSection
    {
        private const int HistoryLines = 6;

        private sealed class GoalRow
        {
            public GoapGoal Goal;
            public VisualElement Row;
            public Label Mark, Name, Priority;
            public float ShownPriority = float.NaN;
        }

        private sealed class Card
        {
            public GOAPPlannerNode Planner;
            public Label Plan, Failure;
            public VisualElement History;
            public readonly List<GoalRow> Goals = new List<GoalRow>();
            public GoapGoal ShownGoal;
            public int ShownStep = -1, ShownHistory = -1;
            public string ShownFailure;
            public bool Shown;
        }

        private readonly Label m_Hint;
        private readonly VisualElement m_Cards;
        private readonly List<Card> m_CardList = new List<Card>();
        private BehaviorTree m_BuiltFor;

        public GoapLiveSection()
        {
            styleSheets.Add(GoapViewUtil.Styles);
            AddToClassList("goap-live-section");
            m_Hint = new Label("Watch an agent in Play mode to see its GOAP planners.");
            m_Hint.AddToClassList("goap-live-hint");
            Add(m_Hint);
            m_Cards = new VisualElement();
            Add(m_Cards);
        }

        public override void OnLiveUpdate(ND_BTLiveContext live)
        {
            if (live.Runtime != m_BuiltFor) Build(live.Runtime);
            var blackboard = live.Runtime != null ? live.Runtime.blackboard : null;
            for (int i = 0; i < m_CardList.Count; i++) UpdateCard(m_CardList[i], blackboard);
        }

        public override void OnLiveStopped() => Build(null);

        // ------------------------------------------------------------------ build (only when the watched tree changes)

        private void Build(BehaviorTree runtime)
        {
            m_BuiltFor = runtime;
            m_Cards.Clear();
            m_CardList.Clear();
            var planners = runtime != null ? runtime.nodes.OfType<GOAPPlannerNode>().ToList() : new List<GOAPPlannerNode>();
            m_Hint.text = runtime == null ? "Watch an agent in Play mode to see its GOAP planners." : "This tree has no GOAP Planner.";
            m_Hint.style.display = planners.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var planner in planners) m_CardList.Add(BuildCard(planner));
        }

        private Card BuildCard(GOAPPlannerNode planner)
        {
            var card = new Card { Planner = planner };
            var root = new VisualElement();
            root.AddToClassList("goap-live-card");

            var header = new VisualElement();
            header.AddToClassList("goap-live-card__header");
            var title = new Label(GOAPPlannerNode.Title(planner));
            title.AddToClassList("goap-live-card__title");
            header.Add(title);
            var check = new Button(planner.RequestGoalCheck) { text = "Check goals", tooltip = "Re-evaluate goals on the next tick (a more important goal can take over)." };
            check.AddToClassList("goap-live-card__button");
            header.Add(check);
            root.Add(header);

            card.Plan = new Label();
            card.Plan.AddToClassList("goap-live");
            root.Add(card.Plan);
            card.Failure = new Label();
            card.Failure.AddToClassList("goap-live");
            card.Failure.AddToClassList("goap-live--failed");
            root.Add(card.Failure);

            root.Add(SectionLabel("GOALS"));
            foreach (var goal in planner.goals)
            {
                var row = new GoalRow { Goal = goal, Row = new VisualElement() };
                row.Row.AddToClassList("goap-row");
                row.Row.AddToClassList("goap-row--need");
                row.Mark = new Label("•");
                row.Mark.AddToClassList("goap-row__mark");
                row.Name = new Label($"{goal.name}: {(goal.conditions.Count == 0 ? "—" : string.Join(", ", goal.conditions))}");
                row.Name.AddToClassList("goap-live-goal__name");
                row.Priority = new Label();
                row.Priority.AddToClassList("goap-live-goal__priority");
                row.Row.Add(row.Mark);
                row.Row.Add(row.Name);
                row.Row.Add(row.Priority);
                root.Add(row.Row);
                card.Goals.Add(row);
            }

            root.Add(SectionLabel("HISTORY (newest first)"));
            card.History = new VisualElement();
            root.Add(card.History);

            m_Cards.Add(root);
            return card;
        }

        private static Label SectionLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("goap-section");
            return label;
        }

        // ------------------------------------------------------------------ update (~30 Hz: compare before touching the UI)

        private static void UpdateCard(Card card, Blackboard blackboard)
        {
            var planner = card.Planner;
            var goal = planner.CurrentGoal;
            int historyVersion = planner.HistoryVersion;
            string failure = planner.LastFailure;

            if (!card.Shown || goal != card.ShownGoal || planner.CurrentStep != card.ShownStep || historyVersion != card.ShownHistory || failure != card.ShownFailure)
            {
                card.Shown = true;
                card.ShownGoal = goal;
                card.ShownStep = planner.CurrentStep;
                card.ShownHistory = historyVersion;
                card.ShownFailure = failure;

                var plan = planner.CurrentPlan;
                card.Plan.text = goal != null
                    ? $"▶ {goal.name}: {(plan.Count == 0 ? "(already met)" : string.Join(" → ", plan.Select((a, i) => i == planner.CurrentStep ? $"[{GOAPPlannerNode.Title(a)}]" : GOAPPlannerNode.Title(a))))}"
                    : failure == null ? "✓ every goal holds" : "no plan";
                card.Failure.text = failure != null ? $"✕ {failure}" : string.Empty;
                card.Failure.style.display = failure != null ? DisplayStyle.Flex : DisplayStyle.None;
                RebuildHistory(card);
            }

            foreach (var row in card.Goals)
            {
                bool allKnown = true, allMet = true;
                foreach (var condition in row.Goal.conditions)
                {
                    if (!condition.TryEvaluate(blackboard, out bool met)) { allKnown = false; break; }
                    allMet &= met;
                }
                string mark = !allKnown ? "?" : allMet ? "✓" : "✗";
                if (row.Mark.text != mark) row.Mark.text = mark;
                row.Row.EnableInClassList("goap-row--met", allKnown && allMet);
                row.Row.EnableInClassList("goap-row--unmet", allKnown && !allMet);
                row.Row.EnableInClassList("goap-live-goal--current", row.Goal == goal);

                float priority = row.Goal.priority;
                if (row.Goal.priorityKey != null && blackboard != null && blackboard.TryGetKey(row.Goal.priorityKey.keyName, out var key))
                    priority = System.Convert.ToSingle(key.GetValueObject());
                if (!priority.Equals(row.ShownPriority))
                {
                    row.ShownPriority = priority;
                    row.Priority.text = row.Goal.priorityKey != null ? $"{priority:0.##} ({row.Goal.priorityKey.keyName})" : $"{priority:0.##}";
                }
            }
        }

        private static void RebuildHistory(Card card)
        {
            card.History.Clear();
            var history = card.Planner.History;
            if (history.Count == 0)
            {
                var none = new Label("—");
                none.AddToClassList("goap-live-history");
                card.History.Add(none);
                return;
            }
            for (int i = history.Count - 1, shown = 0; i >= 0 && shown < HistoryLines; i--, shown++)
            {
                var record = history[i];
                string body = record.Goal != null ? $"{record.Goal}: {record.Plan}" : string.Empty;
                var line = new Label($"{record.Time:0.0}s  {record.Trigger}{(body.Length > 0 ? "  →  " + body : string.Empty)}");
                line.AddToClassList("goap-live-history");
                line.EnableInClassList("goap-live-history--failed", record.Failed);
                card.History.Add(line);
            }
        }
    }
}
