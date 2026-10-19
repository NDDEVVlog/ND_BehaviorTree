using System.Collections.Generic;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>
    /// Regression for "Broken text PPtr" in Wolf.asset: a service attached to a Selector was also listed in m_nodes
    /// (legacy duplicate). Deleting one copy destroyed an object the other still referenced.
    /// </summary>
    public class DeleteSafetyTests
    {
        private GraphFixture f;

        [SetUp] public void SetUp() => f = new GraphFixture();
        [TearDown] public void TearDown() => f.Destroy();

        private (SelectorNode selector, ServiceNode service) BuildLegacyDuplicate()
        {
            var selector = f.Create<SelectorNode>(0);
            f.Graph.Connect(f.Root, selector);
            var service = f.Graph.AddService(selector, typeof(RotateToTargetService));
            f.Tree.nodes.Add(service); // the legacy duplicate entry
            return (selector, service);
        }

        [Test]
        public void DeletingTheOwner_DoesNotDestroyAnAttachmentStillListedElsewhere()
        {
            var (selector, service) = BuildLegacyDuplicate();

            f.Graph.DeleteNodes(new Node[] { selector });

            Assert.IsTrue(service != null, "still referenced by m_nodes → must not be destroyed");
            Assert.IsTrue(f.Tree.nodes.Contains(service));
        }

        [Test]
        public void DeletingAnAttachmentCopy_RemovesItFromItsOwnersList()
        {
            var (selector, service) = BuildLegacyDuplicate();

            f.Graph.DeleteNodes(new Node[] { service });

            Assert.IsTrue(service == null, "deleted");
            Assert.AreEqual(0, selector.services.Count, "no reference left to the destroyed object");
        }

        [Test]
        public void DeletingAnyNode_LeavesNoReferenceToDestroyedObjects()
        {
            var seq = f.Create<SequenceNode>(0);
            var a = f.Create<WaitNode>(100);
            f.Graph.Connect(f.Root, seq);
            f.Graph.Connect(seq, a);
            f.Graph.AddService(a, typeof(RotateToTargetService));
            f.Graph.AddDecorator(a, typeof(InverterNode));

            f.Graph.DeleteNodes(new Node[] { a });

            foreach (var node in f.Tree.nodes)
            {
                Assert.IsFalse(node.GetChildren().Any(c => c == null), $"{node.name} has a dangling child");
                Assert.IsFalse(node.services.Any(s => s == null), $"{node.name} has a dangling service");
                Assert.IsFalse(node.decorators.Any(d => d == null), $"{node.name} has a dangling decorator");
            }
        }

        [Test]
        public void MigrationV2_RemovesDuplicateEntry_KeepsAttachment()
        {
            var (selector, service) = BuildLegacyDuplicate();
            f.Tree.SchemaVersion = 1;
            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.AttachmentListedAsNode));

            Assert.IsTrue(BehaviorTreeMigrator.MigrateIfNeeded(f.Tree));

            Assert.AreEqual(BehaviorTree.CurrentSchemaVersion, f.Tree.SchemaVersion);
            Assert.IsFalse(f.Tree.nodes.Contains(service));
            CollectionAssert.AreEqual(new ServiceNode[] { service }, selector.services);
            Assert.IsFalse(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.AttachmentListedAsNode));
        }
    }
}
