using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Dialogue.Tests
{
    /// <summary>Sequences: parsing, timing, built-in and custom commands, Continue(), following the manager's lines.</summary>
    public class SequencerTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private DialogueEntry m_First, m_Second;
        private DialogueManager m_Manager;
        private DialogueSequencer m_Sequencer;
        private GameObject m_Door;
        private DialogueActorObject m_MalfoyObject;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            var malfoy = m_Db.AddActor("Malfoy");
            m_Conv = m_Db.AddConversation("Scene");
            m_First = m_Conv.AddEntry(malfoy.id);
            m_First.text.Text = "Hmph.";
            m_First.moreLines.Add(new DialoguePage { text = { Text = "Whatever." } });
            m_Second = m_Conv.AddEntry(malfoy.id);
            m_Second.text.Text = "Potter!";
            m_Conv.Link(m_Conv.StartEntry, m_First);
            m_Conv.Link(m_First, m_Second);

            var go = new GameObject("Dialogue");
            m_Track.Add(go);
            go.SetActive(false);
            m_Manager = go.AddComponent<DialogueManager>();
            var so = new SerializedObject(m_Manager);
            so.FindProperty("m_Database").objectReferenceValue = m_Db;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(m_Manager.Initialize());
            m_Sequencer = go.AddComponent<DialogueSequencer>();
            m_Door = new GameObject("Door");
            m_Track.Add(m_Door);
            m_Sequencer.Targets.Add(new DialogueSequencer.Target { name = "Door", gameObject = m_Door });
            go.SetActive(true);
            m_Sequencer.Manager = m_Manager; // binds to the manager's lines (enabled)

            var malfoyGo = new GameObject("Malfoy");
            m_Track.Add(malfoyGo);
            m_MalfoyObject = malfoyGo.AddComponent<DialogueActorObject>();
            m_MalfoyObject.Actor = "Malfoy";
        }

        [TearDown]
        public void TearDown()
        {
            DialogueSequencer.UnregisterCommand("Shout");
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void Parse_ReadsNamesArgsAndTimes_SkipsComments_AndReportsMistakes()
        {
            var errors = new List<string>();
            var commands = DialogueSequence.Parse("Camera(CloseUp)\nAnim(speaker, Blush)@0.5; // a comment\nSendMessage(Door, Say, \"a, b\")@2;  Wait(1) ; Oops; Bad(x)@soon", errors);
            CollectionAssert.AreEqual(new[] { "Camera", "Anim", "SendMessage", "Wait" }, commands.Select(c => c.Name));
            CollectionAssert.AreEqual(new[] { "speaker", "Blush" }, commands[1].Args);
            Assert.AreEqual(0.5f, commands[1].At);
            Assert.AreEqual("a, b", commands[2].Arg(2), "quotes keep commas");
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("Name(arguments)", errors[0]);
            StringAssert.Contains("@seconds", errors[1]);

            m_Second.sequence = "Oops";
            Assert.That(m_Db.Validate(), Has.Some.Contains($"entry {m_Second.id}: sequence 'Oops'"));
        }

        [Test]
        public void ALinesSequence_RunsItsCommandsAtTheirTimes_OnTheNamedObjects()
        {
            m_First.sequence = "SetActive(Door, false)@1; SendMessage(speaker, Blush); Wait(3)";
            var ran = new List<string>();
            m_Sequencer.CommandRun += c => ran.Add(c.Name);
            int finished = 0;
            m_Sequencer.Finished += () => finished++;
            LogAssert.Expect(LogType.Error, new Regex("SendMessage Blush has no receiver"));

            m_Manager.StartConversation("Scene");
            Assert.IsTrue(m_Sequencer.IsPlaying);
            CollectionAssert.IsEmpty(ran, "nothing runs inside the line event itself");
            m_Sequencer.Advance(0f);
            CollectionAssert.AreEqual(new[] { "SendMessage", "Wait" }, ran, "the @0 commands, in written order");
            Assert.IsTrue(m_Door.activeSelf);
            m_Sequencer.Advance(1f);
            Assert.IsFalse(m_Door.activeSelf, "at 1 s");
            m_Sequencer.Advance(1.5f);
            Assert.AreEqual(0, finished, "Wait(3) keeps it busy");
            m_Sequencer.Advance(0.6f);
            Assert.AreEqual(1, finished);
            Assert.IsFalse(m_Sequencer.IsPlaying);
        }

        [Test]
        public void Continue_MovesTheConversationOn_PastAllLinesOfTheEntry_AndCustomCommandsRun()
        {
            var shouted = new List<string>();
            Assert.IsTrue(DialogueSequencer.RegisterCommand("Shout", call => { shouted.Add(call.Arg(0) + "@" + call.Target(1).name); return 0f; }));
            LogAssert.Expect(LogType.Error, new Regex("RegisterCommand rejected"));
            Assert.IsFalse(DialogueSequencer.RegisterCommand("Wait", _ => 0f), "built-ins cannot be replaced");

            m_First.sequence = "Shout(Hmph, Malfoy); Continue()@2";
            m_Second.sequence = "Shout(Potter, speaker)";
            m_Manager.StartConversation("Scene");
            m_Sequencer.Advance(0f);
            CollectionAssert.AreEqual(new[] { "Hmph@Malfoy" }, shouted);
            m_Sequencer.Advance(2f);
            Assert.AreEqual("Potter!", m_Manager.Runner.CurrentLine.Text, "both lines of the entry were passed");
            m_Sequencer.Advance(0f);
            CollectionAssert.AreEqual(new[] { "Hmph@Malfoy", "Potter@Malfoy" }, shouted, "the next entry's sequence ran; speaker = Malfoy");
        }

        [Test]
        public void AMissingTarget_IsReported_AndTheConversationEndingStopsTheSequence()
        {
            m_First.sequence = "SetActive(Nobody, true); SetActive(Door, false)@5";
            m_Manager.StartConversation("Scene");
            LogAssert.Expect(LogType.Error, new Regex("no target 'Nobody'"));
            LogAssert.Expect(LogType.Error, new Regex("needs \\(target, true\\|false\\)"));
            m_Sequencer.Advance(0f);
            m_Manager.StopConversation();
            m_Sequencer.Advance(10f);
            Assert.IsTrue(m_Door.activeSelf, "stopped with the conversation");
        }
    }
}
