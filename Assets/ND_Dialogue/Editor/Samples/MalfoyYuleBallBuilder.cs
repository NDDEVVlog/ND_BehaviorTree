using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ND.Dialogue.Editor.Samples.MalfoyStoryBuilder;

namespace ND.Dialogue.Editor.Samples
{
    /// <summary>
    /// "A Partner for the Yule Ball": a quest episode after several artists' fan comics (invitation and gown: @lattekoi5252;
    /// paper crane: @Hagane666; ferret: @Nef_Arius; hair charm: @nitekidoe; chocolate frog: @Nef_Arius; "Why are u so
    /// beautiful": @Foca_Honesta; portraits also from @Rufuswolfenson, @rugi4107, @Jourd4n_, @dragonslayerGGs,
    /// @vanillanighttt). Shows Stages 4–6 together:
    /// <list type="bullet">
    /// <item><b>Quests</b>: the main quest (5 steps that appear and tick by variables) and a side quest, in the tracker
    /// (top right) and the quest log (J).</item>
    /// <item><b>Barks</b>: Malfoy mutters in her bubble every few seconds; what she says follows the quest.</item>
    /// <item><b>Sequences</b>: at the ball the camera cuts to her and her gown look appears.</item>
    /// <item><b>Looks</b>: this database gives Malfoy her Yule Ball portraits; <b>hubs</b> merge shared replies.</item>
    /// </list>
    /// Same characters as the story (Hogwarts_Characters); Ron, Hermione and Moody are local to this database.
    /// </summary>
    public static class MalfoyYuleBallBuilder
    {
        public const string DatabasePath = Folder + "/MalfoyYuleBall_Dialogue.asset";
        public const string ScenePath = Folder + "/MalfoyYuleBall.unity";
        private const string YuleFolder = PortraitFolder + "/Yule";

        private const string Quest = "yule_ball";
        private const string FrogQuest = "chocolate_frog";
        private const string Asked = "yule.asked";
        private const string Crane = "yule.crane_answered";
        private const string Partner = "yule.partner";
        private const string Ferret = "yule.ferret";
        private const string Forgiven = "yule.forgiven";
        private const string Danced = "yule.danced";
        private const string FrogFound = "frog.found";
        public const string BarkConversation = "Malfoy Barks";
        private const string First = "Yule 1. The Invitation";

        [MenuItem("ND Dialogue/Samples/Malfoy Story/Build Yule Ball (quest sample)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildDatabase();
            BuildScene();
        }

        public static DialogueDatabase BuildDatabase()
        {
            if (AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharactersPath) == null) MalfoyStoryBuilder.BuildDatabase();
            ImportPortraits();
            if (AssetDatabase.LoadMainAssetAtPath(DatabasePath) != null) AssetDatabase.DeleteAsset(DatabasePath);
            var hogwarts = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharactersPath);

            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            db.characterDatabases.Add(hogwarts);
            db.runnerOptions.showPlayerLines = true;
            var cast = new Dictionary<string, Actor>();
            foreach (var actor in hogwarts.characters) cast[actor.name] = actor;

