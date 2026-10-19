using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    public class ExecutionOrderTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void Connect_SortsChildrenLeftToRight_RegardlessOfConnectOrder()
        {
            var seq = f.Create<SequenceNode>(0);
            var right = f.Create<WaitNode>(300);
            var left = f.Create<WaitNode>(100);

            Assert.IsTrue(f.Graph.Connect(seq, right));
            Assert.IsTrue(f.Graph.Connect(seq, left));

            CollectionAssert.AreEqual(new Node[] { left, right }, seq.children);
        }

        [Test]
        public void MoveNodes_ReordersParent_AndIsOneUndoStep()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            var b = f.Create<WaitNode>(300);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);

            f.Graph.MoveNodes(new System.Collections.Generic.Dictionary<Node, Vector2> { { a, new Vector2(500, 0) } });
            CollectionAssert.AreEqual(new Node[] { b, a }, seq.children);
            Assert.AreEqual(500f, a.position.x);

            GraphFixture.UndoOnce();
            CollectionAssert.AreEqual(new Node[] { a, b }, seq.children, "one undo restores order");
            Assert.AreEqual(100f, a.position.x, "the same undo restores position");
        }

        [Test]
        public void MoveNodes_VerticalMoveWithoutCrossing_KeepsOrder()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            var b = f.Create<WaitNode>(300);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);

            f.Graph.MoveNodes(new System.Collections.Generic.Dictionary<Node, Vector2> { { a, new Vector2(100, 400) } });

            CollectionAssert.AreEqual(new Node[] { a, b }, seq.children);
        }

        [Test]
        public void SortByPosition_IsStableForEqualX()
        {
            var a = ScriptableObject.CreateInstance<WaitNode>();
            var b = ScriptableObject.CreateInstance<WaitNode>();
            a.SetPosition(new Rect(100, 0, 0, 0));
            b.SetPosition(new Rect(100, 50, 0, 0));
            var list = new System.Collections.Generic.List<Node> { b, a };

            Assert.IsFalse(ExecutionOrder.SortByPosition(list));
            CollectionAssert.AreEqual(new Node[] { b, a }, list);

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void RuntimeClone_ExecutesInSerializedListOrder()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            var b = f.Create<WaitNode>(300);
            f.Graph.Connect(f.Root, seq);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);

            var clone = f.Tree.Clone();
            try
            {
                var clonedSeq = (CompositeNode)clone.nodes.Single(n => n.id == seq.id);
                CollectionAssert.AreEqual(new[] { a.id, b.id }, clonedSeq.children.Select(c => c.id).ToArray());
            }
            finally
            {
                foreach (var n in clone.nodes) Object.DestroyImmediate(n);
                Object.DestroyImmediate(clone);
            }
        }
    }

    public class ConnectionRuleTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void Root_AcceptsOnlyOneChild()
        {
            var a = f.Create<SequenceNode>(0);
            var b = f.Create<SequenceNode>(200);
            Assert.IsTrue(f.Graph.Connect(f.Root, a));

            Assert.IsFalse(f.Graph.CanConnect(f.Root, b, out var reason));
            StringAssert.Contains("single child", reason);
        }

        [Test]
        public void Child_CannotHaveTwoParents()
        {
            var s1 = f.Create<SequenceNode>(0);
            var s2 = f.Create<SequenceNode>(200);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(s1, w);

            Assert.IsFalse(f.Graph.CanConnect(s2, w, out var reason));
            StringAssert.Contains("already has a parent", reason);
        }

        [Test]
        public void Cycle_IsRejected()
        {
            var s1 = f.Create<SequenceNode>(0);
            var s2 = f.Create<SequenceNode>(200);
            f.Graph.Connect(s1, s2);

            Assert.IsFalse(f.Graph.CanConnect(s2, s1, out var reason));
            StringAssert.Contains("cycle", reason);
        }

        [Test]
        public void ActionWithoutOutputPort_CannotHaveChildren()
        {
            var w1 = f.Create<WaitNode>(0);
            var w2 = f.Create<WaitNode>(200);
            Assert.IsFalse(f.Graph.CanConnect(w1, w2, out var reason));
            StringAssert.Contains("no output port", reason);
        }

        [Test]
        public void RejectedConnect_LogsWarning_AndDoesNotMutate()
        {
            var w1 = f.Create<WaitNode>(0);
            var w2 = f.Create<WaitNode>(200);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Connect rejected"));
            Assert.IsFalse(f.Graph.Connect(w1, w2));
            Assert.AreEqual(0, w1.GetChildren().Count);
        }

        [Test]
        public void Connect_UndoRemovesChild_RedoRestores()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(seq, w);

            GraphFixture.UndoOnce();
            Assert.AreEqual(0, seq.children.Count);

            GraphFixture.RedoOnce();
            CollectionAssert.AreEqual(new Node[] { w }, seq.children);
        }

        [Test]
        public void Disconnect_RemovesChild_AndUndoRestores()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(seq, w);

            Assert.IsTrue(f.Graph.Disconnect(seq, w));
            Assert.AreEqual(0, seq.children.Count);

            GraphFixture.UndoOnce();
            CollectionAssert.AreEqual(new Node[] { w }, seq.children);
        }
    }

    public class DeleteTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void Delete_RemovesNodeAndEdges_UndoRestoresBoth()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(seq, w);
            string id = w.id;

            Assert.AreEqual(1, f.Graph.DeleteNodes(new[] { w }));
            Assert.AreEqual(0, seq.children.Count);
            Assert.IsFalse(f.Tree.nodes.Any(n => n != null && n.id == id));

            GraphFixture.UndoOnce();
            var restored = f.Tree.nodes.SingleOrDefault(n => n != null && n.id == id);
            Assert.IsNotNull(restored, "node restored");
            CollectionAssert.AreEqual(new Node[] { restored }, seq.children, "edge restored");
        }

        [Test]
        public void Root_IsNeverDeleted()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RootNode cannot be deleted"));
            Assert.AreEqual(0, f.Graph.DeleteNodes(new Node[] { f.Root }));
            Assert.IsTrue(f.Tree.nodes.Contains(f.Root));
        }

        [Test]
        public void Delete_Composite_DestroysItsServices()
        {
            var seq = f.Create<SequenceNode>(0);
            var service = f.Graph.AddService(seq, typeof(RotateToTargetService));
            Assert.IsNotNull(service);

            f.Graph.DeleteNodes(new[] { seq });

            Assert.IsTrue(service == null, "service destroyed with its composite");
        }
    }

    public class CopyPasteTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void Paste_CreatesNewIds_RemapsInternalEdges_PreservesOrder()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            var b = f.Create<WaitNode>(300);
            f.Graph.Connect(seq, a);
            f.Graph.Connect(seq, b);

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { seq, a, b }))
            {
                var pasted = f.Graph.Paste(clip, new Vector2(1000, 1000));
                Assert.AreEqual(3, pasted.Count);

                var originals = new[] { seq.id, a.id, b.id };
                Assert.IsTrue(pasted.All(p => !originals.Contains(p.id)), "new ids");
                Assert.AreEqual(f.Tree.nodes.Count, f.Tree.nodes.Select(n => n.id).Distinct().Count(), "no duplicate ids in tree");

                var pSeq = pasted.OfType<SequenceNode>().Single();
                var pWaits = pasted.OfType<WaitNode>().OrderBy(w => w.position.x).ToArray();
                CollectionAssert.AreEqual(pWaits, pSeq.children, "edges point to the copies, left-to-right");
                CollectionAssert.AreEqual(new Node[] { a, b }, seq.children, "original untouched");
            }
        }

        [Test]
        public void Paste_ChildOutsideSelection_IsNotLinked()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(seq, w);

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { seq }))
            {
                var pSeq = (SequenceNode)f.Graph.Paste(clip, Vector2.zero).Single();
                Assert.AreEqual(0, pSeq.children.Count);
            }
        }

        [Test]
        public void Paste_IsDeepCopy_NoSharedState()
        {
            var w = f.Create<WaitNode>(0);
            w.duration = 2f;

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { w }))
            {
                var pw = (WaitNode)f.Graph.Paste(clip, Vector2.zero).Single();
                pw.duration = 9f;
                Assert.AreEqual(2f, w.duration);
            }
        }

        [Test]
        public void Snapshot_IsDetachedFromLaterSourceEdits()
        {
            var w = f.Create<WaitNode>(0);
            w.duration = 2f;

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { w }))
            {
                w.duration = 7f;
                var pw = (WaitNode)f.Graph.Paste(clip, Vector2.zero).Single();
                Assert.AreEqual(2f, pw.duration);
            }
        }

        [Test]
        public void Paste_CopiesServicesAsNewObjects()
        {
            var seq = f.Create<SequenceNode>(0);
            var service = f.Graph.AddService(seq, typeof(RotateToTargetService));

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { seq }))
            {
                var pSeq = (SequenceNode)f.Graph.Paste(clip, Vector2.zero).Single();
                Assert.AreEqual(1, pSeq.services.Count);
                Assert.AreNotSame(service, pSeq.services[0]);
                Assert.AreNotEqual(service.id, pSeq.services[0].id);
            }
        }

        [Test]
        public void Root_IsNeverCopied()
        {
            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { f.Root }))
            {
                Assert.AreEqual(0, clip.Count);
            }
        }

        [Test]
        public void Paste_IsOneUndoStep()
        {
            var seq = f.Create<SequenceNode>(0);
            var w = f.Create<WaitNode>(0);
            f.Graph.Connect(seq, w);
            int before = f.Tree.nodes.Count;

            using (var clip = GraphClipboard.Capture(f.Tree, new Node[] { seq, w }))
            {
                f.Graph.Paste(clip, Vector2.zero);
            }
            Assert.AreEqual(before + 2, f.Tree.nodes.Count);

            GraphFixture.UndoOnce();
            Assert.AreEqual(before, f.Tree.nodes.Count(n => n != null));
        }
    }

    public class ValidatorTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void ValidTree_HasNoErrors()
        {
            var seq = f.Create<SequenceNode>(0);
            f.Graph.Connect(f.Root, seq);
            Assert.IsFalse(GraphValidator.Validate(f.Tree).Any(i => i.Severity == GraphIssueSeverity.Error));
        }

        [Test]
        public void DetectsExtraRoot()
        {
            var extra = ScriptableObject.CreateInstance<RootNode>();
            f.Tree.nodes.Add(extra);
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.RootMultiple && i.Node == extra));
        }

        [Test]
        public void DetectsCycleAndMultipleParents_WrittenBypassingTheService()
        {
            var s1 = f.Create<SequenceNode>(0);
            var s2 = f.Create<SequenceNode>(100);
            s1.children.Add(s2);
            s2.children.Add(s1);
            f.Root.child = s2;

            var issues = GraphValidator.Validate(f.Tree);
            Assert.IsTrue(issues.Any(i => i.Code == GraphValidator.Cycle));
            Assert.IsTrue(issues.Any(i => i.Code == GraphValidator.ChildMultipleParents && i.Node == s2));
        }

        [Test]
        public void DetectsUnreachableNode()
        {
            var orphan = f.Create<WaitNode>(0);
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.NodeUnreachable && i.Node == orphan));
        }

        [Test]
        public void ReportsLegacyOrder_WithoutMutating()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            var b = f.Create<WaitNode>(300);
            seq.children.Add(b);
            seq.children.Add(a);

            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.OrderNotByPosition && i.Node == seq));
            CollectionAssert.AreEqual(new Node[] { b, a }, seq.children);
        }
    }

    public class SettingsResolutionTests
    {
        [Test]
        public void NoAsset_ReturnsNull()
        {
            Assert.IsNull(ND_BehaviorTreeSetting.ResolveSettingsPath(new string[0], out var warning));
            Assert.IsNull(warning);
        }

        [Test]
        public void SingleAsset_AnyFolder_IsUsed()
        {
            const string moved = "Assets/ThirdParty/Moved/NDBT/Editor/Resources/ND_BehaviorTree_Settings.asset";
            Assert.AreEqual(moved, ND_BehaviorTreeSetting.ResolveSettingsPath(new[] { moved }, out var warning));
            Assert.IsNull(warning);
        }

        [Test]
        public void Duplicates_PickDeterministically_AndWarn()
        {
            var paths = new[] { "Assets/B/s.asset", "Assets/A/s.asset" };
            Assert.AreEqual("Assets/A/s.asset", ND_BehaviorTreeSetting.ResolveSettingsPath(paths, out var warning));
            StringAssert.Contains("Assets/B/s.asset", warning);
        }

        [Test]
        public void Instance_ResolvesTheExistingProjectAsset()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ND_BehaviorTreeSetting));
            Assume.That(guids.Length, Is.EqualTo(1), "project should contain exactly one settings asset");
            Assert.AreEqual(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]),
                UnityEditor.AssetDatabase.GetAssetPath(ND_BehaviorTreeSetting.Instance));
        }
    }
}
