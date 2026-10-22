using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Quests.Tests
{
    /// <summary>The quest system on its own: no dialogue, only quests, progress, tracking, outcomes and saves.</summary>
    public class QuestRuntimeTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private QuestDatabase m_Db;

        [SetUp]
        public void SetUp()
        {
            m_Db = Track(ScriptableObject.CreateInstance<QuestDatabase>());
            m_Db.name = "Quests";
            var goblins = m_Db.AddQuest("goblins");
            goblins.title.Text = "Goblin Trouble";
            goblins.goal = 20;
            var wave = m_Db.AddQuest("wave");
            wave.goal = 10;
            wave.progression = new MaxValueProgression();
            var letter = m_Db.AddQuest("letter", QuestState.Active);
            var read = new QuestStep { text = { Text = "Read it" } };
            read.doneWhen.Add(new QuestProgressCondition { quest = "letter", atLeast = 1 });
            var reply = new QuestStep { text = { Text = "Write back" } };
            reply.visibleWhen.Add(new QuestIsCondition { quest = "goblins", state = QuestState.Completed });
            letter.steps.Add(read);
            letter.steps.Add(reply);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        private QuestRuntime Runtime()
        {
            var runtime = new QuestRuntime();
            runtime.Register(m_Db.quests, m_Db.name);
            return runtime;
        }

        [Test]
        public void Quests_StartAtTheirInitialState_AndAnActiveOneIsTracked()
        {
            var runtime = Runtime();
            CollectionAssert.AreEqual(new[] { "goblins", "wave", "letter" }, runtime.Ids);
            Assert.AreEqual(QuestState.Active, runtime.Find("letter").State);
            Assert.IsTrue(runtime.IsTracked("letter"));
            Assert.AreEqual("Goblin Trouble", runtime.Find("goblins").Title);
            Assert.AreEqual("wave", runtime.Find("wave").Title, "no title: the id");
            CollectionAssert.IsEmpty(m_Db.Validate());
        }

        [Test]
        public void Progress_CountsOnlyWhileActive_ClampsAtTheGoal_AndCompletesTheQuest()
        {
            var runtime = Runtime();
            var log = new List<string>();
            runtime.ProgressChanged += (q, from, to) => log.Add($"{q.Name} {from}->{to}");
            runtime.Completed += q => log.Add($"{q.Name} completed");

            Assert.IsFalse(runtime.ReportProgress("goblins", 5), "Inactive: does not count");
            runtime.SetState("goblins", QuestState.Active);
            Assert.IsFalse(runtime.ReportProgress("goblins", 0), "Increment refuses 0");
            runtime.ReportProgress("goblins", 15);
            runtime.ReportProgress("goblins", 15);
            Assert.AreEqual(20, runtime.Find("goblins").Progress, "clamped to the goal");
            Assert.AreEqual(QuestState.Completed, runtime.Find("goblins").State);
            CollectionAssert.AreEqual(new[] { "goblins 0->15", "goblins 15->20", "goblins completed" }, log);
            Assert.IsFalse(runtime.ReportProgress("goblins", 1), "Completed: no more progress");
        }

        [Test]
        public void MaxValue_KeepsTheHighestReport()
        {
            var runtime = Runtime();
            runtime.SetState("wave", QuestState.Active);
            runtime.ReportProgress("wave", 4);
            Assert.IsFalse(runtime.ReportProgress("wave", 3), "lower: no change");
            runtime.ReportProgress("wave", 7);
            Assert.AreEqual(7, runtime.Find("wave").Progress);
            Assert.AreEqual("7/10", runtime.Find("wave").ProgressText);
        }

        [Test]
        public void Steps_UseQuestConditions_AndTheLogFollowsChanges()
        {
            var runtime = Runtime();
            int changed = 0;
            runtime.Changed += () => changed++;
            CollectionAssert.AreEqual(new[] { "Read it" }, runtime.Find("letter").Steps.Select(s => s.Text));
            Assert.IsFalse(runtime.Find("letter").Steps[0].Done);

            runtime.ReportProgress("letter");
            Assert.IsTrue(runtime.Find("letter").Steps[0].Done, "progress ≥ 1");
            runtime.SetState("goblins", QuestState.Completed);
            CollectionAssert.AreEqual(new[] { "Read it", "Write back" }, runtime.Find("letter").Steps.Select(s => s.Text), "shown once goblins is Completed");
            Assert.Greater(changed, 1);
        }

        [Test]
        public void Registration_RefusesASecondDefinition_AndAKnownIdGetsItsJournal()
        {
            var runtime = new QuestRuntime();
            runtime.RegisterId("goblins", QuestState.Active, "a world key");
            runtime.Register(m_Db.quests, m_Db.name);
            Assert.AreEqual(QuestState.Active, runtime.Find("goblins").State, "the known state stays");
            Assert.AreEqual("Goblin Trouble", runtime.Find("goblins").Title, "and gets the journal");
            Assert.IsTrue(runtime.Register(m_Db.quests[0], "again"), "the same definition again: no-op");

            LogAssert.Expect(LogType.Error, new Regex("already defined by 'Quests'"));
            Assert.IsFalse(runtime.Register(new QuestDefinition { name = "goblins" }, "Other"));
            LogAssert.Expect(LogType.Error, new Regex("unknown quest 'ghost'"));
            Assert.IsFalse(runtime.SetState("ghost", QuestState.Active));
        }

        [Test]
        public void SaveAndApply_RestoreEverything_WithoutOutcomes()
        {
            var runtime = Runtime();
            runtime.SetState("goblins", QuestState.Active);
            runtime.ReportProgress("goblins", 7);
            runtime.Track("letter", false);
            var json = JsonUtility.ToJson(runtime.Capture());

            var fresh = Runtime();
            int outcomes = 0, toastsWouldShow = 0;
            fresh.Completed += _ => outcomes++;
            fresh.StateChanged += (q, f, t) => { if (!fresh.IsApplyingSave) toastsWouldShow++; };
            var data = JsonUtility.FromJson<QuestSaveData>(json);
            data.quests.Add(new QuestSaveData.Quest { name = "removed_in_this_build", state = QuestState.Completed });
            LogAssert.Expect(LogType.Warning, new Regex("removed_in_this_build"));
            Assert.IsTrue(fresh.Apply(data));
            Assert.AreEqual(QuestState.Active, fresh.Find("goblins").State);
            Assert.AreEqual(7, fresh.Find("goblins").Progress);
            Assert.IsFalse(fresh.IsTracked("letter"));
            Assert.IsTrue(fresh.IsTracked("goblins"), "tracked when it became Active before the save");
            Assert.AreEqual(0, outcomes);
            Assert.AreEqual(0, toastsWouldShow);
        }

        [Test]
        public void TheManager_LoadsItsDatabases_AndTheOutcomeListenerReacts()
        {
            var go = Track(new GameObject("Quests"));
            go.SetActive(false);
            var manager = go.AddComponent<QuestManager>();
            var so = new SerializedObject(manager);
            var list = so.FindProperty("m_Databases");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            int ready = 0;
            manager.Ready += () => ready++;
            Assert.IsTrue(manager.Initialize());
            Assert.IsTrue(manager.Initialize(), "again: no-op");
            Assert.AreEqual(1, ready);

            var listener = go.AddComponent<QuestOutcomeListener>();
            listener.Manager = manager;
            listener.Quest = "goblins";
            int started = 0, completed = 0;
            listener.OnStarted.AddListener(() => started++);
            listener.OnCompleted.AddListener(() => completed++);
            // Edit mode: Unity does not call OnEnable on an inactive object; call it as Play mode would.
            typeof(QuestOutcomeListener).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(listener, null);

            manager.StartQuest("goblins");
            for (int i = 0; i < 20; i++) manager.ReportProgress("goblins");
            Assert.AreEqual(1, started);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(QuestState.Completed, manager.Runtime.Find("goblins").State);
        }
    }
}
