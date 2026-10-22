using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor.Import;
using NUnit.Framework;
using UnityEngine;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>Twine (Twee, HTML) and Arcweave (JSON) stories become conversations that play as the source reads.</summary>
    public class DialogueImportTests
    {
        private DialogueDatabase m_Db;

        [SetUp]
        public void SetUp() => m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_Db);

        private const string Twee = @":: StoryTitle
The Gate

:: StoryData
{""start"": ""Gate""}

:: Gate [intro] {""position"":""100,100""}
Guard: Halt! Who goes there?
(set: $visits to $visits + 1)

It's late.
[[A friend.->Friend]]
[[Nobody|Leave]]
(if: $gold > 2)[[[Bribe->Bribe]]]

:: Friend
(set: $trust to 1)
Guard: Then pass, friend. You have $gold coins.
[[Leave]]

:: Bribe
Guard: ...Fine.

:: Leave
(goto: ""Gate"")
Bye.
";

        /// <summary>Continues through lines, takes the choices by their text, returns every line shown.</summary>
        private static List<string> Play(DialogueRunner runner, Conversation conversation, List<List<string>> menus, params string[] choices)
        {
            var lines = new List<string>();
            runner.LineStarted += l => lines.Add(l.Text);
            runner.Start(conversation.id);
            var queue = new Queue<string>(choices);
            for (int guard = 0; guard < 50 && runner.IsRunning; guard++)
            {
                if (runner.State == DialogueRunnerState.ShowingLine) { runner.Continue(); continue; }
                menus?.Add(runner.CurrentChoices.Select(c => c.Text).ToList());
                string wanted = queue.Dequeue();
                runner.Choose(runner.CurrentChoices.First(c => c.Text == wanted).Index);
            }
            return lines;
        }

        [Test]
        public void Twee_PassagesBecomeLines_LinksChoices_SetsActions_AndIfsConditions()
        {
            var report = TwineImporter.Import(m_Db, TwineImporter.ParseTwee(Twee));
            var conversation = report.Conversation;
            Assert.AreEqual("The Gate", conversation.title);
            CollectionAssert.AreEquivalent(new[] { "Guard", "NPC", "Player" }, report.CreatedActors);
            CollectionAssert.AreEquivalent(new[] { "visits", "gold", "trust" }, report.CreatedVariables);
            Assert.That(report.Warnings, Has.Some.Contains("(goto:)"), "unsupported macros are reported");
            CollectionAssert.IsEmpty(m_Db.Validate(), string.Join("\n", m_Db.Validate()));

            var world = new DialogueWorld(m_Db);
            var runner = new DialogueRunner(m_Db, world);
            var menus = new List<List<string>>();
            var lines = Play(runner, conversation, menus, "A friend.");
            CollectionAssert.AreEqual(new[] { "A friend.", "Nobody" }, menus[0], "Bribe needs gold > 2");
            CollectionAssert.AreEqual(new[] { "Halt! Who goes there?", "It's late.", "Then pass, friend. You have 0 coins.", "Bye." }, lines,
                "paragraphs = lines, 'Guard:' = the speaker, $gold = its value, a lone link continues");
            Assert.IsTrue(world.TryGetVariable("visits", out var visits) && visits.intValue == 1);
            Assert.IsTrue(world.TryGetVariable("trust", out var trust) && trust.intValue == 1);
            Assert.AreEqual("Guard", m_Db.FindActor(conversation.FindEntry(conversation.StartEntry.links[0].entryId).actorId).name);

            world.SetVariable("gold", VariableValue.Int(5));
            menus.Clear();
            Play(new DialogueRunner(m_Db, world), conversation, menus, "Bribe");
            CollectionAssert.AreEqual(new[] { "A friend.", "Nobody", "Bribe" }, menus[0]);
        }

        [Test]
        public void TwineHtml_IsReadToo()
        {
            const string html = @"<tw-storydata name=""Tiny &amp; Short"" startnode=""2"" format=""Harlowe"">
<tw-passagedata pid=""1"" name=""End"" tags="""" position=""300,100"">Malfoy: Hmph.</tw-passagedata>
<tw-passagedata pid=""2"" name=""Hi"" tags="""" position=""100,100"">Malfoy: You again?
[[Yes &amp; no-&gt;End]]
[[Run->End]]</tw-passagedata>
</tw-storydata>";
            var story = TwineImporter.ParseHtml(html);
            Assert.AreEqual("Tiny & Short", story.Name);
            Assert.AreEqual("Hi", story.Start);
            var report = TwineImporter.Import(m_Db, story);
            var menus = new List<List<string>>();
            var lines = Play(new DialogueRunner(m_Db, new DialogueWorld(m_Db)), report.Conversation, menus, "Yes & no");
            CollectionAssert.AreEqual(new[] { "Yes & no", "Run" }, menus[0]);
            CollectionAssert.AreEqual(new[] { "You again?", "Hmph." }, lines);
        }

        private const string Arcweave = @"{
  ""name"": ""Market"",
  ""startingElement"": ""e1"",
  ""components"": { ""c1"": { ""name"": ""Merchant"" } },
  ""variables"": { ""v1"": { ""name"": ""coins"", ""type"": ""integer"", ""value"": 0 } },
  ""elements"": {
    ""e1"": { ""title"": ""<p>Greet</p>"", ""content"": ""<p>Hello, traveler.</p>"", ""components"": [""c1""], ""outputs"": [""k1""] },
    ""e2"": { ""title"": ""<p>Pay</p>"", ""content"": ""<p>Good.</p><pre><code>coins = coins + 5</code></pre>"", ""components"": [""c1""], ""outputs"": [""k3""] },
    ""e3"": { ""content"": ""<p>Rich!</p>"", ""components"": [""c1""], ""outputs"": [] },
    ""e4"": { ""content"": ""<p>Poor.</p>"", ""components"": [""c1""], ""outputs"": [] }
  },
  ""connections"": {
    ""k1"": { ""label"": ""<p>Hi!</p>"", ""sourceid"": ""e1"", ""targetid"": ""e2"" },
    ""k3"": { ""label"": null, ""sourceid"": ""e2"", ""targetid"": ""b1"" },
    ""k4"": { ""label"": null, ""sourceid"": ""b1"", ""targetid"": ""e3"" },
    ""k5"": { ""label"": null, ""sourceid"": ""b1"", ""targetid"": ""j1"" }
  },
  ""branches"": { ""b1"": { ""conditions"": { ""ifCondition"": ""c1"", ""elseCondition"": ""c2"" } } },
  ""conditions"": {
    ""c1"": { ""output"": ""k4"", ""script"": ""<pre><code>coins &gt;= 5</code></pre>"" },
    ""c2"": { ""output"": ""k5"", ""script"": null }
  },
  ""jumpers"": { ""j1"": { ""elementId"": ""e4"" } }
}";

        [Test]
        public void Arcweave_ElementsLines_LabelsChoices_BranchesConditions_ScriptsActions()
        {
            var report = ArcweaveImporter.Import(m_Db, Arcweave);
            Assert.AreEqual("Market", report.Conversation.title);
            CollectionAssert.Contains(report.CreatedActors, "Merchant");
            CollectionAssert.Contains(report.CreatedVariables, "coins");
            CollectionAssert.IsEmpty(report.Warnings, string.Join("\n", report.Warnings));

            var world = new DialogueWorld(m_Db);
            var menus = new List<List<string>>();
            var lines = Play(new DialogueRunner(m_Db, world), report.Conversation, menus, "Hi!");
            CollectionAssert.AreEqual(new[] { "Hi!" }, menus[0]);
            CollectionAssert.AreEqual(new[] { "Hello, traveler.", "Good.", "Rich!" }, lines, "coins 0 + 5 >= 5: the if branch");
            Assert.IsTrue(world.TryGetVariable("coins", out var coins) && coins.intValue == 5);
        }

        [Test]
        public void BadInput_IsReported_NotThrown()
        {
            var report = ArcweaveImporter.Import(m_Db, "{ not json");
            Assert.That(report.Warnings, Has.Some.Contains("not valid JSON"));
            Assert.IsNull(report.Conversation);
            var twine = TwineImporter.Import(m_Db, TwineImporter.ParseTwee(":: A\n[[Nowhere]]\n"));
            Assert.That(twine.Warnings, Has.Some.Contains("'Nowhere', which does not exist"));
        }
    }
}
