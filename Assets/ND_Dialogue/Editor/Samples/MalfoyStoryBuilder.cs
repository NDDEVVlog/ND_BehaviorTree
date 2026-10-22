using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ND.Dialogue.Editor.Samples
{
    /// <summary>
    /// "Malfoy × Harry" story sample, after the fan comics: the player is Harry and Malfoy is a tsundere.
    /// <list type="bullet">
    /// <item><b>Characters</b> live in a shared Character Database (Hogwarts_Characters). Harry and Malfoy use the
    /// "Students" Character Database as template, which gives them the locked attribute <c>affection</c>.</item>
    /// <item><b>MalfoyStory_Dialogue</b>: five chapters plus a repeatable hallway meeting driven by her mood (annoyance / affection / pride) — several lines per entry with an emotion each, choices,
    /// Malfoy.affection, First / Fallback links for the two endings, events, and an Entry Seen greeting when chapter 1
    /// is played again.</item>
    /// <item><b>MalfoyStory_Epilogue</b>: years later. The same characters, but this database gives Malfoy an older
    /// look (Look override: portraits and name) and has a character of its own (the talking mirror).</item>
    /// </list>
    /// Portraits: MalfoyStory/Portraits/&lt;Actor&gt;_&lt;Emotion&gt;.png, older looks in Portraits/Older.
    /// </summary>
    public static class MalfoyStoryBuilder
    {
        public const string Folder = "Assets/ND_Dialogue/Samples/MalfoyStory";
        public const string DatabasePath = Folder + "/MalfoyStory_Dialogue.asset";
        public const string EpiloguePath = Folder + "/MalfoyStory_Epilogue.asset";
        public const string CharactersPath = Folder + "/Characters/Hogwarts_Characters.asset";
        public const string TemplatePath = Folder + "/Characters/Students_Template.asset";
        public const string ScenePath = Folder + "/MalfoyStory.unity";
        public const string EpilogueScenePath = Folder + "/MalfoyEpilogue.unity";
        private const string PortraitFolder = Folder + "/Portraits";
        private const string Affection = "Malfoy.affection";

        private static readonly string[] MalfoyEmotions = { "Neutral", "Angry", "Embarrassed", "Happy", "Smug", "Sad", "Surprised", "Pout", "Scared" };
        private static readonly string[] HarryEmotions = { "Neutral", "Happy", "Surprised", "Angry" };
        private static readonly string[] OlderMalfoy = { "Angry", "Smug", "Pout" };

        /// <summary>Builds the characters, the story and the epilogue (replacing earlier builds). Returns the story database.</summary>
        [MenuItem("ND Dialogue/Samples/Malfoy Story/Build Databases")]
        public static DialogueDatabase BuildDatabase()
        {
            ImportPortraits();
            foreach (var path in new[] { DatabasePath, EpiloguePath, CharactersPath, TemplatePath })
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            System.IO.Directory.CreateDirectory(Folder + "/Characters");

            var cast = BuildCharacters(out var hogwarts);

            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            db.characterDatabases.Add(hogwarts);
            db.runnerOptions.showPlayerLines = true; // Harry's chosen line is spoken too (with his portrait)
            db.events.Add(new EventDefinition { name = "ending_together", description = "Malfoy admits it. Roll the credits." });
            db.events.Add(new EventDefinition { name = "ending_rivals", description = "Still 'rivals'. For now." });
            FirstMeet(db, cast);
            FlyingLesson(db, cast);
            ForbiddenForest(db, cast);
            DarkLordsMission(db, cast);
            TheInterview(db, cast);
            Hallway(db, cast);
            Save(db, DatabasePath);

            var epilogue = ScriptableObject.CreateInstance<DialogueDatabase>();
            epilogue.characterDatabases.Add(hogwarts);
            epilogue.runnerOptions.showPlayerLines = true;
            var older = new ActorLook { actorId = cast["Malfoy"].id, displayName = { Text = "Malfoy (7th year)" } };
            foreach (var id in OlderMalfoy)
                older.emotions.Add(new ActorEmotion { id = id, portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/Older/Malfoy_{id}.png") });
            epilogue.looks.Add(older);
            var mirror = epilogue.AddActor("Mirror");
            mirror.displayName.Text = "The Mirror";
            mirror.emotions.Add(new ActorEmotion { id = mirror.defaultEmotion });
            cast["Mirror"] = mirror;
            TheMirror(epilogue, cast);
            Morning(epilogue, cast);
            Save(epilogue, EpiloguePath);
            return db;
        }

        /// <summary>The template (affection) and the shared cast, saved as assets.</summary>
        private static Dictionary<string, Actor> BuildCharacters(out CharacterDatabase hogwarts)
        {
            var students = ScriptableObject.CreateInstance<CharacterDatabase>();
            students.templateAttributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0),
                description = "How much this student likes Harry. Malfoy's: 4+ at the interview gives the 'together' ending." });
            students.templateAttributes.Add(new VariableDefinition { name = "annoyance", initialValue = VariableValue.Int(0),
                description = "How fed up this student is with Harry (hate). Malfoy's: 6+ at the interview gives the 'hostile' ending; it changes how she greets him in the hallway." });
            AssetDatabase.CreateAsset(students, TemplatePath);

            hogwarts = ScriptableObject.CreateInstance<CharacterDatabase>();
            var harry = hogwarts.AddCharacter("Harry", isPlayer: true);
            harry.displayName.Text = "Harry";
            harry.template = students;
            AddEmotions(harry, HarryEmotions);
            harry.emotions.Add(new ActorEmotion { id = "Embarrassed", portrait = Portrait("Harry", "Happy") });

            var malfoy = hogwarts.AddCharacter("Malfoy");
            malfoy.displayName.Text = "Malfoy";
            malfoy.template = students;
            AddEmotions(malfoy, MalfoyEmotions);
            malfoy.SetTemplateValue("affection", VariableValue.Int(-1)); // her own start: rivals (the template says 0)
            malfoy.attributes.Add(new VariableDefinition { name = "pride", initialValue = VariableValue.Int(10), description = "Her own attribute, next to the template's." });

            var cast = new Dictionary<string, Actor> { ["Harry"] = harry, ["Malfoy"] = malfoy };
            foreach (var name in new[] { "McGonagall", "Voldemort", "Wormtail", "Rita", "Dumbledore" })
            {
                var a = hogwarts.AddCharacter(name);
                a.displayName.Text = name == "Rita" ? "Rita Skeeter" : name == "McGonagall" ? "Professor McGonagall" : name;
                a.emotions.Add(new ActorEmotion { id = a.defaultEmotion });
                cast[name] = a;
            }
            AssetDatabase.CreateAsset(hogwarts, CharactersPath);
            return cast;
        }

        private static void Save(DialogueDatabase db, string path)
        {
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            var issues = Plugins.DialoguePluginRegistry.Validate(db);
            if (issues.Count > 0) Debug.LogWarning($"[Malfoy Story] '{db.name}' does not validate:\n  - {string.Join("\n  - ", issues)}", db);
            else Debug.Log($"[Malfoy Story] Built {path}: {db.conversations.Count} conversation(s).", db);
        }

        /// <summary>Two scenes to play them: the standard UI plus chapter buttons, Save / Load and the live state (Dialogue Debug View).</summary>
        [MenuItem("ND Dialogue/Samples/Malfoy Story/Build Scenes")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath) == null || AssetDatabase.LoadAssetAtPath<DialogueDatabase>(EpiloguePath) == null) BuildDatabase();
            BuildSceneFor(EpiloguePath, EpilogueScenePath, "Epilogue 1. The Mirror");
            BuildSceneFor(DatabasePath, ScenePath, "1. First Meet");
            Debug.Log($"[Malfoy Story] Scenes saved: {ScenePath} and {EpilogueScenePath}. Press Play; chapter buttons and Save / Load are at the top.");
        }

        private static void BuildSceneFor(string databasePath, string scenePath, string start)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(databasePath); // after NewScene (it unloads unused assets)
            var manager = DialogueUIBuilder.CreateSetup(db, start);
            var view = manager.gameObject.AddComponent<DialogueDebugView>();
            var so = new SerializedObject(view);
            so.FindProperty("m_Manager").objectReferenceValue = manager;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // ------------------------------------------------------------------ chapters

        private static void FirstMeet(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "1. First Meet", "Madam Malkin's, then the Great Hall. Will Harry take her hand this time?", cast);
            var hi = b.Say(1, 0, "Malfoy", ("Happy", "Hi! Are you going to Hogwarts too?"));
            // Played again: she remembers (Entry Seen), and skips straight to the Great Hall.
            b.Link(b.Start, hi).conditions.Add(new EntrySeenCondition { entryId = hi.id, seen = false });
            var yes = b.Choice(2, 0, "Oh? Yeah... I think so.", "Surprised");
            b.Link(hi, yes);
            var brooms = b.Say(3, 0, "Malfoy",
                ("Happy", "Oh, good! Have you ever flown on a broom? I do it all the time!"),
                ("Smug", "First years aren't allowed their own brooms, apparently. But I'm going to sneak one in anyway!"),
                ("Happy", "Oh, do you like Quidditch? Which house do you think you'll be in?"));
            b.Link(yes, brooms);
            var unsure = b.Choice(4, 0, "Er... I'm not really sure yet.");
            var gryffindor = b.Choice(4, 1, "Gryffindor, maybe?", "Happy");
            b.Link(brooms, unsure);
            b.Link(brooms, gryffindor);
            var brave = b.Say(5, 1, "Malfoy", ("Pout", "Gryffindor? Ugh. All courage and no brains."), ("Smug", "You'll grow out of it."));
            b.Link(gryffindor, brave);
            var slytherin = b.Say(6, 0, "Malfoy",
                ("Happy", "I'm definitely going to Slytherin! My whole family's been in it!"),
                ("Happy", "If I end up somewhere like Hufflepuff, I'll bite my tongue off!!"),
                ("Happy", "You should be in Slytherin too! Don't go mixing with the wrong sort! Especially..."),
                ("Angry", "...What's that? He looks like a savage."));
            b.Link(unsure, slytherin);
            b.Link(brave, slytherin);
            var hagrid = b.Choice(7, 0, "That's Hagrid. He's my friend.", "Angry");
            hagrid.actions.Add(AddTo(Annoyance, 1)); // defends the "savage"
            var silent = b.Choice(7, 1, "(Say nothing.)");
            b.Link(slytherin, hagrid);
            b.Link(slytherin, silent);
            var suit = b.Say(8, 0, "Malfoy", ("Surprised", "Your FRIEND?"), ("Pout", "...Hmph. Suit yourself."));
            b.Link(hagrid, suit);
            b.Link(silent, suit);

            var hall = b.Route(9, 0, "Great Hall, after the Sorting");
            b.Link(suit, hall);
            var again = b.Say(1, 2, "Malfoy",
                ("Pout", "...You again, Potter? Don't think I'll be nice twice."),
                ("Smug", "Well? Go on, then. The Great Hall is that way."));
            b.Link(b.Start, again).conditions.Add(new EntrySeenCondition { entryId = hi.id, seen = true });
            b.Link(again, hall);
            var potter = b.Say(10, 0, "Malfoy",
                ("Surprised", "You're Harry Potter?!"),
                ("Smug", "Why are you hanging around with those idiots? I'll help you find better friends!"));
            b.Link(hall, potter);
            var refuse = b.Choice(11, 0, "I can choose my own friends.", "Angry", menu: "Slap her hand away");
            refuse.actions.Add(AddTo(Annoyance, 3)); // humiliated in front of everyone
            var shake = b.Choice(11, 1, "...Fine. But Ron and Hermione aren't idiots.", "Neutral", menu: "Shake her hand");
            shake.actions.Add(AddAffection(2));
            b.Link(potter, refuse);
            b.Link(potter, shake);
            var regret = b.Say(12, 0, "Malfoy", ("Sad", "Huh...?"), ("Sad", "You..."), ("Embarrassed", "Y-you'll regret this!!!!"));
            b.Link(refuse, regret);
            var warm = b.Say(12, 1, "Malfoy",
                ("Surprised", "!!"),
                ("Embarrassed", "I-it's not like I wanted to shake your hand or anything!"),
                ("Pout", "...Your hand is warm. That's disgusting. Hmph."));
            b.Link(shake, warm);
        }

        private static void FlyingLesson(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "2. Flying Lesson", "Harry is a natural on a broom. Malfoy is... not taking it well.", cast);
            var lesson = b.Say(1, 0, "Malfoy", ("Pout", "(How is he so good?!)"), ("Angry", "UGH, HE'S SO ANNOYING!!!"));
            b.Link(b.Start, lesson);
            var night = b.Route(2, 0, "That night...");
            b.Link(lesson, night);
            var sneak = b.Say(3, 0, "Malfoy",
                ("Scared", "(No one's around... right?)"),
                ("Smug", "(If even Potter can manage it, so can I!)"),
                ("Smug", "(He landed like this... didn't he?)"));
            b.Link(night, sneak);
            var help = b.Choice(4, 0, "Need a hand with that landing?", "Happy");
            help.actions.Add(AddAffection(1));
            var watch = b.Choice(4, 1, "(Watch from the shadows.)", menu: "Watch from the shadows");
            b.Link(sneak, help);
            b.Link(sneak, watch);
            var caught = b.Say(5, 0, "Malfoy",
                ("Scared", "EEEEEEK!!!! P-POTTER?!"),
                ("Embarrassed", "I wasn't practising! I was... inspecting the broom!"),
                ("Pout", "...Fine. Show me. Just once. And if you laugh, I'll hex you."));
            b.Link(help, caught);
            var mcg = b.Say(6, 1, "McGonagall", ("", "MISS MALFOY?"));
            b.Link(watch, mcg);
            b.Link(caught, mcg);
            var eek = b.Say(7, 1, "Malfoy", ("Scared", "EEEEEEEEEEEEEEK!!!!"));
            b.Link(mcg, eek);
            var points = b.Say(8, 1, "McGonagall",
                ("", "Stealing Mr Filch's broom and sneaking out after curfew? Minus ten points to Slytherin."));
            b.Link(eek, points);
            var no = b.Say(9, 1, "Malfoy", ("Sad", "NOOOOOOOOO!!!"));
            b.Link(points, no);
            var story = b.Say(10, 1, "McGonagall",
                ("", "Having trouble sleeping? Then perhaps I should tell you a story."),
                ("", "It was somewhere in the jungles of East Asia. A quiet night, much like this one."),
                ("", "It was a forest when I arrived. By the time I left, it was a runway."));
            b.Link(no, story);
        }

        private static void ForbiddenForest(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "3. Forbidden Forest", "Detention in the Forbidden Forest. Someone is very brave. Out loud.", cast);
            var brave = b.Say(1, 0, "Malfoy",
                ("Scared", "H... Hmph. Don't tell me a little thing like this has you scared, Potter?"),
                ("Scared", "(I'm scared!! I'm so scared—!!!)"));
            b.Link(b.Start, brave);
            var point = b.Choice(2, 0, "Oh, over there...", "Surprised");
            point.actions.Add(AddTo(Annoyance, 1)); // scaring her on purpose?
            var sleeve = b.Choice(2, 1, "You can hold my sleeve if you want.", "Happy");
            sleeve.actions.Add(AddAffection(2));
            b.Link(brave, point);
            b.Link(brave, sleeve);
            var monster = b.Say(3, 0, "Malfoy",
                ("Scared", "AAAAH!!! A M-MONSTER!!!!!!"),
                ("Scared", "H-HURRY UP! WHAT ARE YOU WAITING FOR, YOU FILTHY IDIOT!!!"));
            monster.actions.Add(AddAffection(1)); // she came back for him
            b.Link(point, monster);
            var hold = b.Say(3, 1, "Malfoy",
                ("Embarrassed", "Wh-who'd want to hold YOUR sleeve?!"),
                ("Pout", "...Just so you don't get lost. That's all."));
            b.Link(sleeve, hold);
        }

        private static void DarkLordsMission(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "4. The Dark Lord's Mission", "Orders are orders. Surveillance is surveillance. Treacle tart is... surveillance.", cast);
            var orders = b.Say(1, 0, "Voldemort",
                ("", "You understand, I trust... only you can do this."),
                ("", "Do not leave Potter's side. Not for a moment. Report his every move to me."));
            b.Link(b.Start, orders);
            var gasp = b.Say(2, 0, "Malfoy", ("Surprised", "!!"));
            b.Link(orders, gasp);
            var library = b.Say(3, 0, "Malfoy",
                ("Neutral", "Potter, explain this bit to me."),
                ("Embarrassed", "(This is a mission... It's not like I want to do this...)"),
                ("Embarrassed", "(I'm just following orders...!!)"));
            b.Link(gasp, library);
            var sit = b.Choice(4, 0, "Sure. Sit here.", "Happy");
            sit.actions.Add(AddAffection(1));
            var why = b.Choice(4, 1, "What's gotten into you today?", "Surprised");
            why.actions.Add(AddTo(Annoyance, 1));
            b.Link(library, sit);
            b.Link(library, why);
            var tart = b.Say(5, 0, "Malfoy", ("Happy", "...Thanks."), ("Pout", "Here. Take my treacle tart. Don't read into it."));
            b.Link(sit, tart);
            var notFollowing = b.Say(5, 1, "Malfoy", ("Embarrassed", "I'm NOT following you! I just... happen to be going the same way!"));
            b.Link(why, notFollowing);
            var together = b.Choice(6, 0, "Aren't we going together?", "Happy", menu: "Wait for her after class");
            together.actions.Add(AddAffection(1));
            b.Link(tart, together);
            b.Link(notFollowing, together);
            var coming = b.Say(7, 0, "Malfoy", ("Embarrassed", "W-wait, Potter! I'm coming too!"));
            b.Link(together, coming);
            var report = b.Say(8, 0, "Wormtail", ("", "M-my Lord... Are these reports really of any use...? 'Potter appears to be fond of treacle tart'..."));
            b.Link(coming, report);
            var crucio = b.Say(9, 0, "Voldemort", ("", "CRUCIO."));
            b.Link(report, crucio);
        }

        private static void TheInterview(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "5. The Interview", "Rita Skeeter wants to know who Malfoy despises most. The ending depends on Malfoy.affection.", cast);
            var q = b.Say(1, 0, "Rita", ("", "Miss Malfoy, what sort of person do you despise most?"));
            b.Link(b.Start, q);
            var idiot = b.Say(2, 0, "Malfoy", ("Angry", "An idiot who thinks he's something special."));
            b.Link(q, idiot);
            var looks = b.Say(3, 0, "Rita", ("", "And what does he look like?"));
            b.Link(idiot, looks);
            var describe = b.Say(4, 0, "Malfoy", ("Angry", "Messy black hair, ridiculous round glasses... Green eyes!"));
            b.Link(looks, describe);
            var attention = b.Say(5, 0, "Rita", ("", "My, my... You've certainly paid attention."));
            b.Link(describe, attention);
            var deny = b.Say(6, 0, "Malfoy",
                ("Surprised", "?!"),
                ("Embarrassed", "THAT'S BECAUSE HE'S ALWAYS RIGHT IN FRONT OF ME, GETTING ON MY NERVES!"));
            b.Link(attention, deny);
            var fond = b.Say(7, 0, "Rita", ("", "Are you sure you're not rather fond of him?"));
            b.Link(deny, fond);
            var hate = b.Say(8, 0, "Malfoy", ("Embarrassed", "I TOLD YOU, I HATE HIM!!"));
            b.Link(fond, hate);

            var morning = b.Route(9, 0, "Next morning: the Daily Prophet. 'MISS MALFOY'S IDEAL MAN?'");
            b.Link(hate, morning);
            var paper = b.Say(10, 0, "Malfoy", ("Embarrassed", "WHAT THE HELL IS THIS?!"));
            b.Link(morning, paper);
            var read = b.Choice(11, 0, "'Messy black hair, round glasses, green eyes'... Who could that be~?", "Happy", menu: "Read it out loud");
            var glasses = b.Choice(11, 1, "I hate you even without your glasses, too.", "Happy", menu: "Quote the article back at her");
            b.Link(paper, read);
            b.Link(paper, glasses);
            var line = b.Say(12, 1, "Malfoy", ("Embarrassed", "THAT'S MY LINE, YOU FILTHY IDIOT!!!"));
            b.Link(glasses, line);

            // The ending: the "together" branch is tried first and needs affection 4+; otherwise the fallback plays.
            var togetherEnd = b.Say(13, 0, "Malfoy",
                ("Embarrassed", "STOP READING IT OUT LOUD!!"),
                ("Pout", "...It's not wrong, is it."),
                ("Happy", "Guess we were never going to be just \"friends\", were we?"));
            togetherEnd.actions.Add(new RaiseEventAction { eventName = "ending_together" });
            var rivalsEnd = b.Say(13, 1, "Malfoy", ("Angry", "It's LIES! Every single word!"), ("Pout", "...Mostly."));
            rivalsEnd.actions.Add(new RaiseEventAction { eventName = "ending_rivals" });
            var hostileEnd = b.Say(13, 2, "Malfoy",
                ("Angry", "Ideal man? Him? I'd rather kiss a Blast-Ended Skrewt."),
                ("Angry", "I hate you, Potter. Every word in that paper is a lie, and so are you."));
            hostileEnd.actions.Add(new RaiseEventAction { eventName = "ending_hostile" });
            db.events.Add(new EventDefinition { name = "ending_hostile", description = "She means it. This time." });
            // Three endings by priority: hostile first (annoyance 6+), then together (affection 4+), else rivals.
            foreach (var from in new[] { read, line })
            {
                b.Link(from, hostileEnd, LinkPriority.High).conditions.Add(When(Annoyance, CompareOp.GreaterOrEqual, 6));
                b.Link(from, togetherEnd, LinkPriority.AboveNormal).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 4));
                b.Link(from, rivalsEnd, LinkPriority.Low);
            }
            var office = b.Say(14, 0, "Dumbledore", ("", "HARRRRRY POTTERRR! GET TO MY OFFICE NOW!!! ...WE'RE HAVING FIREWORKS!!!"));
            b.Link(togetherEnd, office);
            var huh = b.Choice(15, 0, "!?", "Surprised");
            b.Link(office, huh);
        }

        /// <summary>
        /// A repeatable meeting whose tone follows Malfoy's mood: annoyance (hate), affection and pride pick the greeting
        /// (priorities + an OR group), each reply changes them, and passthrough routers pick the reaction.
        /// </summary>
        private static void Hallway(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "6. Hallway (any time)", "Play it as often as you like: what Malfoy says depends on Malfoy.annoyance, .affection and .pride — and changes them.", cast);

            // Greetings: the first priority group with a valid branch wins.
            var hostile = b.Say(1, 0, "Malfoy", ("Angry", "Move, Potter. Or I'll move you."), ("Angry", "Don't even look at me."));
            b.Link(b.Start, hostile, LinkPriority.High).conditions.Add(When(Annoyance, CompareOp.GreaterOrEqual, 6));
            var shy = b.Say(1, 1, "Malfoy", ("Embarrassed", "Oh. It's you."), ("Pout", "I wasn't waiting here. I just... like this corridor."));
            var shyLink = b.Link(b.Start, shy, LinkPriority.AboveNormal);
            shyLink.conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 4));
            shyLink.conditions.Add(When(Annoyance, CompareOp.Less, 3));
            var smug = b.Say(1, 2, "Malfoy", ("Smug", "Potter. Admiring my new broom, are you?"));
            var proud = new AnyCondition();
            proud.conditions.Add(When(Pride, CompareOp.GreaterOrEqual, 12));
            proud.conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 2));
            b.Link(b.Start, smug).conditions.Add(proud); // Normal priority, "pride 12+ OR affection 2+"
            var cold = b.Say(1, 3, "Malfoy", ("Neutral", "Potter."));
            b.Link(b.Start, cold, LinkPriority.Low);

            var compliment = b.Choice(3, 0, "Nice broom. Seriously.", "Happy");
            compliment.actions.Add(AddTo(Pride, 1));
            compliment.actions.Add(AddTo(Affection, 1));
            var tease = b.Choice(3, 1, "Still scared of the Forbidden Forest?", "Happy");
            tease.actions.Add(AddTo(Annoyance, 2));
            var invite = b.Choice(3, 2, "Want to fly together later?", "Happy");
            var ignore = b.Choice(3, 3, "(Walk past without a word.)", menu: "Walk past without a word");
            ignore.actions.Add(AddTo(Annoyance, 1));
            ignore.actions.Add(AddTo(Affection, -1));
            foreach (var greeting in new[] { hostile, shy, smug, cold })
                foreach (var reply in new[] { compliment, tease, invite, ignore })
                    b.Link(greeting, reply);

            // Reactions: each reply goes through a router that reads her mood.
            var afterCompliment = b.Route(4, 0, "React to the compliment");
            b.Link(compliment, afterCompliment);
            var flattered = b.Say(5, 0, "Malfoy", ("Embarrassed", "O-of course it's nice. It's MINE."), ("Smug", "...You have good taste. For once."));
            var suspicious = b.Say(5, 1, "Malfoy", ("Angry", "Don't flatter me. What do you want?"));
            b.Link(afterCompliment, suspicious, LinkPriority.High).conditions.Add(When(Annoyance, CompareOp.GreaterOrEqual, 6));
            b.Link(afterCompliment, flattered);

            var afterTease = b.Route(4, 1, "React to the tease");
            b.Link(tease, afterTease);
            var hurt = b.Say(5, 2, "Malfoy", ("Sad", "...You promised you wouldn't tell anyone."), ("Pout", "Idiot."));
            var furious = b.Say(5, 3, "Malfoy", ("Angry", "Say that louder and you'll regret it, Potter."));
            b.Link(afterTease, hurt, LinkPriority.High).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 4));
            b.Link(afterTease, furious);

            var afterInvite = b.Route(4, 2, "React to the invitation");
            b.Link(invite, afterInvite);
            var yes = b.Say(5, 4, "Malfoy", ("Embarrassed", "I-if you insist."), ("Pout", "Don't be late. And don't think this means anything."));
            yes.actions.Add(AddTo(Affection, 1));
            var maybe = b.Say(5, 5, "Malfoy", ("Smug", "With you? Ha!"), ("Neutral", "...Maybe. If I'm bored."));
            var no = b.Say(5, 6, "Malfoy", ("Angry", "I'd rather fall off my broom."));
            no.actions.Add(AddTo(Annoyance, 1));
            b.Link(afterInvite, no, LinkPriority.High).conditions.Add(When(Annoyance, CompareOp.GreaterOrEqual, 6));
            b.Link(afterInvite, yes, LinkPriority.AboveNormal).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 3));
            b.Link(afterInvite, maybe, LinkPriority.Low);

            var ignored = b.Say(5, 7, "Malfoy", ("Sad", "...Hmph. Fine. Ignore me, then."));
            b.Link(ignore, ignored);
        }

        // ------------------------------------------------------------------ epilogue (older look, a local character)

        private const string MirrorChapter = "Epilogue 1. The Mirror";
        private static int s_ShoutEntryId;

        private static void TheMirror(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, MirrorChapter, "Years later. A date, an outfit, and a mirror that will not stop talking.", cast);
            var fit = b.Say(1, 0, "Malfoy", ("Neutral", "(...Do I look like I'm trying too hard?)"));
            b.Link(b.Start, fit);
            var tease = b.Say(2, 0, "Mirror", ("", "Oh, darling~ You've really dolled yourself up today!"), ("", "Off on a date, are we~? Shall I guess who with?"));
            b.Link(fit, tease);
            var what = b.Say(3, 0, "Malfoy", ("Embarrassed", "What on earth are you talking about?!"));
            b.Link(tease, what);
            var boy = b.Say(4, 0, "Mirror", ("", "That boy you complain about every night..."));
            b.Link(what, boy);
            var shout = b.Say(5, 0, "Malfoy", ("Angry", "BE QUIET, YOU STUPID MIRROR!!!"));
            s_ShoutEntryId = shout.id;
            b.Link(boy, shout);
            var knock = b.Choice(6, 0, "(Knock) Ready, Malfoy?", "Happy", menu: "Knock on the door");
            var wait = b.Choice(6, 1, "(Wait outside a little longer.)", menu: "Wait outside");
            b.Link(shout, knock);
            b.Link(shout, wait);
            var look = b.Say(7, 0, "Malfoy", ("Pout", "...Don't look at me like that."), ("Smug", "I know. I look amazing."));
            b.Link(knock, look);
            var cover = b.Say(7, 1, "Mirror", ("", "Oh, why cover me up~? \"Potter\" would love that outfit!"));
            b.Link(wait, cover);
            var shut = b.Say(8, 1, "Malfoy", ("Embarrassed", "JUST SHUT UP!!!"));
            b.Link(cover, shut);
            var late = b.Say(9, 1, "Mirror", ("", "All right, all right~ Don't stay out too late!"));
            b.Link(shut, late);
        }

        private static void Morning(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "Epilogue 2. Morning", "The morning after. Toothbrushes, a bathroom mirror (a quiet one), and something Malfoy finally admits.", cast);
            // If the talking mirror scene was played, she has a warning first (Entry Seen in ANOTHER conversation).
            var warning = b.Say(1, 1, "Malfoy", ("Pout", "And if that mirror says ONE word about last night..."));
            b.Link(b.Start, warning, LinkPriority.High).conditions.Add(new EntrySeenCondition { conversation = MirrorChapter, entryId = s_ShoutEntryId, seen = true });
            var morning = b.Say(2, 0, "Malfoy",
                ("Neutral", "Morning, Potter."),
                ("Smug", "Guess you were right."),
                ("Embarrassed", "We were never going to be just \"friends\", were we?"));
            b.Link(b.Start, morning, LinkPriority.Low);
            b.Link(warning, morning);
            var choke = b.Choice(3, 0, "(Choke on the toothpaste.)", "Surprised", menu: "Choke on the toothpaste");
            var grin = b.Choice(3, 1, "I'd never be friends with someone like you, Malfoy.", "Happy", menu: "Quote your first year");
            b.Link(morning, choke);
            b.Link(morning, grin);
            var idiot = b.Say(4, 0, "Malfoy", ("Smug", "Hmph. Idiot."));
            b.Link(choke, idiot);
            var hit = b.Say(4, 1, "Malfoy", ("Angry", "Say that again and I'll hex your toothbrush."), ("Pout", "...Idiot."));
            b.Link(grin, hit);
        }

        // ------------------------------------------------------------------ helpers

        private const string Annoyance = "Malfoy.annoyance";
        private const string Pride = "Malfoy.pride";

        private static SetVariableAction AddTo(string variable, int amount) =>
            new SetVariableAction { variable = variable, mode = SetVariableAction.Mode.Add, value = amount.ToString() };

        private static VariableCondition When(string variable, CompareOp op, int value) =>
            new VariableCondition { variable = variable, op = op, value = value.ToString() };

        private static SetVariableAction AddAffection(int amount) =>
            new SetVariableAction { variable = Affection, mode = SetVariableAction.Mode.Add, value = amount.ToString() };

        private static void AddEmotions(Actor actor, IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                var sprite = Portrait(actor.name, id);
                if (sprite == null) Debug.LogWarning($"[Malfoy Story] No portrait {actor.name}_{id}.png in {PortraitFolder}.");
                actor.emotions.Add(new ActorEmotion { id = id, portrait = sprite });
            }
        }

        private static Sprite Portrait(string actor, string emotion) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/{actor}_{emotion}.png");

        /// <summary>Portraits are plain PNGs: make them single sprites (once).</summary>
        private static void ImportPortraits()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Writes one chapter on a grid (column → x, row → y).</summary>
        private sealed class Writer
        {
            private readonly Conversation m_Conversation;
            private readonly Dictionary<string, Actor> m_Cast;
            public DialogueEntry Start => m_Conversation.StartEntry;

            public Writer(DialogueDatabase db, string title, string description, Dictionary<string, Actor> cast)
            {
                m_Conversation = db.AddConversation(title);
                m_Conversation.description = description;
                m_Conversation.StartEntry.position = new Vector2(0f, 0f);
                m_Cast = cast;
            }

            private static Vector2 At(int column, int row) => new Vector2(column * 300f, row * 190f);

            /// <summary>An entry of <paramref name="actor"/> with one or more lines, each with its emotion ("" = default).</summary>
            public DialogueEntry Say(int column, int row, string actor, params (string emotion, string text)[] lines)
            {
                var entry = m_Conversation.AddEntry(m_Cast[actor].id);
                entry.position = At(column, row);
                entry.emotion = lines[0].emotion;
                entry.text.Text = lines[0].text;
                foreach (var (emotion, text) in lines.Skip(1))
                    entry.moreLines.Add(new DialoguePage { emotion = emotion == lines[0].emotion ? string.Empty : emotion, text = { Text = text } });
                return entry;
            }

            /// <summary>A choice of Harry (the spoken line; <paramref name="menu"/> = a shorter label in the menu).</summary>
            public DialogueEntry Choice(int column, int row, string spoken, string emotion = "Neutral", string menu = null)
            {
                var entry = Say(column, row, "Harry", (emotion, spoken));
                if (menu != null) entry.menuText.Text = menu;
                return entry;
            }

            /// <summary>A passthrough used as a scene break ("That night..."): it only routes.</summary>
            public DialogueEntry Route(int column, int row, string title)
            {
                var entry = m_Conversation.AddEntry(m_Cast["Harry"].id, title);
                entry.position = At(column, row);
                entry.isPassthrough = true;
                return entry;
            }

            public DialogueLink Link(DialogueEntry from, DialogueEntry to, LinkPriority priority = LinkPriority.Normal) =>
                m_Conversation.Link(from, to, priority);
        }
    }
}
