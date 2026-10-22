using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>START of a bark conversation shows every bark line in one table (text, priority, conditions), and adds new ones.</summary>
    public class BarkTableTests
    {
        private DialogueDatabase m_Db;
        private Conversation m_Barks;

        [SetUp]
        public void SetUp()
        {
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            var guard = m_Db.AddActor("Guard");
            m_Db.AddVariable("alarm", VariableValue.Bool(false));
            m_Barks = m_Db.AddConversation("Guard Barks");
            m_Barks.isBark = true;
            var idle = m_Barks.AddEntry(guard.id);
            idle.text.Text = "Hmm.";
            var thief = m_Barks.AddEntry(guard.id);
            thief.text.Text = "Stop, thief!";
            m_Barks.Link(m_Barks.StartEntry, idle);
            m_Barks.Link(m_Barks.StartEntry, thief, LinkPriority.High).conditions.Add(new VariableCondition { variable = "alarm", value = "true" });
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(m_Db);
        }

        [Test]
        public void StartOfABarkConversation_ListsEveryLine_AndAddsNewOnes()
        {
            var inspector = new DialogueEntryInspector();
            inspector.Bind(m_Db, m_Barks, m_Barks.StartEntry, "en");
            var cards = inspector.Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith("bark-")).ToList();
            Assert.AreEqual(2, cards.Count);
            Assert.AreEqual("Stop, thief!", cards[1].Q<TextField>().value);
            Assert.IsNotNull(cards[1].Q<PropertyField>(), "its condition (alarm == true) is listed and editable");

            typeof(DialogueEntryInspector).GetMethod("AddBarkLine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, null);
            Assert.AreEqual(3, m_Barks.StartEntry.links.Count);
            var added = m_Barks.FindEntry(m_Barks.StartEntry.links[2].entryId);
            Assert.AreEqual("Guard", m_Db.FindActor(added.actorId).name, "said by the same character");
            Undo.PerformUndo();
            Assert.AreEqual(2, m_Db.FindConversation(m_Barks.id).StartEntry.links.Count, "undoable");
        }

        [Test]
        public void AnotherLineOfABarkConversation_PointsToStart()
        {
            var inspector = new DialogueEntryInspector();
            inspector.Bind(m_Db, m_Barks, m_Barks.FindEntry(1), "en");
            Assert.That(inspector.Query<HelpBox>().ToList().Select(h => h.text), Has.Some.Contains("select START"));
        }
    }
}
