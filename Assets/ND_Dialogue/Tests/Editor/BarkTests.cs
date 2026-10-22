using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue.Tests
{
    /// <summary>Barks: one line in passing, picked from START's valid links, shown in a bubble while the game goes on.</summary>
    public class BarkTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Barks;
        private Actor m_Guard, m_Player;
        private DialogueEntry m_Hmm, m_Yawn, m_Thief;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Guard = m_Db.AddActor("Guard");
            m_Player = m_Db.AddActor("Player", isPlayer: true);
            m_Db.AddVariable("alarm", VariableValue.Bool(false));
            m_Db.AddVariable("yawns", VariableValue.Int(0));
            m_Barks = m_Db.AddConversation("Guard Barks");
            m_Hmm = Line("Hmm.");
            m_Yawn = Line("*yawn*");
            m_Yawn.moreLines.Add(new DialoguePage { text = { Text = "So boring." } });
            m_Yawn.actions.Add(new SetVariableAction { variable = "yawns", mode = SetVariableAction.Mode.Add, value = "1" });
            m_Thief = Line("Stop, thief!");
            m_Barks.Link(m_Barks.StartEntry, m_Thief, LinkPriority.High).conditions.Add(new VariableCondition { variable = "alarm", value = "true" });
            m_Barks.Link(m_Barks.StartEntry, m_Hmm);
            m_Barks.Link(m_Barks.StartEntry, m_Yawn);
            var reply = m_Barks.AddEntry(m_Player.id);
            reply.text.Text = "(a player line is never barked)";
            m_Barks.Link(m_Barks.StartEntry, reply);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueEntry Line(string text)
        {
            var e = m_Barks.AddEntry(m_Guard.id);
            e.text.Text = text;
            return e;
        }

        [Test]
        public void Bark_PicksFromTheValidNpcLines_InTheChosenOrder_RunsActions_AndRemembersThem()
        {
            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            int sequence = 0;

            var first = runner.Bark(m_Barks.id, BarkOrder.Sequential, ref sequence);
            var second = runner.Bark(m_Barks.id, BarkOrder.Sequential, ref sequence);
            var third = runner.Bark(m_Barks.id, BarkOrder.Sequential, ref sequence);
            Assert.AreEqual("Hmm.", first.Single().Text);
            CollectionAssert.AreEqual(new[] { "*yawn*", "So boring." }, second.Select(l => l.Text), "every line of the entry");
            Assert.AreEqual("Hmm.", third.Single().Text, "in turn, round and round; the player line is skipped");
            Assert.IsTrue(world.TryGetVariable("yawns", out var yawns) && yawns.intValue == 1, "its actions ran");
            Assert.IsTrue(runner.Memory.WasSeen(m_Barks.id, m_Yawn.id));
            Assert.AreEqual(DialogueRunnerState.Idle, runner.State, "no conversation was started");

            world.SetVariable("alarm", VariableValue.Bool(true));
            for (int i = 0; i < 5; i++)
                Assert.AreEqual("Stop, thief!", runner.Bark(m_Barks.id, BarkOrder.Random, ref sequence).Single().Text, "the High group wins");
            Assert.AreEqual("Stop, thief!", runner.Bark(m_Barks.id, BarkOrder.FirstValid, ref sequence).Single().Text);
        }

        [Test]
        public void Bark_WithNothingValid_SaysNothing_AndABusyRunnerRefuses()
        {
            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            var silent = m_Db.AddConversation("Silent");
            int sequence = 0;
            CollectionAssert.IsEmpty(runner.Bark(silent.id, BarkOrder.Random, ref sequence));

            var talk = m_Db.AddConversation("Talk");
            var line = talk.AddEntry(m_Guard.id);
            line.text.Text = "Hello.";
            talk.Link(talk.StartEntry, line);
            runner.Start(talk.id);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("bark with another runner"));
            CollectionAssert.IsEmpty(runner.Bark(m_Barks.id, BarkOrder.Random, ref sequence));
            Assert.AreEqual("Hello.", runner.CurrentLine.Text, "the conversation is untouched");
        }

        [Test]
        public void TheBarker_SaysItThroughTheManager_IntoTheBubble_WhichShowsEachLineThenHides()
        {
            var go = new GameObject("Guard");
            m_Track.Add(go);
            go.SetActive(false);
            var manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(manager.Initialize());

            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(go.transform);
            var text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(bubble.transform);
            var ui = go.AddComponent<BarkUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("m_Root").objectReferenceValue = bubble;
            uiSo.FindProperty("m_Text").objectReferenceValue = text;
            uiSo.FindProperty("m_FaceCamera").boolValue = false;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            var barker = go.AddComponent<DialogueBarker>();
            barker.Manager = manager;
            barker.UI = ui;
            barker.Conversation = "Guard Barks";
            barker.Order = BarkOrder.FirstValid;
            barker.Cooldown = 0f;
            var heard = new List<string>();
            barker.Barked += lines => heard.AddRange(lines.Select(l => l.Text));

            Assert.IsTrue(barker.Bark());
            CollectionAssert.AreEqual(new[] { "Hmm." }, heard);
            Assert.IsTrue(ui.IsShowing);
            Assert.AreEqual("Hmm.", text.text);
            ui.Tick(ui.DurationOf("Hmm.") + 0.01f);
            Assert.IsFalse(ui.IsShowing, "one line: hidden after its time");
            Assert.IsFalse(bubble.activeSelf);

            manager.Context.SetVariable("yawns", VariableValue.Int(0));
            m_Barks.StartEntry.links.RemoveAll(l => l.entryId == m_Hmm.id);
            Assert.IsTrue(barker.Bark());
            Assert.AreEqual("*yawn*", text.text);
            ui.Tick(ui.DurationOf("*yawn*") + 0.01f);
            Assert.AreEqual("So boring.", text.text, "the next line of the entry");
            Assert.IsTrue(manager.Context.TryGetVariable("yawns", out var yawns) && yawns.intValue == 1);
        }

        private DialogueBarker Barker(out DialogueManager manager)
        {
            var go = new GameObject("Guard");
            m_Track.Add(go);
            go.SetActive(false);
            manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(manager.Initialize());
            var barker = go.AddComponent<DialogueBarker>();
            barker.Manager = manager;
            barker.Conversation = "Guard Barks";
            barker.Cooldown = 0f;
            return barker;
        }

        [Test]
        public void TheBarkersRules_SayWhyNot_CountConditionsRangeAndChance()
        {
            var barker = Barker(out var manager);
            barker.MaxBarks = 2;
            Assert.IsTrue(barker.Bark());
            Assert.IsTrue(barker.Bark());
            Assert.IsFalse(barker.CanBark(out string reason));
            StringAssert.Contains("said its 2", reason);
            barker.ResetBarks();
            Assert.IsTrue(barker.CanBark(out _), "reset: may bark again");

            barker.Conditions.Add(new VariableCondition { variable = "alarm", value = "true" });
            Assert.IsFalse(barker.CanBark(out reason));
            StringAssert.Contains("condition failed: alarm == true", reason);
            manager.Context.SetVariable("alarm", VariableValue.Bool(true));
            Assert.IsTrue(barker.CanBark(out _));

            var player = new GameObject("Player");
            m_Track.Add(player);
            player.transform.position = new Vector3(20f, 0f, 0f);
            barker.Listener = player.transform;
            barker.Range = 5f;
            Assert.IsFalse(barker.CanBark(out reason));
            StringAssert.Contains("listener too far", reason);
            player.transform.position = new Vector3(3f, 0f, 0f);
            Assert.IsTrue(barker.CanBark(out _));

            barker.Chance = 0f;
            Assert.IsFalse(barker.Bark(), "chance 0: never");
            Assert.AreEqual(0, barker.Count);
        }

        [Test]
        public void RandomOrder_DoesNotRepeatTheLastLine_WhenAnotherIsValid()
        {
            var barker = Barker(out _);
            barker.Order = BarkOrder.Random;
            barker.AvoidRepeat = true;
            var said = new List<string>();
            barker.Barked += lines => said.Add(lines[0].Text);
            for (int i = 0; i < 12; i++) Assert.IsTrue(barker.Bark());
            for (int i = 1; i < said.Count; i++) Assert.AreNotEqual(said[i - 1], said[i], $"bark {i} repeated '{said[i]}'");
        }

        [Test]
        public void ABarkConversation_IsMarked_AndValidateFlagsPlayerLinesFromStart()
        {
            CollectionAssert.IsEmpty(m_Db.Validate(), "not marked: a normal conversation may start with a choice");
            m_Barks.isBark = true;
            Assert.That(m_Db.Validate(), Has.Some.Contains("Bark conversation 'Guard Barks': START links to").And.Contains("a player line"));
        }
    }
}
