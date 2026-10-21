using System.Linq;
using ND.Dialogue.Editor.Plugins;
using ND.Dialogue.NDBT.Editor.Samples;
using ND.Localization;
using ND_BehaviorTree;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ND.Dialogue.NDBT.Tests
{
    /// <summary>
    /// Builds the Guard sample into a scratch folder (additive scene), checks every wire the builder makes, and plays
    /// the whole quest through the scene's own manager, triggers and interactor — what a person would do in Play mode.
    /// </summary>
    public class GuardSampleEndToEndTests
    {
        private const string Folder = "Assets/_NDDialogueSampleTest";
        private Scene m_Scene;
        private DialogueDatabase m_Db;

        [SetUp]
        public void SetUp()
        {
            SharedBlackboards.Clear();
            LocalizationService.Shutdown();
            m_Db = DialogueGuardSampleBuilder.Build(Folder, NewSceneMode.Additive, out m_Scene);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Scene.IsValid()) EditorSceneManager.CloseScene(m_Scene, true);
            AssetDatabase.DeleteAsset(Folder);
            SharedBlackboards.Clear();
            LocalizationService.Shutdown();
        }

        private T Find<T>() where T : Component =>
            m_Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private T FindNamed<T>(string objectName) where T : Component =>
            m_Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault(c => c.name == objectName);

        [Test]
        public void Build_SavesAValidDatabase_AndAFullyWiredScene()
        {
            Assert.IsNotNull(m_Db);
            CollectionAssert.IsEmpty(DialoguePluginRegistry.Validate(m_Db));
            Assert.IsTrue(System.IO.File.Exists(DialogueGuardSampleBuilder.ScenePathIn(Folder)));

            var manager = Find<DialogueManager>();
            Assert.AreSame(m_Db, manager.Database, "the regression: asset references saved as null");
            Assert.IsNotNull(manager.View);
            Assert.IsNotNull(new SerializedObject(Find<LocalizationBootstrap>()).FindProperty("m_Settings").objectReferenceValue);

            var triggers = m_Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueTrigger>()).ToList();
            CollectionAssert.AreEquivalent(new[] { "Gate Guard", "Merchant" }, triggers.Select(t => t.Conversation));
            Assert.IsTrue(triggers.All(t => t.Manager == manager));
            Assert.IsNotNull(Find<DialogueInteractor>());
            Assert.IsNotNull(Find<DialogueEventListener>());
        }

        [Test]
        public void PlayingTheQuest_ThroughTheSceneObjects_GivesTheExpectedWorld()
        {
            LocalizationService.Initialize((LocalizationSettings)new SerializedObject(Find<LocalizationBootstrap>()).FindProperty("m_Settings").objectReferenceValue);
            var manager = Find<DialogueManager>();
            Assert.IsTrue(manager.Initialize());
            var ui = (StandardDialogueUI)manager.View;
            var so = new SerializedObject(ui);
            so.FindProperty("m_CharactersPerSecond").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var interactor = Find<DialogueInteractor>();
            var guardNpc = FindNamed<DialogueTrigger>("Guard (NPC)");
            var merchantNpc = FindNamed<DialogueTrigger>("Merchant (NPC)");
            var runner = manager.Runner;

            void TalkTo(DialogueTrigger npc)
            {
                interactor.transform.position = npc.transform.position + Vector3.back;
                Assert.AreSame(npc, interactor.RefreshTarget(), $"{npc.name} should be usable");
                Assert.IsTrue(interactor.Interact());
            }

            // 1. Meet the guard and ask for work.
            TalkTo(guardNpc);
            Assert.AreEqual("Halt! Nobody passes this gate.", runner.CurrentLine.Text);
            ui.Continue();
            Assert.AreEqual("I'm looking for work.", runner.CurrentChoices[0].Text);
            ui.Choose(0);
            ui.Continue(); // "Find my old key…" → end
            Assert.IsFalse(manager.IsTalking);

            // 2. Buy the key.
            TalkTo(merchantNpc);
            StringAssert.Contains("You have 10 gold", runner.CurrentLine.Text);
            ui.Continue();
            ui.Choose(0);
            StringAssert.Contains("(mood 1)", runner.CurrentLine.Text);
            ui.Continue();

            // 3. Bring it back.
            TalkTo(guardNpc);
            Assert.AreEqual("My key! Thank you. The gate is yours.", runner.CurrentLine.Text);
            Assert.AreEqual("Happy", runner.CurrentLine.Emotion.id);
            ui.Continue();

            var context = manager.Context;
            Assert.IsTrue(context.TryGetQuestState("key", out var quest));
            Assert.AreEqual(QuestState.Completed, quest);
            context.TryGetVariable("Guard.trust", out var trust);
            Assert.AreEqual(3f, trust.floatValue);
            context.TryGetVariable("player.gold", out var gold);
            Assert.AreEqual(5, gold.intValue);
            context.TryGetVariable("Guard.bannerColor", out var banner);
            Assert.AreEqual("#33CC55FF", banner.ToString());

            // 4. Afterwards.
            TalkTo(guardNpc);
            Assert.AreEqual("Welcome back, friend. (trust 3)", runner.CurrentLine.Text);
            ui.Continue();

            var worldAsset = ((BlackboardSchemaSource)m_Db.schemaSource).blackboard;
            Assert.AreEqual(10, worldAsset.GetValue<int>("player.gold"), "assets keep their design-time values");
        }
    }
}
