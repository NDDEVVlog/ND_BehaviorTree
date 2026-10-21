using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>Player interactor + NPC triggers + manager + UI, as built in a scene (no Play mode).</summary>
    public class DialogueInteractionTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private DialogueManager m_Manager;
        private DialogueInteractor m_Interactor;
        private GameObject m_PromptRoot;
        private Text m_PromptText;
        private DialogueTrigger m_Guard, m_Merchant;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            var guard = m_Db.AddActor("Guard");
            guard.displayName.Text = "Gate Guard";
            m_Db.AddActor("Merchant");
            m_Db.AddVariable("gate_open", VariableValue.Bool(false));
            foreach (var title in new[] { "Gate", "Shop" })
            {
                var c = m_Db.AddConversation(title);
                var line = c.AddEntry(guard.id);
                line.text.Text = title + " line";
                c.Link(c.StartEntry, line);
            }

            m_Manager = DialogueUIBuilder.CreateSetup(m_Db);
            m_Track.Add(m_Manager.gameObject);
            m_Track.Add(m_Manager.View.transform.root.gameObject);
            var events = Object.FindAnyObjectByType<EventSystem>();
            if (events != null) m_Track.Add(events.gameObject);
            Assert.IsTrue(m_Manager.Initialize());

            m_Guard = Npc("Guard NPC", new Vector3(0, 0, 3), "Gate", "Guard");
            m_Merchant = Npc("Merchant NPC", new Vector3(0, 0, 10), "Shop", null);

            var player = new GameObject("Player");
            m_Track.Add(player);
            m_Interactor = player.AddComponent<DialogueInteractor>();
            (m_PromptRoot, m_PromptText) = DialogueUIBuilder.CreatePrompt(m_Manager.View.transform.root);
            var so = new SerializedObject(m_Interactor);
            so.FindProperty("m_Manager").objectReferenceValue = m_Manager;
            so.FindProperty("m_PromptRoot").objectReferenceValue = m_PromptRoot;
            so.FindProperty("m_PromptText").objectReferenceValue = m_PromptText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private DialogueTrigger Npc(string objectName, Vector3 position, string conversation, string actor)
        {
            var go = new GameObject(objectName);
            m_Track.Add(go);
            go.transform.position = position;
            var trigger = go.AddComponent<DialogueTrigger>();
            trigger.Manager = m_Manager;
            trigger.Conversation = conversation;
            trigger.Actor = actor;
            return trigger;
        }

        private void PlacePlayer(Vector3 position) => m_Interactor.transform.position = position;

        [Test]
        public void Prompt_AppearsOnlyInRange_WithTheCharacterDisplayName()
        {
            PlacePlayer(Vector3.zero); // guard 3 m away, range 2.5
            Assert.IsNull(m_Interactor.RefreshTarget());
            Assert.IsFalse(m_PromptRoot.activeSelf);

            PlacePlayer(new Vector3(0, 0, 1));
            Assert.AreSame(m_Guard, m_Interactor.RefreshTarget());
            Assert.IsTrue(m_PromptRoot.activeSelf);
            Assert.AreEqual("E — Talk to Gate Guard", m_PromptText.text);

            PlacePlayer(new Vector3(0, 0, 9));
            Assert.AreSame(m_Merchant, m_Interactor.RefreshTarget());
            Assert.AreEqual("E — Talk to Merchant NPC", m_PromptText.text, "no actor: the object's name");
        }

        [Test]
        public void NearestUsableTrigger_Wins()
        {
            m_Merchant.transform.position = new Vector3(0, 0, 2);
            m_Guard.transform.position = new Vector3(0, 0, 4.5f);
            PlacePlayer(new Vector3(0, 0, 2.5f)); // merchant 0.5 m away, guard 2 m away: both in range
            Assert.AreSame(m_Merchant, m_Interactor.RefreshTarget());
        }

        [Test]
        public void Interact_StartsTheConversation_HidesThePrompt_AndNothingIsUsableWhileTalking()
        {
            PlacePlayer(new Vector3(0, 0, 1));
            m_Interactor.RefreshTarget();
            Assert.IsTrue(m_Interactor.Interact());

            Assert.IsTrue(m_Manager.IsTalking);
            Assert.AreEqual("Gate", m_Manager.Runner.CurrentConversation.title);
            Assert.IsFalse(m_PromptRoot.activeSelf);
            Assert.IsNull(m_Interactor.RefreshTarget(), "no prompt during a conversation");

            m_Manager.StopConversation();
            Assert.AreSame(m_Guard, m_Interactor.RefreshTarget(), "usable again afterwards");
        }

        [Test]
        public void Conditions_HideATrigger_UntilTheWorldAllowsIt()
        {
            m_Guard.Conditions.Add(new VariableCondition { variable = "gate_open", value = "true" });
            PlacePlayer(new Vector3(0, 0, 1));
            Assert.IsNull(m_Interactor.RefreshTarget());
            Assert.IsFalse(m_Guard.CanFire(out string reason));
            StringAssert.Contains("gate_open", reason);

            m_Manager.Context.SetVariable("gate_open", VariableValue.Bool(true));
            Assert.AreSame(m_Guard, m_Interactor.RefreshTarget());
        }

        [Test]
        public void OnceOnly_FiresOneTime()
        {
            m_Guard.Once = true;
            Assert.IsTrue(m_Guard.Fire());
            m_Manager.StopConversation();
            Assert.IsTrue(m_Guard.HasFired);

            LogAssert.Expect(LogType.Warning, new Regex("did not start 'Gate': already used \\(once\\)"));
            Assert.IsFalse(m_Guard.Fire());
            m_Guard.ResetFired();
            Assert.IsTrue(m_Guard.Fire());
        }

        [Test]
        public void DisabledTriggers_AreNotUsable()
        {
            PlacePlayer(new Vector3(0, 0, 1));
            m_Guard.enabled = false;
            Assert.IsFalse(System.Linq.Enumerable.Contains(DialogueTrigger.Enabled, m_Guard));
            Assert.IsNull(m_Interactor.RefreshTarget());
            m_Guard.enabled = true;
            Assert.AreSame(m_Guard, m_Interactor.RefreshTarget());
        }

        [Test]
        public void OnStartTriggers_NeverFireInEditMode()
        {
            m_Guard.When = DialogueTriggerEvent.OnStart;
            var go = new GameObject("Auto");
            m_Track.Add(go);
            var auto = go.AddComponent<DialogueTrigger>();
            auto.Manager = m_Manager;
            auto.Conversation = "Gate";
            auto.When = DialogueTriggerEvent.OnStart;
            Assert.IsFalse(m_Manager.IsTalking, "edit-mode registration must not start dialogue");
        }
    }
}
