using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    public class GraphHistoryTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        private GraphHistory H => f.Graph.History;

        [Test]
        public void EachOperation_IsOneStep_WithItsName()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(100);
            f.Graph.Connect(seq, w);

            Assert.AreEqual(3, H.UndoCount);
            Assert.AreEqual("Connect Nodes", H.NextUndoName);
        }

        [Test]
        public void Capacity_DropsOldestSteps()
        {
            H.Capacity = 3;
            for (int i = 0; i < 5; i++) f.Create<WaitNode>(i * 100);

            Assert.AreEqual(3, H.UndoCount);
            for (int i = 0; i < 3; i++) Assert.IsTrue(H.Undo());
            Assert.IsFalse(H.Undo(), "older steps were dropped");
            Assert.AreEqual(2, f.Tree.nodes.Count(n => n is WaitNode), "the two oldest creations can no longer be undone");
        }

        [Test]
        public void NewEdit_ClearsRedo()
        {
            f.Create<WaitNode>(0);
            H.Undo();
            Assert.IsTrue(H.CanRedo);

            f.Create<SequenceNode>(0);
            Assert.IsFalse(H.CanRedo);
        }

        [Test]
        public void SameCoalesceKey_MergesIntoOneStep_UndoRestoresOriginal()
        {
            var w = f.Create<WaitNode>(0);
            int before = H.UndoCount;

            for (int i = 1; i <= 5; i++)
            {
                using (new GraphUndoScope(f.Tree, "Edit Wait", "props:wait"))
                {
                    w.duration = i;
                    UnityEditor.EditorUtility.SetDirty(w); // edits in a scope must mark the object dirty (as SerializedObject.Apply does)
                }
            }

            Assert.AreEqual(before + 1, H.UndoCount, "five edits within a second → one step");
            H.Undo();
            Assert.AreEqual(1f, w.duration, "back to the value before the first edit");
        }

        [Test]
        public void NoChange_RecordsNoStep()
        {
            int before = H.UndoCount;
            using (new GraphUndoScope(f.Tree, "Nothing")) { }
            Assert.AreEqual(before, H.UndoCount);
        }

        [Test]
        public void DeletedBranch_RecreatedOnUndo_WithAllReferencesRelinked()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(100);
            f.Graph.Connect(f.Root, seq);
            f.Graph.Connect(seq, w);
            f.Graph.AddDecorator(w, typeof(InverterNode));
            f.Graph.AddService(seq, typeof(RotateToTargetService));
            string seqId = seq.id, wId = w.id;

            f.Graph.DeleteNodes(new Node[] { seq, w });
            Assert.IsTrue(seq == null && w == null, "really destroyed");

            H.Undo();
            var seq2 = (SequenceNode)f.Tree.nodes.Single(n => n != null && n.id == seqId);
            var w2 = (WaitNode)f.Tree.nodes.Single(n => n != null && n.id == wId);
            Assert.AreSame(seq2, f.Root.child, "root → recreated sequence");
            CollectionAssert.AreEqual(new Node[] { w2 }, seq2.children, "sequence → recreated wait");
            Assert.IsInstanceOf<InverterNode>(w2.decorators.Single());
            Assert.IsInstanceOf<RotateToTargetService>(seq2.services.Single());
        }

        [Test]
        public void RepeatedUndoRedo_KeepsReferencesConsistent()
        {
            var seq = f.Create<SequenceNode>(0);
            f.Graph.Connect(f.Root, seq);
            string seqId = seq.id;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Undo the connect and the creation, then redo both: the node is destroyed and recreated each cycle.
                H.Undo();
                H.Undo();
                Assert.IsFalse(f.Tree.nodes.Any(n => n != null && n.id == seqId));
                Assert.IsNull(f.Root.child);

                H.Redo();
                H.Redo();
                var current = f.Tree.nodes.Single(n => n != null && n.id == seqId);
                Assert.AreSame(current, f.Root.child, $"cycle {cycle}: root points at the live node");
            }
        }

        [Test]
        public void Undo_RestoresOnlyChangedObjects_NotUnrelatedEdits()
        {
            var a = f.Create<WaitNode>(0);
            var b = f.Create<WaitNode>(200);
            f.Graph.MoveNodes(new System.Collections.Generic.Dictionary<Node, Vector2> { { a, new Vector2(50, 0) } });

            b.duration = 9f; // edited outside the history (e.g. by another tool)
            H.Undo();         // undo the move of 'a'

            Assert.AreEqual(0f, a.position.x);
            Assert.AreEqual(9f, b.duration, "b was not part of the step, so it keeps its value");
        }
    }
}
