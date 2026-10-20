using System.Linq;
using System.Reflection;
using ND_BehaviorTree.Editor.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND_BehaviorTree.Editor.Tests
{
    public class KeySlotTests
    {
        private GraphFixture f;
        private Blackboard m_Blackboard;
        private FloatKey m_Speed;
        private BoolKey m_Alert;

        [SetUp]
        public void SetUp()
        {
            f = new GraphFixture();
            m_Blackboard = ScriptableObject.CreateInstance<Blackboard>();
            m_Speed = ScriptableObject.CreateInstance<FloatKey>();
            m_Speed.keyName = "Speed";
            m_Alert = ScriptableObject.CreateInstance<BoolKey>();
            m_Alert.keyName = "Alert";
            m_Blackboard.keys.Add(m_Speed);
            m_Blackboard.keys.Add(m_Alert);
            f.Tree.blackboard = m_Blackboard;
        }

        [TearDown]
        public void TearDown()
        {
            f.Destroy();
            Object.DestroyImmediate(m_Speed);
            Object.DestroyImmediate(m_Alert);
            Object.DestroyImmediate(m_Blackboard);
        }

        [Test]
        public void Scan_FindsKeyField_WithRequiredType()
        {
            var w = f.Create<WaitNode>(0);
            var check = f.Graph.AddDecorator(w, typeof(CheckConditionVariable));

            var slot = KeySlotScanner.Scan(check).Single(s => s.PropertyPath == "conditionKey");
            Assert.AreEqual(typeof(bool), slot.RequiredValueType);
        }

        [Test]
        public void Scan_FindsKeysInsideSerializeReferenceStrategy()
        {
            var node = f.Create<SetBlackBoardValue>(0);
            node.setBlackBoardValue = new SetFloatValue();

            var slot = KeySlotScanner.Scan(node).Single();
            Assert.AreEqual("setBlackBoardValue.floatKey", slot.PropertyPath);
            Assert.AreEqual(typeof(float), slot.RequiredValueType);
        }

        [Test]
        public void Compatibility_FollowsRequiredValueType()
        {
            var w = f.Create<WaitNode>(0);
            var check = f.Graph.AddDecorator(w, typeof(CheckConditionVariable));
            var slot = KeySlotScanner.Scan(check).Single(s => s.PropertyPath == "conditionKey");

            Assert.IsTrue(KeySlotScanner.IsCompatible(slot, m_Alert, out _));
            Assert.IsFalse(KeySlotScanner.IsCompatible(slot, m_Speed, out var reason));
            StringAssert.Contains("Bool", reason);
        }

        [Test]
        public void AssignKey_LinksKey_AndUndoRestores()
        {
            var node = f.Create<SetBlackBoardValue>(0);
            node.setBlackBoardValue = new SetFloatValue();
            UnityEditor.EditorUtility.SetDirty(node); // edits must mark the object dirty for the incremental history capture

            Assert.IsTrue(f.Graph.AssignBlackboardKey(node, "setBlackBoardValue.floatKey", m_Speed, out var reason), reason);
            Assert.AreSame(m_Speed, ((SetFloatValue)node.setBlackBoardValue).floatKey);

            GraphFixture.UndoOnce();
            Assert.IsNull(((SetFloatValue)node.setBlackBoardValue).floatKey);
        }

        [Test]
        public void AssignKey_RejectsWrongType_AndForeignKeys()
        {
            var w = f.Create<WaitNode>(0);
            var check = f.Graph.AddDecorator(w, typeof(CheckConditionVariable));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("AssignBlackboardKey rejected.*needs a Bool key"));
            Assert.IsFalse(f.Graph.AssignBlackboardKey(check, "conditionKey", m_Speed, out _));

            var foreign = ScriptableObject.CreateInstance<BoolKey>();
            foreign.keyName = "Elsewhere";
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not in the blackboard"));
                Assert.IsFalse(f.Graph.AssignBlackboardKey(check, "conditionKey", foreign, out _));
            }
            finally { Object.DestroyImmediate(foreign); }
        }

        [Test]
        public void AssignNull_ClearsTheField()
        {
            var w = f.Create<WaitNode>(0);
            var check = (CheckConditionVariable)f.Graph.AddDecorator(w, typeof(CheckConditionVariable));
            f.Graph.AssignBlackboardKey(check, "conditionKey", m_Alert, out _);

            Assert.IsTrue(f.Graph.AssignBlackboardKey(check, "conditionKey", null, out _));
            Assert.IsNull(check.conditionKey);
        }
    }

    public class ProgressReaderTests
    {
        [Test]
        public void TimeLimit_ReportsElapsedOverLimit()
        {
            var node = ScriptableObject.CreateInstance<TimeLimitNode>();
            try
            {
                node.timeLimit = 4f;
                typeof(TimeLimitNode).GetField("elapsedTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(node, 1f);

                Assert.IsTrue(ProgressReader.HasProgress(typeof(TimeLimitNode)));
                Assert.IsTrue(ProgressReader.TryGetProgress(node, out float p));
                Assert.AreEqual(0.25f, p, 0.0001f);
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void NodesWithoutProgressBar_ReportNone()
        {
            var node = ScriptableObject.CreateInstance<WaitNode>();
            try
            {
                Assert.IsFalse(ProgressReader.HasProgress(typeof(WaitNode)));
                Assert.IsFalse(ProgressReader.TryGetProgress(node, out _));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void Service_ReportsTickProgressBetweenZeroAndOne()
        {
            var service = ScriptableObject.CreateInstance<CountingService>();
            try
            {
                service.interval = 2f;
                Assert.IsTrue(ProgressReader.TryGetProgress(service, out float p));
                Assert.That(p, Is.InRange(0f, 1f));
            }
            finally { Object.DestroyImmediate(service); }
        }
    }
}
