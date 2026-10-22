using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    public class RerouteTests
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
        public void InsertReroute_KeepsChildSlotInComposite()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            var b = f.Create<CountingActionNode>(200, 200);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);

            // Placed far to the right: the slot (execution order) must not change on insert.
            var reroute = f.Graph.InsertReroute(seq, a, new Vector2(500, 150));

            Assert.IsNotNull(reroute);
            CollectionAssert.AreEqual(new Node[] { reroute, b }, seq.children);
            Assert.AreSame(a, reroute.child);
            Assert.AreSame(a, RerouteNode.Resolve(reroute));
        }

        [Test]
        public void Clone_SkipsReroutes_ParentRunsTargetDirectly()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(seq, a);
            var first = f.Graph.InsertReroute(seq, a, new Vector2(0, 150));
            f.Graph.InsertReroute(first, a, new Vector2(0, 170)); // chain of two reroutes

            m_Clone = f.Tree.Clone();

            Assert.IsFalse(m_Clone.nodes.Any(n => n is RerouteNode), "reroutes are not part of the runtime tree");
            var rtSeq = (SequenceNode)m_Clone.nodes.Single(n => n.id == seq.id);
            var rtA = (CountingActionNode)m_Clone.nodes.Single(n => n.id == a.id);
            Assert.AreSame(rtA, rtSeq.children.Single());

            m_Clone.Update();
            Assert.AreEqual(1, rtA.processCount);
        }

        [Test]
        public void Clone_ReroutesIntoDecoratedNode_EntersOutermostDecorator()
        {
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(f.Root, a);
            f.Graph.AddDecorator(a, typeof(InverterNode));
            f.Graph.InsertReroute(f.Root, a, new Vector2(0, 150));

            m_Clone = f.Tree.Clone();

            var rtRoot = m_Clone.rootNode;
            Assert.IsInstanceOf<InverterNode>(rtRoot.GetChildren().Single());
        }

        [Test]
        public void Clone_UnconnectedReroute_ParentGetsNoChild()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(seq, a);
            var reroute = f.Graph.InsertReroute(seq, a, new Vector2(0, 150));
            f.Graph.Disconnect(reroute, a);

            m_Clone = f.Tree.Clone();

            var rtSeq = (SequenceNode)m_Clone.nodes.Single(n => n.id == seq.id);
            Assert.AreEqual(0, rtSeq.children.Count);
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.RerouteNoChild));
        }

        [Test]
        public void DeleteReroute_ReconnectsParentToTarget_InSameSlot()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            var b = f.Create<CountingActionNode>(200, 200);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);
            var reroute = f.Graph.InsertReroute(seq, a, new Vector2(0, 150));

            f.Graph.DeleteNodes(new Node[] { reroute });

            CollectionAssert.AreEqual(new Node[] { a, b }, seq.children);
            Assert.IsFalse(f.Tree.nodes.Any(n => n is RerouteNode));
        }

        [Test]
        public void DeleteRerouteAndTarget_RemovesTheSlot()
        {
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(f.Root, a);
            var reroute = f.Graph.InsertReroute(f.Root, a, new Vector2(0, 150));

            f.Graph.DeleteNodes(new Node[] { reroute, a });

            Assert.AreEqual(0, f.Root.GetChildren().Count);
        }

        [Test]
        public void InsertReroute_IsOneUndoStep()
        {
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(f.Root, a);
            f.Graph.InsertReroute(f.Root, a, new Vector2(0, 150));

            GraphFixture.UndoOnce();

            Assert.AreSame(a, f.Root.GetChildren().Single());
            Assert.IsFalse(f.Tree.nodes.Any(n => n is RerouteNode));
        }

        [Test]
        public void Reroute_RejectsDecoratorsAndServices()
        {
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(f.Root, a);
            var reroute = f.Graph.InsertReroute(f.Root, a, new Vector2(0, 150));

            Assert.IsFalse(f.Graph.CanAttachDecorator(reroute, out _));
            Assert.IsFalse(f.Graph.CanAttachService(reroute, out _));
        }

        [Test]
        public void ConvertReroutesToBends_KeepsTheSlot_AndPutsOneBendPerReroute()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            var b = f.Create<CountingActionNode>(200, 200);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);
            var first = f.Graph.InsertReroute(seq, a, new Vector2(500, 150));
            f.Graph.InsertReroute(first, a, new Vector2(520, 170));

            int removed = f.Graph.ConvertReroutesToBends(new Vector2(9, 9));

            Assert.AreEqual(2, removed);
            Assert.IsFalse(f.Tree.nodes.Any(n => n is RerouteNode));
            CollectionAssert.AreEqual(new Node[] { a, b }, seq.children, "same slot, linked directly");
            CollectionAssert.AreEqual(new[] { new Vector2(509, 159), new Vector2(529, 179) }, a.parentEdgeBends, "reroute centres, in order");
        }

        [Test]
        public void EdgeBends_AreStoredOnTheChild_AndDroppedWhenTheEdgeIsCut()
        {
            var seq = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, seq);
            var a = f.Create<CountingActionNode>(0, 200);
            f.Graph.Connect(seq, a);

            f.Graph.SetEdgeBends(a, new[] { new Vector2(40, 150) });
            CollectionAssert.AreEqual(new[] { new Vector2(40, 150) }, a.parentEdgeBends);

            f.Graph.Disconnect(seq, a);
            CollectionAssert.IsEmpty(a.parentEdgeBends, "the bends belonged to the removed edge");
        }
    }
}
