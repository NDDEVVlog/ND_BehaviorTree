using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ND_BehaviorTree.GOAP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ND_BehaviorTree.Editor.Tests
{
    [NodeInfo("Exit Counting Selector (test)", "Test/ExitCountingSelector", true, true)]
    public class ExitCountingSelector : SelectorNode
    {
        public int exits;
        protected override void OnExit() => exits++;
    }

    /// <summary>GOAP test precondition: met when worldState[key] is the bool 'expected'. Counts evaluations.</summary>
    [Serializable]
    public class FlagPrecondition : IGoapPrecondition
    {
        public string key;
        public bool expected = true;
        [NonSerialized] public int evaluations;

        public bool IsMet(GameObject agent, Blackboard blackboard, Dictionary<string, object> worldState)
        {
            evaluations++;
            return worldState.TryGetValue(key, out var v) && v is bool b && b == expected;
        }

        public string GetDescription() => $"{key} == {expected}";
    }

    /// <summary>Regression tests for the runtime node fixes (see the fix list in the report).</summary>
    public class NodeFixTests
    {
        private GraphFixture f;
        private readonly List<Object> m_Extra = new List<Object>();

        [SetUp] public void SetUp() => f = new GraphFixture();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Extra) if (o != null) Object.DestroyImmediate(o);
            m_Extra.Clear();
            f.Destroy();
        }

        // ---------------------------------------------------------------- helpers

        private T Track<T>(T o) where T : Object { m_Extra.Add(o); return o; }

        private BehaviorTree CloneTracked()
        {
            var clone = f.Tree.Clone();
            Track(clone);
            m_Extra.AddRange(clone.nodes);
            if (clone.blackboard != null && clone.blackboard != f.Tree.blackboard)
            {
                Track(clone.blackboard);
                m_Extra.AddRange(clone.blackboard.keys);
            }
            foreach (var s in clone.nodes.OfType<SubtreeNode>().Where(s => s.RuntimeSubtree != null))
            {
                Track(s.RuntimeSubtree);
                m_Extra.AddRange(s.RuntimeSubtree.nodes);
            }
            return clone;
        }

        private static T Runtime<T>(BehaviorTree clone, Node original) where T : Node
            => (T)clone.nodes.Single(n => n.id == original.id);

        private TKey MakeKey<TKey>(string keyName) where TKey : Key
        {
            var key = Track(ScriptableObject.CreateInstance<TKey>());
            key.keyName = keyName;
            key.name = keyName;
            return key;
        }

        private Blackboard MakeBlackboard(params Key[] keys)
        {
            var bb = Track(ScriptableObject.CreateInstance<Blackboard>());
            bb.name = "TestBB";
            bb.keys.AddRange(keys);
            f.Tree.blackboard = bb;
            return bb;
        }

        private static int CountLogs(LogType type, string contains, Action action)
        {
            int count = 0;
            Application.LogCallback handler = (msg, stack, t) => { if (t == type && msg.Contains(contains)) count++; };
            Application.logMessageReceived += handler;
            try { action(); }
            finally { Application.logMessageReceived -= handler; }
            return count;
        }

        private static int InvocationCount(Node node) => node.InteruptAction?.GetInvocationList().Length ?? 0;

        private static void InvokeProtected(Node node, string method)
        {
            var mi = node.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi, method);
            mi.Invoke(node, null);
        }

        // ---------------------------------------------------------------- #2 Parallel

        [Test]
        public void Parallel_RequireAllBoth_MixedResults_FailsInsteadOfRunningForever()
        {
            var par = f.Create<ParallelNode>(0);
            par.successPolicy = ParallelNode.Policy.RequireAll;
            par.failurePolicy = ParallelNode.Policy.RequireAll;
            f.Graph.Connect(f.Root, par);
            var a = f.Create<FixedStatusNode>(100); a.result = Node.Status.Success;
            var b = f.Create<FixedStatusNode>(300); b.result = Node.Status.Failure;
            f.Graph.Connect(par, a);
            f.Graph.Connect(par, b);

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Failure, clone.Update());
        }

        [Test]
        public void Parallel_Finishing_ResetsStillRunningSiblings()
        {
            var par = f.Create<ParallelNode>(0);
            par.successPolicy = ParallelNode.Policy.RequireOne;
            f.Graph.Connect(f.Root, par);
            var done = f.Create<FixedStatusNode>(100); done.result = Node.Status.Success;
            var running = f.Create<CountingActionNode>(300); running.result = Node.Status.Running;
            f.Graph.Connect(par, done);
            f.Graph.Connect(par, running);

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Success, clone.Update());

            var rtRunning = Runtime<CountingActionNode>(clone, running);
            Assert.AreEqual(1, rtRunning.processCount);
            Assert.IsFalse(rtRunning.IsRunning(), "a sibling still running when the parallel finishes must be reset");
            Assert.AreEqual(Node.Status.None, rtRunning.status);
        }

        [Test]
        public void Parallel_AllRunning_StaysRunning()
        {
            var par = f.Create<ParallelNode>(0);
            f.Graph.Connect(f.Root, par);
            var a = f.Create<CountingActionNode>(100); a.result = Node.Status.Running;
            var b = f.Create<CountingActionNode>(300); b.result = Node.Status.Running;
            f.Graph.Connect(par, a);
            f.Graph.Connect(par, b);

            Assert.AreEqual(Node.Status.Running, CloneTracked().Update());
        }

        // ---------------------------------------------------------------- Selector double OnExit

        [Test]
        public void Selector_ChildSucceeds_OnExitRunsOnce()
        {
            var sel = f.Create<ExitCountingSelector>(0);
            f.Graph.Connect(f.Root, sel);
            var leaf = f.Create<FixedStatusNode>(100); leaf.result = Node.Status.Success;
            f.Graph.Connect(sel, leaf);

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Success, clone.Update());
            Assert.AreEqual(1, Runtime<ExitCountingSelector>(clone, sel).exits);
        }

        [Test]
        public void Selector_AllChildrenFail_OnExitRunsOnce()
        {
            var sel = f.Create<ExitCountingSelector>(0);
            f.Graph.Connect(f.Root, sel);
            var leaf = f.Create<FixedStatusNode>(100); leaf.result = Node.Status.Failure;
            f.Graph.Connect(sel, leaf);

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Running, clone.Update(), "one child per tick (timing unchanged)");
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.AreEqual(1, Runtime<ExitCountingSelector>(clone, sel).exits);
        }

        // ---------------------------------------------------------------- #3 Cooldown

        [Test]
        public void Cooldown_SurvivesTreeRestartReset()
        {
            var leaf = f.Create<CountingActionNode>(0);
            leaf.result = Node.Status.Success;
            f.Graph.Connect(f.Root, leaf);
            var cooldown = (CooldownNode)f.Graph.AddDecorator(leaf, typeof(CooldownNode));
            cooldown.cooldownDuration = 1000f;

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Success, clone.Update());
            // The tree finished, so the next Update resets the whole main branch (incl. the cooldown decorator).
            Assert.AreEqual(Node.Status.Failure, clone.Update(), "cooldown must survive the reset");
            Assert.AreEqual(1, Runtime<CountingActionNode>(clone, leaf).processCount, "child must not run during cooldown");
        }

        // ---------------------------------------------------------------- #4 CheckConditionVariable / CheckConditionNode

        [Test]
        public void CheckConditionVariable_ConditionTurnsFalse_ResetsRunningChild()
        {
            var go = MakeKey<BoolKey>("Go");
            go.SetValue(true);
            MakeBlackboard(go);

            var leaf = f.Create<CountingActionNode>(0);
            leaf.result = Node.Status.Running;
            f.Graph.Connect(f.Root, leaf);
            var deco = (CheckConditionVariable)f.Graph.AddDecorator(leaf, typeof(CheckConditionVariable));
            deco.conditionKey = MakeKey<BoolKey>("Go");

            var clone = CloneTracked();
            var rtLeaf = Runtime<CountingActionNode>(clone, leaf);
            Assert.AreEqual(Node.Status.Running, clone.Update());
            Assert.IsTrue(rtLeaf.IsRunning());

            clone.blackboard.SetValue("Go", false);
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.IsFalse(rtLeaf.IsRunning(), "child must be reset (OnExit) when the condition fails");
            Assert.AreEqual(Node.Status.None, rtLeaf.status);
        }

        [Test]
        public void CheckConditionVariable_NullChild_FalseCondition_DoesNotThrow()
        {
            var go = MakeKey<BoolKey>("Go");
            var bb = MakeBlackboard(go);
            f.Tree.blackboard = bb;

            var deco = Track(ScriptableObject.CreateInstance<CheckConditionVariable>());
            deco.conditionKey = go;
            deco.SetOwnerTree(f.Tree);
            Assert.AreEqual(Node.Status.Failure, deco.Process());
        }

        [Test]
        public void CheckConditionNode_ConditionTurnsFalse_ResetsRunningChild()
        {
            var hp = MakeKey<FloatKey>("Health");
            hp.SetValue(10f);
            MakeBlackboard(hp);

            var leaf = f.Create<CountingActionNode>(0);
            leaf.result = Node.Status.Running;
            f.Graph.Connect(f.Root, leaf);
            var deco = (CheckConditionNode)f.Graph.AddDecorator(leaf, typeof(CheckConditionNode));
            deco.value = MakeKey<FloatKey>("Health");
            deco.compareType = MakeKey<ComparisonTypeKey>(string.Empty);
            deco.compareType.SetValue(ComparisonType.Greater);
            var threshold = MakeKey<FloatKey>(string.Empty); // inline value
            threshold.SetValue(0f);
            deco.targetValue = threshold;

            var clone = CloneTracked();
            var rtLeaf = Runtime<CountingActionNode>(clone, leaf);
            Assert.AreEqual(Node.Status.Running, clone.Update());

            clone.blackboard.SetValue("Health", -1f);
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.IsFalse(rtLeaf.IsRunning());
        }

        // ---------------------------------------------------------------- #5 Interrupt subscriptions

        [Test]
        public void MoveToTransform_InterruptSubscription_DoesNotAccumulate()
        {
            var node = Track(ScriptableObject.CreateInstance<MoveToTransformNode>());
            for (int i = 0; i < 3; i++) InvokeProtected(node, "OnEnter");
            Assert.AreEqual(1, InvocationCount(node), "re-entering must not add handlers");
            InvokeProtected(node, "OnExit");
            Assert.AreEqual(0, InvocationCount(node), "OnExit unsubscribes");
        }

        [Test]
        public void MoveToTransform_RunThroughTree_LeavesNoHandlers()
        {
            var move = f.Create<MoveToTransformNode>(0);
            f.Graph.Connect(f.Root, move);
            var clone = CloneTracked();
            var rt = Runtime<MoveToTransformNode>(clone, move);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Node.Status.Failure, clone.Update()); // no agent configured
                Assert.AreEqual(0, InvocationCount(rt));
            }
        }

        [Test]
        public void RandomPatrol_OnInterupt_KeepsOtherSubscribers()
        {
            var node = Track(ScriptableObject.CreateInstance<RandomPatrolNode>());
            int calls = 0;
            node.InteruptAction += () => calls++;
            node.OnInterupt();
            node.Interrupt();
            Assert.AreEqual(1, calls, "OnInterupt must not wipe handlers added by other systems");
        }

        // ---------------------------------------------------------------- #6 Blackboard

        [Test]
        public void Blackboard_TryGetKey_FindsAddedRenamedAndReplacedKeys()
        {
            var a = MakeKey<FloatKey>("A");
            var bb = MakeBlackboard(a);

            Assert.IsTrue(bb.TryGetKey("A", out var found));
            Assert.AreSame(a, found);
            Assert.IsFalse(bb.TryGetKey("Missing", out _));
            Assert.IsFalse(bb.TryGetKey(null, out _));

            var b = MakeKey<IntKey>("B");
            bb.keys.Add(b); // count changes
            Assert.IsTrue(bb.TryGetKey("B", out found));
            Assert.AreSame(b, found);

            a.keyName = "A2"; // renamed in place (same count)
            Assert.IsFalse(bb.TryGetKey("A", out _));
            Assert.IsTrue(bb.TryGetKey("A2", out found));
            Assert.AreSame(a, found);

            var replacement = MakeKey<FloatKey>("A2"); // replaced in place (same count), old one destroyed
            bb.keys[0] = replacement;
            Object.DestroyImmediate(a);
            Assert.IsTrue(bb.TryGetKey("A2", out found));
            Assert.AreSame(replacement, found);
        }

        [Test]
        public void Blackboard_GetSetValue_WorkThroughCache_AndCloneHasOwnKeys()
        {
            var speed = MakeKey<FloatKey>("Speed");
            var bb = MakeBlackboard(speed);
            Assert.IsTrue(bb.SetValue("Speed", 3f));
            Assert.AreEqual(3f, bb.GetValue<float>("Speed"));
            Assert.IsTrue(bb.SetValueObject("Speed", 4f));
            Assert.AreEqual(4f, speed.GetValue());

            var clone = Track(bb.Clone());
            m_Extra.AddRange(clone.keys);
            Assert.IsTrue(clone.TryGetKey("Speed", out var cloneKey));
            Assert.AreNotSame(speed, cloneKey);
            clone.SetValue("Speed", 9f);
            Assert.AreEqual(4f, speed.GetValue(), "the clone's cache must point at its own keys");
        }

        [Test]
        public void Blackboard_MissingKeyWarning_LoggedOncePerKey()
        {
            var bb = MakeBlackboard(MakeKey<FloatKey>("A"));
            int warnings = CountLogs(LogType.Warning, "'Nope' not found", () =>
            {
                for (int i = 0; i < 5; i++) bb.GetValue<float>("Nope");
                bb.SetValue("Nope", 1f);
            });
            Assert.AreEqual(1, warnings);
        }

        // ---------------------------------------------------------------- #7 Clear / DebugLog with non-object keys

        [Test]
        public void ClearKey_WorksForValueAndReferenceKeys()
        {
            var speed = MakeKey<FloatKey>("Speed"); speed.SetValue(5f);
            var label = MakeKey<StringKey>("Label"); label.SetValue("x");
            MakeBlackboard(speed, label);

            var seq = f.Create<SequenceNode>(0);
            f.Graph.Connect(f.Root, seq);
            var clearSpeed = f.Create<ClearBlackboardKeyNode>(100);
            clearSpeed.keyToClear = MakeKey<FloatKey>("Speed");
            var clearLabel = f.Create<ClearBlackboardKeyNode>(300);
            clearLabel.keyToClear = MakeKey<StringKey>("Label");
            f.Graph.Connect(seq, clearSpeed);
            f.Graph.Connect(seq, clearLabel);

            var clone = CloneTracked();
            Assert.AreEqual(Node.Status.Running, clone.Update());
            Assert.AreEqual(Node.Status.Success, clone.Update());
            Assert.AreEqual(0f, clone.blackboard.GetValue<float>("Speed"));
            Assert.IsNull(clone.blackboard.GetValue<string>("Label"));
        }

        [Test]
        public void ClearKey_MissingKey_Fails()
        {
            MakeBlackboard(MakeKey<FloatKey>("Speed"));
            var clear = f.Create<ClearBlackboardKeyNode>(0);
            clear.keyToClear = MakeKey<FloatKey>("Nope");
            f.Graph.Connect(f.Root, clear);
            Assert.AreEqual(Node.Status.Failure, CloneTracked().Update());
        }

        [Test]
        public void DebugLog_LogsNonObjectKey_AndTreatsNoneAsSuccess()
        {
            var speed = MakeKey<FloatKey>("Speed"); speed.SetValue(5f);
            MakeBlackboard(speed);
            var log = f.Create<DebugLogNode>(0);
            log.keyToLog = MakeKey<FloatKey>("Speed");
            log.returningStatus = Node.Status.None;
            f.Graph.Connect(f.Root, log);

            LogAssert.Expect(LogType.Log, new Regex("Key 'Speed': 5"));
            Assert.AreEqual(Node.Status.Success, CloneTracked().Update());
        }

        // ---------------------------------------------------------------- #8 SetXxxValue strategies

        [Test]
        public void SetStringValue_WritesConfiguredValue()
        {
            var label = MakeKey<StringKey>("Label"); label.SetValue("old");
            var bb = MakeBlackboard(label);
            var strategy = new SetStringValue { stringKey = MakeKey<StringKey>("Label"), stringValue = "new" };
            Assert.IsTrue(strategy.TrySetValue(null, bb));
            Assert.AreEqual("new", label.GetValue());
        }

        [Test]
        public void SetValueStrategies_NullKey_ReturnFalse()
        {
            var bb = MakeBlackboard();
            Assert.IsFalse(new SetStringValue().TrySetValue(null, bb));
            Assert.IsFalse(new SetFloatValue().TrySetValue(null, bb));
            Assert.IsFalse(new SetIntValue().TrySetValue(null, bb));
            Assert.IsFalse(new SetIntValueIncrement().TrySetValue(null, bb));
        }

        [Test]
        public void SetBooleanValue_MissingKey_Fails()
        {
            MakeBlackboard(MakeKey<FloatKey>("Speed"));
            var set = f.Create<SetBooleanValueNode>(0);
            set.conditionKey = MakeKey<BoolKey>("Nope");
            f.Graph.Connect(f.Root, set);
            Assert.AreEqual(Node.Status.Failure, CloneTracked().Update());
        }

        // ---------------------------------------------------------------- #9 RandomRateSelector

        [Test]
        public void RandomRateSelector_AllZeroPriorities_LogsErrorOnce_AndFails()
        {
            var rrs = f.Create<RandomRateSelectorNode>(0);
            f.Graph.Connect(f.Root, rrs);
            f.Graph.Connect(rrs, f.Create<FixedStatusNode>(100));
            f.Graph.Connect(rrs, f.Create<FixedStatusNode>(300));

            var clone = CloneTracked();
            LogAssert.Expect(LogType.Error, new Regex("priority > 0"));
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.AreEqual(Node.Status.Failure, clone.Update()); // second run: no second error
        }

        [TestCase(RandomRateSelectorNode.WeightingMode.LowerIsBetter)]
        [TestCase(RandomRateSelectorNode.WeightingMode.HigherIsBetter)]
        public void RandomRateSelector_PositivePriorities_AlwaysPicks_EvenThroughAttachedDecorator(RandomRateSelectorNode.WeightingMode mode)
        {
            var rrs = f.Create<RandomRateSelectorNode>(0);
            rrs.weightingMode = mode;
            f.Graph.Connect(f.Root, rrs);
            var a = f.Create<FixedStatusNode>(100); a.priority = 1;
            var b = f.Create<FixedStatusNode>(300); b.priority = 3;
            f.Graph.Connect(rrs, a);
            f.Graph.Connect(rrs, b);
            f.Graph.AddDecorator(b, typeof(SucceederNode)); // runtime child = decorator entry (priority 0 itself)

            var clone = CloneTracked();
            for (int i = 0; i < 50; i++) Assert.AreEqual(Node.Status.Success, clone.Update());
        }

        // ---------------------------------------------------------------- #10 GOAP

        private GOAPActionNode MakeAction(string effectKey, bool effectValue)
        {
            var action = Track(ScriptableObject.CreateInstance<GOAPActionNode>());
            action.preconditions = new List<IGoapPrecondition>();
            action.effects = new List<GOAPState> { new GOAPState { key = effectKey, valueType = GOAPValueType.Bool, boolValue = effectValue } };
            return action;
        }

        [Test]
        public void Planner_GoalAlreadySatisfied_ReturnsEmptyPlan()
        {
            var goal = new List<IGoapPrecondition> { new FlagPrecondition { key = "x", expected = true } };
            var plan = new Planner().FindPlan(null, null, new Dictionary<string, object> { { "x", true } }, goal, new List<GOAPActionNode>());
            Assert.IsNotNull(plan);
            Assert.AreEqual(0, plan.Count);
        }

        [Test]
        public void Planner_FindsPlan()
        {
            var setX = MakeAction("x", true);
            var goal = new List<IGoapPrecondition> { new FlagPrecondition { key = "x", expected = true } };
            var plan = new Planner().FindPlan(null, null, new Dictionary<string, object> { { "x", false } }, goal, new List<GOAPActionNode> { setX });
            CollectionAssert.AreEqual(new[] { setX }, plan);
        }

        [Test]
        public void Planner_UnreachableGoal_TerminatesWithoutExhaustingIterations()
        {
            var actions = new List<GOAPActionNode> { MakeAction("x", true), MakeAction("x", false) };
            var unreachable = new FlagPrecondition { key = "y", expected = true };

            LogAssert.Expect(LogType.Warning, new Regex("No valid plan found"));
            var plan = new Planner().FindPlan(null, null, new Dictionary<string, object> { { "x", false } },
                new List<IGoapPrecondition> { unreachable }, actions);

            Assert.IsNull(plan);
            Assert.Less(unreachable.evaluations, 20, "visited states must not be re-expanded");
        }

        [Test]
        public void GOAPPlannerNode_GoalAlreadySatisfied_Succeeds()
        {
            var x = MakeKey<BoolKey>("x"); x.SetValue(true);
            MakeBlackboard(x);
            var planner = f.Create<GOAPPlannerNode>(0);
            planner.goal = new List<IGoapPrecondition> { new FlagPrecondition { key = "x", expected = true } };
            f.Graph.Connect(f.Root, planner);

            Assert.AreEqual(Node.Status.Success, CloneTracked().Update());
        }

        [Test]
        public void GOAPActionNode_Clone_DoesNotCloneChild_TreeCloneRewiresIt()
        {
            var action = Track(ScriptableObject.CreateInstance<GOAPActionNode>());
            action.child = Track(ScriptableObject.CreateInstance<FixedStatusNode>());
            var clone = Track((GOAPActionNode)action.Clone());
            Assert.IsNull(clone.child);

            var planner = f.Create<GOAPPlannerNode>(0);
            var graphAction = f.Create<GOAPActionNode>(100);
            var leaf = f.Create<FixedStatusNode>(100, 200);
            Assert.IsTrue(f.Graph.Connect(f.Root, planner));
            Assert.IsTrue(f.Graph.Connect(planner, graphAction));
            Assert.IsTrue(f.Graph.Connect(graphAction, leaf));

            var tree = CloneTracked();
            var rtAction = Runtime<GOAPActionNode>(tree, graphAction);
            Assert.AreSame(Runtime<FixedStatusNode>(tree, leaf), rtAction.child);
        }

        // ---------------------------------------------------------------- #11 Runner cleanup

        [Test]
        public void Runner_OnDestroy_DestroysRuntimeClones_NotAssets()
        {
            var assetKey = MakeKey<FloatKey>("Speed");
            var assetBb = MakeBlackboard(assetKey);

            var sub = Track(ScriptableObject.CreateInstance<BehaviorTree>());
            var subRoot = Track(ScriptableObject.CreateInstance<RootNode>());
            var subLeaf = Track(ScriptableObject.CreateInstance<FixedStatusNode>());
            subRoot.child = subLeaf;
            sub.nodes.Add(subRoot);
            sub.nodes.Add(subLeaf);
            sub.rootNode = subRoot;

            var subNode = f.Create<SubtreeNode>(0);
            f.Graph.Connect(f.Root, subNode);
            Assert.IsTrue(f.Graph.AssignSubtree(subNode, sub));

            var go = Track(new GameObject("RunnerCleanupTest"));
            var runner = go.AddComponent<BehaviorTreeRunner>();
            runner.treeAsset = f.Tree;
            typeof(BehaviorTreeRunner).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runner, null);

            var rt = runner.RuntimeTree;
            Assert.IsNotNull(rt);
            var runtimeObjects = new List<Object> { rt, rt.blackboard };
            runtimeObjects.AddRange(rt.nodes);
            runtimeObjects.AddRange(rt.blackboard.keys);
            var rtSub = ((SubtreeNode)rt.nodes.Single(n => n.id == subNode.id)).RuntimeSubtree;
            Assert.AreSame(rt.blackboard, rtSub.blackboard, "precondition: subtree shares the runtime blackboard");
            runtimeObjects.Add(rtSub);
            runtimeObjects.AddRange(rtSub.nodes);
            m_Extra.AddRange(runtimeObjects); // safety net if the test fails before OnDestroy

            typeof(BehaviorTreeRunner).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runner, null);

            Assert.IsNull(runner.RuntimeTree);
            for (int i = 0; i < runtimeObjects.Count; i++) Assert.IsTrue(runtimeObjects[i] == null, $"runtime object #{i} was not destroyed");
            Assert.IsTrue(f.Tree != null && f.Root != null && subNode != null, "tree asset must survive");
            Assert.IsTrue(assetBb != null && assetKey != null, "blackboard asset must survive");
            Assert.IsTrue(sub != null && subRoot != null && subLeaf != null, "subtree asset must survive");
        }

        // ---------------------------------------------------------------- #12 PatrolService

        [Test]
        public void PatrolService_DoesNotThrow_LogsErrorOnce()
        {
            var leaf = f.Create<FixedStatusNode>(0);
            f.Graph.Connect(f.Root, leaf);
            f.Graph.AddService(leaf, typeof(PatrolService));

            var clone = CloneTracked();
            LogAssert.Expect(LogType.Error, new Regex("PatrolService"));
            Assert.AreEqual(Node.Status.Success, clone.Update());
            Assert.AreEqual(Node.Status.Success, clone.Update());
        }

        // ---------------------------------------------------------------- #14 DynamicMethodHub

        [Test]
        public void DynamicMethodHub_NoHub_FailsWithSingleError()
        {
            var node = f.Create<DynamicMethodHubComponentNode>(0);
            node.eventID = "Anything";
            f.Graph.Connect(f.Root, node);

            var clone = CloneTracked();
            clone.Self = Track(new GameObject("NoHubAgent"));
            LogAssert.Expect(LogType.Error, new Regex("no MethodHub found"));
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.AreEqual(Node.Status.Failure, clone.Update());
        }

        [Test]
        public void DynamicMethodHub_NoSelf_FailsWithoutException()
        {
            var node = f.Create<DynamicMethodHubComponentNode>(0);
            f.Graph.Connect(f.Root, node);
            var clone = CloneTracked();
            LogAssert.Expect(LogType.Error, new Regex("no MethodHub found"));
            Assert.AreEqual(Node.Status.Failure, clone.Update());
        }

        // ---------------------------------------------------------------- #15 minor

        [Test]
        public void ComparisonHelper_DestroyedUnityObject_CountsAsNull()
        {
            var go = new GameObject("Destroyed");
            Object.DestroyImmediate(go);
            Assert.IsTrue(ComparisonHelper.PerformComparison(go, null, ComparisonType.Equal));
            Assert.IsFalse(ComparisonHelper.PerformComparison(go, null, ComparisonType.NotEqual));
        }

        [Test]
        public void ComparisonHelper_MismatchedTypes_NoException()
        {
            Assert.IsFalse(ComparisonHelper.PerformComparison("a", true, ComparisonType.Equal));
            Assert.IsTrue(ComparisonHelper.PerformComparison("a", true, ComparisonType.NotEqual));
            Assert.IsTrue(ComparisonHelper.PerformComparison("b", "a", ComparisonType.Greater));
            Assert.IsTrue(ComparisonHelper.PerformComparison(2, 1.5f, ComparisonType.Greater));
        }

        [Test]
        public void Decorators_WithoutChild_KeepResults_AndWarnOnce()
        {
            var inverter = Track(ScriptableObject.CreateInstance<InverterNode>());
            var timeLimit = Track(ScriptableObject.CreateInstance<TimeLimitNode>());
            int warnings = CountLogs(LogType.Warning, "has no child", () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreEqual(Node.Status.Success, inverter.Process());
                    Assert.AreEqual(Node.Status.Failure, timeLimit.Process());
                }
            });
            Assert.AreEqual(2, warnings);
        }

        [Test]
        public void PlayAudioSource_NullKey_FailsWithSingleError()
        {
            MakeBlackboard();
            var play = f.Create<PlayAudioSourceNode>(0);
            f.Graph.Connect(f.Root, play);
            var clone = CloneTracked();
            LogAssert.Expect(LogType.Error, new Regex("audioSourceKey is not assigned"));
            Assert.AreEqual(Node.Status.Failure, clone.Update());
            Assert.AreEqual(Node.Status.Failure, clone.Update());
        }
    }
}
