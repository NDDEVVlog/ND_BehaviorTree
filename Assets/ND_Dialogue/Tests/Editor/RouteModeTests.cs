using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ND.Dialogue.Tests
{
    /// <summary>Routers: an entry picks one of several valid NPC lines at random, by weight, in turn, shuffled or by utility.</summary>
    public class RouteModeTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private Actor m_Npc, m_Player;
        private DialogueEntry m_Router, m_A, m_B, m_C;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Npc = m_Db.AddActor("Malfoy");
            m_Player = m_Db.AddActor("Harry", isPlayer: true);
            m_Db.AddVariable("trust", VariableValue.Int(1));
            m_Db.AddVariable("mood", VariableValue.String("calm"));
            m_Conv = m_Db.AddConversation("Greeting");
            m_Router = m_Conv.AddEntry(m_Npc.id, "router");
            m_Router.isPassthrough = true;
            m_A = Line("A");
            m_B = Line("B");
            m_C = Line("C");
            m_Conv.Link(m_Conv.StartEntry, m_Router);
            foreach (var e in new[] { m_A, m_B, m_C }) m_Conv.Link(m_Router, e);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueEntry Line(string text, Actor actor = null)
        {
            var e = m_Conv.AddEntry((actor ?? m_Npc).id);
            e.text.Text = text;
            return e;
        }

        private DialogueLink LinkTo(DialogueEntry target) => m_Router.links.First(l => l.entryId == target.id);

        private DialogueRunner NewRunner(int seed = 7) => new DialogueRunner(m_Db, new DialogueWorld(m_Db)) { Rng = new System.Random(seed) };

        /// <summary>The first line of each of <paramref name="visits"/> runs of the conversation, with one runner (its memory persists).</summary>
        private static List<string> Visit(DialogueRunner runner, Conversation conversation, int visits)
        {
            var said = new List<string>();
            for (int i = 0; i < visits; i++)
            {
                Assert.IsTrue(runner.Start(conversation.id));
                said.Add(runner.CurrentLine.Text);
                runner.Stop();
            }
            return said;
        }

        [Test]
        public void FirstValid_IsTheDefault_AndAlwaysSaysTheFirstLink()
        {
            Assert.AreEqual(RouteMode.FirstValid, new DialogueEntry().routeMode);
            CollectionAssert.AreEqual(new[] { "A", "A", "A" }, Visit(NewRunner(), m_Conv, 3));
        }

        [Test]
        public void Random_SaysEveryLineSometimes_AndReportsTheChoice()
        {
            m_Router.routeMode = RouteMode.Random;
            var runner = NewRunner();
            var choices = new List<RouteChoice>();
            runner.RouteChosen += choices.Add;
            var said = Visit(runner, m_Conv, 60);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, said.Distinct());
            Assert.AreEqual(60, choices.Count);
            Assert.IsTrue(choices.All(c => c.From == m_Router && c.Count == 3 && c.Mode == RouteMode.Random));
        }

        [Test]
        public void Cycle_SaysEachInTurn_ThenStartsOver()
        {
            m_Router.routeMode = RouteMode.Cycle;
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "A", "B" }, Visit(NewRunner(), m_Conv, 5));
        }

        [Test]
        public void Shuffle_SaysEveryLineOncePerRound()
        {
            m_Router.routeMode = RouteMode.Shuffle;
            var said = Visit(NewRunner(), m_Conv, 9);
            for (int round = 0; round < 3; round++)
                CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, said.Skip(round * 3).Take(3), $"round {round + 1}: {string.Join(",", said)}");
        }

        [Test]
        public void WeightedRandom_FollowsTheWeights_AndNeverPicksWeightZero()
        {
            m_Router.routeMode = RouteMode.WeightedRandom;
            LinkTo(m_A).weight = 0f;
            LinkTo(m_B).weight = 1f;
            LinkTo(m_C).weight = 3f;
            var said = Visit(NewRunner(), m_Conv, 400);
            Assert.AreEqual(0, said.Count(s => s == "A"), "weight 0: never");
            int b = said.Count(s => s == "B"), c = said.Count(s => s == "C");
            Assert.That(c, Is.GreaterThan(2 * b), $"C (weight 3) about three times B (weight 1): B={b}, C={c}");
        }

        [Test]
        public void Utility_PicksTheHighestWeight_ReadFromAVariable()
        {
            m_Router.routeMode = RouteMode.Utility;
            LinkTo(m_A).weight = 2f;
            LinkTo(m_B).weightVariable = "trust"; // 1 × trust
            LinkTo(m_C).weight = 0f;
            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            CollectionAssert.AreEqual(new[] { "A" }, Visit(runner, m_Conv, 1), "trust 1 < 2");
            world.SetVariable("trust", VariableValue.Int(5));
            CollectionAssert.AreEqual(new[] { "B" }, Visit(runner, m_Conv, 1), "trust 5 > 2: the score follows the variable");
        }

        [Test]
        public void Priority_StillComesFirst_TheRouterPicksOnlyInsideTheBestGroup()
        {
            m_Router.routeMode = RouteMode.Random;
            var urgent = Line("Urgent");
            m_Conv.Link(m_Router, urgent, LinkPriority.High).conditions.Add(new VariableCondition { variable = "trust", op = CompareOp.GreaterOrEqual, value = "3" });
            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world) { Rng = new System.Random(3) };
            Assert.IsFalse(Visit(runner, m_Conv, 20).Contains("Urgent"), "its condition fails: the Normal group is used");
            world.SetVariable("trust", VariableValue.Int(3));
            CollectionAssert.AreEqual(Enumerable.Repeat("Urgent", 5), Visit(runner, m_Conv, 5), "the High link is the only one in the best group");
        }

        [Test]
        public void PlayerResponses_AreNeverPicked_TheyStillMakeTheMenu()
        {
            var ask = Line("Well?");
            ask.routeMode = RouteMode.Random;
            m_Conv.StartEntry.links.Clear();
            m_Conv.Link(m_Conv.StartEntry, ask);
            m_Conv.Link(ask, Line("Yes.", m_Player));
            m_Conv.Link(ask, Line("No.", m_Player));
            var runner = NewRunner();
            Assert.IsTrue(runner.Start(m_Conv.id));
            runner.Continue();
            Assert.AreEqual(DialogueRunnerState.WaitingForChoice, runner.State);
            Assert.AreEqual(2, runner.CurrentChoices.Count);
        }

        [Test]
        public void Cycle_GoesThroughAHub_LikeThroughPlainLinks()
        {
            m_Router.links.Clear();
            var hub = m_Conv.AddEntry(m_Npc.id, "hub");
            hub.isPassthrough = hub.isHub = true;
            m_Conv.Link(m_Router, hub);
            m_Conv.Link(hub, m_A);
            m_Conv.Link(hub, m_B);
            m_Router.routeMode = RouteMode.Cycle;
            CollectionAssert.AreEqual(new[] { "A", "B", "A" }, Visit(NewRunner(), m_Conv, 3));
        }

        [Test]
        public void CycleAndShuffleState_SurviveSaveAndLoad()
        {
            m_Router.routeMode = RouteMode.Cycle;
            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            CollectionAssert.AreEqual(new[] { "A", "B" }, Visit(runner, m_Conv, 2));
            string json = DialogueSaveSystem.ToJson(DialogueSaveSystem.Capture(m_Db, world, runner.Memory));

            var loadedWorld = new DialogueWorld(m_Db);
            var loaded = new DialogueRunner(m_Db, loadedWorld);
            Assert.AreEqual(0, DialogueSaveSystem.Apply(m_Db, loadedWorld, loaded.Memory, DialogueSaveSystem.FromJson(json)));
            CollectionAssert.AreEqual(new[] { "C", "A" }, Visit(loaded, m_Conv, 2), "goes on where the save left it");
        }

        [Test]
        public void Validate_ReportsAWeightVariableThatIsNotANumber()
        {
            LinkTo(m_A).weightVariable = "mood";
            LinkTo(m_B).weightVariable = "nope";
            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("'mood' is String, not a number"));
            Assert.That(issues, Has.Some.Contains("'nope' is unknown"));
        }
    }
}
