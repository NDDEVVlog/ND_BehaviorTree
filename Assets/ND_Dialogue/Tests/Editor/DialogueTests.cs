using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ND.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Dialogue.Tests
{
    public class DialogueTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private StringTable m_Text;
        private ColorTable m_Colors;
        private LocalizationSettings m_Settings;
        private Actor m_Guard, m_Player;

        // What the runner showed, in order: "Guard: text" for lines, "> a | b" for menus, "END" at the end.
        private readonly List<string> m_Log = new List<string>();

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            LocalizationService.Shutdown();
            var en = Track(ScriptableObject.CreateInstance<Locale>()); en.code = "en";
            var vi = Track(ScriptableObject.CreateInstance<Locale>()); vi.code = "vi"; vi.fallback = en;
            m_Text = Track(ScriptableObject.CreateInstance<StringTable>()); m_Text.tableName = "Dialogue";
            m_Colors = Track(ScriptableObject.CreateInstance<ColorTable>());
            m_Settings = Track(ScriptableObject.CreateInstance<LocalizationSettings>());
            m_Settings.locales = new List<Locale> { en, vi };
            m_Settings.defaultLocale = en;
            m_Settings.playerPrefsKey = string.Empty;
            m_Settings.startupSelectors = new List<IStartupLocaleSelector>();
            LocalizationService.Initialize(m_Settings);

            m_Db = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Db.textTable = m_Text;
            m_Guard = m_Db.AddActor("Guard");
            m_Player = m_Db.AddActor("Player", isPlayer: true);
            m_Log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationService.Shutdown();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        // ---------------------------------------------------------------- helpers

        private DialogueEntry Line(Conversation c, Actor actor, string en, string vi = null)
        {
            var entry = c.AddEntry(actor.id);
            long id = m_Text.AddEntry($"{c.title}.{entry.id}");
            m_Text.SetValue(id, "en", en);
            if (vi != null) m_Text.SetValue(id, "vi", vi);
            entry.text.SetReference(m_Text, id);
            return entry;
        }

        private DialogueRunner Runner(IDialogueContext context, DialogueRunnerOptions options = null)
        {
            var runner = new DialogueRunner(m_Db, context, options);
            runner.LineStarted += l => m_Log.Add($"{l.Speaker.name}: {l.Text}");
            runner.ChoicesPresented += cs => m_Log.Add("> " + string.Join(" | ", cs.Select(c => c.Text)));
            runner.ConversationEnded += _ => m_Log.Add("END");
            return runner;
        }

        /// <summary>Continues through lines; at a menu picks the choice whose text is <paramref name="picks"/>[n].</summary>
        private static void Play(DialogueRunner runner, params string[] picks)
        {
            int pick = 0;
            for (int guard = 0; guard < 50 && runner.IsRunning; guard++)
            {
                if (runner.State == DialogueRunnerState.ShowingLine) runner.Continue();
                else
                {
                    int index = runner.CurrentChoices.ToList().FindIndex(c => c.Text == picks[pick]);
                    Assert.GreaterOrEqual(index, 0, $"choice '{picks[pick]}' not offered");
                    pick++;
                    runner.Choose(index);
                }
            }
        }

        // ---------------------------------------------------------------- the quest scenario from the brief

        /// <summary>
        /// Guard offers a quest only while it is inactive; accepting it sets state through an action; returning
        /// without / with the key gives different branches; completing it changes trust and the next visit.
        /// </summary>
        private Conversation BuildGuardConversation()
        {
            m_Db.AddVariable("player.has_old_key", VariableValue.Bool(false));
            m_Db.AddVariable("npc.guard.trust", VariableValue.Float(0f));
            m_Db.AddQuest("key");

            var c = m_Db.AddConversation("Guard");
            var start = c.StartEntry;

            var offer = Line(c, m_Guard, "I lost my old key. Can you find it?");
            offer.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Inactive });
            var yes = Line(c, m_Player, "Sure.");
            yes.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Active });
            var no = Line(c, m_Player, "Not now.");
            var thanks = Line(c, m_Guard, "Thank you!");
            var pity = Line(c, m_Guard, "Pity.");
            c.Link(start, offer); c.Link(offer, yes); c.Link(offer, no); c.Link(yes, thanks); c.Link(no, pity);

            var waiting = Line(c, m_Guard, "Still looking?");
            waiting.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            waiting.conditions.Add(new VariableCondition { variable = "player.has_old_key", value = "false" });
            c.Link(start, waiting);

            var found = Line(c, m_Guard, "You found it! Trust is now [var=npc.guard.trust].");
            found.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            found.conditions.Add(new VariableCondition { variable = "player.has_old_key", value = "true" });
            found.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Completed });
            found.actions.Add(new SetVariableAction { variable = "npc.guard.trust", mode = SetVariableAction.Mode.Add, value = "1.5" });
            c.Link(start, found);

            var after = Line(c, m_Guard, "Thanks again, friend.");
            after.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Completed });
            c.Link(start, after);
            return c;
        }

        [Test]
        public void QuestScenario_BranchesFollowWorldState_AcrossVisits()
        {
            BuildGuardConversation();
            var world = new DialogueWorld(m_Db);
            var questChanges = new List<string>();
            world.QuestStateChanged += (q, from, to) => questChanges.Add($"{q}:{from}->{to}");

            var runner = Runner(world);
            runner.Start("Guard"); Play(runner, "Not now.");
            runner.Start("Guard"); Play(runner, "Sure.");
            runner.Start("Guard"); Play(runner);                       // no key yet
            world.SetVariable("player.has_old_key", VariableValue.Bool(true)); // gameplay picks it up
            runner.Start("Guard"); Play(runner);
            runner.Start("Guard"); Play(runner);

            CollectionAssert.AreEqual(new[]
            {
                "Guard: I lost my old key. Can you find it?", "> Sure. | Not now.", "Guard: Pity.", "END",
                "Guard: I lost my old key. Can you find it?", "> Sure. | Not now.", "Guard: Thank you!", "END",
                "Guard: Still looking?", "END",
                "Guard: You found it! Trust is now 1.5.", "END",
                "Guard: Thanks again, friend.", "END",
            }, m_Log);
            CollectionAssert.AreEqual(new[] { "key:Inactive->Active", "key:Active->Completed" }, questChanges);
        }

        [Test]
        public void SwitchingLanguage_MidConversation_AffectsTheNextLine()
        {
            var c = m_Db.AddConversation("Hello");
            var a = Line(c, m_Guard, "Hello.", "Xin chào.");
            var b = Line(c, m_Guard, "Goodbye.", "Tạm biệt.");
            var onlyEnglish = Line(c, m_Guard, "Only English.");
            c.Link(c.StartEntry, a); c.Link(a, b); c.Link(b, onlyEnglish);

            var runner = Runner(new DialogueWorld(m_Db));
            runner.Start("Hello");
            LocalizationService.SetLocale("vi");
            Play(runner);

            CollectionAssert.AreEqual(new[] { "Guard: Hello.", "Guard: Tạm biệt.", "Guard: Only English.", "END" }, m_Log);
        }

        [Test]
        public void Emotion_TintIsLocalizedPerRegion()
        {
            long tintId = m_Colors.AddEntry("guard.angry");
            m_Colors.SetValue(tintId, "en", Color.red);
            m_Colors.SetValue(tintId, "vi", Color.white);
            var angry = new ActorEmotion { id = "Angry", tint = Color.gray };
            angry.localizedTint.SetReference(m_Colors, tintId);
            m_Guard.emotions.Add(new ActorEmotion { id = "Neutral" });
            m_Guard.emotions.Add(angry);

            var c = m_Db.AddConversation("Mood");
            var line = Line(c, m_Guard, "Grr.");
            line.emotion = "Angry";
            c.Link(c.StartEntry, line);

            var runner = new DialogueRunner(m_Db, new DialogueWorld(m_Db));
            Color shown = Color.clear;
            runner.LineStarted += l => shown = l.Emotion.ResolveTint();
            runner.Start("Mood");
            Assert.AreEqual(Color.red, shown);

            runner.Stop();
            LocalizationService.SetLocale("vi");
            runner.Start("Mood");
            Assert.AreEqual(Color.white, shown);
        }

        // ---------------------------------------------------------------- branching rules

        [Test]
        public void HigherPriority_Wins_WhenItsConditionsHold()
        {
            m_Db.AddVariable("vip", VariableValue.Bool(false));
            var c = m_Db.AddConversation("Door");
            var normal = Line(c, m_Guard, "Move along.");
            var vip = Line(c, m_Guard, "Welcome, sir.");
            vip.conditions.Add(new VariableCondition { variable = "vip", value = "true" });
            c.Link(c.StartEntry, normal);
            c.Link(c.StartEntry, vip, LinkPriority.High);

            var world = new DialogueWorld(m_Db);
            var runner = Runner(world);
            runner.Start("Door"); Play(runner);
            world.SetVariable("vip", VariableValue.Bool(true));
            runner.Start("Door"); Play(runner);

            CollectionAssert.AreEqual(new[] { "Guard: Move along.", "END", "Guard: Welcome, sir.", "END" }, m_Log);
        }

        [Test]
        public void NpcLine_TakesPrecedence_OverPlayerResponsesInTheSameGroup()
        {
            var c = m_Db.AddConversation("Mixed");
            var reply = Line(c, m_Player, "Hi?");
            var npc = Line(c, m_Guard, "Interrupting!");
            c.Link(c.StartEntry, reply);
            c.Link(c.StartEntry, npc);

            var runner = Runner(new DialogueWorld(m_Db));
            runner.Start("Mixed"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Interrupting!", "END" }, m_Log);
        }

        [Test]
        public void Passthrough_RoutesAndRunsActions_WithoutShowingALine()
        {
            m_Db.AddVariable("visits", VariableValue.Int(0));
            var c = m_Db.AddConversation("Counter");
            var hub = c.AddEntry(m_Guard.id, "count visit");
            hub.isPassthrough = true;
            hub.actions.Add(new SetVariableAction { variable = "visits", mode = SetVariableAction.Mode.Add, value = "1" });
            var first = Line(c, m_Guard, "First time here?");
            first.conditions.Add(new VariableCondition { variable = "visits", value = "1" });
            var again = Line(c, m_Guard, "Visit number [var=visits].");
            again.conditions.Add(new VariableCondition { variable = "visits", op = CompareOp.Greater, value = "1" });
            c.Link(c.StartEntry, hub); c.Link(hub, first); c.Link(hub, again);

            var runner = Runner(new DialogueWorld(m_Db));
            for (int i = 0; i < 3; i++) { runner.Start("Counter"); Play(runner); }

            CollectionAssert.AreEqual(new[] { "Guard: First time here?", "END", "Guard: Visit number 2.", "END", "Guard: Visit number 3.", "END" }, m_Log);
        }

        [Test]
        public void PassthroughLoop_IsStopped_WithAnError()
        {
            var c = m_Db.AddConversation("Loop");
            var a = c.AddEntry(m_Guard.id); a.isPassthrough = true;
            var b = c.AddEntry(m_Guard.id); b.isPassthrough = true;
            c.Link(c.StartEntry, a); c.Link(a, b); c.Link(b, a);

            var runner = Runner(new DialogueWorld(m_Db), new DialogueRunnerOptions { maxSilentSteps = 10 });
            LogAssert.Expect(LogType.Error, new Regex("passthrough loop"));
            runner.Start("Loop");
            Assert.AreEqual(DialogueRunnerState.Ended, runner.State);
            CollectionAssert.AreEqual(new[] { "END" }, m_Log);
        }

        [Test]
        public void BrokenLink_IsReportedAndSkipped_OtherLinksStillWork()
        {
            var c = m_Db.AddConversation("Broken");
            var ok = Line(c, m_Guard, "Still here.");
            c.StartEntry.links.Add(new DialogueLink { conversationId = c.id, entryId = 999, priority = LinkPriority.High });
            c.Link(c.StartEntry, ok);

            var runner = Runner(new DialogueWorld(m_Db));
            LogAssert.Expect(LogType.Error, new Regex("missing conversation .* entry 999"));
            runner.Start("Broken"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Still here.", "END" }, m_Log);
        }

        [Test]
        public void LinkIntoAnotherConversation_ContinuesThere()
        {
            var main = m_Db.AddConversation("Main");
            var side = m_Db.AddConversation("Side");
            var a = Line(main, m_Guard, "Let me tell you a story.");
            var b = Line(side, m_Guard, "Once upon a time.");
            main.Link(main.StartEntry, a);
            a.links.Add(new DialogueLink { conversationId = side.id, entryId = b.id });

            var runner = Runner(new DialogueWorld(m_Db));
            Conversation ended = null;
            runner.ConversationEnded += conv => ended = conv;
            runner.Start("Main"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Let me tell you a story.", "Guard: Once upon a time.", "END" }, m_Log);
            Assert.AreSame(side, ended);
        }

        [Test]
        public void Choices_UseMenuText_AndPlayerLinesAreOptional()
        {
            var c = m_Db.AddConversation("Ask");
            var q = Line(c, m_Guard, "Well?");
            var answer = Line(c, m_Player, "I would like to buy the sword, please.");
            long menuId = m_Text.AddEntry("Ask.menu");
            m_Text.SetValue(menuId, "en", "Buy sword");
            answer.menuText.SetReference(m_Text, menuId);
            var bye = Line(c, m_Guard, "Done.");
            c.Link(c.StartEntry, q); c.Link(q, answer); c.Link(answer, bye);

            var silent = Runner(new DialogueWorld(m_Db));
            silent.Start("Ask"); Play(silent, "Buy sword");
            CollectionAssert.AreEqual(new[] { "Guard: Well?", "> Buy sword", "Guard: Done.", "END" }, m_Log);

            m_Log.Clear();
            var spoken = Runner(new DialogueWorld(m_Db), new DialogueRunnerOptions { showPlayerLines = true });
            spoken.Start("Ask"); Play(spoken, "Buy sword");
            CollectionAssert.AreEqual(new[] { "Guard: Well?", "> Buy sword", "Player: I would like to buy the sword, please.", "Guard: Done.", "END" }, m_Log);
        }

        [Test]
        public void SingleResponse_IsPickedAutomatically_WhenMenuNotForced()
        {
            var c = m_Db.AddConversation("Auto");
            var q = Line(c, m_Guard, "Ready?");
            var yes = Line(c, m_Player, "Yes.");
            var go = Line(c, m_Guard, "Go!");
            c.Link(c.StartEntry, q); c.Link(q, yes); c.Link(yes, go);

            var runner = Runner(new DialogueWorld(m_Db), new DialogueRunnerOptions { alwaysShowChoiceMenu = false });
            runner.Start("Auto"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Ready?", "Guard: Go!", "END" }, m_Log);
        }

        [Test]
        public void WrongStateCalls_AreRejectedWithTheState()
        {
            var c = m_Db.AddConversation("Short");
            var q = Line(c, m_Guard, "Hm?");
            c.Link(c.StartEntry, q);
            var runner = Runner(new DialogueWorld(m_Db));

            LogAssert.Expect(LogType.Warning, new Regex("Continue rejected: state=Idle"));
            Assert.IsFalse(runner.Continue());
            runner.Start("Short");
            LogAssert.Expect(LogType.Warning, new Regex("Choose rejected: state=ShowingLine"));
            Assert.IsFalse(runner.Choose(0));
            LogAssert.Expect(LogType.Warning, new Regex("Start rejected: state=ShowingLine"));
            Assert.IsFalse(runner.Start("Short"));
        }

        // ---------------------------------------------------------------- world state

        [Test]
        public void World_RejectsUnknownNamesAndTypeChanges_ConvertsNumbers_AndOnlyReportsRealChanges()
        {
            m_Db.AddVariable("gold", VariableValue.Int(5));
            m_Db.AddVariable("name", VariableValue.String("Ann"));
            var world = new DialogueWorld(m_Db);
            var changes = new List<string>();
            world.VariableChanged += (n, from, to) => changes.Add($"{n}:{from}->{to}");

            Assert.IsTrue(world.SetVariable("gold", VariableValue.Float(7.6f)));
            Assert.IsTrue(world.SetVariable("gold", VariableValue.Int(8)));  // same value: no event
            LogAssert.Expect(LogType.Error, new Regex("'name' is String, got Bool"));
            Assert.IsFalse(world.SetVariable("name", VariableValue.Bool(true)));
            LogAssert.Expect(LogType.Error, new Regex("unknown variable 'ghost'"));
            Assert.IsFalse(world.SetVariable("ghost", VariableValue.Int(1)));

            CollectionAssert.AreEqual(new[] { "gold:5->8" }, changes);
        }

        [Test]
        public void World_SnapshotRoundTrip_RestoresValues_AndRaisesEvents()
        {
            m_Db.AddVariable("gold", VariableValue.Int(0));
            m_Db.AddQuest("key");
            var world = new DialogueWorld(m_Db);
            world.SetVariable("gold", VariableValue.Int(42));
            world.SetQuestState("key", QuestState.Completed);
            string json = JsonUtility.ToJson(world.Capture());

            var fresh = new DialogueWorld(m_Db);
            var events = new List<string>();
            fresh.VariableChanged += (n, a, b) => events.Add(n);
            fresh.QuestStateChanged += (n, a, b) => events.Add(n);
            fresh.Restore(JsonUtility.FromJson<DialogueWorld.Snapshot>(json));

            Assert.IsTrue(fresh.TryGetVariable("gold", out var gold));
            Assert.AreEqual(42, gold.intValue);
            Assert.IsTrue(fresh.TryGetQuestState("key", out var state));
            Assert.AreEqual(QuestState.Completed, state);
            CollectionAssert.AreEquivalent(new[] { "gold", "key" }, events);
        }

        // ---------------------------------------------------------------- database

        [Test]
        public void Validate_ReportsBrokenReferences()
        {
            m_Db.AddVariable("flag", VariableValue.Bool(false));
            var c = m_Db.AddConversation("Bad");
            var e = Line(c, m_Guard, "x");
            e.links.Add(new DialogueLink { conversationId = c.id, entryId = 77 });
            e.conditions.Add(new VariableCondition { variable = "flag", value = "maybe" });
            e.conditions.Add(new VariableCondition { variable = "nope" });
            e.actions.Add(new SetQuestStateAction { quest = "ghost" });
            var orphan = c.AddEntry(actorId: 99);

            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("missing entry 77"));
            Assert.That(issues, Has.Some.Contains("'maybe' is not a valid Bool"));
            Assert.That(issues, Has.Some.Contains("unknown variable 'nope'"));
            Assert.That(issues, Has.Some.Contains("unknown quest 'ghost'"));
            Assert.That(issues, Has.Some.Contains($"entry {orphan.id}: unknown actor id 99"));
        }

        [Test]
        public void Validate_CleanDatabase_HasNoIssues()
        {
            BuildGuardConversation();
            CollectionAssert.IsEmpty(m_Db.Validate());
        }

        // ---------------------------------------------------------------- link conditions

        [Test]
        public void LinkConditions_ChooseTheBranch_AndEntryConditionsStillApply()
        {
            m_Db.AddVariable("vip", VariableValue.Bool(false));
            m_Db.AddVariable("open", VariableValue.Bool(true));
            var c = m_Db.AddConversation("Door");
            var hello = Line(c, m_Guard, "Hello.");
            var vipLine = Line(c, m_Guard, "Welcome, sir.");
            vipLine.conditions.Add(new VariableCondition { variable = "open", value = "true" }); // every way in
            var normal = Line(c, m_Guard, "Move along.");
            c.Link(c.StartEntry, hello);
            c.Link(hello, vipLine).conditions.Add(new VariableCondition { variable = "vip", value = "true" }); // this branch only
            c.Link(hello, normal).conditions.Add(new VariableCondition { variable = "vip", value = "false" });
            CollectionAssert.IsEmpty(m_Db.Validate());

            var world = new DialogueWorld(m_Db);
            var runner = Runner(world);
            runner.Start("Door"); Play(runner);
            world.SetVariable("vip", VariableValue.Bool(true));
            runner.Start("Door"); Play(runner);
            world.SetVariable("open", VariableValue.Bool(false)); // entry condition fails: the vip branch is closed
            runner.Start("Door"); Play(runner);

            CollectionAssert.AreEqual(new[]
            {
                "Guard: Hello.", "Guard: Move along.", "END",
                "Guard: Hello.", "Guard: Welcome, sir.", "END",
                "Guard: Hello.", "END",
            }, m_Log);
        }

        [Test]
        public void SameTarget_ReachableByTwoLinks_EachWithItsOwnRule()
        {
            m_Db.AddVariable("a", VariableValue.Bool(false));
            var c = m_Db.AddConversation("Two");
            var target = Line(c, m_Guard, "You made it.");
            var detour = c.AddEntry(m_Guard.id);
            detour.isPassthrough = true;
            c.Link(c.StartEntry, target).conditions.Add(new VariableCondition { variable = "a", value = "true" });
            c.Link(c.StartEntry, detour);
            c.Link(detour, target); // no rule on this way in

            var world = new DialogueWorld(m_Db);
            var runner = Runner(world);
            runner.Start("Two"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: You made it.", "END" }, m_Log, "reached through the detour although 'a' is false");
        }

        [Test]
        public void Validate_ChecksLinkConditions()
        {
            var c = m_Db.AddConversation("Bad");
            var e = Line(c, m_Guard, "x");
            c.Link(c.StartEntry, e).conditions.Add(new VariableCondition { variable = "ghost" });
            Assert.That(m_Db.Validate(), Has.Some.Contains($"link → {e.id}: condition uses unknown variable 'ghost'"));
        }

        // ---------------------------------------------------------------- plain text & value types

        [Test]
        public void PlainText_PlaysWithoutAnyTable_AndConnectsLater()
        {
            var c = m_Db.AddConversation("Plain");
            var hi = c.AddEntry(m_Guard.id);
            hi.text.Text = "Hello, [var=name].";
            c.Link(c.StartEntry, hi);
            m_Db.AddVariable("name", VariableValue.String("Ann"));

            var runner = Runner(new DialogueWorld(m_Db));
            runner.Start("Plain"); Play(runner);
            Assert.AreEqual("Guard: Hello, Ann.", m_Log[0]);

            long id = m_Text.AddEntry("plain.hi");
            m_Text.SetValue(id, "en", "Greetings, [var=name].");
            hi.text.SetReference(m_Text, id); // connected later: the translation wins, the plain text stays as a note
            m_Log.Clear();
            runner.Start("Plain"); Play(runner);
            Assert.AreEqual("Guard: Greetings, Ann.", m_Log[0]);
            Assert.AreEqual("Hello, [var=name].", hi.text.Text);
        }

        [TestCase(VariableType.Vector2, "(1, 2)", 1f, 2f, 0f)]
        [TestCase(VariableType.Vector3, "1,2,3", 1f, 2f, 3f)]
        [TestCase(VariableType.Vector3, "1 2.5 -3", 1f, 2.5f, -3f)]
        public void Vectors_ParseFromText(VariableType type, string text, float x, float y, float z)
        {
            Assert.IsTrue(VariableValue.TryParse(type, text, out var v));
            Assert.AreEqual(new Vector4(x, y, z, 0f), v.vectorValue);
            Assert.IsFalse(VariableValue.TryParse(type, "1", out _));
        }

        [Test]
        public void Colors_ParseFromHtmlOrComponents_AndObjectsOnlyFromNull()
        {
            Assert.IsTrue(VariableValue.TryParse(VariableType.Color, "#FF0000", out var red));
            Assert.AreEqual(Color.red, red.colorValue);
            Assert.IsTrue(VariableValue.TryParse(VariableType.Color, "0,1,0", out var green));
            Assert.AreEqual(Color.green, green.colorValue);
            Assert.IsTrue(VariableValue.TryParse(VariableType.Object, "null", out var none));
            Assert.IsNull(none.objectValue);
            Assert.IsFalse(VariableValue.TryParse(VariableType.Object, "Player", out _), "a reference cannot be written as text");
        }

        [Test]
        public void VectorColorAndObjectVariables_WorkInConditionsAndActions()
        {
            var key = Track(ScriptableObject.CreateInstance<StringTable>()); // any Unity object
            m_Db.AddVariable("spawn", VariableValue.Vector3(Vector3.zero));
            m_Db.AddVariable("banner", VariableValue.Color(Color.white));
            m_Db.AddVariable("held", VariableValue.Object(null));
            var c = m_Db.AddConversation("Types");
            var give = Line(c, m_Guard, "Take it.");
            give.conditions.Add(new VariableCondition { variable = "held", value = "null" });
            give.actions.Add(new SetVariableAction { variable = "held", objectValue = key });
            give.actions.Add(new SetVariableAction { variable = "spawn", mode = SetVariableAction.Mode.Add, value = "1,0,2" });
            give.actions.Add(new SetVariableAction { variable = "banner", value = "#FF0000" });
            var has = Line(c, m_Guard, "You have it. Spawn at [var=spawn].");
            has.conditions.Add(new VariableCondition { variable = "held", op = CompareOp.Equal, objectValue = key });
            has.conditions.Add(new VariableCondition { variable = "banner", value = "#FF0000" });
            c.Link(c.StartEntry, give);
            c.Link(c.StartEntry, has);
            CollectionAssert.IsEmpty(m_Db.Validate());

            var world = new DialogueWorld(m_Db);
            var runner = Runner(world);
            runner.Start("Types"); Play(runner);
            runner.Start("Types"); Play(runner);

            CollectionAssert.AreEqual(new[] { "Guard: Take it.", "END", "Guard: You have it. Spawn at (1, 0, 2).", "END" }, m_Log);
            world.TryGetVariable("held", out var held);
            Assert.AreSame(key, held.objectValue);
        }

        [Test]
        public void Validate_RejectsBadVectorText_AndOrderingOnVectors()
        {
            m_Db.AddVariable("spawn", VariableValue.Vector3(Vector3.zero));
            var c = m_Db.AddConversation("Bad");
            var e = Line(c, m_Guard, "x");
            e.conditions.Add(new VariableCondition { variable = "spawn", value = "1,2" });
            e.conditions.Add(new VariableCondition { variable = "spawn", op = CompareOp.Greater, value = "1,2,3" });
            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("'1,2' is not a valid Vector3"));
            Assert.That(issues, Has.Some.Contains("only numbers can be ordered"));
        }

        // ---------------------------------------------------------------- characters: inheritance & attributes

        [System.Serializable]
        public class Merchant : Actor
        {
            public float priceMultiplier = 1f;
            public string shopId;
        }

        [Test]
        public void ActorSubclass_KeepsItsOwnFields_ThroughSerialization()
        {
            var merchant = m_Db.AddActor(new Merchant { name = "Ann", priceMultiplier = 1.5f, shopId = "bakery" });
            Assert.IsNotNull(merchant);

            var copy = Track(Object.Instantiate(m_Db)); // round-trips through Unity serialization
            var restored = copy.FindActor("Ann") as Merchant;
            Assert.IsNotNull(restored, "the subclass survives ([SerializeReference] list)");
            Assert.AreEqual(1.5f, restored.priceMultiplier);
            Assert.AreEqual("bakery", restored.shopId);
            Assert.IsInstanceOf<Actor>(copy.FindActor("Guard"));
        }

        [Test]
        public void ActorNames_WithTheAttributeSeparator_AreRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex("contains '\\.'"));
            Assert.IsNull(m_Db.AddActor("Mr.Smith"));
        }

        [Test]
        public void Attributes_AreVariablesNamedActorDotAttribute_AndDriveTheConversation()
        {
            m_Guard.attributes.Add(new VariableDefinition { name = "met", initialValue = VariableValue.Bool(false) });
            m_Guard.attributes.Add(new VariableDefinition { name = "trust", initialValue = VariableValue.Float(0f) });
            var c = m_Db.AddConversation("Visit");
            var first = Line(c, m_Guard, "Who are you?");
            first.conditions.Add(new VariableCondition { variable = "Guard.met", value = "false" });
            first.actions.Add(new SetVariableAction { variable = "Guard.met", value = "true" });
            first.actions.Add(new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "2" });
            var again = Line(c, m_Guard, "Welcome back. Trust: [var=Guard.trust].");
            again.conditions.Add(new VariableCondition { variable = "Guard.met", value = "true" });
            c.Link(c.StartEntry, first);
            c.Link(c.StartEntry, again);

            CollectionAssert.IsSupersetOf(m_Db.Schema.VariableNames.ToList(), new[] { "Guard.met", "Guard.trust" });
            Assert.IsTrue(m_Db.Schema.TryGetVariableType("Guard.trust", out var type));
            Assert.AreEqual(VariableType.Float, type);
            CollectionAssert.IsEmpty(m_Db.Validate());

            var runner = Runner(m_Db.CreateContext());
            runner.Start("Visit"); Play(runner);
            runner.Start("Visit"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Who are you?", "END", "Guard: Welcome back. Trust: 2.", "END" }, m_Log);
        }

        [Test]
        public void Validate_ReportsAttributeProblems()
        {
            m_Guard.attributes.Add(new VariableDefinition { name = "trust" });
            m_Guard.attributes.Add(new VariableDefinition { name = "trust" });
            m_Db.AddVariable("Guard.trust", VariableValue.Int(0));
            m_Db.actors.Add(new Actor { id = 99, name = "Dr.Who" });

            var issues = m_Db.Validate();
            Assert.That(issues, Has.Some.Contains("duplicate attribute name 'trust'"));
            Assert.That(issues, Has.Some.Contains("hides the variable 'Guard.trust'"));
            Assert.That(issues, Has.Some.Contains("Actor 'Dr.Who': the name contains '.'"));
        }

        // ---------------------------------------------------------------- events & settings

        private Conversation EventConversation(string eventName, string argument)
        {
            var c = m_Db.AddConversation("Gift");
            var line = Line(c, m_Guard, "Take this.");
            line.actions.Add(new RaiseEventAction { eventName = eventName, argument = argument });
            c.Link(c.StartEntry, line);
            return c;
        }

        [Test]
        public void RaiseEvent_ReachesSceneListeners_ThroughTheHub()
        {
            m_Db.events.Add(new EventDefinition { name = "give_item" });
            EventConversation("give_item", "old_key");

            var go = Track(new GameObject("listener"));
            go.SetActive(false);
            var listener = go.AddComponent<DialogueEventListener>();
            string received = null;
            var binding = new DialogueEventListener.Binding { eventName = "give_item" };
            binding.response.AddListener(arg => received = arg);
            listener.Bindings.Add(binding);
            var other = new DialogueEventListener.Binding { eventName = "open_door" };
            bool wrongFired = false;
            other.response.AddListener(_ => wrongFired = true);
            listener.Bindings.Add(other);
            go.SetActive(true);

            var runner = Runner(new DialogueWorld(m_Db));
            runner.Start("Gift");

            Assert.AreEqual("old_key", received);
            Assert.IsFalse(wrongFired, "only the matching binding runs");

            go.SetActive(false);
            received = null;
            runner.Stop();
            runner.Start("Gift");
            Assert.IsNull(received, "a disabled listener does not react");
        }

        [Test]
        public void Validate_ChecksEventNames_OnlyOnceEventsAreDeclared()
        {
            EventConversation("open_door", null);
            CollectionAssert.IsEmpty(m_Db.Validate(), "no events declared: names are free-form");

            m_Db.events.Add(new EventDefinition { name = "give_item" });
            Assert.That(m_Db.Validate(), Has.Some.Contains("event 'open_door' is not declared"));

            m_Db.events.Add(new EventDefinition { name = "open_door" });
            CollectionAssert.IsEmpty(m_Db.Validate());

            m_Db.events.Add(new EventDefinition { name = "open_door" });
            Assert.That(m_Db.Validate(), Has.Some.Contains("duplicate event name 'open_door'"));
        }

        [Test]
        public void Runner_WithoutOptions_UsesTheDatabaseDefaults()
        {
            var c = m_Db.AddConversation("Auto");
            var q = Line(c, m_Guard, "Ready?");
            var yes = Line(c, m_Player, "Yes.");
            var go = Line(c, m_Guard, "Go!");
            c.Link(c.StartEntry, q); c.Link(q, yes); c.Link(yes, go);
            m_Db.runnerOptions.alwaysShowChoiceMenu = false;

            var runner = Runner(new DialogueWorld(m_Db));
            runner.Start("Auto"); Play(runner);
            CollectionAssert.AreEqual(new[] { "Guard: Ready?", "Guard: Go!", "END" }, m_Log);
        }

        [Test]
        public void RemoveConversation_DropsLinksIntoItFromOtherConversations()
        {
            var main = m_Db.AddConversation("Main");
            var side = m_Db.AddConversation("Side");
            var caller = Line(main, m_Guard, "caller");
            main.Link(main.StartEntry, caller);
            caller.links.Add(new DialogueLink { conversationId = side.id, entryId = Conversation.StartEntryId });

            Assert.IsTrue(m_Db.RemoveConversation(side.id));

            Assert.IsNull(m_Db.FindConversation(side.id));
            Assert.IsEmpty(caller.links);
            CollectionAssert.IsEmpty(m_Db.Validate());
        }

        [Test]
        public void Schema_ListsDeclaredNames_AndWithoutSourceIsTheDatabase()
        {
            m_Db.AddVariable("gold", VariableValue.Int(0));
            m_Db.AddQuest("key");
            Assert.AreSame(m_Db, m_Db.Schema);
            CollectionAssert.AreEqual(new[] { "gold" }, m_Db.Schema.VariableNames);
            CollectionAssert.AreEqual(new[] { "key" }, m_Db.Schema.QuestNames);
            Assert.IsInstanceOf<DialogueWorld>(m_Db.CreateContext());
        }

        [Test]
        public void RemoveEntry_FromDatabase_AlsoDropsLinksFromOtherConversations()
        {
            var main = m_Db.AddConversation("Main");
            var side = m_Db.AddConversation("Side");
            var target = Line(side, m_Guard, "target");
            var caller = Line(main, m_Guard, "caller");
            main.Link(main.StartEntry, caller);
            caller.links.Add(new DialogueLink { conversationId = side.id, entryId = target.id });

            Assert.IsTrue(m_Db.RemoveEntry(side.id, target.id));

            Assert.IsEmpty(caller.links, "a link from another conversation must not be left dangling");
            CollectionAssert.IsEmpty(m_Db.Validate());
        }

        [Test]
        public void RemoveEntry_DropsLinksPointingAtIt_StartCannotBeRemoved()
        {
            var c = m_Db.AddConversation("Edit");
            var a = Line(c, m_Guard, "a");
            var b = Line(c, m_Guard, "b");
            c.Link(c.StartEntry, a); c.Link(a, b);

            Assert.IsTrue(m_Db.RemoveEntry(c.id, b.id));
            Assert.IsEmpty(a.links);
            Assert.IsNull(c.FindEntry(b.id));
            Assert.IsFalse(m_Db.RemoveEntry(c.id, Conversation.StartEntryId));
            CollectionAssert.IsEmpty(m_Db.Validate());
        }
    }
}
