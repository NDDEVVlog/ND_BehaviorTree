using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ND.Dialogue.Tests
{
    /// <summary>Characters shared through Character Databases: same id everywhere, a look per dialogue database, template attributes.</summary>
    public class CharacterDatabaseTests
    {
        private readonly List<Object> m_Track = new List<Object>();
        private CharacterDatabase m_Cast;
        private DialogueDatabase m_Year1, m_Adult;
        private Actor m_Draco;

        private T Track<T>(T o) where T : Object { m_Track.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            m_Cast = Track(ScriptableObject.CreateInstance<CharacterDatabase>());
            m_Draco = m_Cast.AddCharacter("Draco");
            m_Draco.displayName.Text = "Draco Malfoy";
            m_Draco.emotions.Add(new ActorEmotion { id = "Neutral", tint = Color.white });
            m_Draco.emotions.Add(new ActorEmotion { id = "Angry", tint = Color.red });
            m_Year1 = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Adult = Track(ScriptableObject.CreateInstance<DialogueDatabase>());
            m_Year1.characterDatabases.Add(m_Cast);
            m_Adult.characterDatabases.Add(m_Cast);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        [Test]
        public void ASharedCharacter_IsTheSameInEveryDialogueDatabase()
        {
            Assert.GreaterOrEqual(m_Draco.id, CharacterDatabase.SharedIdBase);
            Assert.AreSame(m_Draco, m_Year1.FindActor(m_Draco.id));
            Assert.AreSame(m_Draco, m_Adult.FindActor("Draco"));
            Assert.AreSame(m_Cast, m_Year1.SourceOf(m_Draco));
            Assert.IsNull(m_Year1.SourceOf(m_Year1.AddActor("Local")));
            CollectionAssert.IsEmpty(m_Year1.Validate());
        }

        [Test]
        public void ALook_ChangesOnlyTheOverriddenEmotions_AndTheName_InItsDatabase()
        {
            var adultAngry = new ActorEmotion { id = "Angry", tint = Color.magenta };
            m_Adult.looks.Add(new ActorLook { actorId = m_Draco.id, displayName = { Text = "Adult Draco" }, emotions = { adultAngry } });

            Assert.AreSame(adultAngry, m_Adult.FindEmotion(m_Draco, "Angry"));
            Assert.AreEqual(Color.white, m_Adult.FindEmotion(m_Draco, "Neutral").tint, "not overridden: the character's own");
            Assert.AreEqual(Color.red, m_Year1.FindEmotion(m_Draco, "Angry").tint, "the other database is unchanged");
            Assert.AreEqual("Adult Draco", m_Adult.DisplayNameOf(m_Draco));
            Assert.AreEqual("Draco Malfoy", m_Year1.DisplayNameOf(m_Draco));

            // The runner shows the look of the database it plays.
            var c = m_Adult.AddConversation("Talk");
            var e = c.AddEntry(m_Draco.id);
            e.text.Text = "Potter.";
            e.emotion = "Angry";
            c.Link(c.StartEntry, e);
            var runner = new DialogueRunner(m_Adult, m_Adult.CreateContext());
            DialogueLine shown = default;
            runner.LineStarted += l => shown = l;
            runner.Start(c.id);
            Assert.AreSame(adultAngry, shown.Emotion);
            Assert.AreEqual("Adult Draco", shown.SpeakerName);
        }

        [Test]
        public void ALookForAnEmotionTheCharacterLacks_IsReported()
        {
            m_Adult.looks.Add(new ActorLook { actorId = m_Draco.id, emotions = { new ActorEmotion { id = "Bored" } } });
            Assert.That(m_Adult.Validate(), Has.Some.Contains("'Bored' does not exist"));
        }

        [Test]
        public void ACharacter_CanGiveATemplateAttributeItsOwnValue_ButKeepsTheAttribute()
        {
            var student = Track(ScriptableObject.CreateInstance<CharacterDatabase>());
            student.templateAttributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0) });
            m_Draco.template = student;

            m_Draco.SetTemplateValue("affection", VariableValue.Int(-1));
            Assert.AreEqual(-1, m_Draco.FindAttribute("affection").initialValue.intValue, "its own starting value");
            Assert.AreEqual(0, student.templateAttributes[0].initialValue.intValue, "the template is unchanged");
            Assert.IsTrue(new DialogueWorld(m_Year1).TryGetVariable("Draco.affection", out var value));
            Assert.AreEqual(-1, value.intValue);

            student.templateAttributes[0].initialValue = VariableValue.Float(0.5f); // the template changes the type
            Assert.AreEqual(VariableType.Float, m_Draco.FindAttribute("affection").initialValue.type, "a value of the old type is ignored");
            Assert.That(m_Year1.Validate(), Has.Some.Contains("the template now says Float"));

            m_Draco.SetTemplateValue("affection", null);
            student.templateAttributes.Clear();
            m_Draco.SetTemplateValue("gone", VariableValue.Int(1));
            Assert.That(m_Year1.Validate(), Has.Some.Contains("no longer declares"));
        }

        [Test]
        public void TemplateAttributes_AreGivenToTheCharacter_AndFollowTheTemplate()
        {
            var student = Track(ScriptableObject.CreateInstance<CharacterDatabase>());
            student.templateAttributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0) });
            m_Draco.template = student;
            m_Draco.attributes.Add(new VariableDefinition { name = "pride", initialValue = VariableValue.Int(5) });

            CollectionAssert.AreEqual(new[] { "affection", "pride" }, m_Draco.AttributeDefinitions.Select(a => a.name));
            Assert.IsTrue(m_Draco.IsTemplateAttribute("affection"));
            Assert.IsFalse(m_Draco.IsTemplateAttribute("pride"));

            student.templateAttributes.Add(new VariableDefinition { name = "house", initialValue = VariableValue.String("Slytherin") });
            Assert.IsTrue(m_Year1.Schema.TryGetVariableType("Draco.house", out var type), "a new template attribute reaches the character");
            Assert.AreEqual(VariableType.String, type);

            var world = new DialogueWorld(m_Year1);
            Assert.IsTrue(world.TryGetVariable("Draco.affection", out var affection));
            Assert.AreEqual(0, affection.intValue);

            m_Draco.attributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(9) });
            Assert.That(m_Year1.Validate(), Has.Some.Contains("already given by the template"));
            Assert.AreEqual(0, m_Draco.FindAttribute("affection").initialValue.intValue, "the template's wins");
        }

        [Test]
        public void CharactersOfTwoCharacterDatabases_DoNotCollide()
        {
            var other = Track(ScriptableObject.CreateInstance<CharacterDatabase>());
            var harry = other.AddCharacter("Harry");
            m_Year1.characterDatabases.Add(other);
            Assert.AreNotEqual(m_Draco.id, harry.id);
            Assert.AreSame(harry, m_Year1.FindActor(harry.id));

            var clash = other.AddCharacter("Draco");
            Assert.That(m_Year1.Validate(), Has.Some.Contains("duplicate name 'Draco'"));
            Assert.IsNotNull(clash, "a name only has to be unique inside its own Character Database");
        }

        [Test]
        public void AdoptCharacter_KeepsTheIdSoLinesKeepTheirSpeaker()
        {
            var local = m_Year1.AddActor("Ron");
            int id = local.id;
            Assert.IsTrue(m_Cast.AdoptCharacter(local));
            m_Year1.actors.Remove(local);
            Assert.AreSame(local, m_Year1.FindActor(id));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot adopt 'Ron'"));
            Assert.IsFalse(m_Cast.AdoptCharacter(local), "already there");
        }
    }
}
