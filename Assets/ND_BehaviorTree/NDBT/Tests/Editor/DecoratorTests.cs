using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    [NodeInfo("Fixed Status (test)", "Test/FixedStatus", true, false)]
    public class FixedStatusNode : ActionNode
    {
        public Status result = Status.Success;
        protected override Status OnProcess() => result;
    }

    [NodeInfo("Counting Service (test)", "Test/CountingService", isChildOnly: true)]
    public class CountingService : ServiceNode
    {
        public int ticks;
        protected override void OnTick() => ticks++;
    }

    public class AttachmentOnAnyNodeTests
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

        [Test]
        public void ServicesAndDecorators_AttachToActions_ButNotToAttachments()
        {
            var w = f.Create<WaitNode>(0);
            Assert.IsTrue(f.Graph.CanAttachService(w, out _));
            Assert.IsTrue(f.Graph.CanAttachDecorator(w, out _));
            Assert.IsTrue(f.Graph.CanAttachService(f.Root, out _));
            Assert.IsTrue(f.Graph.CanAttachDecorator(f.Root, out _));

            var service = f.Graph.AddService(w, typeof(RotateToTargetService));
            var decorator = f.Graph.AddDecorator(w, typeof(InverterNode));
            Assert.IsFalse(f.Graph.CanAttachService(service, out _));
            Assert.IsFalse(f.Graph.CanAttachDecorator(decorator, out _));
        }

        [Test]
        public void ServiceOnAction_TicksWhenActionRuns()
        {
            var leaf = f.Create<FixedStatusNode>(0);
            f.Graph.Connect(f.Root, leaf);
            f.Graph.AddService(leaf, typeof(CountingService));

            m_Clone = f.Tree.Clone();
            m_Clone.Update();

            var runtimeLeaf = m_Clone.nodes.Single(n => n.id == leaf.id);
            Assert.AreEqual(1, ((CountingService)runtimeLeaf.services[0]).ticks);
            Assert.AreNotSame(leaf.services[0], runtimeLeaf.services[0], "runtime uses a cloned service");
        }

        [Test]
        public void DecoratorOnRoot_AppliesAtRuntime()
        {
            var leaf = f.Create<FixedStatusNode>(0);
            leaf.result = Node.Status.Success;
            f.Graph.Connect(f.Root, leaf);
            f.Graph.AddDecorator(f.Root, typeof(InverterNode));

            m_Clone = f.Tree.Clone();
            Assert.AreEqual(Node.Status.Failure, m_Clone.Update());
        }
    }

    public class DecoratorAttachmentTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void AddDecorator_AttachesInOrder_NotAGraphNode()
        {
            var w = f.Create<WaitNode>(0);
            var d1 = f.Graph.AddDecorator(w, typeof(InverterNode));
            var d2 = f.Graph.AddDecorator(w, typeof(SucceederNode));

            CollectionAssert.AreEqual(new DecoratorNode[] { d1, d2 }, w.decorators);
            Assert.IsFalse(f.Tree.nodes.Contains(d1));
        }

        [Test]
        public void AddDecorator_RejectsRootAndNonDecoratorTypes()
        {
            Assert.IsFalse(f.Graph.CanAttachDecorator(f.Root, out _));
            var w = f.Create<WaitNode>(0);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("AddDecorator rejected"));
            Assert.IsNull(f.Graph.AddDecorator(w, typeof(WaitNode)));
        }

        [Test]
        public void CreateNode_RejectsStandaloneDecorator()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CreateNode rejected"));
            Assert.IsNull(f.Graph.CreateNode(typeof(InverterNode), Vector2.zero));
        }

        [Test]
        public void MoveDecorator_Reorders_AndUndoRestores()
        {
            var w = f.Create<WaitNode>(0);
            var d1 = f.Graph.AddDecorator(w, typeof(InverterNode));
            var d2 = f.Graph.AddDecorator(w, typeof(SucceederNode));

            Assert.IsTrue(f.Graph.MoveDecorator(w, d2, 0));
            CollectionAssert.AreEqual(new DecoratorNode[] { d2, d1 }, w.decorators);

            GraphFixture.UndoOnce();
            CollectionAssert.AreEqual(new DecoratorNode[] { d1, d2 }, w.decorators);
        }

        [Test]
        public void RemoveDecorator_UndoRestores()
        {
            var w = f.Create<WaitNode>(0);
            var d = f.Graph.AddDecorator(w, typeof(InverterNode));
            string id = d.id;

            Assert.IsTrue(f.Graph.RemoveDecorator(w, d));
            Assert.AreEqual(0, w.decorators.Count);

            GraphFixture.UndoOnce();
            Assert.AreEqual(1, w.decorators.Count);
            Assert.AreEqual(id, w.decorators[0].id);
        }

        [Test]
        public void DeleteNode_DestroysItsDecorators()
        {
            var w = f.Create<WaitNode>(0);
            var d = f.Graph.AddDecorator(w, typeof(InverterNode));
            f.Graph.DeleteNodes(new Node[] { w });
            Assert.IsTrue(d == null);
        }

        [Test]
        public void Paste_CopiesDecorators_WithNewIds_AndOrder()
        {
            var w = f.Create<WaitNode>(0);
            var d1 = f.Graph.AddDecorator(w, typeof(InverterNode));
            var d2 = f.Graph.AddDecorator(w, typeof(SucceederNode));

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { w }))
            {
                var pw = f.Graph.Paste(clip, Vector2.zero).Single();
                Assert.AreEqual(2, pw.decorators.Count);
                Assert.IsInstanceOf<InverterNode>(pw.decorators[0]);
                Assert.IsInstanceOf<SucceederNode>(pw.decorators[1]);
                Assert.AreNotEqual(d1.id, pw.decorators[0].id);
                Assert.AreNotSame(d2, pw.decorators[1]);
            }
        }
    }

    public class DecoratorRuntimeTests
    {
        private GraphFixture f;
        private readonly List<BehaviorTree> m_Clones = new List<BehaviorTree>();

        [SetUp] public void SetUp() => f = new GraphFixture();

        [TearDown]
        public void TearDown()
        {
            foreach (var clone in m_Clones)
            {
                foreach (var n in clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(clone);
            }
            m_Clones.Clear();
            f.Destroy();
        }

        private Node.Status Tick()
        {
            var clone = f.Tree.Clone();
            m_Clones.Add(clone);
            return clone.Update();
        }

        [Test]
        public void Clone_BuildsChain_TopDecoratorIsOutermost()
        {
            var leaf = f.Create<FixedStatusNode>(0);
            f.Graph.Connect(f.Root, leaf);
            var d1 = f.Graph.AddDecorator(leaf, typeof(InverterNode));
            var d2 = f.Graph.AddDecorator(leaf, typeof(SucceederNode));

            var clone = f.Tree.Clone();
            m_Clones.Add(clone);

            var outer = (DecoratorNode)clone.rootNode.child;
            Assert.AreEqual(d1.id, outer.id);
            var inner = (DecoratorNode)outer.child;
            Assert.AreEqual(d2.id, inner.id);
            Assert.AreEqual(leaf.id, inner.child.id);
        }

        [TestCase(true, Node.Status.Failure)]  // Inverter(Succeeder(Failure)) = Inverter(Success) = Failure
        [TestCase(false, Node.Status.Success)] // Succeeder(Inverter(Failure)) = Succeeder(Success) = Success
        public void DecoratorOrder_ChangesResult(bool inverterOnTop, Node.Status expected)
        {
            var leaf = f.Create<FixedStatusNode>(0);
            leaf.result = Node.Status.Failure;
            f.Graph.Connect(f.Root, leaf);
            f.Graph.AddDecorator(leaf, inverterOnTop ? typeof(InverterNode) : typeof(SucceederNode));
            f.Graph.AddDecorator(leaf, inverterOnTop ? typeof(SucceederNode) : typeof(InverterNode));

            Assert.AreEqual(expected, Tick());
        }

        [Test]
        public void AttachedDecorator_InsideComposite_KeepsChildIndex()
        {
            var seq = f.Create<SelectorNode>(0);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<FixedStatusNode>(100);
            a.result = Node.Status.Success;
            var b = f.Create<FixedStatusNode>(300);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);
            f.Graph.AddDecorator(a, typeof(InverterNode)); // a now fails → selector tries b

            var clone = f.Tree.Clone();
            m_Clones.Add(clone);
            var clonedSel = (CompositeNode)clone.nodes.Single(n => n.id == seq.id);
            Assert.IsInstanceOf<InverterNode>(clonedSel.children[0]);
            Assert.AreEqual(b.id, clonedSel.children[1].id);
        }
    }

    public class DecoratorMigrationTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        private T Legacy<T>() where T : Node
        {
            var n = ScriptableObject.CreateInstance<T>();
            n.name = typeof(T).Name;
            f.Tree.nodes.Add(n);
            return n;
        }

        [Test]
        public void V0Chain_CollapsesOntoTarget_KeepingParentIndex()
        {
            var seq = Legacy<SequenceNode>();
            var first = Legacy<FixedStatusNode>();
            var inv = Legacy<InverterNode>();
            var suc = Legacy<SucceederNode>();
            var target = Legacy<FixedStatusNode>();
            f.Root.child = seq;
            seq.children.Add(first);
            seq.children.Add(inv);
            inv.child = suc;
            suc.child = target;

            Assert.IsTrue(BehaviorTreeMigrator.MigrateIfNeeded(f.Tree));

            Assert.AreEqual(BehaviorTree.CurrentSchemaVersion, f.Tree.SchemaVersion);
            CollectionAssert.AreEqual(new Node[] { first, target }, seq.children);
            CollectionAssert.AreEqual(new DecoratorNode[] { inv, suc }, target.decorators);
            Assert.IsNull(inv.child);
            Assert.IsNull(suc.child);
            Assert.IsFalse(f.Tree.nodes.Contains(inv) || f.Tree.nodes.Contains(suc));
        }

        [Test]
        public void V0Decorator_UnderRoot_IsRewired()
        {
            var inv = Legacy<InverterNode>();
            var leaf = Legacy<FixedStatusNode>();
            f.Root.child = inv;
            inv.child = leaf;

            BehaviorTreeMigrator.MigrateIfNeeded(f.Tree);

            Assert.AreSame(leaf, f.Root.child);
            CollectionAssert.AreEqual(new DecoratorNode[] { inv }, leaf.decorators);
        }

        [Test]
        public void DanglingDecorator_IsLeftStandalone_AndReported()
        {
            var inv = Legacy<InverterNode>();

            BehaviorTreeMigrator.MigrateIfNeeded(f.Tree);

            Assert.IsTrue(f.Tree.nodes.Contains(inv));
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.DecoratorStandalone && i.Node == inv));
        }

        [Test]
        public void Migration_PreservesRuntimeResult()
        {
            var inv = Legacy<InverterNode>();
            var leaf = Legacy<FixedStatusNode>();
            leaf.result = Node.Status.Success;
            f.Root.child = inv;
            inv.child = leaf;

            Node.Status before = RunOnce();
            BehaviorTreeMigrator.MigrateIfNeeded(f.Tree);
            Node.Status after = RunOnce();

            Assert.AreEqual(Node.Status.Failure, before);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void AlreadyCurrent_IsNoOp()
        {
            f.Tree.SchemaVersion = BehaviorTree.CurrentSchemaVersion;
            Assert.IsFalse(BehaviorTreeMigrator.MigrateIfNeeded(f.Tree));
        }

        private Node.Status RunOnce()
        {
            var clone = f.Tree.Clone();
            try { return clone.Update(); }
            finally
            {
                foreach (var n in clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(clone);
            }
        }
    }
}