            // This database's look of Malfoy: her Yule Ball portraits (the other emotions stay the story's).
            var look = new ActorLook { actorId = cast["Malfoy"].id };
            foreach (var id in new[] { "Embarrassed", "Happy", "Sad", "Surprised" })
                look.emotions.Add(new ActorEmotion { id = id, portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{YuleFolder}/Malfoy_{id}.png") });
            db.looks.Add(look);
            cast["Ron"] = Local(db, "Ron", "Ron Weasley", "Angry");
            cast["Hermione"] = Local(db, "Hermione", "Hermione Granger", "Angry");
            cast["Moody"] = Local(db, "Moody", "Professor Moody", null);

            foreach (var name in new[] { Asked, Crane, Partner, Ferret, Forgiven, Danced, FrogFound })
                db.AddVariable(name, VariableValue.Bool(false));
            db.events.Add(new EventDefinition { name = "yule_dance", description = "The dance starts: music, lights." });
            BuildQuests(db);

            Invitation(db, cast);
            PaperCrane(db, cast);
            TheFerret(db, cast);
            TheBall(db, cast);
            ChocolateFrog(db, cast);
            Barks(db, cast);

            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            var issues = Plugins.DialoguePluginRegistry.Validate(db);
            if (issues.Count > 0) Debug.LogWarning($"[Yule Ball] '{db.name}' does not validate:\n  - {string.Join("\n  - ", issues)}", db);
            else Debug.Log($"[Yule Ball] Built {DatabasePath}: {db.conversations.Count} conversation(s), {db.quests.Count} quest(s).", db);
            return db;
        }

        private static Actor Local(DialogueDatabase db, string name, string displayName, string emotion)
        {
            var actor = db.AddActor(name);
            actor.displayName.Text = displayName;
            if (emotion != null)
            {
                actor.defaultEmotion = emotion;
                actor.emotions.Add(new ActorEmotion { id = emotion, portrait = Portrait(name, emotion) });
            }
            else actor.emotions.Add(new ActorEmotion { id = actor.defaultEmotion });
            return actor;
        }

        private static VariableCondition Is(string variable, bool value = true) => new VariableCondition { variable = variable, value = value ? "true" : "false" };
        private static SetVariableAction Set(string variable, bool value = true) => new SetVariableAction { variable = variable, value = value ? "true" : "false" };
        private static SetQuestStateAction QuestTo(string quest, QuestState state) => new SetQuestStateAction { quest = quest, state = state };
        private static QuestStateCondition QuestIs(string quest, QuestState state) => new QuestStateCondition { quest = quest, state = state };

        private static QuestStep Step(string text, DialogueCondition shownWhen, DialogueCondition doneWhen)
        {
            var step = new QuestStep { text = { Text = text } };
            if (shownWhen != null) step.visibleWhen.Add(shownWhen);
            if (doneWhen != null) step.doneWhen.Add(doneWhen);
            return step;
        }

        private static void BuildQuests(DialogueDatabase db)
        {
            var yule = db.AddQuest(Quest);
            yule.title.Text = "A Partner for the Yule Ball";
            yule.summary.Text = "Everyone is pairing up for the Yule Ball. You asked Malfoy. Of course she said no. ...Didn't she?";
            yule.steps.Add(Step("Ask Malfoy to the Yule Ball", null, Is(Asked)));
            yule.steps.Add(Step("Answer the paper crane", Is(Asked), Is(Crane)));
            yule.steps.Add(Step("Make it up to her (the ferret incident)", Is(Ferret), Is(Forgiven)));
            yule.steps.Add(Step("Meet her at the Great Hall", Is(Partner), Is(Danced)));
            yule.steps.Add(Step("Share the dance", Is(Partner), null)); // ticks when the quest is completed

            var frog = db.AddQuest(FrogQuest);
            frog.title.Text = "A Frog Before the Match";
            frog.summary.Text = "Someone slipped a Chocolate Frog into your bag before the Quidditch match.";
            frog.group = "Side";
            frog.steps.Add(Step("Find out who left it", null, Is(FrogFound)));
            frog.steps.Add(Step("Decide what to do with it", Is(FrogFound), null));
        }

        // ------------------------------------------------------------------ the quest

        private static void Invitation(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, First, "Harry asks; Malfoy refuses (after @lattekoi5252). Starts the quest.", cast);
            var ask = b.Say(1, 0, "Harry", ("Neutral", "Malfoy. Go to the Yule Ball with me."));
            ask.actions.Add(QuestTo(Quest, QuestState.Active));
            ask.actions.Add(Set(Asked));
            b.Link(b.Start, ask).conditions.Add(Is(Asked, false));
            var no = b.Say(2, 0, "Malfoy", ("Surprised", "Have you lost your mind, Potter?!"), ("Angry", "Me? With YOU? In your next lifetime!"));
            b.Link(ask, no);
            var fun = b.Choice(3, 0, "Come on. It'll be fun.", "Happy");
            fun.actions.Add(AddTo(Affection, 1));
            var loss = b.Choice(3, 1, "Fine. Your loss.", "Neutral");
            loss.actions.Add(AddTo(Annoyance, 1));
            loss.actions.Add(AddTo(Pride, 1));
            b.Link(no, fun);
            b.Link(no, loss);
            var hmph = b.Say(4, 0, "Malfoy", ("Pout", "Hmph. Ask someone who cares."));
            b.Link(fun, hmph);
            b.Link(loss, hmph);

            var again = b.Say(1, 2, "Malfoy", ("Smug", "Still moping about the ball, Potter?"));
            b.Link(b.Start, again).conditions.Add(Is(Asked));
        }

        private static void PaperCrane(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "Yule 2. The Paper Crane", "In class a paper crane lands on Harry's desk (after @Hagane666; her answer after @lattekoi5252).", cast);
            var crane = b.Say(1, 0, "Malfoy", ("Neutral", "(A paper crane lands on your desk: \"How about me as a dance partner?\")"), ("Pout", "...Well? What do you think?"));
            crane.title = "the crane";
            var link = b.Link(b.Start, crane);
            link.conditions.Add(Is(Asked));
            link.conditions.Add(Is(Crane, false));
            var notNow = b.Say(1, 2, "Malfoy", ("Smug", "Eyes on your own parchment, Potter."));
            b.Link(b.Start, notNow, LinkPriority.Low);

