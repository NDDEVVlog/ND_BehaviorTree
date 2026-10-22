using System.Collections.Generic;
using System.Linq;
using ND.Quests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Dialogue.Tests
{
    /// <summary>
    /// Dialogue on top of the quest system: the world's quests are a QuestRuntime (its own, or a QuestManager's shared
    /// with gameplay), steps read dialogue variables, dialogue reports progress, saves keep progress and tracking.
    /// </summary>
    public class DialogueQuestTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private QuestDatabase m_Quests;
        private QuestDefinition m_Key;

        [SetUp]
        public void SetUp()
        {
            m_Db = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Db.name = "Dialogue";
            m_Db.AddVariable("has_key", VariableValue.Bool(false));
            m_Quests = Track(ScriptableObject.CreateInstance<QuestDatabase>());
            m_Quests.name = "Quests";
            m_Db.questDatabases.Add(m_Quests);
            m_Key = m_Quests.AddQuest("key");
            m_Key.title.Text = "The Old Key";
            m_Key.summary.Text = "Find it.";
            var findStep = new QuestStep { text = { Text = "Find the key" } };
            findStep.doneWhen.Add(new VariableCondition { variable = "has_key", value = "true" });
            var bringStep = new QuestStep { text = { Text = "Bring it back" } };
            bringStep.visibleWhen.Add(new VariableCondition { variable = "has_key", value = "true" });
            m_Key.steps.Add(findStep);
            m_Key.steps.Add(bringStep);
            var rats = m_Quests.AddQuest("rats");
            rats.goal = 3;
            rats.title.Text = "Rat Problem";
            m_Db.AddQuest("side").trackWhenActive = false; // an older, database-own quest still works
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        [Test]
        public void TheWorldsQuests_ComeFromQuestDatabasesAndTheDatabase_AndStepsReadDialogueVariables()
        {
            var world = new DialogueWorld(m_Db);
            var quests = world.Quests;
            CollectionAssert.AreEquivalent(new[] { "side", "key", "rats" }, quests.Ids);
            Assert.IsTrue(m_Db.Schema.HasQuest("key"), "dialogue conditions may name Quest Database quests");
            var moves = new List<string>();
            quests.StateChanged += (q, from, to) => moves.Add($"{q.Name}:{from}->{to}");

            world.SetQuestState("key", QuestState.Active);
            var key = quests.Find("key");
            Assert.AreEqual("The Old Key", key.Title);
            Assert.IsTrue(key.IsTracked, "tracked when it becomes Active");
            CollectionAssert.AreEqual(new[] { "Find the key" }, key.Steps.Select(s => s.Text));

            world.SetVariable("has_key", VariableValue.Bool(true));
            CollectionAssert.AreEqual(new[] { true, false }, quests.Find("key").Steps.Select(s => s.Done), "a dialogue variable ticks the step");
            world.SetQuestState("side", QuestState.Active);
            Assert.IsFalse(quests.Find("side").IsTracked, "trackWhenActive off");
            CollectionAssert.AreEqual(new[] { "key:Inactive->Active", "side:Inactive->Active" }, moves);
            CollectionAssert.IsEmpty(m_Db.Validate());
        }

        [Test]
        public void DialogueReportsProgress_AndChecksIt_TheGoalCompletesTheQuest()
        {
            var world = new DialogueWorld(m_Db);
            var c = m_Db.AddConversation("Rats");
            var npc = m_Db.AddActor("Innkeeper");
            var line = c.AddEntry(npc.id);
            line.text.Text = "Got one!";
            line.actions.Add(new ReportQuestProgressAction { quest = "rats", amount = 1 });
            c.Link(c.StartEntry, line);
            var done = new QuestProgressAtLeastCondition { quest = "rats", atLeast = 3 };

            int completed = 0;
            world.Quests.Completed += _ => completed++;
            var runner = new DialogueRunner(m_Db, world);
            runner.Start(c.id);
            Assert.AreEqual(0, world.Quests.Find("rats").Progress, "not Active: the report does not count");

            world.SetQuestState("rats", QuestState.Active);
            for (int i = 0; i < 3; i++) { runner.Stop(); runner.Start(c.id); }
            Assert.AreEqual(3, world.Quests.Find("rats").Progress);
            Assert.IsTrue(done.Evaluate(world));
            Assert.AreEqual(QuestState.Completed, world.Quests.Find("rats").State, "goal reached");
            Assert.AreEqual(1, completed);
            Assert.AreEqual("3/3", world.Quests.Find("rats").ProgressText);
        }

        private DialogueManager Manager(QuestManager quests)
        {
            var go = new GameObject("Dialogue");
            Track(go);
            go.SetActive(false);
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.FindProperty("m_QuestManager").objectReferenceValue = quests;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(manager.Initialize());
            return manager;
        }

        private QuestManager QuestManager()
        {
            var go = new GameObject("Quests");
            Track(go);
            go.SetActive(false);
            var quests = go.AddComponent<QuestManager>();
            var so = new SerializedObject(quests);
            var list = so.FindProperty("m_Databases");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = m_Quests;
            so.ApplyModifiedPropertiesWithoutUndo();
            return quests;
        }

        [Test]
        public void WithAQuestManager_DialogueAndGameplayShareTheQuests()
        {
            var quests = QuestManager();
            var manager = Manager(quests);
            Assert.AreSame(quests.Runtime, manager.Quests, "one runtime for everyone");
            Assert.IsTrue(quests.Runtime.Has("side"), "the dialogue database's own quests join the manager's");

            quests.StartQuest("rats");             // gameplay
            Assert.IsTrue(manager.Context.TryGetQuestState("rats", out var state) && state == QuestState.Active, "dialogue sees it");
            manager.Context.SetQuestState("key", QuestState.Active); // dialogue
            Assert.AreEqual(QuestState.Active, quests.Runtime.Find("key").State, "gameplay sees it");
            quests.ReportProgress("rats");
            Assert.AreEqual(1, quests.Runtime.Find("rats").Progress);

            manager.Context.SetVariable("has_key", VariableValue.Bool(true));
            Assert.IsTrue(quests.Runtime.Find("key").Steps[0].Done, "steps read the dialogue world through the manager");
        }

        [Test]
        public void TheSave_KeepsStatesProgressAndTracking_AndLoadingRaisesNoOutcomes()
        {
            var quests = QuestManager();
            var manager = Manager(quests);
            Assert.IsNull(manager.SaveData, "nothing captured yet");

            quests.StartQuest("rats");
            quests.ReportProgress("rats");
            manager.Context.SetQuestState("key", QuestState.Completed);
            quests.Runtime.Track("side", true);
            var data = manager.CaptureSave();
            Assert.AreSame(data, manager.SaveData);
            CollectionAssert.AreEquivalent(new[] { "rats", "side" }, data.trackedQuests, "rats (tracked when it became Active) and side (by hand)");
            Assert.AreEqual(1, data.quests.Single(q => q.name == "rats").progress);

            quests.Runtime.SetState("key", QuestState.Inactive);
            quests.Runtime.SetProgress("rats", 0);
            quests.Runtime.Track("side", false);
            int outcomes = 0, toasts = 0;
            quests.Runtime.Completed += _ => outcomes++;
            quests.Runtime.StateChanged += (q, f, t) => { if (!quests.Runtime.IsApplyingSave) toasts++; };
            var loaded = DialogueSaveSystem.FromJson(DialogueSaveSystem.ToJson(data));
            Assert.IsTrue(manager.ApplySave(loaded));
            Assert.AreEqual(0, outcomes, "loading is not 'quest completed'");
            Assert.AreEqual(0, toasts, "nor 'quest started'");
            Assert.AreEqual(QuestState.Completed, quests.Runtime.Find("key").State);
            Assert.AreEqual(1, quests.Runtime.Find("rats").Progress);
            Assert.IsTrue(quests.Runtime.IsTracked("side"));
            Assert.AreSame(loaded, manager.SaveData, "the applied data");
        }

        [Test]
        public void Validate_ChecksStepConditions_AndTheQuestDatabases()
        {
            m_Key.steps[0].doneWhen.Add(new VariableCondition { variable = "missing", value = "1" });
            m_Key.steps.Add(null);
            m_Db.questDatabases.Add(null);
            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("Quest 'key' step 1"));
            Assert.That(issues, Has.Some.Contains("empty step slot 3"));
            Assert.That(issues, Has.Some.Contains("Quest Databases: empty slot"));
        }
    }
}
