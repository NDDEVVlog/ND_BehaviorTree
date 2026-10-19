using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    public class InitNodeTests
    {
        private GraphFixture f;
        private BehaviorTree m_Clone;

        [SetUp] public void SetUp() => f = new GraphFixture();

        [TearDown]
        public void TearDown()
        {
            if (m_Clone != null)
            {
                foreach (var n in m_Clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(m_Clone);
            }
            f.Destroy();
        }

        private (CountingActionNode main, InitNode init, CountingActionNode initAction) Build(Node.Status initResult, bool stopOnFailure = false)
        {
            var main = f.Create<CountingActionNode>(0);
            main.result = Node.Status.Success;
            f.Graph.Connect(f.Root, main);
            var init = f.Create<InitNode>(-400);
            init.stopTreeOnFailure = stopOnFailure;
            var initAction = f.Create<CountingActionNode>(-400, 200);
            initAction.result = initResult;
            f.Graph.Connect(init, initAction);
            return (main, init, initAction);
        }

        private T Runtime<T>(Node original) where T : Node => (T)m_Clone.nodes.Single(n => n.id == original.id);

        [Test]
        public void InstantInit_RunsOnceInRunInitialization_ThenNeverAgain()
        {
            var (main, _, initAction) = Build(Node.Status.Success);
            m_Clone = f.Tree.Clone();
            var rtInit = Runtime<CountingActionNode>(initAction);

            Assert.IsFalse(m_Clone.IsInitialized);
            Assert.AreEqual(Node.Status.Success, m_Clone.RunInitialization(), "what BehaviorTreeRunner.Start calls");
            Assert.IsTrue(m_Clone.IsInitialized);

            for (int i = 0; i < 5; i++) m_Clone.Update(); // main completes and restarts several times
            Assert.AreEqual(1, rtInit.processCount, "init never re-runs when the main tree restarts");
            Assert.AreEqual(5, Runtime<CountingActionNode>(main).processCount);
        }

        [Test]
        public void RunningInit_DelaysMainTree()
        {
            var (main, _, initAction) = Build(Node.Status.Running);
            m_Clone = f.Tree.Clone();
            var rtInit = Runtime<CountingActionNode>(initAction);
            var rtMain = Runtime<CountingActionNode>(main);

            m_Clone.RunInitialization();
            m_Clone.Update();
            Assert.AreEqual(0, rtMain.processCount, "main waits while init is running");

            rtInit.result = Node.Status.Success;
            m_Clone.Update();
            Assert.AreEqual(1, rtMain.processCount, "main starts in the tick init finishes");
        }

        [Test]
        public void FailedInit_StartsMain_WithWarning()
        {
            var (main, _, _) = Build(Node.Status.Failure);
            m_Clone = f.Tree.Clone();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Init branch .* failed; starting the main tree"));
            m_Clone.Update();
            Assert.AreEqual(1, Runtime<CountingActionNode>(main).processCount);
        }

        [Test]
        public void FailedInit_WithStopTreeOnFailure_StopsTree()
        {
            var (main, _, _) = Build(Node.Status.Failure, stopOnFailure: true);
            m_Clone = f.Tree.Clone();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("stopTreeOnFailure"));
            Assert.AreEqual(Node.Status.Failure, m_Clone.Update());
            Assert.AreEqual(Node.Status.Failure, m_Clone.Update());
            Assert.AreEqual(0, Runtime<CountingActionNode>(main).processCount);
        }

        [Test]
        public void TreeWithoutInit_IsInitializedImmediately()
        {
            var main = f.Create<CountingActionNode>(0);
            f.Graph.Connect(f.Root, main);
            m_Clone = f.Tree.Clone();
            Assert.IsTrue(m_Clone.IsInitialized);
            Assert.AreEqual(Node.Status.Success, m_Clone.RunInitialization());
        }

        [Test]
        public void OnlyOneInitNode_IsAllowed()
        {
            Build(Node.Status.Success);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already has an Init node"));
            Assert.IsNull(f.Graph.CreateNode(typeof(InitNode), Vector2.zero));

            var extra = ScriptableObject.CreateInstance<InitNode>();
            f.Tree.nodes.Add(extra); // bypassing the service, as a hand-edited asset would
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.InitMultiple));
        }

        [Test]
        public void InitBranch_IsNotReportedUnreachable()
        {
            var (_, init, initAction) = Build(Node.Status.Success);
            Assert.IsFalse(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.NodeUnreachable && (i.Node == init || i.Node == initAction)));
        }
    }

    public class LiveDebugTests
    {
        private GraphFixture f;
        private BehaviorTree m_Clone;

        [SetUp] public void SetUp() => f = new GraphFixture();

        [TearDown]
        public void TearDown()
        {
            if (m_Clone != null)
            {
                foreach (var s in m_Clone.nodes.OfType<SubtreeNode>().Where(s => s.RuntimeSubtree != null))
                {
                    foreach (var n in s.RuntimeSubtree.nodes) Object.DestroyImmediate(n);
                    Object.DestroyImmediate(s.RuntimeSubtree);
                }
                foreach (var n in m_Clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(m_Clone);
            }
            f.Destroy();
        }

        [Test]
        public void ProcessedNodes_RecordFrame_UnprocessedStayInactive()
        {
            var sel = f.Create<SelectorNode>(0);
            f.Graph.Connect(f.Root, sel);
            var running = f.Create<CountingActionNode>(100);
            running.result = Node.Status.Running;
            var never = f.Create<CountingActionNode>(300);
            f.Graph.Connect(sel, running);
            f.Graph.Connect(sel, never);

            m_Clone = f.Tree.Clone();
            m_Clone.Update();
            var lookup = LiveDebugUtility.BuildLookup(m_Clone);
            int frame = Time.frameCount;

            Assert.AreEqual(LiveState.Running, LiveDebugUtility.GetState(lookup[running.id], frame));
            Assert.AreEqual(LiveState.Running, LiveDebugUtility.GetState(lookup[sel.id], frame));
            Assert.AreEqual(LiveState.Inactive, LiveDebugUtility.GetState(lookup[never.id], frame));
            Assert.AreEqual(LiveState.Running, LiveDebugUtility.GetEdgeState(lookup[running.id], frame));
            Assert.AreSame(lookup[running.id], LiveDebugUtility.FindActiveLeaf(m_Clone, frame));
        }

        [Test]
        public void FinishedNodes_FadeOut_AfterRecentFrames()
        {
            var leaf = f.Create<CountingActionNode>(0);
            leaf.result = Node.Status.Failure;
            f.Graph.Connect(f.Root, leaf);
            m_Clone = f.Tree.Clone();
            m_Clone.Update();
            var rt = LiveDebugUtility.BuildLookup(m_Clone)[leaf.id];
            int frame = Time.frameCount;

            Assert.AreEqual(LiveState.Failed, LiveDebugUtility.GetState(rt, frame));
            Assert.AreEqual(LiveState.Inactive, LiveDebugUtility.GetState(rt, frame + LiveDebugUtility.RecentFrames + 1));
        }

        [Test]
        public void ActivePath_FollowsIntoSubtree()
        {
            var subtree = ScriptableObject.CreateInstance<BehaviorTree>();
            var subRoot = ScriptableObject.CreateInstance<RootNode>();
            var inner = ScriptableObject.CreateInstance<CountingActionNode>();
            inner.result = Node.Status.Running;
            inner.typeName = "InnerWork";
            subRoot.child = inner;
            subtree.nodes.Add(subRoot);
            subtree.nodes.Add(inner);
            subtree.rootNode = subRoot;

            var node = f.Create<SubtreeNode>(0);
            node.typeName = "Patrol";
            f.Graph.Connect(f.Root, node);
            f.Graph.AssignSubtree(node, subtree);

            try
            {
                m_Clone = f.Tree.Clone();
                m_Clone.Update();
                Assert.AreEqual("Patrol ▸ InnerWork", LiveDebugUtility.DescribeActivePath(m_Clone, Time.frameCount));
            }
            finally
            {
                Object.DestroyImmediate(inner);
                Object.DestroyImmediate(subRoot);
                Object.DestroyImmediate(subtree);
            }
        }
    }
}
