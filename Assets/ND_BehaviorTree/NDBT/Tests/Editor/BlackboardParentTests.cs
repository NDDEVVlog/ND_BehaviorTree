using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ND_BehaviorTree.Editor.Tests
{
    /// <summary>
    /// World state through blackboard parents: agent blackboards keep their own keys, world keys live in one shared
    /// runtime instance of the parent asset, and writes from any agent (or dialogue) are seen by all.
    /// </summary>
    public class BlackboardParentTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private Blackboard m_World, m_Agent;

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        private T AddKey<T>(Blackboard board, string name) where T : Key
        {
            var key = Track(ScriptableObject.CreateInstance<T>());
            key.keyName = name;
            board.keys.Add(key);
            return key;
        }

        private Blackboard CloneTracked(Blackboard board)
        {
            var clone = Track(board.Clone());
            m_Track.AddRange(clone.keys);
            return clone;
        }

        [SetUp]
        public void SetUp()
        {
            SharedBlackboards.Clear();
            m_World = Track(ScriptableObject.CreateInstance<Blackboard>());
            m_World.name = "World";
            AddKey<BoolKey>(m_World, "player.has_old_key");
            AddKey<IntKey>(m_World, "quest.key");
            AddKey<FloatKey>(m_World, "shadowed");

            m_Agent = Track(ScriptableObject.CreateInstance<Blackboard>());
            m_Agent.name = "Guard";
            m_Agent.parent = m_World;
            AddKey<FloatKey>(m_Agent, "npc.alertness");
            AddKey<FloatKey>(m_Agent, "shadowed");
        }

        [TearDown]
        public void TearDown()
        {
            SharedBlackboards.Clear();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void Lookup_FindsParentKeys_OwnKeyHidesParentKey()
        {
            Assert.IsTrue(m_Agent.TryGetKey("player.has_old_key", out var worldKey));
            Assert.IsTrue(m_World.keys.Contains(worldKey));
            Assert.IsTrue(m_Agent.TryGetKey("shadowed", out var shadowed));
            Assert.IsTrue(m_Agent.keys.Contains(shadowed), "the agent's own key wins over the parent's");
            Assert.IsFalse(m_Agent.TryGetKey("nope", out _));
        }

        [Test]
        public void AllKeys_ListsOwnThenParent_WithoutShadowedDuplicates()
        {
            CollectionAssert.AreEqual(
                new[] { "npc.alertness", "shadowed", "player.has_old_key", "quest.key" },
                m_Agent.AllKeys.Select(k => k.keyName).ToArray());
        }

        [Test]
        public void TwoAgents_ShareOneWorldInstance_ButKeepTheirOwnKeys()
        {
            var guardA = CloneTracked(m_Agent);
            var guardB = CloneTracked(m_Agent);

            Assert.AreSame(guardA.parent, guardB.parent, "both agents resolve world keys into the same runtime instance");
            Assert.AreNotSame(m_World, guardA.parent, "the asset itself is never written at runtime");

            guardA.SetValue("player.has_old_key", true);
            guardA.SetValue("npc.alertness", 0.9f);

            Assert.IsTrue(guardB.GetValue<bool>("player.has_old_key"), "a world write by one agent is seen by the other");
            Assert.AreEqual(0f, guardB.GetValue<float>("npc.alertness"), "agent keys stay per agent");
            Assert.IsFalse(m_World.GetValue<bool>("player.has_old_key"), "asset unchanged");
            Assert.AreSame(guardA.parent, SharedBlackboards.GetRuntime(m_World), "non-tree systems get the same instance");
        }

        [Test]
        public void KeyChanged_IsRaisedOnTheWorldInstance_OnlyForRealChanges()
        {
            var guard = CloneTracked(m_Agent);
            var world = SharedBlackboards.GetRuntime(m_World);
            var changed = new List<string>();
            world.KeyChanged += k => changed.Add($"{k.keyName}={k.GetValueObject()}");
            var agentChanged = new List<string>();
            guard.KeyChanged += k => agentChanged.Add(k.keyName);

            guard.SetValue("quest.key", 1);
            guard.SetValue("quest.key", 1);      // same value: no event
            guard.SetValue("npc.alertness", 0.5f);

            CollectionAssert.AreEqual(new[] { "quest.key=1" }, changed);
            CollectionAssert.AreEqual(new[] { "npc.alertness" }, agentChanged, "each board reports its own keys");
        }

        [Test]
        public void ParentCycle_LookupStops()
        {
            m_World.parent = m_Agent; // Guard → World → Guard …
            Assert.IsFalse(m_Agent.TryGetKey("missing", out _));
            Assert.LessOrEqual(m_Agent.AllKeys.Count(), 5);
        }

        [Test]
        public void ContainsInChain_SeesParentKeys()
        {
            m_World.TryGetKey("quest.key", out var questKey);
            Assert.IsTrue(m_Agent.ContainsInChain(questKey));
            Assert.IsFalse(m_World.ContainsInChain(m_Agent.keys[0]));
        }
    }
}
