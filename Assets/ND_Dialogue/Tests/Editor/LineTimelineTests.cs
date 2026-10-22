using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace ND.Dialogue.Tests
{
    /// <summary>The line pipeline: markup → commands → timeline → frames, the TextMeshPro typewriter on it, voices of lines.</summary>
    public class LineTimelineTests
    {
        private readonly List<Object> m_Track = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private static string Fix(string text)
        {
            LineStyles.TryGet(LineStyles.Fix, out var fix);
            return fix.RichOpen + text + fix.RichClose;
        }

        [Test]
        public void Final_PlainText_IsUnchanged() => Assert.AreEqual("Hello, Potter.", LineTimeline.Final("Hello, Potter."));

        [Test]
        public void Final_UnknownTags_StayRichText() => Assert.AreEqual("<b>bold</b> <color=red>red</color>", LineTimeline.Final("<b>bold</b> <color=red>red</color>"));

        [Test]
        public void Final_DropsTimingTags() => Assert.AreEqual("Well... fine.", LineTimeline.Final("Well...<pause 0.8> <speed 10>fine.</speed><event name=\"x\">"));

        [Test]
        public void Pause_HoldsTheTyping()
        {
            var timeline = LineTimeline.Build("Hi<pause 1> you", 10f);
            Assert.AreEqual(1.6f, timeline.Duration, 1e-4f);
            Assert.AreEqual("H", timeline.Compose(0f).Text, "the first character shows at once");
            Assert.AreEqual("Hi", timeline.Compose(1.0f).Text, "still paused");
            Assert.AreEqual("Hi ", timeline.Compose(1.25f).Text);
            Assert.IsFalse(timeline.Compose(1.5f).Done);
            Assert.IsTrue(timeline.Compose(1.6f).Done);
        }

        [Test]
        public void Speed_ChangesThePaceUntilClosed()
        {
            var timeline = LineTimeline.Build("ab<speed 2>cd</speed>ef", 10f);
            Assert.AreEqual(0.2f + 1f + 0.2f, timeline.Duration, 1e-4f);
            Assert.AreEqual("abc", timeline.Compose(0.3f).Text);
            Assert.AreEqual("abcd", timeline.Compose(0.75f).Text);
        }

        [Test]
        public void Fix_SwapsTheErrorWordsAtItsMoment()
        {
            const string line = "I <err id=\"a\">hate</err> <fix id=\"a\" text=\"love\">you";
            var errors = new List<string>();
            LineMarkup.Parse(line, errors);
            CollectionAssert.IsEmpty(errors);

            var timeline = LineTimeline.Build(line, 10f);
            string before = timeline.Compose(0.65f).Text;
            StringAssert.Contains("hate", before);
            StringAssert.DoesNotContain("love", before);
            string after = timeline.Compose(0.75f).Text;
            StringAssert.DoesNotContain("hate", after);
            StringAssert.Contains(Fix("love"), after);
            Assert.AreEqual(0.7f + LineTimeline.FixHold + 0.3f, timeline.Duration, 1e-4f, "typing holds on the fix");
            Assert.AreEqual("I " + Fix("love") + " you", LineTimeline.Final(line));
        }

        [Test]
        public void Break_StartsAnIndentedLine() =>
            Assert.AreEqual("a\n<indent=40px>b</indent>", LineTimeline.Final("a<br posX=\"40\">b"));

        [Test]
        public void Styles_GiveEffectRangesInVisibleCharacters()
        {
            var frame = LineTimeline.Build("<b>x</b><wave>yz</wave>", 0f).Compose(float.PositiveInfinity);
            Assert.AreEqual(3, frame.VisibleCharacters);
            Assert.AreEqual(1, frame.Effects.Count);
            Assert.AreEqual(1, frame.Effects[0].Start, "rich tags do not count");
            Assert.AreEqual(2, frame.Effects[0].Length);
            Assert.AreEqual(LineEffect.Wave, frame.Effects[0].Effect);
        }

        [Test]
        public void Events_AreReachedWhenTypingGetsThere()
        {
            var timeline = LineTimeline.Build("a<event name=\"door\" arg=\"open\">b", 10f);
            var reached = new List<LineEvent>();
            timeline.EventsBetween(0f, 0.05f, reached);
            CollectionAssert.IsEmpty(reached);
            timeline.EventsBetween(0.05f, 0.1f, reached);
            Assert.AreEqual(1, reached.Count);
            Assert.AreEqual("door", reached[0].Name);
            Assert.AreEqual("open", reached[0].Argument);
        }

        [Test]
        public void FitSeconds_StretchesTheLineToTheVoice()
        {
            var timeline = LineTimeline.Build("ab<event name=\"x\">cd", 10f, fitSeconds: 2f);
            Assert.AreEqual(2f, timeline.Duration, 1e-4f);
            Assert.AreEqual(1f, timeline.Events[0].Time, 1e-4f);
            Assert.AreEqual("abc", timeline.Compose(1.0f).Text);
        }

        [Test]
        public void Parse_ReportsMistakes()
        {
            int Errors(string line)
            {
                var errors = new List<string>();
                LineMarkup.Parse(line, errors);
                return errors.Count;
            }
            Assert.AreEqual(1, Errors("<pause>"), "pause without seconds");
            Assert.AreEqual(1, Errors("<wave>never closed"), "unclosed style");
            Assert.AreEqual(1, Errors("<fix id=\"q\" text=\"y\">"), "fix without its err");
            Assert.AreEqual(1, Errors("x</shake>"), "closes nothing");
            Assert.AreEqual(0, Errors("<b>fine</b> <pause 0.2> <wave>ok</wave>"));
        }

        [Test]
        public void LegacyText_KeepsOnlyTheTagsUIText_Draws()
        {
            string rich = LineTimeline.Final("<b>Hi</b> <err id=\"a\">bad</err><fix id=\"a\" text=\"good\"> <whisper>psst</whisper><br posX=\"40\">end");
            Assert.AreEqual("<b>Hi</b> <color=#7CFFB2>good</color> <color=#9AA3B2>psst</color>\nend", UIText.ForLegacyText(rich));
            Assert.AreEqual("a < b", UIText.ForLegacyText("a < b"), "a lone '<' is text");
        }

        [Test]
        public void Validate_ReportsLineMarkupErrors()
        {
            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(db);
            var npc = db.AddActor("Malfoy");
            var conversation = db.AddConversation("Talk");
            var entry = conversation.AddEntry(npc.id);
            entry.text.Text = "Hmph.<pause>";
            conversation.Link(conversation.StartEntry, entry);
            Assert.IsTrue(db.Validate().Any(i => i.Contains($"entry {entry.id}") && i.Contains("markup")), string.Join("\n", db.Validate()));
        }

        [Test]
        public void Runner_GivesEachLineItsVoice()
        {
            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            m_Track.Add(db);
            var npc = db.AddActor("Malfoy");
            var conversation = db.AddConversation("Talk");
            var entry = conversation.AddEntry(npc.id);
            var first = AudioClip.Create("first", 4410, 1, 44100, false);
            var second = AudioClip.Create("second", 4410, 1, 44100, false);
            m_Track.Add(first);
            m_Track.Add(second);
            entry.text.Text = "One.";
            entry.voice = first;
            entry.moreLines.Add(new DialoguePage { text = { Text = "Two." }, voice = second });
            entry.moreLines.Add(new DialoguePage { text = { Text = "Three." } });
            conversation.Link(conversation.StartEntry, entry);

            var voices = new List<AudioClip>();
            var runner = new DialogueRunner(db, new DialogueWorld(db));
            runner.LineStarted += l => voices.Add(l.Voice);
            runner.Start(conversation.id);
            runner.Continue();
            runner.Continue();
            CollectionAssert.AreEqual(new[] { first, second, null }, voices);
        }

        [Test]
        public void TypewriterText_TypesRaisesEventsAndFinishes()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
                Assert.Ignore("TextMeshPro Essential Resources are not imported (no default font).");
            var go = new GameObject("Line", typeof(RectTransform));
            m_Track.Add(go);
            var text = go.AddComponent<TextMeshProUGUI>();
            var typewriter = go.AddComponent<TypewriterText>();
            typewriter.Text = text;
            typewriter.CharactersPerSecond = 10f;
            var events = new List<string>();
            int finished = 0;
            typewriter.EventReached += e => events.Add(e.Name);
            typewriter.Finished += () => finished++;

            typewriter.Play("Hi<event name=\"blink\">!");
            Assert.IsTrue(typewriter.IsPlaying);
            Assert.AreEqual("H", text.text);
            typewriter.Advance(0.15f);
            Assert.AreEqual("Hi", text.text);
            CollectionAssert.IsEmpty(events);
            typewriter.Advance(0.1f);
            Assert.AreEqual("Hi!", text.text);
            CollectionAssert.AreEqual(new[] { "blink" }, events);
            typewriter.Advance(0.1f);
            Assert.IsFalse(typewriter.IsPlaying);
            Assert.AreEqual(1, finished);

            typewriter.Play("<wave>Long line</wave> here<event name=\"end\">");
            typewriter.Complete();
            Assert.IsFalse(typewriter.IsPlaying);
            Assert.AreEqual("Long line here", text.text);
            CollectionAssert.AreEqual(new[] { "blink", "end" }, events, "Complete still raises the events left");
            Assert.AreEqual(2, finished);
        }
    }
}
