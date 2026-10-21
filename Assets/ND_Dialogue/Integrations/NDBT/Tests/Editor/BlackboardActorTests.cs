using System.Collections.Generic;
using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.Localization;
using ND_BehaviorTree;
using NUnit.Framework;
using UnityEngine;

namespace ND.Dialogue.NDBT.Tests
{
    /// <summary>Characters whose attributes are blackboard keys, shared with the NPC's behavior-tree agent.</summary>
    public class BlackboardActorTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private Blackboard m_World, m_GuardBoard;
        private DialogueDatabase m_Db;
        private StringTable m_Text;
        private BlackboardActor m_Guard;
        private Blackboard m_Agent;

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

            m_World = Track(ScriptableObject.CreateInstance<Blackboard>());
            AddKey<IntKey>(m_World, "Merchant.mood");

            m_GuardBoard = Track(ScriptableObject.CreateInstance<Blackboard>()); // the guard NPC's tree blackboard
            m_GuardBoard.name = "GuardBB";
            m_GuardBoard.parent = m_World;
            AddKey<FloatKey>(m_GuardBoard, "trust");
            AddKey<BoolKey>(m_GuardBoard, "met");
            AddKey<Vector3Key>(m_GuardBoard, "homePosition");
            AddKey<QuaternionKey>(m_GuardBoard, "facing"); // not an attribute type

            m_Db = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            var source = Track(ScriptableObject.CreateInstance<BlackboardSchemaSource>());
            source.blackboard = m_World;
            m_Db.schemaSource = source;
            m_Guard = m_Db.AddActor(new BlackboardActor { name = "Guard", blackboard = m_GuardBoard });
            var merchant = m_Db.AddActor("Merchant");
            merchant.attributes.Add(new VariableDefinition { name = "mood", initialValue = VariableValue.Int(0) });

            m_Agent = Track(m_GuardBoard.Clone()); // what the guard's BehaviorTreeRunner creates at Start
            m_Track.AddRange(m_Agent.keys);
        }

        [TearDown]
        public void TearDown()
        {
            DialogueActorBoards.Unbind("Guard", m_Agent);
            SharedBlackboards.Clear();
            LocalizationService.Shutdown();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueRunner RunnerFor(string conversation, params DialogueAction[] actions)
        {
            var c = m_Db.AddConversation(conversation);
            var line = c.AddEntry(m_Guard.id);
            long id = m_Text.AddEntry(conversation);
            m_Text.SetValue(id, "en", "Hm.");
            line.text.SetReference(m_Text, id);
            line.actions.AddRange(actions);
            c.Link(c.StartEntry, line);
            return new DialogueRunner(m_Db, m_Db.CreateContext());
        }

        [Test]
        public void Attributes_AreTheSupportedKeysOfTheCharacterBlackboard()
        {
            CollectionAssert.AreEquivalent(new[] { "trust", "met", "homePosition" }, m_Guard.AttributeDefinitions.Select(a => a.name).ToArray());
            var names = m_Db.Schema.VariableNames.ToList();
            CollectionAssert.IsSupersetOf(names, new[] { "Guard.trust", "Guard.met", "Merchant.mood" });
            CollectionAssert.DoesNotContain(names, "Guard.facing");
            Assert.IsNotNull(m_Guard.EditorNote);
        }

        [Test]
        public void BoundAgent_SharesTheValues_WithDialogue()
        {
            DialogueActorBoards.Bind("Guard", m_Agent);
            var runner = RunnerFor("Gift",
                new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "1.5" },
                new SetVariableAction { variable = "Guard.met", value = "true" });

            runner.Start("Gift");

            Assert.AreEqual(1.5f, m_Agent.GetValue<float>("trust"), "the AI reads what dialogue wrote");
            Assert.IsTrue(m_Agent.GetValue<bool>("met"));
            Assert.AreEqual(0f, m_GuardBoard.GetValue<float>("trust"), "the asset is untouched");
        }

        [Test]
        public void UnboundCharacter_UsesTheSharedInstanceOfItsBlackboard()
        {
            var runner = RunnerFor("Gift", new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "2" });
            runner.Start("Gift");
            Assert.AreEqual(2f, SharedBlackboards.GetRuntime(m_GuardBoard).GetValue<float>("trust"));
            Assert.AreEqual(0f, m_Agent.GetValue<float>("trust"), "not bound: the agent's own copy is separate");
        }

        [Test]
        public void PlainCharacterAttributes_AreWorldKeysWithTheFullName()
        {
            var runner = RunnerFor("Mood", new SetVariableAction { variable = "Merchant.mood", value = "3" });
            runner.Start("Mood");
            Assert.AreEqual(3, SharedBlackboards.GetRuntime(m_World).GetValue<int>("Merchant.mood"));
            CollectionAssert.IsEmpty(DialoguePluginRegistry.Validate(m_Db));
        }

        [Test]
        public void PluginRule_ReportsMissingWorldKeysAndBlackboardlessActors()
        {
            m_Db.FindActor("Merchant").attributes.Add(new VariableDefinition { name = "debt" });
            m_Db.AddActor(new BlackboardActor { name = "Ghost" });

            var issues = DialoguePluginRegistry.Validate(m_Db);
            Assert.That(issues, Has.Some.Contains("needs the key 'Merchant.debt'"));
            Assert.That(issues, Has.Some.Contains("'Ghost' is a Blackboard Actor without a blackboard"));
        }

        [Test]
        public void VectorColorAndObjectKeys_AreAttributesToo_AndObjectsMustFitTheKeyType()
        {
            AddKey<ColorKey>(m_GuardBoard, "tint");
            AddKey<TransformKey>(m_GuardBoard, "post");
            var agent = Track(m_GuardBoard.Clone());
            m_Track.AddRange(agent.keys);
            DialogueActorBoards.Bind("Guard", agent);
            var post = Track(new GameObject("Post")).transform;
            var material = Track(new Material(Shader.Find("Hidden/InternalErrorShader")));
            var context = m_Db.CreateContext();

            Assert.IsTrue(context.SetVariable("Guard.homePosition", VariableValue.Vector3(new Vector3(1, 2, 3))));
            Assert.IsTrue(context.SetVariable("Guard.tint", VariableValue.Color(Color.red)));
            Assert.IsTrue(context.SetVariable("Guard.post", VariableValue.Object(post)));
            Assert.AreEqual(new Vector3(1, 2, 3), agent.GetValue<Vector3>("homePosition"));
            Assert.AreEqual(Color.red, agent.GetValue<Color>("tint"));
            Assert.AreSame(post, agent.GetValue<Transform>("post"));

            Assert.IsTrue(context.TryGetVariable("Guard.post", out var read));
            Assert.AreEqual(VariableType.Object, read.type);
            Assert.AreSame(post, read.objectValue);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("holds Transform, got Material"));
            Assert.IsFalse(context.SetVariable("Guard.post", VariableValue.Object(material)));
            Assert.AreSame(post, agent.GetValue<Transform>("post"), "unchanged");
            DialogueActorBoards.Unbind("Guard", agent);
        }

        [Test]
        public void BlackboardActor_SurvivesSerialization()
        {
            var copy = Track(Object.Instantiate(m_Db));
            var guard = copy.FindActor("Guard") as BlackboardActor;
            Assert.IsNotNull(guard);
            Assert.AreSame(m_GuardBoard, guard.blackboard);
        }
    }
}
