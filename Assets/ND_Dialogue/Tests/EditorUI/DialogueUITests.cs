using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>The Standard Dialogue UI built by the menu, driven by a manager, exactly as in a scene (no Play mode).</summary>
    public class DialogueUITests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private DialogueManager m_Manager;
        private StandardDialogueUI m_UI;
        private Actor m_Guard;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Guard = m_Db.AddActor("Guard");
            m_Guard.displayName.Text = "Gate Guard";
            m_Guard.emotions.Add(new ActorEmotion { id = "Neutral", tint = Color.white });
            m_Guard.emotions.Add(new ActorEmotion { id = "Angry", tint = Color.red });
            var player = m_Db.AddActor("Player", isPlayer: true);
            m_Db.AddVariable("asked", VariableValue.Bool(false));

            var c = m_Db.AddConversation("Gate");
            var hi = Line(c, m_Guard, "Hi <b>there</b>.");
            var yes = Line(c, player, "Yes.");
            yes.actions.Add(new SetVariableAction { variable = "asked", value = "true" });
            var no = Line(c, player, "No.");
            var bye = Line(c, m_Guard, "Then go.", "Angry");
            c.Link(c.StartEntry, hi); c.Link(hi, yes); c.Link(hi, no); c.Link(no, bye);

            var check = m_Db.AddConversation("Check");
            var remembered = Line(check, m_Guard, "You asked before.");
            remembered.conditions.Add(new VariableCondition { variable = "asked", value = "true" });
            check.Link(check.StartEntry, remembered);

            m_Manager = DialogueUIBuilder.CreateSetup(m_Db);
            m_UI = (StandardDialogueUI)m_Manager.View;
            SetTypingSpeed(0f);
            m_Track.Add(m_Manager.gameObject);
            m_Track.Add(m_UI.transform.root.gameObject);
            var events = Object.FindAnyObjectByType<EventSystem>();
            if (events != null) m_Track.Add(events.gameObject);
            Assert.IsTrue(m_Manager.Initialize());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private static DialogueEntry Line(Conversation c, Actor actor, string text, string emotion = null)
        {
            var e = c.AddEntry(actor.id);
            e.text.Text = text;
            e.emotion = emotion;
            return e;
        }

        private void SetTypingSpeed(float charsPerSecond)
        {
            var so = new SerializedObject(m_UI);
            so.FindProperty("m_CharactersPerSecond").floatValue = charsPerSecond;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Panel => m_UI.transform.Find("Panel").gameObject;
        private Text Find(string path) => m_UI.transform.Find(path).GetComponent<Text>();
        private List<Button> Responses => m_UI.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Response ")).ToList();

        [Test]
        public void FullConversation_ShowsLines_Choices_AndHidesAtTheEnd()
        {
            var started = new List<string>();
            m_Manager.ConversationStarted += c => started.Add("start:" + c.title);
            m_Manager.ConversationEnded += c => started.Add("end:" + c.title);
            Assert.IsFalse(Panel.activeSelf, "hidden before talking");

            Assert.IsTrue(m_Manager.StartConversation("Gate"));
            Assert.IsTrue(Panel.activeSelf);
            Assert.AreEqual("Gate Guard", Find("Panel/Subtitle/Speaker").text, "display name, not the id");
            Assert.AreEqual("Hi <b>there</b>.", Find("Panel/Subtitle/Line").text);

            m_UI.Continue();
            var buttons = Responses;
            CollectionAssert.AreEqual(new[] { "1. Yes.", "2. No." }, buttons.Select(b => b.GetComponentInChildren<Text>().text));
            Assert.IsTrue(m_UI.transform.Find("Panel/Responses").gameObject.activeSelf);

            buttons[1].onClick.Invoke(); // "No."
            Assert.AreEqual(0, Responses.Count, "the menu is cleared after a pick");
            Assert.AreEqual("Then go.", Find("Panel/Subtitle/Line").text);
            Assert.AreEqual(Color.red, Find("Panel/Subtitle/Speaker").color, "speaker tinted by the line's emotion");

            m_UI.Continue();
            Assert.IsFalse(Panel.activeSelf, "hidden after the conversation");
            Assert.IsFalse(m_Manager.IsTalking);
            CollectionAssert.AreEqual(new[] { "start:Gate", "end:Gate" }, started);
        }

        [Test]
        public void WorldState_PersistsAcrossConversations_OfTheSameManager()
        {
            m_Manager.StartConversation("Gate");
            m_UI.Continue();
            Responses[0].onClick.Invoke(); // "Yes." sets asked = true, then the conversation ends
            Assert.IsFalse(m_Manager.IsTalking);

            Assert.IsTrue(m_Manager.StartConversation("Check"));
            Assert.AreEqual("You asked before.", Find("Panel/Subtitle/Line").text);
        }

        [Test]
        public void Typewriter_FirstContinueCompletesTheLine_SecondAdvances()
        {
            SetTypingSpeed(30f);
            m_Manager.StartConversation("Gate");
            Assert.IsTrue(m_UI.IsTyping);
            Assert.AreEqual(string.Empty, Find("Panel/Subtitle/Line").text);

            m_UI.Continue();
            Assert.IsFalse(m_UI.IsTyping);
            Assert.AreEqual("Hi <b>there</b>.", Find("Panel/Subtitle/Line").text);
            Assert.AreEqual(DialogueRunnerState.ShowingLine, m_Manager.Runner.State, "still on the same line");

            m_UI.Continue();
            Assert.AreEqual(DialogueRunnerState.WaitingForChoice, m_Manager.Runner.State);
        }

        [Test]
        public void Manager_RejectsWhileTalking_AndUnknownTitles()
        {
            m_Manager.StartConversation("Gate");
            LogAssert.Expect(LogType.Warning, new Regex("already in 'Gate'"));
            Assert.IsFalse(m_Manager.StartConversation("Check"));
            m_Manager.StopConversation();
            Assert.IsFalse(Panel.activeSelf);

            LogAssert.Expect(LogType.Error, new Regex("no conversation titled 'Nope'"));
            Assert.IsFalse(m_Manager.StartConversation("Nope"));
        }

        [Test]
        public void Manager_WithoutDatabase_StaysNotReady_AndSaysSo()
        {
            var go = new GameObject("Empty Manager");
            m_Track.Add(go);
            var manager = go.AddComponent<DialogueManager>();
            LogAssert.Expect(LogType.Error, new Regex("no Dialogue Database assigned"));
            Assert.IsFalse(manager.Initialize());
            LogAssert.Expect(LogType.Warning, new Regex("rejected: state=NotReady"));
            Assert.IsFalse(manager.StartConversation("Gate"));
        }

        [TestCase("Hello <b>world</b>!", 7, "Hello <b>w</b>", false)]
        [TestCase("Hello <b>world</b>!", 11, "Hello <b>world</b>", false)]
        [TestCase("Hello <b>world</b>!", 12, "Hello <b>world</b>!", true)]
        [TestCase("<color=#ff0000>Red</color> fox", 2, "<color=#ff0000>Re</color>", false)]
        [TestCase("Plain", 99, "Plain", true)]
        [TestCase("a < b", 3, "a <", false)]
        public void TypewriterSlice_NeverShowsHalfATag(string text, int visible, string expected, bool complete)
        {
            Assert.AreEqual(expected, StandardDialogueUI.TypewriterSlice(text, visible, out bool done));
            Assert.AreEqual(complete, done);
        }
    }
}
