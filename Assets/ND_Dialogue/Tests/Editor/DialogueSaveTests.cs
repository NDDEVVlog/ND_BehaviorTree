using ND.Quests;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Dialogue.Tests
{
    /// <summary>Save / load: values, quests, seen lines, and resuming a conversation where it was.</summary>
    public class DialogueSaveTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private Actor m_Npc, m_Player;
        private string m_Folder;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Db.name = "SaveTestDb";
            m_Track.Add(m_Db);
            m_Npc = m_Db.AddActor("Draco");
            m_Npc.emotions.Add(new ActorEmotion { id = "Neutral" });
            m_Npc.attributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0) });
            m_Player = m_Db.AddActor("Harry", isPlayer: true);
            m_Player.emotions.Add(new ActorEmotion { id = "Neutral" });
            m_Db.AddVariable("gold", VariableValue.Int(10));
            m_Db.AddVariable("speed", VariableValue.Float(1.5f));
            m_Db.AddVariable("met", VariableValue.Bool(false));
            m_Db.AddVariable("motto", VariableValue.String("  spaces kept  "));
            m_Db.AddVariable("home", VariableValue.Vector3(new Vector3(1f, 2f, 3f)));
            m_Db.AddVariable("banner", VariableValue.Color(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            m_Db.AddQuest("key");
            m_Conv = m_Db.AddConversation("Hall");
            m_Folder = Path.Combine(Path.GetTempPath(), "NDDialogueSaveTests");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
            if (Directory.Exists(m_Folder)) Directory.Delete(m_Folder, true);
        }

        private DialogueEntry Say(Actor actor, string text, params string[] more)
        {
            var e = m_Conv.AddEntry(actor.id);
            e.text.Text = text;
            foreach (var m in more) e.moreLines.Add(new DialoguePage { text = { Text = m } });
            return e;
        }

        [Test]
        public void EveryValueType_Quests_AndAttributes_RoundTripThroughJson()
        {
            var world = new DialogueWorld(m_Db);
            world.SetVariable("gold", VariableValue.Int(3));
            world.SetVariable("speed", VariableValue.Float(0.1f));
            world.SetVariable("met", VariableValue.Bool(true));
            world.SetVariable("motto", VariableValue.String("  Hmph.  "));
            world.SetVariable("home", VariableValue.Vector3(new Vector3(-1.25f, 0.3f, 7f)));
            world.SetVariable("banner", VariableValue.Color(new Color(0.33f, 0.66f, 0.99f, 0.5f)));
            world.SetVariable("Draco.affection", VariableValue.Int(4));
            world.SetQuestState("key", QuestState.Completed);

            string json = DialogueSaveSystem.ToJson(DialogueSaveSystem.Capture(m_Db, world, new DialogueMemory()));
            var fresh = new DialogueWorld(m_Db);
            Assert.AreEqual(0, DialogueSaveSystem.Apply(m_Db, fresh, new DialogueMemory(), DialogueSaveSystem.FromJson(json)));

            foreach (var name in new[] { "gold", "speed", "met", "motto", "home", "banner", "Draco.affection" })
            {
                world.TryGetVariable(name, out var expected);
                fresh.TryGetVariable(name, out var actual);
                Assert.AreEqual(expected, actual, name);
            }
            fresh.TryGetQuestState("key", out var state);
            Assert.AreEqual(QuestState.Completed, state);
        }

        [Test]
        public void SeenLines_AreRemembered_Saved_AndReadByTheSeenCondition()
        {
            var first = Say(m_Npc, "Who are you?");
            var again = Say(m_Npc, "You again.");
            first.conditions.Add(new EntrySeenCondition { entryId = first.id, seen = false });
            again.conditions.Add(new EntrySeenCondition { entryId = first.id, seen = true });
            m_Conv.Link(m_Conv.StartEntry, first);
            m_Conv.Link(m_Conv.StartEntry, again);

            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            runner.Start(m_Conv.id);
            Assert.AreEqual("Who are you?", runner.CurrentLine.Text);
            runner.Stop();
            Assert.IsTrue(runner.Memory.WasSeen(m_Conv.id, first.id));

            var data = DialogueSaveSystem.Capture(m_Db, world, runner.Memory);
            var later = new DialogueRunner(m_Db, world);
            DialogueSaveSystem.Apply(m_Db, world, later.Memory, DialogueSaveSystem.FromJson(DialogueSaveSystem.ToJson(data)));
            later.Start(m_Conv.id);
            Assert.AreEqual("You again.", later.CurrentLine.Text, "after loading, the first line counts as seen");
        }

        [Test]
        public void AConversationSavedMidway_ResumesAtTheSameLine_WithoutRunningActionsAgain()
        {
            var speech = Say(m_Npc, "One.", "Two.", "Three.");
            speech.actions.Add(new SetVariableAction { variable = "gold", mode = SetVariableAction.Mode.Add, value = "1" });
            m_Conv.Link(m_Conv.StartEntry, speech);

            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            runner.Start(m_Conv.id);
            runner.Continue(); // "Two."
            var data = DialogueSaveSystem.Capture(m_Db, world, runner.Memory, runner);
            Assert.IsTrue(data.position.active);
            Assert.AreEqual(1, data.position.lineIndex);

            var fresh = new DialogueWorld(m_Db);
            var resumed = new DialogueRunner(m_Db, fresh);
            DialogueSaveSystem.Apply(m_Db, fresh, resumed.Memory, data);
            Assert.IsTrue(resumed.Resume(data.position));
            Assert.AreEqual("Two.", resumed.CurrentLine.Text);
            fresh.TryGetVariable("gold", out var gold);
            Assert.AreEqual(11, gold.intValue, "the saved value; the entry's action did not run a second time");
            resumed.Continue();
            Assert.AreEqual("Three.", resumed.CurrentLine.Text);
        }

        [Test]
        public void AConversationSavedAtAChoice_OffersTheChoicesAgain()
        {
            var question = Say(m_Npc, "Shake my hand?");
            var yes = Say(m_Player, "Fine.");
            var no = Say(m_Player, "No.");
            m_Conv.Link(m_Conv.StartEntry, question);
            m_Conv.Link(question, yes);
            m_Conv.Link(question, no);

            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            runner.Start(m_Conv.id);
            runner.Continue();
            Assert.AreEqual(DialogueRunnerState.WaitingForChoice, runner.State);
            var position = runner.CapturePosition();

            var resumed = new DialogueRunner(m_Db, world);
            Assert.IsTrue(resumed.Resume(position));
            Assert.AreEqual(DialogueRunnerState.WaitingForChoice, resumed.State);
            CollectionAssert.AreEqual(new[] { "Fine.", "No." }, resumed.CurrentChoices.Select(c => c.Text));
        }

        [Test]
        public void ASaveFromAnOlderDatabase_SkipsWhatChanged_WithWarnings()
        {
            var data = new DialogueSaveData();
            data.variables.Add(new DialogueSaveData.Variable { name = "gold", type = VariableType.Int, value = "42" });
            data.variables.Add(new DialogueSaveData.Variable { name = "removed", type = VariableType.Int, value = "1" });
            data.variables.Add(new DialogueSaveData.Variable { name = "met", type = VariableType.Int, value = "1" });
            data.quests.Add(new DialogueSaveData.Quest { name = "gone", state = QuestState.Active });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("'removed' is no longer declared"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("'met' is Bool now"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("quest 'gone'"));

            var world = new DialogueWorld(m_Db);
            Assert.AreEqual(3, DialogueSaveSystem.Apply(m_Db, world, null, data));
            world.TryGetVariable("gold", out var gold);
            Assert.AreEqual(42, gold.intValue);
        }

        [Test]
        public void FileStorage_WritesReadsAndDeletesSlots()
        {
            var storage = new FileSaveStorage(m_Folder);
            Assert.IsFalse(storage.Exists("slot 1"));
            Assert.IsTrue(storage.Write("slot 1", "{\"version\":1}"));
            Assert.IsTrue(storage.Exists("slot 1"));
            Assert.IsTrue(storage.TryRead("slot 1", out string json));
            Assert.AreEqual("{\"version\":1}", json);
            StringAssert.EndsWith("slot_1.json", storage.PathOf("slot 1"), "slot names become safe file names");
            Assert.IsTrue(storage.Delete("slot 1"));
            Assert.IsFalse(storage.TryRead("slot 1", out _));
        }

        [Test]
        public void Manager_SavesToASlot_AndLoadsItBack()
        {
            var go = new GameObject("Dialogue Manager");
            go.SetActive(false);
            m_Track.Add(go);
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(manager.Initialize());
            manager.Storage = new FileSaveStorage(m_Folder);

            manager.Context.SetVariable("gold", VariableValue.Int(99));
            string saved = null, loaded = null;
            manager.Saved += s => saved = s;
            manager.Loaded += s => loaded = s;
            Assert.IsTrue(manager.Save("A"));
            Assert.AreEqual("A", saved);
            Assert.IsTrue(manager.HasSave("A"));

            manager.Context.SetVariable("gold", VariableValue.Int(0));
            Assert.IsTrue(manager.Load("A"));
            Assert.AreEqual("A", loaded);
            manager.Context.TryGetVariable("gold", out var gold);
            Assert.AreEqual(99, gold.intValue);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no such save"));
            Assert.IsFalse(manager.Load("missing"));
        }
    }
}
