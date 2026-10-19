using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.CustomGraph;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    public class SearchPopupLevelTests
    {
        private static readonly string[] Paths =
        {
            "Action/Movement/Wander", "Action/Movement/Flee", "Action/Wait", "Composite/Selector", "Root Thing"
        };

        [Test]
        public void Root_ShowsFoldersThenLeaves()
        {
            var level = ND_BTSearchPopup.GetLevel(Paths, "");
            CollectionAssert.AreEqual(new[] { "Action", "Composite", "Root Thing" }, level.Select(r => r.Label).ToArray());
            CollectionAssert.AreEqual(new[] { true, true, false }, level.Select(r => r.IsFolder).ToArray());
        }

        [Test]
        public void Subfolder_ShowsNestedFolderAndLeaf()
        {
            var level = ND_BTSearchPopup.GetLevel(Paths, "Action");
            Assert.AreEqual("Movement", level[0].Label);
            Assert.IsTrue(level[0].IsFolder);
            Assert.AreEqual("Action/Movement", level[0].Path);
            Assert.AreEqual("Wait", level[1].Label);
            Assert.AreEqual("Action/Wait", level[1].Path);
        }

        [Test]
        public void DeepestFolder_ShowsSortedLeaves()
        {
            var level = ND_BTSearchPopup.GetLevel(Paths, "Action/Movement");
            CollectionAssert.AreEqual(new[] { "Flee", "Wander" }, level.Select(r => r.Label).ToArray());
            Assert.IsTrue(level.All(r => !r.IsFolder));
        }
    }

    public class GroupTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void MoveGroup_MovesMembers_ResortsParent_OneUndo()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100, 300);
            var b = f.Create<WaitNode>(300, 300);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);
            var group = f.Graph.CreateGroup("G", new Rect(50, 250, 150, 150), new Node[] { a });

            f.Graph.MoveGroup(group, new Vector2(450, 250)); // +400 on x: a passes b

            Assert.AreEqual(500f, a.position.x);
            Assert.AreEqual(300f, b.position.x, "non-members stay");
            CollectionAssert.AreEqual(new Node[] { b, a }, seq.children);

            GraphFixture.UndoOnce();
            Assert.AreEqual(100f, a.position.x);
            Assert.AreEqual(50f, f.Tree.groups[0].rect.x, "undo re-deserializes the tree, so read the group again");
            CollectionAssert.AreEqual(new Node[] { a, b }, seq.children);
        }

        [Test]
        public void DraggingNode_IntoAndOutOfGroup_UpdatesMembership()
        {
            var w = f.Create<WaitNode>(0, 0);
            var group = f.Graph.CreateGroup("G", new Rect(500, 500, 200, 200), new Node[0]);

            f.Graph.MoveNodes(new Dictionary<Node, Vector2> { { w, new Vector2(550, 550) } });
            CollectionAssert.Contains(group.nodeIds, w.id);

            f.Graph.MoveNodes(new Dictionary<Node, Vector2> { { w, new Vector2(0, 0) } });
            CollectionAssert.DoesNotContain(group.nodeIds, w.id);
        }

        [Test]
        public void DeleteNode_RemovesItFromGroup()
        {
            var w = f.Create<WaitNode>(0);
            var group = f.Graph.CreateGroup("G", new Rect(-50, -50, 200, 200), new Node[] { w });
            f.Graph.DeleteNodes(new Node[] { w });
            Assert.AreEqual(0, group.nodeIds.Count);
        }

        [Test]
        public void ResizeRenameRecolorDelete_AreUndoable()
        {
            var group = f.Graph.CreateGroup("G", new Rect(0, 0, 200, 200), new Node[0]);

            f.Graph.ResizeGroup(group, new Rect(0, 0, 400, 300));
            Assert.AreEqual(400f, group.rect.width);
            GraphFixture.UndoOnce();
            Assert.AreEqual(200f, f.Tree.groups[0].rect.width);

            f.Graph.UpdateGroup(f.Tree.groups[0], "Renamed", Color.red);
            Assert.AreEqual("Renamed", f.Tree.groups[0].title);

            f.Graph.DeleteGroup(f.Tree.groups[0]);
            Assert.AreEqual(0, f.Tree.groups.Count);
            GraphFixture.UndoOnce();
            Assert.AreEqual(1, f.Tree.groups.Count);
        }
    }

    public class NoteTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void CreateEditDelete_PersistOnTree_AndUndo()
        {
            var note = f.Graph.CreateNote(new Rect(10, 10, 200, 100), "Title", "Body");
            Assert.AreEqual(1, f.Tree.notes.Count);

            f.Graph.UpdateNote(note, "T2", "B2", Color.green, new Rect(20, 20, 300, 150));
            Assert.AreEqual("B2", note.content);
            GraphFixture.UndoOnce();
            Assert.AreEqual("Body", f.Tree.notes[0].content);

            f.Graph.DeleteNote(f.Tree.notes[0]);
            Assert.AreEqual(0, f.Tree.notes.Count);
        }

        [Test]
        public void NotesAndGroups_DoNotAffectExecution()
        {
            var leaf = f.Create<FixedStatusNode>(0);
            leaf.result = Node.Status.Failure;
            f.Graph.Connect(f.Root, leaf);
            f.Graph.CreateNote(new Rect(0, 0, 100, 100));
            f.Graph.CreateGroup("G", new Rect(-50, -50, 300, 300), new Node[] { leaf });

            var clone = f.Tree.Clone();
            try
            {
                Assert.AreEqual(Node.Status.Failure, clone.Update());
                Assert.IsFalse(GraphValidator.Validate(f.Tree).Any(i => i.Severity == GraphIssueSeverity.Error));
            }
            finally
            {
                foreach (var n in clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(clone);
            }
        }
    }

    public class SubtreeTests
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

        /// <summary>A standalone in-memory tree: Root -> FixedStatusNode(result).</summary>
        private BehaviorTree MakeLeafTree(Node.Status result)
        {
            var tree = ScriptableObject.CreateInstance<BehaviorTree>();
            var root = ScriptableObject.CreateInstance<RootNode>();
            var leaf = ScriptableObject.CreateInstance<FixedStatusNode>();
            leaf.result = result;
            root.child = leaf;
            tree.nodes.Add(root);
            tree.nodes.Add(leaf);
            tree.rootNode = root;
            m_Extra.AddRange(new Object[] { tree, root, leaf });
            return tree;
        }

        private BehaviorTree CloneTracked()
        {
            var clone = f.Tree.Clone();
            m_Extra.Add(clone);
            m_Extra.AddRange(clone.nodes);
            foreach (var s in clone.nodes.OfType<SubtreeNode>().Where(s => s.RuntimeSubtree != null))
            {
                m_Extra.Add(s.RuntimeSubtree);
                m_Extra.AddRange(s.RuntimeSubtree.nodes);
            }
            return clone;
        }

        [TestCase(Node.Status.Success)]
        [TestCase(Node.Status.Failure)]
        public void Subtree_ReturnsSubtreeStatus(Node.Status result)
        {
            var sub = MakeLeafTree(result);
            var node = f.Create<SubtreeNode>(0);
            f.Graph.Connect(f.Root, node);
            Assert.IsTrue(f.Graph.AssignSubtree(node, sub));

            Assert.AreEqual(result, CloneTracked().Update());
        }

        [Test]
        public void TwoReferences_GetIsolatedRuntimeInstances()
        {
            var sub = MakeLeafTree(Node.Status.Success);
            var seq = f.Create<SequenceNode>(0);
            var n1 = f.Create<SubtreeNode>(100);
            var n2 = f.Create<SubtreeNode>(300);
            f.Graph.Connect(f.Root, seq);
            f.Graph.Connect(seq, n1);
            f.Graph.Connect(seq, n2);
            f.Graph.AssignSubtree(n1, sub);
            f.Graph.AssignSubtree(n2, sub);

            var clone = CloneTracked();
            var r1 = ((SubtreeNode)clone.nodes.Single(n => n.id == n1.id)).RuntimeSubtree;
            var r2 = ((SubtreeNode)clone.nodes.Single(n => n.id == n2.id)).RuntimeSubtree;
            Assert.IsNotNull(r1);
            Assert.AreNotSame(r1, r2);
            Assert.AreNotSame(sub, r1, "never runs the asset itself");
        }

        [Test]
        public void Subtree_SharesOwnerRuntimeBlackboard()
        {
            var bb = ScriptableObject.CreateInstance<Blackboard>();
            m_Extra.Add(bb);
            f.Tree.blackboard = bb;
            var sub = MakeLeafTree(Node.Status.Success);
            var node = f.Create<SubtreeNode>(0);
            f.Graph.Connect(f.Root, node);
            f.Graph.AssignSubtree(node, sub);

            var clone = CloneTracked();
            m_Extra.Add(clone.blackboard);
            var runtime = ((SubtreeNode)clone.nodes.Single(n => n.id == node.id)).RuntimeSubtree;
            Assert.AreSame(clone.blackboard, runtime.blackboard);
        }

        [Test]
        public void MissingReference_FailsWithError_AndValidatorWarns()
        {
            var node = f.Create<SubtreeNode>(0);
            f.Graph.Connect(f.Root, node);

            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.SubtreeMissing));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot run"));
            Assert.AreEqual(Node.Status.Failure, CloneTracked().Update());
        }

        [Test]
        public void SelfReference_IsRejectedByService()
        {
            var node = f.Create<SubtreeNode>(0);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("AssignSubtree rejected"));
            Assert.IsFalse(f.Graph.AssignSubtree(node, f.Tree));
        }

        [Test]
        public void IndirectRecursion_IsDetected_AndFailsAtRuntime()
        {
            // f.Tree -> sub -> f.Tree, written directly (bypassing the service) as a corrupted asset would be.
            var sub = MakeLeafTree(Node.Status.Success);
            var back = ScriptableObject.CreateInstance<SubtreeNode>();
            back.subtree = f.Tree;
            sub.nodes.Add(back);
            ((RootNode)sub.rootNode).child = back;
            m_Extra.Add(back);

            var node = f.Create<SubtreeNode>(0);
            f.Graph.Connect(f.Root, node);
            node.subtree = sub;

            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.SubtreeRecursive));
            Assert.IsFalse(f.Graph.CanAssignSubtree(node, sub, out _));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("recursive reference"));
            Assert.AreEqual(Node.Status.Failure, CloneTracked().Update());
        }
    }
}
