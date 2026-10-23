using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace ND.Dialogue.Editor.Tests
{
    /// <summary>A middleware cue on a line, as FMOD's is: no core change needed for a new middleware.</summary>
    [Serializable]
    public class TestVoiceCue : DialogueVoiceCue
    {
        public string id = "vo_1";
        public override string Describe() => "Test: " + id;
    }

    /// <summary>Plays <see cref="TestVoiceCue"/> lines; the test decides when the "sound" ends (length unknown, like a streamed FMOD event).</summary>
    public class TestVoicePlayer : DialogueVoicePlayer
    {
        public bool Sounding;
        public readonly List<string> Played = new List<string>();
        public override bool CanPlay(DialogueLine line) => line.VoiceCue is TestVoiceCue;
        public override float Play(DialogueLine line) { Played.Add(((TestVoiceCue)line.VoiceCue).id); Sounding = true; return 0f; }
        public override bool IsPlaying => Sounding;
        public override void Stop() => Sounding = false;
    }

    /// <summary>The TextMeshPro dialogue UI plays voices through pluggable players: the one for the line's cue wins, and it moves on when that voice ends.</summary>
    public class VoicePlayerTests
    {
        private readonly List<Object> m_Track = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private static readonly MethodInfo s_Update = typeof(TypewriterDialogueUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void MiddlewareCue_IsPlayedByItsPlayer_AndTheLineMovesOnWhenTheVoiceEnds()
        {
            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(db);
            var npc = db.AddActor("Malfoy");
            var conversation = db.AddConversation("Talk");
            var first = conversation.AddEntry(npc.id);
            first.text.Text = "Hmph.";
            first.voiceCue = new TestVoiceCue { id = "vo_hmph" };
            var clip = AudioClip.Create("clip", 4410, 1, 44100, false);
            m_Track.Add(clip);
            first.voice = clip; // the cue wins over the clip
            var second = conversation.AddEntry(npc.id);
            second.text.Text = "Whatever.";
            conversation.Link(conversation.StartEntry, first);
            conversation.Link(first, second);

            var manager = DialogueUIBuilder.CreateTypewriterSetup(db);
            if (manager == null) Assert.Ignore("TextMeshPro Essential Resources are not imported (no default font).");
            var ui = (TypewriterDialogueUI)manager.View;
            m_Track.Add(manager.gameObject);
            m_Track.Add(ui.transform.root.gameObject);
            var events = Object.FindAnyObjectByType<EventSystem>();
            if (events != null) m_Track.Add(events.gameObject);
            // The menu skips players from test assemblies, so this one is added by hand, first in the list.
            var player = ui.gameObject.AddComponent<TestVoicePlayer>();
            var so = new SerializedObject(ui);
            var players = so.FindProperty("m_VoicePlayers");
            players.InsertArrayElementAtIndex(0);
            players.GetArrayElementAtIndex(0).objectReferenceValue = player;
            so.FindProperty("m_ContinueAfterVoice").boolValue = true;
            so.FindProperty("m_ContinueDelay").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(ui.GetComponents<TestVoicePlayer>().Length > 1, "the menu did not add the test player itself");

            Assert.IsTrue(manager.Initialize());
            Assert.IsTrue(manager.StartConversation("Talk"));
            CollectionAssert.AreEqual(new[] { "vo_hmph" }, player.Played);
            ui.Line.Complete();

            s_Update.Invoke(ui, null);
            Assert.AreEqual(first.id, manager.Runner.CurrentEntry.id, "still sounding: the line waits");
            player.Sounding = false;
            s_Update.Invoke(ui, null);
            Assert.AreEqual(second.id, manager.Runner.CurrentEntry.id, "the voice ended: on to the next line");
        }

        [Test]
        public void TestChat_ListsTheMiddlewareCue()
        {
            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(db);
            var npc = db.AddActor("Malfoy");
            var conversation = db.AddConversation("Talk");
            var line = conversation.AddEntry(npc.id);
            line.text.Text = "Hmph.";
            line.voiceCue = new TestVoiceCue { id = "vo_hmph" };
            conversation.Link(conversation.StartEntry, line);

            var session = new DialogueTestSession(db, "en");
            var items = session.Start(conversation);
            Assert.That(DialogueTestSession.Format(items), Does.Contain("🔊 voice: Test: vo_hmph"));
        }
    }
}
