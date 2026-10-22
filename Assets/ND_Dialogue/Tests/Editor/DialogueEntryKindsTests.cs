using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ND.Dialogue.Tests
{
    /// <summary>An entry kind made by a game: its own data and behaviour.</summary>
    [Serializable]
    public class TestShopEntry : DialogueEntry
    {
        public int price = 5;
        [NonSerialized] public static int Entered;
        public override void OnEnter(IDialogueContext context) => Entered++;
    }

    /// <summary>Entry subclasses, several lines per entry, and loading databases saved before both existed.</summary>
    public class DialogueEntryKindsTests
    {
        private const string TempFolder = "Assets/_NDDialogueEntryKindsTest";
        private readonly List<Object> m_Track = new List<Object>();
        private DialogueDatabase m_Db;
        private Conversation m_Conv;
        private Actor m_Npc, m_Player;

        [SetUp]
        public void SetUp()
        {
            TestShopEntry.Entered = 0;
            m_Db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(m_Db);
            m_Npc = m_Db.AddActor("Draco");
            foreach (var id in new[] { "Neutral", "Angry", "Embarrassed" }) m_Npc.emotions.Add(new ActorEmotion { id = id });
            m_Player = m_Db.AddActor("Harry", isPlayer: true);
            m_Conv = m_Db.AddConversation("Hall");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void SeveralLines_PlayOneAfterAnother_WithTheirOwnEmotion_ThenTheLinksAreFollowed()
        {
            var e = m_Conv.AddEntry(m_Npc.id);
            e.text.Text = "Potter.";
            e.emotion = "Angry";
            e.moreLines.Add(new DialoguePage { text = { Text = "It's not like I waited for you." }, emotion = "Embarrassed" });
            e.moreLines.Add(new DialoguePage { text = { Text = "Hmph." } }); // no emotion: the entry's
            var next = m_Conv.AddEntry(m_Npc.id);
            next.text.Text = "Whatever.";
            m_Conv.Link(m_Conv.StartEntry, e);
            m_Conv.Link(e, next);

            var runner = new DialogueRunner(m_Db, m_Db.CreateContext());
            var shown = new List<DialogueLine>();
            runner.LineStarted += l => shown.Add(l);
            Assert.IsTrue(runner.Start(m_Conv.id));
            runner.Continue();
            runner.Continue();
            runner.Continue();

            CollectionAssert.AreEqual(new[] { "Potter.", "It's not like I waited for you.", "Hmph.", "Whatever." }, shown.Select(l => l.Text));
            CollectionAssert.AreEqual(new[] { "Angry", "Embarrassed", "Angry", "Neutral" }, shown.Select(l => l.Emotion?.id));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, shown.Select(l => l.LineIndex));
            Assert.AreEqual(3, shown[0].LineCount);
            Assert.IsTrue(shown[1].HasMoreLines);
            Assert.IsFalse(shown[2].HasMoreLines);
        }

        [Test]
        public void EntrySubclass_KeepsItsData_InTheDatabase_AndRunsOnEnter()
        {
            var shop = m_Conv.AddEntry<TestShopEntry>(m_Npc.id);
            shop.text.Text = "Buy something.";
            shop.price = 42;
            m_Conv.Link(m_Conv.StartEntry, shop);

            var so = new SerializedObject(m_Db);
            int index = m_Conv.entries.IndexOf(shop);
            var price = so.FindProperty($"{nameof(DialogueDatabase.conversations)}.Array.data[0].{Conversation.EntriesField}.Array.data[{index}].{nameof(TestShopEntry.price)}");
            Assert.IsNotNull(price, "the subclass field is serialized with the entry");
            Assert.AreEqual(42, price.intValue);

            var runner = new DialogueRunner(m_Db, m_Db.CreateContext());
            runner.Start(m_Conv.id);
            Assert.AreEqual(1, TestShopEntry.Entered);
            Assert.IsInstanceOf<TestShopEntry>(m_Conv.FindEntry(shop.id));
        }

        [Test]
        public void AddEntry_RefusesATypeThatIsNotAnEntry()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not a concrete DialogueEntry"));
            Assert.IsNull(m_Conv.AddEntry(typeof(string), m_Npc.id));
        }

        [Test]
        public void ADatabaseSavedBeforeEntrySubclasses_LoadsItsEntries()
        {
            AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
            string path = $"{TempFolder}/Legacy.asset";
            string script = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(m_Db)));
            File.WriteAllText(path,
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                $"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name: Legacy\n  m_EditorClassIdentifier: \n" +
                "  conversations:\n  - id: 1\n    title: Old\n    entries:\n" +
                "    - id: 0\n      title: START\n      isPassthrough: 1\n      links:\n      - conversationId: 1\n        entryId: 5\n        priority: 2\n" +
                "    - id: 5\n      title: Hello\n      actorId: 1\n      text:\n        m_Text: Hi there\n      emotion: Angry\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(path);
            Assert.IsNotNull(db);
            var conversation = db.FindConversation(1);
            CollectionAssert.AreEqual(new[] { 0, 5 }, conversation.entries.Select(e => e.id));
            Assert.AreEqual("Hi there", conversation.FindEntry(5).text.Text);
            Assert.AreEqual(5, conversation.StartEntry.links[0].entryId);

            // Saved again: the new format, still the same entries.
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            StringAssert.Contains(Conversation.EntriesField, File.ReadAllText(path));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Resources.UnloadAsset(db);
            db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(path);
            CollectionAssert.AreEqual(new[] { 0, 5 }, db.FindConversation(1).entries.Select(e => e.id));
        }
    }
}
