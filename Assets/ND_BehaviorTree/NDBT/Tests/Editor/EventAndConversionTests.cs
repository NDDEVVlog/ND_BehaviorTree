using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    [NodeInfo("Counting Action (test)", "Test/CountingAction", true, false)]
    public class CountingActionNode : ActionNode
    {
        public Status result = Status.Success;
        public int processCount;
        protected override Status OnProcess()
        {
            processCount++;
            return result;
        }
    }

    public class EventNodeTests
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

        private (CountingActionNode main, EventNode ev, CountingActionNode handler) Build(Node.Status mainResult, Node.Status handlerResult, bool interrupt = false)
        {
            var main = f.Create<CountingActionNode>(0);
            main.result = mainResult;
            f.Graph.Connect(f.Root, main);

            var ev = f.Create<EventNode>(400);
            ev.eventName = "Alarm";
            ev.interruptMainTree = interrupt;
            var handler = f.Create<CountingActionNode>(400, 200);
            handler.result = handlerResult;
            f.Graph.Connect(ev, handler);
            return (main, ev, handler);
        }

        private T Runtime<T>(Node original) where T : Node => (T)m_Clone.nodes.Single(n => n.id == original.id);

        [Test]
        public void EventBranch_RunsOnlyWhenSent()
        {
            var (_, _, handler) = Build(Node.Status.Running, Node.Status.Success);
            m_Clone = f.Tree.Clone();
            var rtHandler = Runtime<CountingActionNode>(handler);

            m_Clone.Update();
            m_Clone.Update();
            Assert.AreEqual(0, rtHandler.processCount, "not sent → never runs");

            Assert.IsTrue(m_Clone.SendEvent("Alarm"));
            Assert.AreEqual(0, rtHandler.processCount, "starts on the next Update, not inside SendEvent");
            m_Clone.Update();
            Assert.AreEqual(1, rtHandler.processCount);
            m_Clone.Update();
            Assert.AreEqual(1, rtHandler.processCount, "finished with Success → stops");
            Assert.IsFalse(m_Clone.IsEventRunning("Alarm"));
        }

        [Test]
        public void AlongsideMode_MainTreeKeepsRunning()
        {
            var (main, _, handler) = Build(Node.Status.Running, Node.Status.Running);
            m_Clone = f.Tree.Clone();
            m_Clone.SendEvent("Alarm");
            m_Clone.Update();
            m_Clone.Update();

            Assert.AreEqual(2, Runtime<CountingActionNode>(handler).processCount);
            Assert.AreEqual(2, Runtime<CountingActionNode>(main).processCount);
        }

        [Test]
        public void InterruptMode_PausesMainTree_UntilEventFinishes()
        {
            var (main, _, handler) = Build(Node.Status.Running, Node.Status.Running, interrupt: true);
            m_Clone = f.Tree.Clone();
            var rtMain = Runtime<CountingActionNode>(main);
            var rtHandler = Runtime<CountingActionNode>(handler);

            m_Clone.Update();
            Assert.AreEqual(1, rtMain.processCount);

            m_Clone.SendEvent("Alarm");
            m_Clone.Update();
            m_Clone.Update();
            Assert.AreEqual(1, rtMain.processCount, "main paused while the interrupting event runs");
            Assert.AreEqual(2, rtHandler.processCount);

            rtHandler.result = Node.Status.Success;
            m_Clone.Update(); // the event finishes first in this tick, so the main tree runs again in the same tick
            Assert.AreEqual(2, rtMain.processCount, "main resumes once the event is no longer running");
        }

        [Test]
        public void SendWhileRunning_IsIgnored_UnlessRestart()
        {
            var (_, ev, handler) = Build(Node.Status.Running, Node.Status.Running);
            m_Clone = f.Tree.Clone();
            var rtHandler = Runtime<CountingActionNode>(handler);

            m_Clone.SendEvent("Alarm");
            m_Clone.Update();
            Assert.IsTrue(m_Clone.IsEventRunning("Alarm"));
            Assert.IsTrue(m_Clone.SendEvent("Alarm"), "known name → true even when ignored");
            m_Clone.Update();
            Assert.AreEqual(2, rtHandler.processCount, "kept running, no restart");
        }

        [Test]
        public void UnknownEvent_ReturnsFalse_WithWarning()
        {
            Build(Node.Status.Success, Node.Status.Success);
            m_Clone = f.Tree.Clone();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no EventNode named 'Nope'"));
            Assert.IsFalse(m_Clone.SendEvent("Nope"));
        }

        [Test]
        public void SendEventOnAsset_IsAnError()
        {
            Build(Node.Status.Success, Node.Status.Success);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("called on asset"));
            Assert.IsFalse(f.Tree.SendEvent("Alarm"));
        }

        [Test]
        public void Validator_EventBranchIsReachable_AndNameRules()
        {
            var (_, ev, handler) = Build(Node.Status.Success, Node.Status.Success);
            var issues = GraphValidator.Validate(f.Tree);
            Assert.IsFalse(issues.Any(i => i.Code == GraphValidator.NodeUnreachable && (i.Node == handler || i.Node == ev)));

            ev.eventName = "";
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.EventNameEmpty));
        }

        [Test]
        public void EventNode_CannotBeAChild()
        {
            var seq = f.Create<SequenceNode>(0);
            var ev = f.Create<EventNode>(100);
            Assert.IsFalse(f.Graph.CanConnect(seq, ev, out var reason));
            StringAssert.Contains("no input port", reason);
        }
    }

    public class ConvertToSubtreeTests
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

        private void Track(BehaviorTree tree)
        {
            m_Extra.Add(tree);
            foreach (var n in tree.nodes) { m_Extra.Add(n); m_Extra.AddRange(n.decorators); m_Extra.AddRange(n.services); }
        }

        [Test]
        public void Branch_IsMoved_AndReplacedAtSameIndex_WithSameRuntimeResult()
        {
            var sel = f.Create<SelectorNode>(0);
            f.Graph.Connect(f.Root, sel);
            var first = f.Create<FixedStatusNode>(-200, 200);
            first.result = Node.Status.Failure;
            var seq = f.Create<SequenceNode>(200, 200);
            var a = f.Create<FixedStatusNode>(150, 400);
            var b = f.Create<FixedStatusNode>(300, 400);
            f.Graph.Connect(sel, first);
            f.Graph.Connect(sel, seq);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);
            f.Graph.AddDecorator(seq, typeof(InverterNode)); // Inverter(Success) → Failure

            var before = Run(f.Tree);
            var node = f.Graph.ConvertToSubtree(new Node[] { seq, a, b }, null, out var error);
            Assert.IsNull(error);
            Track(node.subtree);

            Assert.AreEqual(2, sel.children.Count);
            Assert.AreSame(node, sel.children[1], "subtree node takes the branch's child index");
            Assert.IsFalse(f.Tree.nodes.Contains(seq) || f.Tree.nodes.Contains(a) || f.Tree.nodes.Contains(b));

            var subRootChild = (SequenceNode)node.subtree.rootNode.child;
            Assert.AreEqual(2, subRootChild.children.Count);
            Assert.AreEqual(1, subRootChild.decorators.Count, "decorators move with the top node");
            Assert.AreSame(f.Tree.blackboard, node.subtree.blackboard);

            Assert.AreEqual(before, Run(f.Tree), "same runtime result after conversion");
        }

        [Test]
        public void Rejects_OpenBranch_MultipleTops_Root_Events()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(0);
            var lone = f.Create<WaitNode>(500);
            f.Graph.Connect(seq, a);
            var ev = f.Create<EventNode>(800);

            Assert.IsNull(f.Graph.ConvertToSubtree(new Node[] { seq }, null, out var e1));
            StringAssert.Contains("whole branch", e1);
            Assert.IsNull(f.Graph.ConvertToSubtree(new Node[] { seq, a, lone }, null, out var e2));
            StringAssert.Contains("single top node", e2);
            Assert.IsNull(f.Graph.ConvertToSubtree(new Node[] { f.Root }, null, out var e3));
            StringAssert.Contains("Root", e3);
            Assert.IsNull(f.Graph.ConvertToSubtree(new Node[] { ev }, null, out var e4));
            StringAssert.Contains("event", e4);
        }

        [Test]
        public void Conversion_IsOneUndoStep_ForTheHostTree()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(0, 200);
            f.Graph.Connect(f.Root, seq);
            f.Graph.Connect(seq, a);
            string seqId = seq.id;

            var node = f.Graph.ConvertToSubtree(new Node[] { seq, a }, null, out _);
            Track(node.subtree);

            GraphFixture.UndoOnce();
            var restored = f.Tree.nodes.FirstOrDefault(n => n != null && n.id == seqId);
            Assert.IsNotNull(restored, "branch restored");
            Assert.AreSame(restored, f.Root.child);
            Assert.IsFalse(f.Tree.nodes.Any(n => n is SubtreeNode));
        }

        private Node.Status Run(BehaviorTree tree)
        {
            var clone = tree.Clone();
            try { return clone.Update(); }
            finally
            {
                foreach (var s in clone.nodes.OfType<SubtreeNode>().Where(s => s.RuntimeSubtree != null))
                {
                    foreach (var n in s.RuntimeSubtree.nodes) Object.DestroyImmediate(n);
                    Object.DestroyImmediate(s.RuntimeSubtree);
                }
                foreach (var n in clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(clone);
            }
        }
    }

    /// <summary>Groups and notes must reach the .asset file on disk.</summary>
    public class AnnotationPersistenceTests
    {
        private const string Folder = "Assets/__NDBT_TestTemp_Annotations";
        private string m_Path;
        private BehaviorTree m_Tree;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__NDBT_TestTemp_Annotations");
            m_Path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Tree.asset");
            m_Tree = ScriptableObject.CreateInstance<BehaviorTree>();
            AssetDatabase.CreateAsset(m_Tree, m_Path);
            m_Tree.EditorInit();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void GroupAndNote_AreWrittenToDisk_AndReloaded()
        {
            var graph = new BehaviorTreeGraphService(m_Tree);
            var seq = graph.CreateNode(typeof(SequenceNode), new Vector2(10, 10));
            var group = graph.CreateGroup("PersistedGroup", new Rect(0, 0, 300, 200), new Node[] { seq });
            var note = graph.CreateNote(new Rect(400, 0, 200, 100), "PersistedNote", "first");
            graph.UpdateNote(note, note.title, "edited body", Color.cyan, note.rect);
            graph.Save();

            string text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), m_Path));
            StringAssert.Contains("PersistedGroup", text);
            StringAssert.Contains(seq.id, text);
            StringAssert.Contains("PersistedNote", text);
            StringAssert.Contains("edited body", text);

            AssetDatabase.ImportAsset(m_Path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<BehaviorTree>(m_Path);
            Assert.AreEqual("PersistedGroup", reloaded.groups.Single().title);
            CollectionAssert.Contains(reloaded.groups.Single().nodeIds, seq.id);
            Assert.AreEqual("edited body", reloaded.notes.Single().content);
        }
    }
}