            var yes = b.Choice(2, 0, "I'd love to.", "Happy");
            yes.actions.Add(AddTo(Affection, 2));
            var joke = b.Choice(2, 1, "Is this a joke?", "Surprised");
            joke.actions.Add(AddTo(Annoyance, 1));
            joke.actions.Add(AddTo(Pride, 1));
            var myrtle = b.Choice(2, 2, "I'd rather ask Moaning Myrtle!", "Happy");
            myrtle.actions.Add(AddTo(Annoyance, 3));
            myrtle.actions.Add(Set(Ferret));
            foreach (var choice in new[] { yes, joke, myrtle }) { choice.actions.Add(Set(Crane)); b.Link(crane, choice); }

            var flustered = b.Say(3, 0, "Malfoy", ("Embarrassed", "W-well, obviously. Who else would put up with your dancing?"));
            flustered.actions.Add(Set(Partner));
            b.Link(yes, flustered);
            var pity = b.Say(3, 1, "Malfoy", ("Angry", "A JOKE?! I've scared off every candidate for— never mind!"), ("Pout", "Fine! I'll go with you! But only out of pity for your pathetic look!"));
            pity.actions.Add(Set(Partner));
            b.Link(joke, pity);
            var wand = b.Say(3, 2, "Malfoy", ("Angry", "...Myrtle. MYRTLE?!"), ("Angry", "(She draws her wand.)"));
            b.Link(myrtle, wand);
        }

