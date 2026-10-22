using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ND.Dialogue.Tests
{
    /// <summary>
    /// The Malfoy Saga sample plays from the first chapter to the last: each chapter opens the next (saga.chapter),
    /// the main quest ticks its steps, the Yule Ball quest and the endings work, barks follow the quest.
    /// </summary>
    public class MalfoySagaTests
    {
        private const string DatabasePath = "Assets/ND_Dialogue/Samples/MalfoyStory/MalfoySaga_Dialogue.asset";
        private DialogueDatabase m_Db;
        private DialogueWorld m_World;
        private DialogueRunner m_Runner;
        private QuestRuntime m_Log;

        [SetUp]
        public void SetUp()
        {
            m_Db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
            if (m_Db == null) Assert.Ignore($"Build the sample first (ND Dialogue › Samples › Malfoy Saga › Build): {DatabasePath} is missing.");
            m_World = new DialogueWorld(m_Db);
            m_Runner = new DialogueRunner(m_Db, m_World);
            m_Log = m_World.Quests;
            m_Log.ConditionWorld = new DialogueConditionWorld(m_World, m_Runner.Memory); // as the manager does: steps may use Entry Seen
        }

        [TearDown]
        public void TearDown() => m_World?.Dispose();

        /// <summary>Plays a conversation to its end, taking the choices whose text starts with the given words.</summary>
        private List<string> Play(string conversation, params string[] choices)
        {
            var lines = new List<string>();
            void Record(DialogueLine l) => lines.Add(l.Text);
            m_Runner.LineStarted += Record;
            Assert.IsTrue(m_Runner.Start(conversation), conversation);
            var queue = new Queue<string>(choices);
            for (int guard = 0; guard < 200 && m_Runner.IsRunning; guard++)
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
        private int Number(string name) => m_World.TryGetVariable(name, out var v) ? v.intValue : int.MinValue;
        private int Chapter => Number("saga.chapter");

        [Test]
        public void TheSample_Validates()
        {
            CollectionAssert.IsEmpty(m_Db.Validate());
            Assert.AreEqual(1, Chapter, "the story starts at chapter 1");
            var saga = m_Log.Find("malfoy_saga");
            Assert.AreEqual(QuestState.Active, saga.State);
            Assert.IsTrue(saga.IsTracked);
            CollectionAssert.AreEqual(new[] { "Diagon Alley: meet the girl at Madam Malkin's" }, saga.Steps.Select(s => s.Text), "only the open chapter is listed");
        }

        [Test]
        public void TheWholeSaga_KindPath_OpensEveryChapter_AndEndsTogether()
        {
            Play("1. First Meet", "Oh?", "Er", "(Say nothing", "Shake her hand");
            Assert.AreEqual(2, Chapter);
            Play("2. Flying Lesson", "Need a hand");
            Assert.AreEqual(3, Chapter);
            Play("3. Forbidden Forest", "You can hold");
            Assert.AreEqual(4, Chapter);
            Play("4. The Dark Lord's Mission", "Sure", "Wait for her");
            Assert.AreEqual(5, Chapter);
            Assert.AreEqual(4, m_Log.Find("malfoy_saga").Steps.Count(s => s.Done));

            Play("5. Yule Ball: The Invitation", "Malfoy. Go", "Come on");
            var yule = m_Log.Find("yule_ball");
            Assert.AreEqual(QuestState.Active, yule.State);
            CollectionAssert.AreEqual(new[] { "Ask Malfoy to the Yule Ball", "Answer the paper crane" }, yule.Steps.Select(s => s.Text));
            Play("5. Yule Ball: The Paper Crane", "I'd love to");
            Assert.IsTrue(Flag("yule.partner"));

            int sequences = 0;
            void CountSequences(Conversation c, DialogueEntry e) { if (!string.IsNullOrEmpty(e.sequence)) sequences++; }
            m_Runner.EntryEntered += CountSequences;
            var ball = Play("5. Yule Ball: The Ball", "Why are you so beautiful", "Just one");
            m_Runner.EntryEntered -= CountSequences;
            Assert.AreEqual(2, sequences, "the arrival and the dance have sequences");
            Assert.That(ball, Has.Some.Contains("Two. And you're not allowed"), "high affection: a second dance");
            Assert.IsTrue(m_Log.Find("yule_ball").Steps.All(s => s.Done));
            Assert.AreEqual(QuestState.Completed, m_Log.Find("yule_ball").State);
            Assert.AreEqual(6, Chapter);

            var interview = Play("6. The Interview", "Read it out loud", "!?");
            Assert.That(interview, Has.Some.Contains("never going to be just"), "the 'together' ending");
            Assert.AreEqual(7, Chapter);
            Play("7. Epilogue: The Mirror", "Knock on the door");
            Assert.AreEqual(8, Chapter);
            var morning = Play("8. Epilogue: Morning", "Choke on the toothpaste");
            Assert.AreEqual("And if that mirror says ONE word about last night...", morning.First(), "she remembers the mirror (Entry Seen)");
            Assert.AreEqual(9, Chapter);
            var saga = m_Log.Find("malfoy_saga");
            Assert.AreEqual(QuestState.Completed, saga.State);
            Assert.AreEqual(8, saga.Steps.Count);
            Assert.IsTrue(saga.Steps.All(s => s.Done));
        }

        [Test]
        public void AHatefulPath_EndsTheSagaAtTheInterview()
        {
            m_World.SetVariable("saga.chapter", VariableValue.Int(6));
            m_World.SetVariable("Malfoy.annoyance", VariableValue.Int(7));
            var interview = Play("6. The Interview", "Read it out loud");
            Assert.That(interview, Has.Some.Contains("Blast-Ended Skrewt"));
            Assert.AreEqual(9, Chapter, "no epilogue");
            Assert.AreEqual(QuestState.Failed, m_Log.Find("malfoy_saga").State);
        }

        [Test]
        public void TheMyrtlePath_GoesThroughTheFerret_AndBarksFollowTheQuest()
        {
            m_World.SetVariable("saga.chapter", VariableValue.Int(5));
            Play("5. Yule Ball: The Invitation", "Malfoy. Go", "Fine");
            Play("5. Yule Ball: The Paper Crane", "I'd rather ask Moaning Myrtle");
            Assert.IsTrue(Flag("yule.ferret"));
            Assert.IsFalse(Flag("yule.partner"));

            int sequence = 0;
            var barker = new DialogueRunner(m_Db, m_World, null, m_Runner.Memory);
            var barks = m_Db.FindConversation("Malfoy Barks");
            Assert.AreEqual("Not. A. Word.", barker.Bark(barks.id, BarkOrder.Random, ref sequence).Single().Text);

            Play("5. Yule Ball: The Ferret", "I'm sorry");
            Assert.IsTrue(Flag("yule.partner"));
            Assert.IsTrue(m_Log.Find("yule_ball").Steps.Single(s => s.Text.StartsWith("Make it up")).Done);
            Assert.AreEqual("...Don't be late. Not that I care.", barker.Bark(barks.id, BarkOrder.Random, ref sequence).Single().Text);
        }

        [Test]
        public void TheSideQuest_CanFail()
        {
            Play("Side: Chocolate Frog", "Bin it");
            Assert.AreEqual(QuestState.Failed, m_Log.Find("chocolate_frog").State);
            Assert.AreEqual("Side", m_Log.Find("chocolate_frog").Group);
        }
    }
}
