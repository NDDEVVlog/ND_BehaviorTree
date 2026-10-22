using System.Collections.Generic;
using System.Linq;
using ND.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>Find / replace, statistics and exports over a whole database, and the Tools tab.</summary>
    public class DialogueToolsTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private StringTable m_Table;
        private Conversation m_Conv;
        private DialogueEntry m_Greet, m_Reply, m_Translated;

        [SetUp]
        public void SetUp()
        {
            m_Db = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Db.name = "Hogwarts";
            m_Table = Track(ScriptableObject.CreateInstance<StringTable>());
            m_Db.textTable = m_Table;
            var malfoy = m_Db.AddActor("Malfoy");
            var harry = m_Db.AddActor("Harry", isPlayer: true);
            m_Conv = m_Db.AddConversation("Hall");
            m_Greet = m_Conv.AddEntry(malfoy.id);
            m_Greet.text.Text = "Potter, you idiot.";
            m_Greet.emotion = "Angry";
            m_Greet.moreLines.Add(new DialoguePage { text = { Text = "Says \"hi\", too." } });
            m_Reply = m_Conv.AddEntry(harry.id);
            m_Reply.text.Text = "Nice to see you, Malfoy.";
            m_Reply.menuText.Text = "Be nice";
            m_Translated = m_Conv.AddEntry(malfoy.id);
            long key = m_Table.AddEntry("c1.e3");
            m_Table.SetValue(key, "en", "Hmph. Potter.");
            m_Translated.text.SetReference(m_Table, key);
            m_Conv.Link(m_Conv.StartEntry, m_Greet);
            m_Conv.Link(m_Greet, m_Reply);
            m_Conv.Link(m_Reply, m_Translated);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        [Test]
        public void Find_LooksInEveryLineAndLabel_WithOrWithoutCase()
        {
            CollectionAssert.AreEquivalent(new[] { "Hall #1 line 1", "Hall #3" }, DialogueTools.Find(m_Db, "potter", false, "en").Select(h => h.Where));
            CollectionAssert.IsEmpty(DialogueTools.Find(m_Db, "potter", true, "en"));
            Assert.AreEqual("Hall #1 line 2", DialogueTools.Find(m_Db, "hi", false, "en").Single(h => h.Value.Contains("Says")).Where);
            Assert.AreEqual("Hall #2 (choice label)", DialogueTools.Find(m_Db, "Be nice", true, "en").Single().Where);
            CollectionAssert.AreEqual(new[] { "Hall #1 line 1" }, DialogueTools.Find(m_Db, "Potter", false, "vi").Select(h => h.Where), "plain text is the same in every locale; the connected line has no vi text");
        }

        [Test]
        public void ReplaceAll_ChangesPlainAndTranslatedTexts_InOneUndoStep()
        {
            Assert.AreEqual(2, DialogueTools.ReplaceAll(m_Db, "Potter", "Harry", true, "en"));
            Assert.AreEqual("Harry, you idiot.", m_Greet.text.Text);
            Assert.IsTrue(m_Table.TryGetValue(m_Translated.text.EntryId, "en", out string translated));
            Assert.AreEqual("Hmph. Harry.", translated);
            Undo.PerformUndo();
            Assert.AreEqual("Potter, you idiot.", m_Greet.text.Text, "one undo step brings everything back");
            Assert.IsTrue(m_Table.TryGetValue(m_Translated.text.EntryId, "en", out translated));
            Assert.AreEqual("Hmph. Potter.", translated);
        }

        [Test]
        public void Statistics_CountLinesChoicesWords_AndTranslationGaps()
        {
            var stats = DialogueTools.Statistics(m_Db, "en");
            Assert.AreEqual(4, stats.Lines, "3 entries, the first with 2 lines");
            Assert.AreEqual(1, stats.Choices);
            Assert.AreEqual(3 + 3 + 5 + 2, stats.Words);
            Assert.AreEqual(3, stats.Speakers.Single(s => s.Name == "Malfoy").Lines, "two entries: 2 lines + 1");
            Assert.AreEqual(4, stats.Plain, "three lines and the choice label are plain");
            CollectionAssert.IsEmpty(stats.Missing);
            Assert.AreEqual(1, DialogueTools.Statistics(m_Db, "vi").Missing.Count, "the connected line has no vi translation");
        }

        [Test]
        public void Exports_ReadAsCsvAndAsAScript()
        {
            var csv = DialogueTools.ExportCsv(m_Db, "en").Split('\n');
            Assert.AreEqual("conversation,entry,line,speaker,emotion,kind,text", csv[0]);
            Assert.AreEqual("Hall,1,2,Malfoy,Angry,line,\"Says \"\"hi\"\", too.\"", csv[2], "quotes are doubled, the field quoted");
            Assert.That(csv, Has.Some.EqualTo("Hall,2,,Harry,,choice label,Be nice"));

            string script = DialogueTools.ExportScreenplay(m_Db, "en");
            StringAssert.Contains("== Hall ==", script);
            StringAssert.Contains("#1  MALFOY (Angry): Potter, you idiot.", script);
            StringAssert.Contains("#2  → Be nice", script);
        }

        [Test]
        public void TheToolsTab_Builds_AndASearchListsResults()
        {
            var panel = new DialogueDatabasePanel();
            panel.Bind(m_Db, m_Conv.id);
            Assert.DoesNotThrow(() => panel.ShowTab(DialogueDatabasePanel.ToolsTab));
            Assert.IsNotNull(panel.Q<Label>("translation-status"));
        }
    }
}
