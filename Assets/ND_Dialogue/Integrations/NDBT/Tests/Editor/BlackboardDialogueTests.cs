using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ND.Localization;
using ND_BehaviorTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Dialogue.NDBT.Tests
{
    /// <summary>Dialogue and a behavior-tree agent sharing one world blackboard.</summary>
    public class BlackboardDialogueTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private Blackboard m_WorldAsset, m_GuardAsset;
        private DialogueDatabase m_Db;
        private StringTable m_Text;
        private Actor m_Guard, m_Player;

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        private T AddKey<T>(Blackboard board, string name) where T : Key
        {
            var key = Track(ScriptableObject.CreateInstance<T>());
            key.keyName = name;
            board.keys.Add(key);
            return key;
        }

        [SetUp]
        public void SetUp()
        {
            SharedBlackboards.Clear();
            LocalizationService.Shutdown();
            var en = Track(ScriptableObject.CreateInstance<Locale>()); en.code = "en";
            var settings = Track(ScriptableObject.CreateInstance<LocalizationSettings>());
            settings.locales = new List<Locale> { en };
            settings.defaultLocale = en;
            settings.playerPrefsKey = string.Empty;
            settings.startupSelectors = new List<IStartupLocaleSelector>();
            LocalizationService.Initialize(settings);
            m_Text = Track(ScriptableObject.CreateInstance<StringTable>());

            m_WorldAsset = Track(ScriptableObject.CreateInstance<Blackboard>());
            m_WorldAsset.name = "World";
            AddKey<BoolKey>(m_WorldAsset, "player.has_old_key");
            AddKey<FloatKey>(m_WorldAsset, "npc.guard.trust");
            AddKey<IntKey>(m_WorldAsset, "quest.key");
            AddKey<QuaternionKey>(m_WorldAsset, "unsupported");

            m_GuardAsset = Track(ScriptableObject.CreateInstance<Blackboard>());
            m_GuardAsset.parent = m_WorldAsset;
            AddKey<FloatKey>(m_GuardAsset, "npc.alertness");

            m_Db = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Guard = m_Db.AddActor("Guard");
            m_Player = m_Db.AddActor("Player", isPlayer: true);
        }

        [TearDown]
        public void TearDown()
        {
            SharedBlackboards.Clear();
            LocalizationService.Shutdown();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueEntry Line(Conversation c, Actor actor, string text)
        {
            var entry = c.AddEntry(actor.id);
            long id = m_Text.AddEntry($"{c.title}.{entry.id}");
            m_Text.SetValue(id, "en", text);
            entry.text.SetReference(m_Text, id);
            return entry;
        }

        private void BuildQuestConversation()
        {
            var c = m_Db.AddConversation("Guard");
            var offer = Line(c, m_Guard, "Find my key?");
            offer.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Inactive });
            var yes = Line(c, m_Player, "Sure.");
            yes.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Active });
            c.Link(c.StartEntry, offer); c.Link(offer, yes);

            var found = Line(c, m_Guard, "You found it!");
            found.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            found.conditions.Add(new VariableCondition { variable = "player.has_old_key", value = "true" });
            found.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Completed });
            found.actions.Add(new SetVariableAction { variable = "npc.guard.trust", mode = SetVariableAction.Mode.Add, value = "1" });
            c.Link(c.StartEntry, found);
        }

        private static List<string> Play(DialogueRunner runner, string conversation)
        {
            var lines = new List<string>();
            runner.LineStarted += Record;
            runner.Start(conversation);
            for (int guard = 0; guard < 20 && runner.IsRunning; guard++)
            {
                if (runner.State == DialogueRunnerState.ShowingLine) runner.Continue();
                else runner.Choose(0);
            }
            runner.LineStarted -= Record;
            return lines;

            void Record(DialogueLine l) => lines.Add(l.Text);
        }

        [Test]
        public void QuestDialogue_WritesWorldBlackboard_AndTheAgentSeesIt()
        {
            BuildQuestConversation();
            var guardAgent = Track(m_GuardAsset.Clone()); // what BehaviorTreeRunner does for the guard
            m_Track.AddRange(guardAgent.keys);
            var context = BlackboardDialogueContext.ForWorld(m_WorldAsset);
            var worldChanges = new List<string>();
            context.Blackboard.KeyChanged += k => worldChanges.Add($"{k.keyName}={k.GetValueObject()}");
            var runner = new DialogueRunner(m_Db, context);

            CollectionAssert.AreEqual(new[] { "Find my key?" }, Play(runner, "Guard"));
            Assert.AreEqual((int)QuestState.Active, guardAgent.GetValue<int>("quest.key"), "the AI reads the quest state dialogue wrote");

            guardAgent.SetValue("player.has_old_key", true); // gameplay/AI writes through its own blackboard
            CollectionAssert.AreEqual(new[] { "You found it!" }, Play(runner, "Guard"));

            Assert.AreEqual((int)QuestState.Completed, guardAgent.GetValue<int>("quest.key"));
            Assert.AreEqual(1f, guardAgent.GetValue<float>("npc.guard.trust"));
            CollectionAssert.AreEqual(new[] { "quest.key=1", "player.has_old_key=True", "quest.key=2", "npc.guard.trust=1" }, worldChanges);
            Assert.AreEqual(0, m_WorldAsset.GetValue<int>("quest.key"), "the asset keeps its design-time values");
        }

        [Test]
        public void TheBlackboardWorld_MirrorsQuests_BothWays_AndReportsChanges()
        {
            var source = Track(ScriptableObject.CreateInstance<BlackboardSchemaSource>());
            source.blackboard = m_WorldAsset; // the quests are the world's quest.* keys
            m_Db.schemaSource = source;
            var context = BlackboardDialogueContext.ForWorld(m_WorldAsset, database: m_Db);
            var changes = new List<string>();
            context.StateChanged += (name, quest) => changes.Add($"{(quest ? "quest " : string.Empty)}{name}");
            var runtime = SharedBlackboards.GetRuntime(m_WorldAsset);

            context.SetQuestState("key", QuestState.Active); // dialogue
            Assert.AreEqual((int)QuestState.Active, runtime.GetValue<int>("quest.key"), "the key follows the quest");
            Assert.IsTrue(runtime.TryGetKey("player.has_old_key", out var flag));
            flag.SetValueObject(true); // an agent writing the shared blackboard
            CollectionAssert.AreEqual(new[] { "quest key", "player.has_old_key" }, changes);
            Assert.AreEqual(QuestState.Active, context.Quests.Find("key").State);

            runtime.SetValue("quest.key", (int)QuestState.Completed); // an agent finishing the quest
            Assert.AreEqual(QuestState.Completed, context.Quests.Find("key").State, "the quest follows the key");
            context.Dispose();
        }

        [Test]
        public void WithASharedQuestRuntime_TheRuntimeIsTheTruth_AndTheKeysFollowIt()
        {
            var shared = new QuestRuntime();
            shared.RegisterId("key", QuestState.Completed, "a quest database");
            var context = BlackboardDialogueContext.ForWorld(m_WorldAsset, database: m_Db, quests: shared);
            Assert.AreSame(shared, context.Quests);
            Assert.AreEqual((int)QuestState.Completed, SharedBlackboards.GetRuntime(m_WorldAsset).GetValue<int>("quest.key"), "the runtime wrote its state into the key");
            Assert.IsTrue(context.TryGetQuestState("key", out var state) && state == QuestState.Completed);
            context.Dispose();
        }

        [Test]
        public void Validate_UsesTheBlackboardAsSchema()
        {
            BuildQuestConversation();
            var context = new BlackboardDialogueContext(m_WorldAsset);
            CollectionAssert.IsEmpty(m_Db.Validate(context));

            var c = m_Db.FindConversation("Guard");
            var extra = Line(c, m_Guard, "?");
            extra.conditions.Add(new VariableCondition { variable = "unsupported", value = "1" });
            extra.actions.Add(new SetQuestStateAction { quest = "missing" });
            var issues = m_Db.Validate(context);
            Assert.That(issues, Has.Some.Contains("unknown variable 'unsupported'"));
            Assert.That(issues, Has.Some.Contains("unknown quest 'missing'"));
            Assert.That(m_Db.Validate(), Has.Some.Contains("unknown quest 'key'"), "without the schema the database's own (empty) lists are used");
        }

        [Test]
        public void Variables_ConvertNumbers_RejectOtherTypeChanges_AndUnsupportedKeys()
        {
            var context = BlackboardDialogueContext.ForWorld(m_WorldAsset);
            Assert.IsTrue(context.SetVariable("npc.guard.trust", VariableValue.Int(3)));
            Assert.IsTrue(context.TryGetVariable("npc.guard.trust", out var trust));
            Assert.AreEqual(VariableType.Float, trust.type);
            Assert.AreEqual(3f, trust.floatValue);

            LogAssert.Expect(LogType.Error, new Regex("'player.has_old_key' is Bool, got String"));
            Assert.IsFalse(context.SetVariable("player.has_old_key", VariableValue.String("yes")));
            LogAssert.Expect(LogType.Error, new Regex("must be bool, int, float, string"));
            Assert.IsFalse(context.TryGetVariable("unsupported", out _));
            LogAssert.Expect(LogType.Error, new Regex("Unknown variable 'ghost'"));
            Assert.IsFalse(context.TryGetVariable("ghost", out _));
        }

        [Test]
        public void SchemaSource_MakesTheBlackboardTheDatabaseWorld()
        {
            var source = Track(ScriptableObject.CreateInstance<BlackboardSchemaSource>());
            source.blackboard = m_GuardAsset; // agent board: its own keys plus the World parent's
            m_Db.schemaSource = source;

            CollectionAssert.AreEquivalent(new[] { "npc.alertness", "player.has_old_key", "npc.guard.trust" }, m_Db.Schema.VariableNames.ToArray(),
                "supported key types only, quest keys excluded, parent keys included");
            CollectionAssert.AreEqual(new[] { "key" }, m_Db.Schema.QuestNames.ToArray());

            var context = m_Db.CreateContext();
            Assert.IsInstanceOf<BlackboardDialogueContext>(context);
            Assert.AreSame(SharedBlackboards.GetRuntime(m_GuardAsset), ((BlackboardDialogueContext)context).Blackboard,
                "runtime context = the shared play-session instance, not the asset");
        }

        [Test]
        public void SchemaSource_WithoutBlackboard_IsEmpty_NotAnError()
        {
            var source = Track(ScriptableObject.CreateInstance<BlackboardSchemaSource>());
            m_Db.schemaSource = source;
            CollectionAssert.IsEmpty(m_Db.Schema.VariableNames);
            Assert.IsFalse(m_Db.Schema.HasQuest("key"));
        }

        [Test]
        public void QuestKey_HoldingAnInvalidNumber_IsReported()
        {
            var context = BlackboardDialogueContext.ForWorld(m_WorldAsset);
            context.Blackboard.SetValue("quest.key", 99);
            LogAssert.Expect(LogType.Error, new Regex("holds 99"));
            Assert.IsFalse(context.TryGetQuestState("key", out _));
        }
    }
}
