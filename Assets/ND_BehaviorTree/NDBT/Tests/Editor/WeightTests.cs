using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    public class WeightTests
    {
        private GraphFixture f;
        private BehaviorTree m_Clone;
        private Blackboard m_Blackboard;
        private FloatKey m_Fear;

        [SetUp]
        public void SetUp()
        {
            f = new GraphFixture();
            m_Blackboard = ScriptableObject.CreateInstance<Blackboard>();
            m_Fear = ScriptableObject.CreateInstance<FloatKey>();
            m_Fear.keyName = "Fear";
            m_Blackboard.keys.Add(m_Fear);
            f.Tree.blackboard = m_Blackboard;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Clone != null)
            {
                foreach (var n in m_Clone.nodes) Object.DestroyImmediate(n);
                if (m_Clone.blackboard != null)
                {
                    foreach (var k in m_Clone.blackboard.keys) Object.DestroyImmediate(k);
                    Object.DestroyImmediate(m_Clone.blackboard);
                }
                Object.DestroyImmediate(m_Clone);
            }
            f.Destroy();
            Object.DestroyImmediate(m_Fear);
            Object.DestroyImmediate(m_Blackboard);
        }

        private CountingActionNode Child(Node parent, float x, float weight, Node.Status result)
        {
            var node = f.Create<CountingActionNode>(x, 200);
            node.weight = weight;
            node.result = result;
            f.Graph.Connect(parent, node);
            return node;
        }

        private WeightFromKeyNode DriveWeight(Node node, WeightFromKeyNode.Mode mode)
        {
            var modifier = (WeightFromKeyNode)f.Graph.AddDecorator(node, typeof(WeightFromKeyNode));
            modifier.mode = mode;
            Assert.IsTrue(f.Graph.AssignBlackboardKey(modifier, "key", m_Fear, out var reason), reason);
            return modifier;
        }

        private T Runtime<T>(Node original) where T : Node => (T)m_Clone.nodes.Single(n => n.id == original.id);

        private UtilitySelectorNode Utility()
        {
            var utility = f.Create<UtilitySelectorNode>(0, 100);
            utility.evaluationInterval = 0f;
            utility.hysteresis = 0.1f;
            f.Graph.Connect(f.Root, utility);
            return utility;
        }

        [Test]
        public void Utility_RunsTheHighestScore()
        {
            var utility = Utility();
            var low = Child(utility, 0, 1f, Node.Status.Running);
            var high = Child(utility, 200, 3f, Node.Status.Running);

            m_Clone = f.Tree.Clone();
            m_Clone.Update();

            Assert.AreEqual(0, Runtime<CountingActionNode>(low).processCount);
            Assert.AreEqual(1, Runtime<CountingActionNode>(high).processCount);
        }

        [Test]
        public void Utility_SwitchesWhenAScoreOvertakes_ButRespectsHysteresis()
        {
            var utility = Utility();
            var fear = Child(utility, 0, 1f, Node.Status.Running);
            DriveWeight(fear, WeightFromKeyNode.Mode.Replace); // score = Fear
            var idle = Child(utility, 200, 2f, Node.Status.Running);

            m_Clone = f.Tree.Clone();
            var rtFear = Runtime<CountingActionNode>(fear);
            var rtIdle = Runtime<CountingActionNode>(idle);

            m_Clone.blackboard.SetValue("Fear", 0f);
            m_Clone.Update();
            Assert.AreEqual(1, rtIdle.processCount, "Fear 0 < idle 2");

            m_Clone.blackboard.SetValue("Fear", 2.05f); // beats idle by less than the hysteresis
            m_Clone.Update();
            Assert.AreEqual(0, rtFear.processCount);
            Assert.AreEqual(2, rtIdle.processCount, "no flip inside the hysteresis band");

            m_Clone.blackboard.SetValue("Fear", 3f);
            m_Clone.Update();
            Assert.AreEqual(1, rtFear.processCount, "Fear 3 > idle 2 + 0.1");
            Assert.AreEqual(2, rtIdle.processCount);
            Assert.IsFalse(rtIdle.IsRunning(), "the overtaken child is interrupted");
        }

        [Test]
        public void Utility_FailedChild_TriesTheNextBest()
        {
            var utility = Utility();
            var fallback = Child(utility, 0, 1f, Node.Status.Success);
            Child(utility, 200, 5f, Node.Status.Failure);

            m_Clone = f.Tree.Clone();
            Assert.AreEqual(Node.Status.Success, m_Clone.Update());
            Assert.AreEqual(1, Runtime<CountingActionNode>(fallback).processCount);
        }

        [Test]
        public void Utility_NoPositiveScore_Fails()
        {
            var utility = Utility();
            Child(utility, 0, 0f, Node.Status.Success);

            m_Clone = f.Tree.Clone();
            Assert.AreEqual(Node.Status.Failure, m_Clone.Update());
        }

        [TestCase(WeightFromKeyNode.Mode.Multiply, 2f, 4f, 8f)]
        [TestCase(WeightFromKeyNode.Mode.Replace, 2f, 4f, 4f)]
        [TestCase(WeightFromKeyNode.Mode.Add, 2f, 4f, 6f)]
        public void WeightFromKey_AppliesItsMode(WeightFromKeyNode.Mode mode, float baseWeight, float fear, float expected)
        {
            var utility = Utility();
            var node = Child(utility, 0, baseWeight, Node.Status.Running);
            DriveWeight(node, mode);

            m_Clone = f.Tree.Clone();
            m_Clone.blackboard.SetValue("Fear", fear);
            var entry = Runtime<UtilitySelectorNode>(utility).children[0]; // the outermost decorator
            Assert.AreEqual(expected, NodeWeight.Evaluate(entry), 0.0001f);
        }

        [Test]
        public void RandomRate_WeightDrivenToZero_IsNeverPicked()
        {
            var rrs = f.Create<RandomRateSelectorNode>(0, 100);
            rrs.weightingMode = RandomRateSelectorNode.WeightingMode.HigherIsBetter;
            f.Graph.Connect(f.Root, rrs);
            var muted = Child(rrs, 0, 1f, Node.Status.Success);
            DriveWeight(muted, WeightFromKeyNode.Mode.Multiply); // × Fear (0)
            var other = Child(rrs, 200, 1f, Node.Status.Success);

            m_Clone = f.Tree.Clone();
            m_Clone.blackboard.SetValue("Fear", 0f);
            for (int i = 0; i < 40; i++) m_Clone.Update();

            Assert.AreEqual(0, Runtime<CountingActionNode>(muted).processCount);
            Assert.AreEqual(40, Runtime<CountingActionNode>(other).processCount);
        }

        [Test]
        public void ComputeChances_LowerIsBetter_InvertsWeights()
        {
            var weights = new[] { 1f, 3f, 0f };
            RandomRateSelectorNode.ComputeChances(weights, RandomRateSelectorNode.WeightingMode.LowerIsBetter);
            CollectionAssert.AreEqual(new[] { 3f, 1f, 0f }, weights);
        }

        [Test]
        public void Validator_FlagsWeightsNoParentReads()
        {
            var sequence = f.Create<SequenceNode>(0, 100);
            f.Graph.Connect(f.Root, sequence);
            Child(sequence, 0, 5f, Node.Status.Success);                       // weight under a sequence
            var withModifier = Child(sequence, 200, 1f, Node.Status.Success);
            f.Graph.AddDecorator(withModifier, typeof(WeightFromKeyNode));     // modifier under a sequence
            Child(sequence, 400, 0f, Node.Status.Success);                     // old default (priority 0): fine

            var codes = GraphValidator.Validate(f.Tree).Select(i => i.Code).ToList();
            Assert.AreEqual(1, codes.Count(c => c == GraphValidator.WeightUnused));
            Assert.AreEqual(1, codes.Count(c => c == GraphValidator.WeightModifierUnused));
        }

        [Test]
        public void Validator_FlagsNeverPickedChild_ThroughAReroute()
        {
            var utility = Utility();
            var dead = Child(utility, 0, 0f, Node.Status.Success);
            f.Graph.InsertReroute(utility, dead, new Vector2(0, 150));

            Assert.IsTrue(GraphValidator.Validate(f.Tree).Any(i => i.Code == GraphValidator.WeightNeverPicked && i.Node == dead));
            Assert.AreSame(utility, WeightGraph.FindLogicalParent(f.Tree, dead, out var slot));
            Assert.IsInstanceOf<RerouteNode>(slot);
        }
    }
}
