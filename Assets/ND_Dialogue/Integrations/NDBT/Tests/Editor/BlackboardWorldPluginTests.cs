using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.Dialogue.NDBT.Editor;
using ND_BehaviorTree;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.NDBT.Tests
{
    public class BlackboardWorldPluginTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Blackboard m_World;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_World = ScriptableObject.CreateInstance<Blackboard>();
            m_Track.Add(m_Db);
            m_Track.Add(m_World);
            var key = ScriptableObject.CreateInstance<IntKey>();
            key.keyName = "quest.key";
            m_World.keys.Add(key);
            m_Track.Add(key);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (m_Db.schemaSource != null) m_Track.Add(m_Db.schemaSource);
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void Plugin_IsRegisteredByTheDomainScan_WithItsTab()
        {
            Assert.IsTrue(DialoguePluginRegistry.Entries.Any(e => e.Plugin is BlackboardWorldPlugin && e.Installed));
            Assert.IsTrue(DialoguePluginRegistry.DatabaseTabs.Any(t => t.Title == BlackboardWorldPlugin.TabTitle));
        }

        [Test]
        public void Use_MakesTheBlackboardTheWorld_AndValidationSeesItsQuests()
        {
            BlackboardWorldPlugin.Use(new DialoguePluginArgs { Database = m_Db }, m_World);

            var source = m_Db.schemaSource as BlackboardSchemaSource;
            Assert.IsNotNull(source);
            Assert.AreSame(m_World, source.blackboard);
            CollectionAssert.AreEqual(new[] { "key" }, m_Db.Schema.QuestNames.ToArray());
        }

        [Test]
        public void Rule_ReportsASourceWithoutBlackboard()
        {
            m_Db.schemaSource = ScriptableObject.CreateInstance<BlackboardSchemaSource>();
            Assert.That(DialoguePluginRegistry.Validate(m_Db), Has.Some.Contains("[Behavior Tree World]").And.Contains("has no blackboard"));
        }
    }
}