        private static void TheFerret(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "Yule 3. The Ferret", "Moody stops the duel the hard way (after @Nef_Arius). Only after the Myrtle answer.", cast);
            var moody = b.Say(1, 0, "Moody", ("", "Wands down! ...Too late."), ("", "(A flash. Where Malfoy stood, a small white ferret squeaks with rage.)"));
            var link = b.Link(b.Start, moody);
            link.conditions.Add(Is(Ferret));
            link.conditions.Add(Is(Forgiven, false));
            var mcg = b.Say(2, 0, "McGonagall", ("", "Professor! We NEVER use transfiguration as a punishment!"), ("", "(She turns the ferret back.)"));
            b.Link(moody, mcg);
            var father = b.Say(3, 0, "Malfoy", ("Sad", "MY FATHER WILL HEAR ABOUT THIS!!!"));
            b.Link(mcg, father);
            var sorry = b.Choice(4, 0, "I'm sorry. Myrtle was a stupid joke. Come to the ball with me?", "Neutral");
            sorry.actions.Add(AddTo(Affection, 1));
            sorry.actions.Add(AddTo(Annoyance, -2));
            var laugh = b.Choice(4, 1, "I rather liked you better that way.", "Happy");
            laugh.actions.Add(AddTo(Annoyance, 2));
            b.Link(father, sorry);
            b.Link(father, laugh);
            var forgiven = b.Say(5, 0, "Malfoy", ("Pout", "...You'd better dance well enough to make up for it, Potter."));
            forgiven.actions.Add(Set(Forgiven));
            forgiven.actions.Add(Set(Partner));
            b.Link(sorry, forgiven);
            var furious = b.Say(5, 1, "Malfoy", ("Angry", "Get. Out. Of. My. Sight."));
            b.Link(laugh, furious);
            var done = b.Say(1, 2, "Malfoy", ("Angry", "Not. A. Word. About. Ferrets."));
            b.Link(b.Start, done, LinkPriority.Low);
        }

        private static void TheBall(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "Yule 4. The Ball", "The Great Hall (hair charm after @nitekidoe, the dance after @lattekoi5252, \"beautiful\" after @Foca_Honesta). The camera cuts to her and her gown look appears (sequence).", cast);
            var arrive = b.Say(1, 0, "Malfoy", ("Embarrassed", "...Don't stare."), ("Smug", "It's only a temporary hair charm, Potter. Try to enjoy the view."));
            arrive.sequence = "SetActive(Malfoy, false); SetActive(MalfoyGown, true); Camera(CloseUp)@0.3";
            var link = b.Link(b.Start, arrive);
            link.conditions.Add(Is(Partner));
            link.conditions.Add(QuestIs(Quest, QuestState.Active));
            var alone = b.Say(1, 2, "Malfoy", ("Smug", "No partner, Potter? How tragic."));
            b.Link(b.Start, alone, LinkPriority.Low);

            var beautiful = b.Choice(2, 0, "Why are you so beautiful?", "Happy");
            beautiful.actions.Add(AddTo(Affection, 1));
            var mock = b.Choice(2, 1, "Is that a dress or a curtain?", "Happy");
            mock.actions.Add(AddTo(Annoyance, 2));
            var knew = b.Choice(2, 2, "I knew you'd come.", "Neutral");
            knew.actions.Add(AddTo(Pride, -1));
            knew.actions.Add(AddTo(Annoyance, 1));
            // Every answer leads to the same reaction router: a hub keeps it one link each.
            var replies = b.Hub(1.5f, 1f, "answers");
            b.Link(arrive, replies);
            foreach (var reply in new[] { beautiful, mock, knew }) b.Link(replies, reply);

            var shutUp = b.Say(3, 0, "Malfoy", ("Embarrassed", "Sh-shut up!! Stupid Potter..."), ("Happy", "(He called me beautiful!!)"));
            b.Link(beautiful, shutUp);
            var cold = b.Say(3, 1, "Malfoy", ("Angry", "Say one more word and you'll dance alone."));
            b.Link(mock, cold);
            b.Link(knew, cold);

            var dance = b.Say(4, 0, "Malfoy", ("Embarrassed", "Don't get used to this."), ("Pout", "...Just one dance."));
            dance.actions.Add(Set(Danced));
            dance.actions.Add(new RaiseEventAction { eventName = "yule_dance" });
            dance.sequence = "Camera(DanceFloor)";
            b.Link(shutUp, dance);
            b.Link(cold, dance);
            var oneMore = b.Choice(5, 0, "Just one?", "Happy");
            var thanks = b.Choice(5, 1, "Thank you, Malfoy.", "Neutral");
            thanks.actions.Add(AddTo(Pride, 1));
            b.Link(dance, oneMore);
            b.Link(dance, thanks);

            var stay = b.Say(6, 0, "Malfoy", ("Happy", "...Fine. Two. And you're not allowed to step on my feet."));
            stay.actions.Add(QuestTo(Quest, QuestState.Completed));
            b.Link(oneMore, stay, LinkPriority.High).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 2));
            var bristle = b.Say(6, 1, "Malfoy", ("Pout", "Don't push your luck, Potter."));
            bristle.actions.Add(QuestTo(Quest, QuestState.Completed));
            b.Link(oneMore, bristle);
            var proud = b.Say(6, 2, "Malfoy", ("Smug", "Naturally. You're welcome."));
            proud.actions.Add(QuestTo(Quest, QuestState.Completed));
            b.Link(thanks, proud);
        }

        private static void ChocolateFrog(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, "Side. Chocolate Frog", "Before the match (after @Nef_Arius). A side quest.", cast);
            var taunt = b.Say(1, 0, "Malfoy", ("Smug", "Dementor, dementor! Woooo~"));
            taunt.actions.Add(QuestTo(FrogQuest, QuestState.Active));
            b.Link(b.Start, taunt).conditions.Add(QuestIs(FrogQuest, QuestState.Inactive));
            var bag = b.Say(2, 0, "Ron", ("Angry", "Harry, there's a Chocolate Frog in your bag. From HER. Must be poisoned."));
            bag.actions.Add(Set(FrogFound));
            b.Link(taunt, bag);
            var eat = b.Choice(3, 0, "I'm eating it.", "Happy");
            eat.actions.Add(AddTo(Affection, 1));
            var bin = b.Choice(3, 1, "Bin it, Ron.", "Neutral");
            bin.actions.Add(AddTo(Annoyance, 1));
            b.Link(bag, eat);
            b.Link(bag, bin);
            var wisely = b.Say(4, 0, "Malfoy", ("Embarrassed", "I won't have Dementors making you faint and handing me an unearned victory!"), ("Pout", "...Use it wisely."));
            wisely.actions.Add(QuestTo(FrogQuest, QuestState.Completed));
            b.Link(eat, wisely);
            var dare = b.Say(4, 1, "Malfoy", ("Angry", "Don't you DARE, Weasley!!"));
            dare.actions.Add(QuestTo(FrogQuest, QuestState.Failed));
            b.Link(bin, dare);
            var later = b.Say(1, 2, "Hermione", ("Angry", "Can we PLEASE talk about something other than Malfoy?"));
            b.Link(b.Start, later, LinkPriority.Low);
        }

        /// <summary>What Malfoy mutters in her bubble: the first valid group by priority, then one at random.</summary>
        private static void Barks(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, BarkConversation, "Barks: one line in her bubble every few seconds (Dialogue Barker). Follows the quest.", cast);
            var ferret = b.Say(1, 0, "Malfoy", ("Angry", "Not. A. Word."));
            var ferretLink = b.Link(b.Start, ferret, LinkPriority.High);
            ferretLink.conditions.Add(Is(Ferret));
            ferretLink.conditions.Add(Is(Forgiven, false));
            var waiting = b.Say(1, 1, "Malfoy", ("Pout", "...Don't be late. Not that I care."));
            var waitLink = b.Link(b.Start, waiting, LinkPriority.AboveNormal);
            waitLink.conditions.Add(Is(Partner));
            waitLink.conditions.Add(Is(Danced, false));
            var after = b.Say(1, 2, "Malfoy", ("Happy", "Two dances. Hmph. Not bad, Potter."));
            b.Link(b.Start, after, LinkPriority.AboveNormal).conditions.Add(Is(Danced));
            foreach (var (row, emotion, text) in new[] { (3, "Pout", "Hmph."), (4, "Smug", "Staring again, Potter?"), (5, "Angry", "Ugh. Potter.") })
                b.Link(b.Start, b.Say(1, row, "Malfoy", (emotion, text)));
        }

        // ------------------------------------------------------------------ scene

        /// <summary>
        /// The standard UI with quest tracker and log, Malfoy as a sprite with a bark bubble (every 8 s), a sequencer
        /// (camera cuts, her gown look at the ball) and the Dialogue Debug View (chapter buttons, live state).
        /// </summary>
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath); // after NewScene (it unloads unused assets)
            var manager = DialogueUIBuilder.CreateSetup(db, First);
            var debug = manager.gameObject.AddComponent<DialogueDebugView>();
            var debugSo = new SerializedObject(debug);
            debugSo.FindProperty("m_Manager").objectReferenceValue = manager;
            debugSo.ApplyModifiedPropertiesWithoutUndo();
            var canvas = DialogueUIBuilder.CanvasOf(manager);
            DialogueUIBuilder.CreateQuestTracker(canvas, manager);
            DialogueUIBuilder.CreateQuestLog(canvas, manager);

            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.2f);

            var malfoy = Sprite("Malfoy", Portrait("Malfoy", "Neutral"), new Vector3(3f, 1f, 0f));
            malfoy.AddComponent<DialogueActorObject>().Actor = "Malfoy";
            var barker = DialogueUIBuilder.CreateBark(malfoy, manager, BarkConversation, 1.8f);
            var barkerSo = new SerializedObject(barker);
            barkerSo.FindProperty("m_When").enumValueIndex = (int)BarkTriggerEvent.Interval;
            barkerSo.FindProperty("m_Interval").floatValue = 8f;
            barkerSo.ApplyModifiedPropertiesWithoutUndo();
            var gown = Sprite("Malfoy (Yule Ball)", AssetDatabase.LoadAssetAtPath<Sprite>($"{YuleFolder}/Malfoy_Embarrassed.png"), new Vector3(3f, 1f, 0f));
            gown.SetActive(false);

            var closeUp = new GameObject("CloseUp").transform;
            closeUp.SetPositionAndRotation(new Vector3(3f, 1f, -4.5f), Quaternion.identity);
            var danceFloor = new GameObject("DanceFloor").transform;
            danceFloor.SetPositionAndRotation(new Vector3(1.5f, 1.2f, -7f), Quaternion.Euler(0f, 8f, 0f));

            var sequencer = manager.gameObject.AddComponent<DialogueSequencer>();
            var so = new SerializedObject(sequencer);
            so.FindProperty("m_Manager").objectReferenceValue = manager;
            so.FindProperty("m_Camera").objectReferenceValue = camera.transform;
            var targets = so.FindProperty("m_Targets");
            var named = new (string name, GameObject go)[] { ("Malfoy", malfoy), ("MalfoyGown", gown), ("CloseUp", closeUp.gameObject), ("DanceFloor", danceFloor.gameObject) };
            targets.arraySize = named.Length;
            for (int i = 0; i < named.Length; i++)
            {
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = named[i].name;
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("gameObject").objectReferenceValue = named[i].go;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Yule Ball] Scene saved: {ScenePath}. Press Play: chapter buttons at the top, J opens the quest log, Malfoy barks every 8 s.");
        }

        private static GameObject Sprite(string name, Sprite sprite, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (sprite == null) Debug.LogError($"[Yule Ball] No sprite for '{name}': it would render nothing.");
            return go;
        }
    }
}
