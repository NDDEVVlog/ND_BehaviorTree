using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Tests
{
    /// <summary>The quest journal: states, journal texts, steps by conditions, tracking, toasts, save.</summary>
    public class QuestLogTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private QuestDefinition m_Key;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Db.AddVariable("has_key", VariableValue.Bool(false));
            m_Key = m_Db.AddQuest("key");
            m_Key.title.Text = "The Old Key";
            m_Key.summary.Text = "Find it.";
            var findStep = new QuestStep { text = { Text = "Find the key" } };
            findStep.doneWhen.Add(new VariableCondition { variable = "has_key", value = "true" });
            var bringStep = new QuestStep { text = { Text = "Bring it back" } };
            bringStep.visibleWhen.Add(new VariableCondition { variable = "has_key", value = "true" });
            m_Key.steps.Add(findStep);
            m_Key.steps.Add(bringStep);
            m_Db.AddQuest("side").trackWhenActive = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void TheLog_FollowsTheWorld_WithJournalTexts_StepsAndTracking()
        {
            var world = new DialogueWorld(m_Db);
            var log = new QuestLog(m_Db, world);
            var moves = new List<string>();
            log.QuestStateChanged += (q, from, to) => moves.Add($"{q.Name}:{from}->{to}");

            var key = log.Find("key");
            Assert.AreEqual("The Old Key", key.Title);
            Assert.AreEqual(QuestState.Inactive, key.State);
            Assert.IsFalse(key.IsTracked);

            world.SetQuestState("key", QuestState.Active); // from gameplay: the log hears it through the world
            key = log.Find("key");
            Assert.AreEqual(QuestState.Active, key.State);
            Assert.IsTrue(key.IsTracked, "tracked when it becomes Active");
            CollectionAssert.AreEqual(new[] { "Find the key" }, key.Steps.Select(s => s.Text), "'Bring it back' is not shown yet");
            Assert.IsFalse(key.Steps[0].Done);

            world.SetVariable("has_key", VariableValue.Bool(true));
            key = log.Find("key");
            CollectionAssert.AreEqual(new[] { true, false }, key.Steps.Select(s => s.Done), "found; 'bring it back' (no doneWhen) waits for Completed");

            world.SetQuestState("key", QuestState.Completed);
            Assert.IsTrue(log.Find("key").Steps.All(s => s.Done));
            world.SetQuestState("side", QuestState.Active);
            Assert.IsFalse(log.Find("side").IsTracked, "trackWhenActive off");
            CollectionAssert.AreEqual(new[] { "key:Inactive->Active", "key:Active->Completed", "side:Inactive->Active" }, moves);
            log.Dispose();
        }

        [Test]
        public void Track_ShowsAndHides_AndAQuestWithoutJournal_UsesItsId()
        {
            var world = new DialogueWorld(m_Db);
            var log = new QuestLog(m_Db, world);
            log.Track("side", true);
            Assert.IsTrue(log.Find("side").IsTracked);
            log.Track("side", false);
            Assert.IsFalse(log.IsTracked("side"));
            Assert.AreEqual("side", log.Find("side").Title, "no title: the id");
            log.Dispose();
        }

        [Test]
        public void TheManager_SavesTrackedQuests_AndLoadingRaisesNoToasts()
        {
            var go = new GameObject("Manager");
            go.SetActive(false);
            m_Track.Add(go);
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(manager.Initialize());
            Assert.IsNull(manager.SaveData, "nothing captured yet");

            manager.Context.SetQuestState("key", QuestState.Active);
            manager.QuestLog.Track("side", true);
            var data = manager.CaptureSave();
            CollectionAssert.AreEquivalent(new[] { "key", "side" }, data.trackedQuests);
            Assert.AreSame(data, manager.SaveData);

            manager.Context.SetQuestState("key", QuestState.Inactive);
            manager.QuestLog.Track("side", false);
            int toasts = 0;
            manager.QuestLog.QuestStateChanged += (q, f, t) => toasts++;
            var loaded = DialogueSaveSystem.FromJson(DialogueSaveSystem.ToJson(data));
            Assert.IsTrue(manager.ApplySave(loaded));
            Assert.AreSame(loaded, manager.SaveData, "the applied data");
            Assert.AreEqual(0, toasts, "loading is not 'quest started'");
            Assert.AreEqual(QuestState.Active, manager.QuestLog.Find("key").State);
            Assert.IsTrue(manager.QuestLog.IsTracked("side"));
        }

        [Test]
        public void Validate_ChecksStepConditions()
        {
            m_Key.steps[0].doneWhen.Add(new VariableCondition { variable = "missing", value = "1" });
            m_Key.steps.Add(null);
            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("Quest 'key' step 1"));
            Assert.That(issues, Has.Some.Contains("empty step slot 3"));
        }
    }
}
