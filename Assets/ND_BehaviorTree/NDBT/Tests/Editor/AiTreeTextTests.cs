using System.Linq;
using ND_BehaviorTree.Editor.Bridge;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    public class AiTreeTextTests
    {
        private GraphFixture source;
        private GraphFixture target;
        private Blackboard m_Blackboard;
        private BoolKey m_Alert;

        [SetUp]
        public void SetUp()
        {
            m_Blackboard = ScriptableObject.CreateInstance<Blackboard>();
            m_Alert = ScriptableObject.CreateInstance<BoolKey>();
            m_Alert.keyName = "Alert";
            m_Blackboard.keys.Add(m_Alert);

            target = new GraphFixture();   // created first: GraphFixture.Current must be the source for its edits
            target.Tree.blackboard = m_Blackboard;
            source = new GraphFixture();
            source.Tree.blackboard = m_Blackboard;

            var main = source.Create<SequenceNode>(0, 100);
            main.typeName = "Main";
            source.Graph.Connect(source.Root, main);
            var wait = source.Create<WaitNode>(0, 300);
            wait.duration = 2f;
            source.Graph.Connect(main, wait);
            var act = source.Create<CountingActionNode>(200, 300);
            source.Graph.Connect(main, act);
            source.Graph.AddDecorator(act, typeof(InverterNode));
            var check = source.Graph.AddDecorator(act, typeof(CheckConditionVariable));
            Assert.IsTrue(source.Graph.AssignBlackboardKey(check, "conditionKey", m_Alert, out var reason), reason);
            source.Graph.AddService(main, typeof(CountingService));

            var ev = source.Create<EventNode>(-400, 100);
            ev.eventName = "Alarm";
            source.Graph.Connect(ev, source.Create<WaitNode>(-400, 300));
        }

        [TearDown]
        public void TearDown()
        {
            source.Destroy();
            target.Destroy();
            Object.DestroyImmediate(m_Alert);
            Object.DestroyImmediate(m_Blackboard);
        }

        private void AssertSameShape(BehaviorTree tree)
        {
            var main = (SequenceNode)tree.rootNode.GetChildren().Single();
            Assert.AreEqual("Main", main.typeName);
            Assert.IsInstanceOf<CountingService>(main.services.Single());
            Assert.AreEqual(2, main.children.Count);
            Assert.AreEqual(2f, ((WaitNode)main.children[0]).duration, 0.0001f);

            var act = (CountingActionNode)main.children[1];
            CollectionAssert.AreEqual(new[] { typeof(InverterNode), typeof(CheckConditionVariable) }, act.decorators.Select(d => d.GetType()).ToArray());
            Assert.AreSame(m_Alert, ((CheckConditionVariable)act.decorators[1]).conditionKey);

            var ev = tree.nodes.OfType<EventNode>().Single();
            Assert.AreEqual("Alarm", ev.eventName);
            Assert.IsInstanceOf<WaitNode>(ev.child);
        }

        [Test]
        public void Text_RoundTrips_IntoAnotherTree()
        {
            string text = AiTreeText.ToText(source.Tree);
            StringAssert.Contains("Root", text);
            StringAssert.Contains("@decorator CheckConditionVariable keys {\"conditionKey\":\"Alert\"}", text);

            var result = AiTreeText.Import(target.Tree, text, dryRun: false);

            Assert.IsFalse(result.ContainsKey("error"), result.TryGetValue("error", out var e) ? e.ToString() : null);
            AssertSameShape(target.Tree);
        }

        [Test]
        public void Json_RoundTrips_IntoAnotherTree()
        {
            var result = AiTreeText.Import(target.Tree, AiTreeText.ToJsonText(source.Tree), dryRun: false);

            Assert.IsFalse(result.ContainsKey("error"), result.TryGetValue("error", out var e) ? e.ToString() : null);
            AssertSameShape(target.Tree);
        }

        [Test]
        public void Import_ReplacesTheExistingContent_AndAcceptsCodeFences()
        {
            target.Graph.Connect(target.Root, target.Create<WaitNode>(0, 100));
            string answer = "Here you go:\n```\nRoot\n  SelectorNode \"New\"\n    WaitNode {\"duration\": 5}\n```";

            var result = AiTreeText.Import(target.Tree, answer, dryRun: false);

            Assert.IsFalse(result.ContainsKey("error"), result.TryGetValue("error", out var e) ? e.ToString() : null);
            var selector = (SelectorNode)target.Tree.rootNode.GetChildren().Single();
            Assert.AreEqual("New", selector.typeName);
            Assert.AreEqual(5f, ((WaitNode)selector.children.Single()).duration, 0.0001f);
            Assert.AreEqual(3, target.Tree.nodes.Count, "root + selector + wait: the old wait is gone");
        }

        [Test]
        public void DryRun_ChangesNothing()
        {
            int before = target.Tree.nodes.Count;
            var result = AiTreeText.Import(target.Tree, AiTreeText.ToText(source.Tree), dryRun: true);

            Assert.IsFalse(result.ContainsKey("error"));
            Assert.AreEqual(before, target.Tree.nodes.Count);
            Assert.IsTrue(result.ContainsKey("preview"));
        }

        [TestCase("Root\n   WaitNode", "multiple of 2")]
        [TestCase("Root\n  NoSuchNodeType", "Unknown")]
        [TestCase("Root\n  WaitNode keys {\"x\": \"Missing\"}", "Missing")]
        [TestCase("Root\n      WaitNode", "indented too far")]
        public void BadText_ReportsTheProblem(string text, string expected)
        {
            var result = AiTreeText.Import(target.Tree, text, dryRun: true);
            Assert.IsTrue(result.ContainsKey("error"));
            StringAssert.Contains(expected, result["error"].ToString());
        }

        [Test]
        public void OperationList_IsAppliedOnTopOfTheTree()
        {
            target.Graph.Connect(target.Root, target.Create<SequenceNode>(0, 100));
            string ops = "[{\"op\":\"create\",\"type\":\"WaitNode\",\"ref\":\"w\",\"position\":[0,300]}]";

            var result = AiTreeText.Import(target.Tree, ops, dryRun: false);

            Assert.IsFalse(result.ContainsKey("error"), result.TryGetValue("error", out var e) ? e.ToString() : null);
            Assert.AreEqual(1, target.Tree.nodes.OfType<SequenceNode>().Count(), "existing content kept");
            Assert.AreEqual(1, target.Tree.nodes.OfType<WaitNode>().Count());
        }

        [Test]
        public void NodeCatalog_ListsNodesWithDescriptions_ButNotTestTypes()
        {
            string catalog = AiTreeText.NodeCatalog();
            StringAssert.Contains("- WaitNode (Wait): Waits a number of seconds", catalog);
            StringAssert.Contains("UtilitySelectorNode", catalog);
            StringAssert.DoesNotContain("CountingActionNode", catalog);
        }
    }
}
