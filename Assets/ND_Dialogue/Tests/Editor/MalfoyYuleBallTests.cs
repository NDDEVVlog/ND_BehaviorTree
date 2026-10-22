using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ND.Dialogue.Tests
{
    /// <summary>The Yule Ball sample database plays its quest: invitation → crane → (ferret →) ball, the quest log ticks, barks follow.</summary>
    public class MalfoyYuleBallTests
    {
        private const string DatabasePath = "Assets/ND_Dialogue/Samples/MalfoyStory/MalfoyYuleBall_Dialogue.asset";
        private DialogueDatabase m_Db;
        private DialogueWorld m_World;
        private DialogueRunner m_Runner;
        private QuestLog m_Log;

        [SetUp]
        public void SetUp()
        {
            m_Db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
            if (m_Db == null) Assert.Ignore($"Build the sample first (ND Dialogue › Samples › Malfoy Story › Build Yule Ball): {DatabasePath} is missing.");
            m_World = new DialogueWorld(m_Db);
            m_Runner = new DialogueRunner(m_Db, m_World);
            m_Log = new QuestLog(m_Db, m_World, m_Runner.Memory);
        }

        [TearDown]
        public void TearDown() => m_Log?.Dispose();

        /// <summary>Plays a conversation to its end, taking the choices whose text starts with the given words.</summary>
        private List<string> Play(string conversation, params string[] choices)
        {
            var lines = new List<string>();
            void Record(DialogueLine l) => lines.Add(l.Text);
            m_Runner.LineStarted += Record;
            Assert.IsTrue(m_Runner.Start(conversation), conversation);
            var queue = new Queue<string>(choices);
            for (int guard = 0; guard < 100 && m_Runner.IsRunning; guard++)
            {
                if (m_Runner.State == DialogueRunnerState.ShowingLine) { m_Runner.Continue(); continue; }
                Assert.IsTrue(queue.Count > 0, $"'{conversation}' asks: {string.Join(" | ", m_Runner.CurrentChoices.Select(c => c.Text))}");
                string wanted = queue.Dequeue();
                var choice = m_Runner.CurrentChoices.FirstOrDefault(c => c.Text.StartsWith(wanted));
                Assert.IsNotNull(choice.Entry, $"no choice '{wanted}' in: {string.Join(" | ", m_Runner.CurrentChoices.Select(c => c.Text))}");
                m_Runner.Choose(choice.Index);
            }
            m_Runner.LineStarted -= Record;
            return lines;
        }

        private bool Flag(string name) => m_World.TryGetVariable(name, out var v) && v.boolValue;

        [Test]
        public void TheSample_Validates()
        {
            CollectionAssert.IsEmpty(m_Db.Validate());
            Assert.IsNotNull(m_Db.FindQuest("yule_ball"));
        }

        [Test]
        public void TheKindPath_EndsWithTwoDances_AndEveryStepTicks()
        {
            Play("Yule 1. The Invitation", "Come on");
            var quest = m_Log.Find("yule_ball");
            Assert.AreEqual(QuestState.Active, quest.State);
            Assert.IsTrue(quest.IsTracked);
            CollectionAssert.AreEqual(new[] { "Ask Malfoy to the Yule Ball", "Answer the paper crane" }, quest.Steps.Select(s => s.Text));

            Play("Yule 2. The Paper Crane", "I'd love to");
            Assert.IsTrue(Flag("yule.partner"));
            Assert.IsFalse(m_Log.Find("yule_ball").Steps.Any(s => s.Text.StartsWith("Make it up")), "no ferret step on the kind path");

            int sequenceCount = 0;
            void CountSequences(Conversation c, DialogueEntry e) { if (!string.IsNullOrEmpty(e.sequence)) sequenceCount++; }
            m_Runner.EntryEntered += CountSequences;
            var ball = Play("Yule 4. The Ball", "Why are you so beautiful", "Just one");
            m_Runner.EntryEntered -= CountSequences;
            Assert.AreEqual(2, sequenceCount, "the arrival and the dance have sequences");
            Assert.That(ball, Has.Some.Contains("Two. And you're not allowed"), "affection 2+: a second dance");
            quest = m_Log.Find("yule_ball");
            Assert.AreEqual(QuestState.Completed, quest.State);
            Assert.IsTrue(quest.Steps.All(s => s.Done));
        }

        [Test]
        public void TheMyrtlePath_GoesThroughTheFerret_AndBarksFollowTheQuest()
        {
            Play("Yule 1. The Invitation", "Fine");
            Play("Yule 2. The Paper Crane", "I'd rather ask Moaning Myrtle");
            Assert.IsTrue(Flag("yule.ferret"));
            Assert.IsFalse(Flag("yule.partner"));
            Assert.That(Play("Yule 4. The Ball"), Has.Some.Contains("No partner"), "no partner yet");

            int sequence = 0;
            var barker = new DialogueRunner(m_Db, m_World, null, m_Runner.Memory);
            var conversation = m_Db.FindConversation("Malfoy Barks");
            Assert.AreEqual("Not. A. Word.", barker.Bark(conversation.id, BarkOrder.Random, ref sequence).Single().Text);

            Play("Yule 3. The Ferret", "I'm sorry");
            Assert.IsTrue(Flag("yule.partner"));
            Assert.IsTrue(m_Log.Find("yule_ball").Steps.Single(s => s.Text.StartsWith("Make it up")).Done);
            Assert.AreEqual("...Don't be late. Not that I care.", barker.Bark(conversation.id, BarkOrder.Random, ref sequence).Single().Text);
        }

        [Test]
        public void TheSideQuest_CanFail()
        {
            Play("Side. Chocolate Frog", "Bin it");
            Assert.AreEqual(QuestState.Failed, m_Log.Find("chocolate_frog").State);
            Assert.AreEqual("Side", m_Log.Find("chocolate_frog").Group);
        }
    }
}
