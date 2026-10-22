using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>The "Test Dialogue" chat: bubbles, effect lines, replies, start at an id, graph highlight, no scene events.</summary>
    public class DialogueTestPanelTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private Actor m_Npc, m_Player;
        private DialogueEntry m_Greet, m_Yes, m_No, m_Thanks;
        private DialogueTestPanel m_Panel;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Npc = m_Db.AddActor("Draco");
            m_Npc.emotions.Add(new ActorEmotion { id = "Neutral" });
            m_Npc.emotions.Add(new ActorEmotion { id = "Embarrassed" });
            m_Player = m_Db.AddActor("Harry", isPlayer: true);
            m_Player.emotions.Add(new ActorEmotion { id = "Neutral" });
            m_Db.AddVariable("affection", VariableValue.Int(0));
            m_Db.events.Add(new EventDefinition { name = "blush" });
            m_Conv = m_Db.AddConversation("Hall");

            m_Greet = m_Conv.AddEntry(m_Npc.id);
            m_Greet.text.Text = "Potter.";
            m_Greet.moreLines.Add(new DialoguePage { text = { Text = "It's not like I waited." }, emotion = "Embarrassed" });
            m_Yes = m_Conv.AddEntry(m_Player.id);
            m_Yes.text.Text = "Thanks for waiting.";
            m_Yes.actions.Add(new SetVariableAction { variable = "affection", mode = SetVariableAction.Mode.Add, value = "2" });
            m_No = m_Conv.AddEntry(m_Player.id);
            m_No.text.Text = "Whatever.";
            m_Thanks = m_Conv.AddEntry(m_Npc.id);
            m_Thanks.text.Text = "Hmph.";
            m_Thanks.actions.Add(new RaiseEventAction { eventName = "blush" });
            m_Conv.Link(m_Conv.StartEntry, m_Greet);
            m_Conv.Link(m_Greet, m_Yes);
            m_Conv.Link(m_Greet, m_No);
            m_Conv.Link(m_Yes, m_Thanks);

            m_Panel = new DialogueTestPanel();
            m_Panel.Bind(m_Db, "en", null, m_Conv.id);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private List<string> Bubbles() => m_Panel.Log.Query<Label>("bubble").ToList().Select(l => l.text).ToList();
        private List<string> Effects() => m_Panel.Log.Query<Label>(className: "dlg-effect").ToList().Select(l => l.text).ToList();
        private List<string> Replies() => m_Panel.Replies.Query<Button>().ToList().Select(b => b.text).ToList();

        [Test]
        public void TheChat_ShowsLines_Choices_AndWhatHappened()
        {
            Assert.IsTrue(m_Panel.Start());
            CollectionAssert.AreEqual(new[] { "Potter." }, Bubbles());
            StringAssert.StartsWith("Next line", Replies().Single(), "more lines in the entry");

            m_Panel.Continue();
            CollectionAssert.AreEqual(new[] { "Potter.", "It's not like I waited." }, Bubbles());
            StringAssert.StartsWith("Continue", Replies().Single(), "the last line of the entry");
            m_Panel.Continue(); // the choices come after the last line
            Assert.AreEqual(2, Replies().Count, "the two choices");
            StringAssert.Contains("Thanks for waiting.", Replies()[0]);

            int raised = 0;
            void OnRaised(string n, string a) => raised++;
            DialogueEvents.Raised += OnRaised;
            try { m_Panel.Choose(0); }
            finally { DialogueEvents.Raised -= OnRaised; }

            CollectionAssert.AreEqual(new[] { "Potter.", "It's not like I waited.", "Thanks for waiting.", "Hmph." }, Bubbles());
            var effects = Effects();
            Assert.That(effects, Has.Some.Contains("affection:  0 → 2"));
            Assert.That(effects, Has.Some.Contains("⚡ blush"));
            Assert.That(effects, Has.Some.Contains($"#{m_Yes.id} does:"));
            Assert.AreEqual(0, raised, "events stay in the chat: scene listeners do not react to a test");
        }

        [Test]
        public void IdsAreShown_AndAConversationCanStartAtAnyEntry()
        {
            Assert.IsTrue(m_Panel.StartAt(m_Conv.id, m_Thanks.id));
            CollectionAssert.AreEqual(new[] { "Hmph." }, Bubbles());
            Assert.AreEqual($"#{m_Thanks.id}", m_Panel.Log.Q<Label>("entry-id").text);

            var view = new DialogueGraphCanvas();
            view.Load(m_Db, m_Conv, "en");
            Assert.AreEqual($"#{m_Greet.id}", view.GetNodeView(m_Greet.id.ToString()).Q<Label>("entry-id").text, "every node shows its id");
        }

        [Test]
        public void TheGraph_HighlightsTheCurrentEntry_AndTheSeenOnes()
        {
            m_Panel.Start();
            m_Panel.Continue();
            m_Panel.Continue();
            m_Panel.Choose(1); // "Whatever." → end
            Assert.IsTrue(m_Panel.Graph.GetNodeView(m_Greet.id.ToString()).ClassListContains("dlg-node--visited"));
            Assert.IsFalse(m_Panel.Graph.GetNodeView(m_Thanks.id.ToString()).ClassListContains("dlg-node--visited"));
            Assert.IsNotNull(m_Panel.Log.Query<Label>(className: "dlg-chat__system--end").First(), "the end is marked");

            m_Panel.Start();
            Assert.IsTrue(m_Panel.Graph.GetNodeView(m_Greet.id.ToString()).ClassListContains("dlg-node--current"));
        }

        [Test]
        public void AHeadlessSession_ReportsTheSameRunAsText()
        {
            var session = new DialogueTestSession(m_Db, "en");
            var shown = session.Start(m_Conv);
            StringAssert.Contains($"#{m_Greet.id} 1/2 Draco", DialogueTestSession.Format(shown));
            session.Continue();
            session.Continue();
            CollectionAssert.AreEqual(new[] { $"1. Thanks for waiting. (#{m_Yes.id})", $"2. Whatever. (#{m_No.id})" }, session.ChoiceLabels());
            string text = DialogueTestSession.Format(session.Choose(0));
            StringAssert.Contains("Harry (choice): \"Thanks for waiting.\"", text);
            StringAssert.Contains("✎ affection:  0 → 2", text);
            StringAssert.Contains("⚡ blush", text);
            StringAssert.Contains("— end of conversation —", DialogueTestSession.Format(session.Continue()), "after the last line");
            Assert.AreEqual("2", session.Values()["affection"]);

            Assert.IsNull(session.Choose(5));
            StringAssert.Contains("No choice", session.Error);
            Assert.IsNull(session.Start(m_Conv, 999));
            StringAssert.Contains("no entry #999", session.Error);
        }

        [Test]
        public void ResetWorld_StartsAgainFromInitialValues_WithAnEmptyChat()
        {
            m_Panel.Start();
            m_Panel.Continue();
            m_Panel.Continue();
            m_Panel.Choose(0);
            m_Panel.ResetWorld();
            Assert.AreEqual(0, Bubbles().Count);
            m_Panel.StartAt(m_Conv.id, m_Yes.id);
            Assert.That(Effects(), Has.Some.Contains("affection:  0 → 2"), "the value started from 0 again");
        }
    }
}
