using ND.Quests;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Dialogue.Editor.Samples
{
    /// <summary>
    /// Chapter 5 of the saga, "A Partner for the Yule Ball", and its side quest, after several artists' fan comics
    /// (invitation and gown: @lattekoi5252; paper crane: @Hagane666; ferret and chocolate frog: @Nef_Arius; hair charm:
    /// @nitekidoe; "Why are u so beautiful": @Foca_Honesta; portraits also from @Rufuswolfenson, @rugi4107, @Jourd4n_,
    /// @dragonslayerGGs, @vanillanighttt). Also Malfoy's barks.
    /// </summary>
    public static partial class MalfoyStoryBuilder
    {
        internal const string YuleQuest = "yule_ball";
        internal const string FrogQuest = "chocolate_frog";
        private const string Asked = "yule.asked";
        private const string Crane = "yule.crane_answered";
        internal const string Partner = "yule.partner";
        private const string Ferret = "yule.ferret";
        private const string Forgiven = "yule.forgiven";
        private const string Danced = "yule.danced";
        private const string FrogFound = "frog.found";
        public const string BarkConversation = "Malfoy Barks";
        public const string PartingBarkConversation = "Malfoy Parting Barks";
        public const string InvitationChapter = "5. Yule Ball: The Invitation";
        public const string CraneChapter = "5. Yule Ball: The Paper Crane";
        public const string FerretChapter = "5. Yule Ball: The Ferret";
        public const string BallChapter = "5. Yule Ball: The Ball";
        public const string FrogChapter = "Side: Chocolate Frog";

        private static readonly string[] YuleVariables = { Asked, Crane, Partner, Ferret, Forgiven, Danced, FrogFound };

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

        private static void BuildYuleQuests(QuestDatabase db)
        {
            var yule = db.AddQuest(YuleQuest);
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
            var b = new Writer(db, InvitationChapter, "Harry asks; Malfoy refuses (after @lattekoi5252). Starts the quest.", cast);
            var ask = b.Say(1, 0, "Harry", ("Neutral", "Malfoy. Go to the Yule Ball with me."));
            ask.actions.Add(QuestTo(YuleQuest, QuestState.Active));
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
            var b = new Writer(db, CraneChapter, "In class a paper crane lands on Harry's desk (after @Hagane666; her answer after @lattekoi5252).", cast);
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
            var b = new Writer(db, FerretChapter, "Moody stops the duel the hard way (after @Nef_Arius). Only after the Myrtle answer.", cast);
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
            var b = new Writer(db, BallChapter, "The Great Hall (hair charm after @nitekidoe, the dance after @lattekoi5252, \"beautiful\" after @Foca_Honesta). The camera cuts to her and her gown look appears (sequence).", cast);
            var arrive = b.Say(1, 0, "Malfoy", ("Ball_Embarrassed", "...Don't stare."), ("Smug", "It's only a temporary hair charm, Potter. Try to enjoy the view."));
            arrive.sequence = "SetActive(BallMalfoy, false); SetActive(BallGown, true); Camera(BallCloseUp)@0.3";
            var link = b.Link(b.Start, arrive);
            link.conditions.Add(Is(Partner));
            link.conditions.Add(QuestIs(YuleQuest, QuestState.Active));
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

            var shutUp = b.Say(3, 0, "Malfoy", ("Ball_Embarrassed", "Sh-shut up!! Stupid Potter..."), ("Ball_Happy", "(He called me beautiful!!)"));
            b.Link(beautiful, shutUp);
            var cold = b.Say(3, 1, "Malfoy", ("Angry", "Say one more word and you'll dance alone."));
            b.Link(mock, cold);
            b.Link(knew, cold);

            var dance = b.Say(4, 0, "Malfoy", ("Ball_Embarrassed", "Don't get used to this."), ("Pout", "...Just one dance."));
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

            var stay = b.Say(6, 0, "Malfoy", ("Ball_Happy", "...Fine. Two. And you're not allowed to step on my feet."));
            stay.actions.Add(QuestTo(YuleQuest, QuestState.Completed));
            stay.actions.Add(ChapterTo(6));
            b.Link(oneMore, stay, LinkPriority.High).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 2));
            var bristle = b.Say(6, 1, "Malfoy", ("Pout", "Don't push your luck, Potter."));
            bristle.actions.Add(QuestTo(YuleQuest, QuestState.Completed));
            bristle.actions.Add(ChapterTo(6));
            b.Link(oneMore, bristle);
            var proud = b.Say(6, 2, "Malfoy", ("Smug", "Naturally. You're welcome."));
            proud.actions.Add(QuestTo(YuleQuest, QuestState.Completed));
            proud.actions.Add(ChapterTo(6));
            b.Link(thanks, proud);
        }

        private static void ChocolateFrog(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, FrogChapter, "Before the match (after @Nef_Arius). A side quest.", cast);
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
            db.FindConversation(BarkConversation).isBark = true;
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

        /// <summary>A second kind of bark for the same Malfoy: what she says as a talk ends (its own Dialogue Barker, When = After Conversation).</summary>
        private static void PartingBarks(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, PartingBarkConversation, "Barks after a talk ends (a second Dialogue Barker on the same Malfoy).", cast);
            db.FindConversation(PartingBarkConversation).isBark = true;
            var hostile = b.Say(1, 0, "Malfoy", ("Angry", "And stay away."));
            b.Link(b.Start, hostile, LinkPriority.High).conditions.Add(When(Annoyance, CompareOp.GreaterOrEqual, 6));
            var soft = b.Say(1, 1, "Malfoy", ("Embarrassed", "...See you later, Potter."));
            b.Link(b.Start, soft, LinkPriority.AboveNormal).conditions.Add(When(Affection, CompareOp.GreaterOrEqual, 4));
            b.Link(b.Start, b.Say(1, 2, "Malfoy", ("Pout", "Don't follow me.")));
            b.Link(b.Start, b.Say(1, 3, "Malfoy", ("Smug", "We're done here.")));
        }

    }
}
