using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.Editor.Plugins;
using UnityEngine;

namespace ND_BehaviorTree.GOAP.Editor
{
    /// <summary>Entry point of the GOAP plugin: plan preview and validation rules.</summary>
    public sealed class GoapPlugin : ND_BTPlugin
    {
        public const string Misconfigured = "goap.misconfigured";
        public const string GoalEmpty = "goap.goal-empty";
        public const string GoalKeyNeverChanged = "goap.goal-key-never-changed";
        public const string ActionOutsidePlanner = "goap.action-outside-planner";

        public override string DisplayName => "GOAP";
        public override string Version => "2.0.0";
        public override string Description => "Goal-oriented action planning: prioritized goals, actions with preconditions / effects / cost, replanning.";

        public override void Register(ND_BTPluginContext context)
        {
            context.AddNodeMenuItem<GOAPPlannerNode>("GOAP/Preview Plan (blackboard values)", PreviewPlan);
            context.AddValidationRule("GOAP", Check);
        }

        private static void PreviewPlan(ND_BTPluginArgs args)
        {
            var planner = (GOAPPlannerNode)args.Node;
            string result = planner.PreviewPlan(args.Tree.blackboard);
            Debug.Log($"[GOAP Preview] '{GOAPPlannerNode.Title(planner)}' with the blackboard's current values: {result}", planner);
            args.Canvas?.ShowNotification(result);
        }

        private static IEnumerable<GraphIssue> Check(BehaviorTree tree)
        {
            var planners = tree.nodes.OfType<GOAPPlannerNode>().ToList();
            var plannedActions = new HashSet<GOAPActionNode>();
            foreach (var planner in planners)
            {
                string error = planner.CheckConfiguration(tree.blackboard);
                if (error != null)
                    yield return new GraphIssue(GraphIssueSeverity.Error, Misconfigured, $"GOAP Planner '{GOAPPlannerNode.Title(planner)}': {error}.", planner);

                var actions = planner.children.Select(Terminal).OfType<GOAPActionNode>().ToList();
                plannedActions.UnionWith(actions);
                var changedKeys = new HashSet<string>(actions.SelectMany(a => a.effects).Where(e => e.key != null).Select(e => e.key.keyName));
                foreach (var goal in planner.goals)
                {
                    if (goal.conditions.Count == 0)
                    {
                        yield return new GraphIssue(GraphIssueSeverity.Warning, GoalEmpty, $"GOAP Planner '{GOAPPlannerNode.Title(planner)}': goal '{goal.name}' has no conditions (it always holds).", planner);
                        continue;
                    }
                    foreach (var condition in goal.conditions.Where(c => c.key != null && !changedKeys.Contains(c.key.keyName)))
                        yield return new GraphIssue(GraphIssueSeverity.Warning, GoalKeyNeverChanged,
                            $"GOAP Planner '{GOAPPlannerNode.Title(planner)}': goal '{goal.name}' needs {condition}, but no action has an effect on '{condition.key.keyName}' (only the world can make it true).", planner);
                }
            }
            foreach (var action in tree.nodes.OfType<GOAPActionNode>().Where(a => !plannedActions.Contains(a)))
                yield return new GraphIssue(GraphIssueSeverity.Warning, ActionOutsidePlanner, $"GOAP Action '{GOAPPlannerNode.Title(action)}' is not under a GOAP Planner; it only runs its child.", action);
        }

        private static Node Terminal(Node entry)
        {
            for (int guard = 0; guard < 64 && entry is DecoratorNode decorator && decorator.child != null; guard++) entry = decorator.child;
            return entry;
        }
    }
}
