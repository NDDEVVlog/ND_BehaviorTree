using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.GOAP.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ND_BehaviorTree.GOAP.Tests
{
    [NodeInfo("GOAP Test Action", "Test/GoapTestAction", true, false)]
    public class GoapTestAction : ActionNode
    {
        public Status result = Status.Success;
        public int processCount, exitCount;
        protected override Status OnProcess() { processCount++; return result; }
        protected override void OnExit() => exitCount++;
    }

    public class GoapPlannerSearchTests
    {
        private static GoapFactCondition Is(int fact, object value, ComparisonType op = ComparisonType.Equal) => new GoapFactCondition(fact, op, value);
        private static GoapFactEffect Set(int fact, object value) => new GoapFactEffect(fact, GoapEffectOperation.Set, value);
        private static GoapPlanAction Action(float cost, GoapFactCondition[] pre, params GoapFactEffect[] effects) =>
            new GoapPlanAction { Cost = cost, Preconditions = pre, Effects = effects };

        // facts: 0 = hasAxe (bool), 1 = treeFound (bool), 2 = logs (int)
        private static readonly object[] Start = { false, false, 0 };

        [Test]
        public void FindsMultiStepPlan_InDependencyOrder()
        {
            var actions = new[]
            {
                Action(1, new[] { Is(0, true), Is(1, true) }, new GoapFactEffect(2, GoapEffectOperation.Add, 10)), // 0 chop
                Action(1, new GoapFactCondition[0], Set(1, true)),                                                // 1 scout
                Action(1, new GoapFactCondition[0], Set(0, true)),                                                // 2 get axe
            };
            var plan = GoapPlanner.FindPlan(Start, new[] { Is(2, 10, ComparisonType.GreaterEqual) }, actions, 1000, out _);

            Assert.IsNotNull(plan);
            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual(0, plan.Last(), "chop comes last, after the axe and the tree");
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, plan.Take(2));
        }

        [Test]
        public void PrefersCheaperRoute()
        {
            var actions = new[]
            {
                Action(10, new GoapFactCondition[0], Set(0, true)),  // buy axe: expensive
                Action(2, new GoapFactCondition[0], Set(1, true)),   // find tree
                Action(1, new[] { Is(1, true) }, Set(0, true)),      // craft axe from the tree: 2 + 1 = 3
            };
            var plan = GoapPlanner.FindPlan(Start, new[] { Is(0, true) }, actions, 1000, out _);
            CollectionAssert.AreEqual(new[] { 1, 2 }, plan);
        }

        [Test]
        public void AddEffects_RepeatActionUntilGoal()
        {
            var actions = new[] { Action(1, new GoapFactCondition[0], new GoapFactEffect(2, GoapEffectOperation.Add, 10)) };
            var plan = GoapPlanner.FindPlan(Start, new[] { Is(2, 30, ComparisonType.GreaterEqual) }, actions, 1000, out _);
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, plan);
        }

        [Test]
        public void GoalAlreadyMet_EmptyPlan_Unreachable_NullWithReason()
        {
            Assert.AreEqual(0, GoapPlanner.FindPlan(Start, new[] { Is(0, false) }, new GoapPlanAction[0], 10, out _).Count);

            var actions = new[] { Action(1, new GoapFactCondition[0], Set(0, true)) };
            Assert.IsNull(GoapPlanner.FindPlan(Start, new[] { Is(1, true) }, actions, 1000, out string reason));
            StringAssert.Contains("no sequence", reason);

            var endless = new[] { Action(1, new GoapFactCondition[0], new GoapFactEffect(2, GoapEffectOperation.Add, 1)) };
            Assert.IsNull(GoapPlanner.FindPlan(Start, new[] { Is(1, true) }, endless, 50, out reason));
            StringAssert.Contains("search limit", reason, "unbounded Add effects stop at the budget instead of hanging");
        }
    }

    public class GoapNodeTests
    {
        private BehaviorTree m_Tree;
        private BehaviorTreeGraphService m_Graph;
        private RootNode m_Root;
        private Blackboard m_Blackboard;
        private readonly List<Object> m_Track = new List<Object>();
        private BoolKey m_HasAxe, m_TreeFound;
        private IntKey m_Logs, m_Health;

        [SetUp]
        public void SetUp()
        {
            m_Tree = Track(ScriptableObject.CreateInstance<BehaviorTree>());
            m_Tree.name = "GoapTestTree";
            m_Root = ScriptableObject.CreateInstance<RootNode>();
            m_Root.name = "Root";
            m_Root.repeat = false;
            m_Tree.nodes.Add(m_Root);
            m_Tree.rootNode = m_Root;
            m_Graph = new BehaviorTreeGraphService(m_Tree);
            m_Graph.History.Capacity = 100;

            m_Blackboard = Track(ScriptableObject.CreateInstance<Blackboard>());
            m_HasAxe = MakeKey<BoolKey>("HasAxe");
            m_TreeFound = MakeKey<BoolKey>("TreeFound");
            m_Logs = MakeKey<IntKey>("Logs");
            m_Health = MakeKey<IntKey>("Health");
            m_Health.SetValue(100);
            m_Tree.blackboard = m_Blackboard;
        }

        [TearDown]
        public void TearDown()
        {
            m_Graph.History.Clear();
            foreach (var n in m_Tree.nodes) if (n != null) { m_Track.Add(n); m_Track.AddRange(n.decorators); }
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        private T MakeKey<T>(string name) where T : Key
        {
            var key = Track(ScriptableObject.CreateInstance<T>());
            key.keyName = key.name = name;
            m_Blackboard.keys.Add(key);
            return key;
        }

        private T Create<T>(float x = 0, float y = 0) where T : Node => (T)m_Graph.CreateNode(typeof(T), new Vector2(x, y));

        private static KeyCondition Cond(Key key, ComparisonType op, bool b = false, int i = 0) =>
            new KeyCondition { key = key, comparison = op, value = new GoapValue { boolValue = b, intValue = i } };

        private static KeyEffect Eff(Key key, bool b = false, int i = 0, GoapEffectOperation op = GoapEffectOperation.Set) =>
            new KeyEffect { key = key, operation = op, value = new GoapValue { boolValue = b, intValue = i } };

        private GOAPPlannerNode Planner(params GoapGoal[] goals)
        {
            var planner = Create<GOAPPlannerNode>(0, 100);
            m_Graph.Connect(m_Root, planner);
            planner.goals = goals.ToList();
            planner.maxReplans = 2;
            return planner;
        }

        private (GOAPActionNode action, GoapTestAction work) AddAction(GOAPPlannerNode planner, string name, float cost, KeyCondition[] pre, params KeyEffect[] effects)
        {
            var action = Create<GOAPActionNode>(100 * planner.children.Count, 200);
            action.name = name;
            action.cost = cost;
            action.preconditions = pre.ToList();
            action.effects = effects.ToList();
            m_Graph.Connect(planner, action);
            var work = Create<GoapTestAction>(100 * planner.children.Count, 300);
            m_Graph.Connect(action, work);
            return (action, work);
        }

        private static GoapGoal Goal(string name, float priority, params KeyCondition[] conditions) =>
            new GoapGoal { name = name, priority = priority, conditions = conditions.ToList() };

        /// <summary>The woodcutter domain: get axe, find tree, chop (+10 logs, uses up the tree).</summary>
        private (GOAPActionNode axe, GOAPActionNode scout, GOAPActionNode chop) Woodcutter(GOAPPlannerNode planner)
        {
            var axe = AddAction(planner, "GetAxe", 1, new[] { Cond(m_HasAxe, ComparisonType.Equal, b: false) }, Eff(m_HasAxe, b: true)).action;
            var scout = AddAction(planner, "Scout", 1, new[] { Cond(m_TreeFound, ComparisonType.Equal, b: false) }, Eff(m_TreeFound, b: true)).action;
            var chop = AddAction(planner, "Chop", 1, new[] { Cond(m_HasAxe, ComparisonType.Equal, b: true), Cond(m_TreeFound, ComparisonType.Equal, b: true) },
                Eff(m_Logs, i: 10, op: GoapEffectOperation.Add), Eff(m_TreeFound, b: false)).action;
            return (axe, scout, chop);
        }

        private BehaviorTree Clone()
        {
            var clone = Track(m_Tree.Clone());
            m_Track.AddRange(clone.nodes);
            if (clone.blackboard != null) { m_Track.Add(clone.blackboard); m_Track.AddRange(clone.blackboard.keys); }
            return clone;
        }

        private static T Rt<T>(BehaviorTree clone, Node original) where T : Node => (T)clone.nodes.Single(n => n.id == original.id);
        private static object Value(BehaviorTree clone, string key) => clone.blackboard.TryGetKey(key, out var k) ? k.GetValueObject() : null;

        private static Node.Status Run(BehaviorTree clone, int maxTicks = 50)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                var status = clone.Update();
                if (status != Node.Status.Running) return status;
            }
            return Node.Status.Running;
        }

        // ---------- execution ----------

        [Test]
        public void RunsPlan_AppliesEffectsToBlackboard_SucceedsAtGoal()
        {
            var planner = Planner(Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 20)));
            Woodcutter(planner);
            var clone = Clone();

            Assert.AreEqual(Node.Status.Success, Run(clone));

            Assert.AreEqual(20, Value(clone, "Logs"), "two chops, each +10, written by the planner (no Set nodes in the subtrees)");
            Assert.AreEqual(true, Value(clone, "HasAxe"));
            Assert.AreEqual(0, m_Logs.GetValue(), "the asset blackboard is untouched");
        }

        [Test]
        public void ApplyEffectsOff_ChildMustWrite_OtherwiseReplansThenFails()
        {
            var planner = Planner(Goal("Axe", 1, Cond(m_HasAxe, ComparisonType.Equal, b: true)));
            var (axe, _, _) = Woodcutter(planner);
            axe.applyEffects = false; // the child does not write HasAxe either
            var clone = Clone();
            LogAssert.Expect(LogType.Warning, new Regex("gave up after 2 replan"));

            Assert.AreEqual(Node.Status.Failure, Run(clone));
            StringAssert.Contains("goal does not hold", Rt<GOAPPlannerNode>(clone, planner).LastFailure);
        }

        [Test]
        public void HighestPriorityUnmetGoal_IsPursued_PriorityKeyCanChangeIt()
        {
            var eat = Goal("Eat", 5, Cond(m_Health, ComparisonType.GreaterEqual, i: 50));
            var logs = Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 10));
            var planner = Planner(logs, eat);
            Woodcutter(planner);
            AddAction(planner, "EatFood", 1, new KeyCondition[0], Eff(m_Health, i: 100));

            m_Health.SetValue(10);
            Assert.That(planner.PreviewPlan(m_Blackboard), Does.StartWith("Goal 'Eat'"));

            m_Health.SetValue(100); // Eat already holds → the next goal
            Assert.That(planner.PreviewPlan(m_Blackboard), Does.StartWith("Goal 'Logs'"));

            var urgency = MakeKey<FloatKey>("LogUrgency");
            logs.priorityKey = urgency;
            urgency.SetValue(9f);
            m_Health.SetValue(10);
            Assert.That(planner.PreviewPlan(m_Blackboard), Does.StartWith("Goal 'Logs'"), "priority read from the key (9) beats Eat (5)");
        }

        [Test]
        public void MoreImportantGoal_TakesOverRunningPlan_AndAbortsCurrentAction()
        {
            var planner = Planner(Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 10)), Goal("Eat", 5, Cond(m_Health, ComparisonType.GreaterEqual, i: 50)));
            var (axe, _, _) = Woodcutter(planner);
            var axeWork = (GoapTestAction)axe.child;
            axeWork.result = Node.Status.Running; // getting the axe takes a while
            var (_, eatWork) = AddAction(planner, "EatFood", 1, new KeyCondition[0], Eff(m_Health, i: 100));
            var clone = Clone();
            var rtPlanner = Rt<GOAPPlannerNode>(clone, planner);

            clone.Update();
            Assert.AreEqual("Logs", rtPlanner.CurrentGoal.name);
            Assert.AreEqual("GetAxe", rtPlanner.CurrentPlan[0].name);

            clone.blackboard.SetValue("Health", 10); // gets hungry
            rtPlanner.RequestGoalCheck();
            clone.Update();

            Assert.AreEqual("Eat", rtPlanner.CurrentGoal.name);
            Assert.AreEqual(1, Rt<GoapTestAction>(clone, axeWork).exitCount, "the running GetAxe work was aborted");
            Assert.AreEqual(1, Rt<GoapTestAction>(clone, eatWork).processCount);
        }

        [Test]
        public void FailedAction_TriggersReplan_ThroughAnotherRoute()
        {
            var planner = Planner(Goal("Axe", 1, Cond(m_HasAxe, ComparisonType.Equal, b: true)));
            var (buy, buyWork) = AddAction(planner, "BuyAxe", 1, new KeyCondition[0], Eff(m_HasAxe, b: true));
            AddAction(planner, "CraftAxe", 5, new KeyCondition[0], Eff(m_HasAxe, b: true));
            buyWork.result = Node.Status.Failure;
            // after BuyAxe fails, mark it impossible so the replan takes the other route
            buy.preconditions = new List<KeyCondition> { Cond(m_TreeFound, ComparisonType.Equal, b: false) };
            var clone = Clone();

            clone.Update(); // BuyAxe runs and fails → replan (still BuyAxe: preconditions hold)
            clone.blackboard.SetValue("TreeFound", true); // world changed: BuyAxe no longer possible
            var status = Run(clone);

            Assert.AreEqual(Node.Status.Success, status);
            Assert.AreEqual(true, Value(clone, "HasAxe"));
            Assert.IsNull(Rt<GOAPPlannerNode>(clone, planner).LastFailure);
        }

        [Test]
        public void BrokenPrecondition_BeforeStart_Replans()
        {
            var planner = Planner(Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 10)));
            var (_, scout, chop) = Woodcutter(planner);
            m_HasAxe.SetValue(true);
            var clone = Clone();
            var rtPlanner = Rt<GOAPPlannerNode>(clone, planner);

            clone.Update(); // plan: Scout → Chop; Scout done (TreeFound = true)
            Assert.AreEqual(new[] { "Scout", "Chop" }, rtPlanner.CurrentPlan.Select(a => a.name).ToArray());
            clone.blackboard.SetValue("HasAxe", false); // lost the axe before chopping
            clone.Update();

            Assert.AreEqual(new[] { "GetAxe", "Chop" }, rtPlanner.CurrentPlan.Select(a => a.name).ToArray(), "replanned from the live blackboard");
        }

        [Test]
        public void Abort_PropagatesToRunningAction()
        {
            var planner = Planner(Goal("Axe", 1, Cond(m_HasAxe, ComparisonType.Equal, b: true)));
            var (axe, work) = AddAction(planner, "GetAxe", 1, new KeyCondition[0], Eff(m_HasAxe, b: true));
            work.result = Node.Status.Running;
            var clone = Clone();
            clone.Update();

            Rt<GOAPPlannerNode>(clone, planner).Abort();

            Assert.AreEqual(1, Rt<GoapTestAction>(clone, work).exitCount);
            Assert.IsFalse(Rt<GOAPActionNode>(clone, axe).IsRunning());
        }

        [Test]
        public void DecoratorOnAction_IsRespected()
        {
            var planner = Planner(Goal("Axe", 1, Cond(m_HasAxe, ComparisonType.Equal, b: true)));
            var (axe, work) = AddAction(planner, "GetAxe", 1, new KeyCondition[0], Eff(m_HasAxe, b: true));
            work.result = Node.Status.Failure;
            Assert.IsNotNull(m_Graph.AddDecorator(axe, typeof(InverterNode)), "decorators on GOAP actions are allowed now");
            var clone = Clone();

            Assert.AreEqual(Node.Status.Success, Run(clone), "the Inverter turned the failing work into success");
            Assert.AreEqual(true, Value(clone, "HasAxe"));
        }

        [Test]
        public void MissingKey_FailsWithReason()
        {
            var stray = Track(ScriptableObject.CreateInstance<BoolKey>());
            stray.keyName = "NotOnBlackboard";
            var planner = Planner(Goal("Stray", 1, Cond(stray, ComparisonType.Equal, b: true)));
            var clone = Clone();
            LogAssert.Expect(LogType.Error, new Regex("misconfigured.*NotOnBlackboard"));

            Assert.AreEqual(Node.Status.Failure, Run(clone));
            StringAssert.Contains("not on the blackboard", Rt<GOAPPlannerNode>(clone, planner).LastFailure);
        }

        [Test]
        public void KeyCondition_TryEvaluate_ReadsTheGivenBlackboardByName()
        {
            var condition = Cond(m_Health, ComparisonType.GreaterEqual, i: 50);
            var clone = Clone();

            Assert.IsTrue(condition.TryEvaluate(clone.blackboard, out bool met));
            Assert.IsTrue(met, "runtime Health is 100");
            clone.blackboard.SetValue("Health", 10);
            condition.TryEvaluate(clone.blackboard, out met);
            Assert.IsFalse(met);
            Assert.AreEqual(100, m_Health.GetValue(), "the asset key is not what gets read");

            var stray = Track(ScriptableObject.CreateInstance<IntKey>());
            stray.keyName = "Elsewhere";
            Assert.IsFalse(Cond(stray, ComparisonType.Equal).TryEvaluate(clone.blackboard, out _), "missing key: unknown, not false");
        }

        // ---------- Live Debug ----------

        [Test]
        public void History_RecordsWhyThePlannerReplanned()
        {
            var planner = Planner(Goal("Axe", 1, Cond(m_HasAxe, ComparisonType.Equal, b: true)));
            var (_, buyWork) = AddAction(planner, "BuyAxe", 1, new KeyCondition[0], Eff(m_HasAxe, b: true));
            AddAction(planner, "CraftAxe", 5, new KeyCondition[0], Eff(m_HasAxe, b: true));
            buyWork.result = Node.Status.Failure;
            var clone = Clone();
            var rt = Rt<GOAPPlannerNode>(clone, planner);
            LogAssert.Expect(LogType.Warning, new Regex("gave up"));

            Run(clone);

            var triggers = rt.History.Select(h => h.Trigger).ToList();
            Assert.AreEqual("started", triggers[0]);
            Assert.AreEqual("BuyAxe", rt.History[0].Plan);
            Assert.That(triggers, Has.Some.Contains("action 'BuyAxe' failed"));
            Assert.IsTrue(rt.History.Last().Failed, "the last record is the give-up");
            Assert.Greater(rt.HistoryVersion, 0);
        }

        [Test]
        public void LiveSection_ShowsGoalPlanGoalMarksAndHistory()
        {
            var planner = Planner(Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 10)), Goal("Healthy", 5, Cond(m_Health, ComparisonType.GreaterEqual, i: 50)));
            Woodcutter(planner);
            ((GoapTestAction)planner.children.Select(c => ((GOAPActionNode)c).child).First()).result = Node.Status.Running; // stay on GetAxe
            var clone = Clone();
            clone.Update();

            var section = new GoapLiveSection();
            var live = new ND_BehaviorTree.Editor.Plugins.ND_BTLiveContext(null, clone, m_Tree, 1, id => clone.nodes.FirstOrDefault(n => n.id == id));
            section.OnLiveUpdate(live);

            var texts = section.Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(texts, Has.Some.StartsWith("▶ Logs: [GetAxe] → Scout → Chop"), string.Join(" | ", texts));
            Assert.That(texts, Has.Some.Contains("started"), "history line");
            var marks = section.Query<Label>(className: "goap-row__mark").ToList().Select(l => l.text).ToList();
            CollectionAssert.AreEqual(new[] { "✗", "✓" }, marks, "Logs not met, Healthy met (runtime Health is 100)");

            section.OnLiveStopped();
            Assert.AreEqual(0, section.Query<Label>(className: "goap-row__mark").ToList().Count);
        }

        // ---------- editor: validation ----------

        [Test]
        public void Validator_ReportsConfigAndDesignProblems_ButNotChildOrder()
        {
            var planner = Planner(Goal("Logs", 1, Cond(m_Logs, ComparisonType.GreaterEqual, i: 10)), Goal("Healthy", 2, Cond(m_Health, ComparisonType.GreaterEqual, i: 50)));
            Woodcutter(planner);
            planner.children[0].position = new Rect(5000, 200, 100, 50); // order is irrelevant for a planner
            var loose = Create<GOAPActionNode>(900, 900);
            var issues = GraphValidator.Validate(m_Tree);

            Assert.IsFalse(issues.Any(i => i.Code == GoapPlugin.Misconfigured));
            Assert.IsTrue(issues.Any(i => i.Code == GoapPlugin.GoalKeyNeverChanged && i.Message.Contains("Health")), "no action changes Health");
            Assert.IsTrue(issues.Any(i => i.Code == GoapPlugin.ActionOutsidePlanner && i.Node == loose));
            Assert.IsFalse(issues.Any(i => i.Code == GraphValidator.OrderNotByPosition && i.Node == planner));

            planner.goals[0].conditions[0].key = null;
            Assert.IsTrue(GraphValidator.Validate(m_Tree).Any(i => i.Code == GoapPlugin.Misconfigured && i.Severity == GraphIssueSeverity.Error));
        }
    }
}
