using ND.Quests;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Dialogue.Editor.Samples
{
    /// <summary>
    /// "The Malfoy Saga": one database and one scene, after the fan comics (the player is Harry, Malfoy is a tsundere).
    /// Walk around a small map of Hogwarts (WASD), press E where Malfoy waits: each chapter opens the next one
    /// (<c>saga.chapter</c>), and the main quest "The Malfoy Saga" says where to go.
    /// <list type="bullet">
    /// <item><b>Chapters</b>: 1 First Meet (Diagon Alley) → 2 Flying Lesson (Quidditch Pitch) → 3 Forbidden Forest →
    /// 4 The Dark Lord's Mission (Library) → 5 the Yule Ball quest (Courtyard, Classroom, Great Hall) → 6 The Interview
    /// (Hogsmeade; three endings by her mood) → Epilogue: The Mirror, then Morning (Malfoy Manor) — unless she hates you.</item>
    /// <item><b>Any time</b>: the Courtyard meeting (her mood: annoyance / affection / pride), the Chocolate Frog side quest,
    /// and her barks.</item>
    /// <item><b>Features</b>: shared Character Database with a template (affection, annoyance), several lines per entry
    /// with an emotion each, choices, priorities and fallbacks, Entry Seen, hubs, quests with steps, barks, sequences
    /// (camera cuts, her gown at the ball), save / load (F5 / F9).</item>
    /// </list>
    /// Portraits: MalfoyStory/Portraits/&lt;Actor&gt;_&lt;Emotion&gt;.png; her gown (Ball_*) in Portraits/Yule and her older
    /// look (Older_*) in Portraits/Older.
    /// </summary>
    public static partial class MalfoyStoryBuilder
    {
        public const string Folder = "Assets/ND_Dialogue/Samples/MalfoyStory";
        public const string DatabasePath = Folder + "/MalfoySaga_Dialogue.asset";
        /// <summary>The saga's quests, in a Quest Database of their own: shared with gameplay and UI through the scene's Quest Manager.</summary>
        public const string QuestsPath = Folder + "/MalfoySaga_Quests.asset";
        public const string CharactersPath = Folder + "/Characters/Hogwarts_Characters.asset";
        public const string TemplatePath = Folder + "/Characters/Students_Template.asset";
        public const string ScenePath = Folder + "/MalfoySaga.unity";
        internal const string PortraitFolder = Folder + "/Portraits";
        internal const string Affection = "Malfoy.affection";
        /// <summary>Which chapter is open (1…8; 9 = the saga is over).</summary>
        internal const string Chapter = "saga.chapter";
        internal const string SagaQuest = "malfoy_saga";

        public const string FirstMeetChapter = "1. First Meet";
        public const string FlyingChapter = "2. Flying Lesson";
        public const string ForestChapter = "3. Forbidden Forest";
        public const string MissionChapter = "4. The Dark Lord's Mission";
        public const string InterviewChapter = "6. The Interview";
        public const string MirrorChapter = "7. Epilogue: The Mirror";
        public const string MorningChapter = "8. Epilogue: Morning";
        public const string HallwayChapter = "Courtyard (any time)";

        private static readonly string[] MalfoyEmotions = { "Neutral", "Angry", "Embarrassed", "Happy", "Smug", "Sad", "Surprised", "Pout", "Scared" };
        private static readonly string[] HarryEmotions = { "Neutral", "Happy", "Surprised", "Angry" };
        private static readonly string[] BallEmotions = { "Embarrassed", "Happy", "Sad", "Surprised" };
        private static readonly string[] OlderEmotions = { "Angry", "Smug", "Pout" };

        /// <summary>Earlier versions built three samples; the saga replaces them.</summary>
        private static readonly string[] OldSamples =
        {
            Folder + "/MalfoyStory_Dialogue.asset", Folder + "/MalfoyStory_Epilogue.asset", Folder + "/MalfoyYuleBall_Dialogue.asset",
            Folder + "/MalfoyStory.unity", Folder + "/MalfoyEpilogue.unity", Folder + "/MalfoyYuleBall.unity",
        };

        [MenuItem("ND Dialogue/Samples/Malfoy Saga/Build (database + scene)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildDatabase();
            BuildScene();
        }

        /// <summary>Builds the characters and the saga database (replacing earlier builds). Returns the database.</summary>
        [MenuItem("ND Dialogue/Samples/Malfoy Saga/Build Database Only")]
        public static DialogueDatabase BuildDatabase()
        {
            ImportPortraits();
            foreach (var path in OldSamples.Concat(new[] { DatabasePath, QuestsPath, CharactersPath, TemplatePath }))
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            System.IO.Directory.CreateDirectory(Folder + "/Characters");

            var cast = BuildCharacters(out var hogwarts);
            var db = ScriptableObject.CreateInstance<DialogueDatabase>();
            db.characterDatabases.Add(hogwarts);
            db.runnerOptions.showPlayerLines = true; // Harry's chosen line is spoken too (with his portrait)
            var mirror = db.AddActor("Mirror");
            mirror.displayName.Text = "The Mirror";
            mirror.emotions.Add(new ActorEmotion { id = mirror.defaultEmotion });
            cast["Mirror"] = mirror;
            cast["Ron"] = Local(db, "Ron", "Ron Weasley", "Angry");
            cast["Hermione"] = Local(db, "Hermione", "Hermione Granger", "Angry");
            cast["Moody"] = Local(db, "Moody", "Professor Moody", null);

            db.AddVariable(Chapter, VariableValue.Int(1));
            foreach (var name in YuleVariables) db.AddVariable(name, VariableValue.Bool(false));
            db.events.Add(new EventDefinition { name = "ending_together", description = "Malfoy admits it." });
            db.events.Add(new EventDefinition { name = "ending_rivals", description = "Still 'rivals'. For now." });
            db.events.Add(new EventDefinition { name = "yule_dance", description = "The dance starts: music, lights." });
            var quests = ScriptableObject.CreateInstance<QuestDatabase>();
            BuildSagaQuest(quests);
            BuildYuleQuests(quests);
            AssetDatabase.CreateAsset(quests, QuestsPath);
            db.questDatabases.Add(quests);

            FirstMeet(db, cast);
            FlyingLesson(db, cast);
            ForbiddenForest(db, cast);
            DarkLordsMission(db, cast);
            Invitation(db, cast);
            PaperCrane(db, cast);
            TheFerret(db, cast);
            TheBall(db, cast);
            TheInterview(db, cast);
            TheMirror(db, cast);
            Morning(db, cast);
            Hallway(db, cast);
            ChocolateFrog(db, cast);
            Barks(db, cast);
            PartingBarks(db, cast);
            // Reaching the end of a chapter opens the next one.
            EndChapter(db, FirstMeetChapter, 2);
            EndChapter(db, FlyingChapter, 3);
            EndChapter(db, ForestChapter, 4);
            EndChapter(db, MissionChapter, 5);
            EndChapter(db, InterviewChapter, 7);
            EndChapter(db, MirrorChapter, 8);

            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            var issues = Plugins.DialoguePluginRegistry.Validate(db);
            if (issues.Count > 0) Debug.LogWarning($"[Malfoy Saga] '{db.name}' does not validate:\n  - {string.Join("\n  - ", issues)}", db);
            else Debug.Log($"[Malfoy Saga] Built {DatabasePath}: {db.conversations.Count} conversation(s), {db.quests.Count} quest(s).", db);
            return db;
        }

        /// <summary>The template (affection, annoyance) and the shared cast, saved as assets.</summary>
        private static Dictionary<string, Actor> BuildCharacters(out CharacterDatabase hogwarts)
        {
            var students = ScriptableObject.CreateInstance<CharacterDatabase>();
            students.templateAttributes.Add(new VariableDefinition { name = "affection", initialValue = VariableValue.Int(0),
                description = "How much this student likes Harry. Malfoy's: 4+ at the interview gives the 'together' ending." });
            students.templateAttributes.Add(new VariableDefinition { name = "annoyance", initialValue = VariableValue.Int(0),
                description = "How fed up this student is with Harry (hate). Malfoy's: 6+ at the interview gives the 'hostile' ending; it changes how she greets him." });
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
            foreach (var id in BallEmotions) // her Yule Ball gown
                malfoy.emotions.Add(new ActorEmotion { id = "Ball_" + id, portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/Yule/Malfoy_{id}.png") });
            foreach (var id in OlderEmotions) // years later
                malfoy.emotions.Add(new ActorEmotion { id = "Older_" + id, portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/Older/Malfoy_{id}.png") });
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

        /// <summary>The main quest: one step per chapter, shown once the chapter opens, ticked once it is played.</summary>
        private static void BuildSagaQuest(QuestDatabase db)
        {
            var saga = db.AddQuest(SagaQuest, QuestState.Active);
            saga.title.Text = "The Malfoy Saga";
            saga.summary.Text = "From a shop in Diagon Alley to a very awkward morning. Find Malfoy where the story continues (look for the \"!\").";
            string[] steps =
            {
                "Diagon Alley: meet the girl at Madam Malkin's",
                "Quidditch Pitch: the first flying lesson",
                "Forbidden Forest: detention, at night",
                "Library: someone wants to study with you",
                "The Yule Ball: find a partner (see its quest)",
                "Hogsmeade: read the Daily Prophet",
                "Years later, Malfoy Manor: knock on her door",
                "Malfoy Manor: the morning after",
            };
            for (int i = 0; i < steps.Length; i++)
                saga.steps.Add(Step(steps[i], When(Chapter, CompareOp.GreaterOrEqual, i + 1), When(Chapter, CompareOp.Greater, i + 1)));
        }

        private static SetVariableAction ChapterTo(int chapter) => new SetVariableAction { variable = Chapter, value = chapter.ToString() };

        /// <summary>Every last line of the chapter (an entry with no links) opens <paramref name="next"/>, unless it already sets the chapter.</summary>
        private static void EndChapter(DialogueDatabase db, string title, int next)
        {
            var conversation = db.FindConversation(title);
            foreach (var entry in conversation.entries)
            {
                if (entry == null || entry.id == Conversation.StartEntryId || entry.links.Count > 0) continue;
                if (entry.actions.OfType<SetVariableAction>().Any(a => a.variable == Chapter)) continue;
                entry.actions.Add(ChapterTo(next));
            }
        }

        // ------------------------------------------------------------------ scene

        private sealed class Place
        {
            public string Name;
            public Vector3 Position;
            public Color Color;
            public Place(string name, float x, float z, Color color) { Name = name; Position = new Vector3(x, 0f, z); Color = color; }
        }

        private static readonly Place DiagonAlley = new Place("Diagon Alley", -16f, -12f, new Color(0.55f, 0.45f, 0.35f));
        private static readonly Place Pitch = new Place("Quidditch Pitch", -16f, 2f, new Color(0.35f, 0.6f, 0.3f));
        private static readonly Place Forest = new Place("Forbidden Forest", -16f, 16f, new Color(0.15f, 0.3f, 0.18f));
        private static readonly Place Library = new Place("Library", 0f, 16f, new Color(0.5f, 0.35f, 0.25f));
        private static readonly Place Courtyard = new Place("Courtyard", 0f, 2f, new Color(0.55f, 0.55f, 0.6f));
        private static readonly Place Classroom = new Place("Classroom", 0f, -12f, new Color(0.45f, 0.4f, 0.55f));
        private static readonly Place GreatHall = new Place("Great Hall", 16f, 16f, new Color(0.65f, 0.55f, 0.25f));
        private static readonly Place Hogsmeade = new Place("Hogsmeade", 16f, 2f, new Color(0.6f, 0.5f, 0.5f));
        private static readonly Place Manor = new Place("Malfoy Manor (years later)", 16f, -12f, new Color(0.25f, 0.4f, 0.35f));

        /// <summary>
        /// The map: a pad per place, Malfoy standing where a chapter is open (with a "!"), the player (WASD, E to
        /// talk), a following camera, the standard UI, quest tracker (top right) and log (J), barks at the Courtyard,
        /// the sequencer (ball camera cuts and gown) and F5 / F9 save / load.
        /// </summary>
        [MenuItem("ND Dialogue/Samples/Malfoy Saga/Build Scene Only")]
        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath) == null) BuildDatabase();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath); // after NewScene (it unloads unused assets)
            // The TextMeshPro typewriter UI (markup, voice) when TMP Essentials are imported; else the legacy-Text one.
            var manager = TMPro.TMP_Settings.instance != null && TMPro.TMP_Settings.defaultFontAsset != null
                ? DialogueUIBuilder.CreateTypewriterSetup(db)
                : DialogueUIBuilder.CreateSetup(db);
            var canvas = DialogueUIBuilder.CanvasOf(manager);
            DialogueUIBuilder.CreateQuestTracker(canvas, manager);
            DialogueUIBuilder.CreateQuestLog(canvas, manager);
            var hint = HelpLine(canvas, manager);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 4.4f);
            ground.transform.position = new Vector3(0f, -0.01f, 2f);
            var groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.sharedMaterial = Material("Ground", new Color(0.2f, 0.22f, 0.25f), groundRenderer.sharedMaterial);
            foreach (var place in new[] { DiagonAlley, Pitch, Forest, Library, Courtyard, Classroom, GreatHall, Hogsmeade, Manor }) Pad(place);

            VariableCondition AtChapter(int n) => When(Chapter, CompareOp.Equal, n);
            Standee(manager, DiagonAlley, 0f, FirstMeetChapter, "Neutral", AtChapter(1));
            Standee(manager, Pitch, 0f, FlyingChapter, "Pout", AtChapter(2));
            Standee(manager, Forest, 0f, ForestChapter, "Scared", AtChapter(3));
            Standee(manager, Library, 0f, MissionChapter, "Embarrassed", AtChapter(4));
            Standee(manager, Courtyard, -2.5f, InvitationChapter, "Smug", AtChapter(5), Is(Asked, false));
            Standee(manager, Classroom, 0f, CraneChapter, "Pout", AtChapter(5), Is(Asked), Is(Crane, false));
            Standee(manager, Courtyard, 2.5f, FerretChapter, "Sad", AtChapter(5), Is(Ferret), Is(Forgiven, false));
            var ball = Standee(manager, GreatHall, 0f, BallChapter, "Embarrassed", AtChapter(5), Is(Partner));
            Standee(manager, Hogsmeade, 0f, InterviewChapter, "Angry", AtChapter(6));
            Standee(manager, Manor, -2f, MirrorChapter, "Older_Pout", AtChapter(7));
            Standee(manager, Manor, 2f, MorningChapter, "Older_Smug", AtChapter(8));
            var hallway = Standee(manager, Courtyard, 0f, HallwayChapter, "Neutral", When(Chapter, CompareOp.GreaterOrEqual, 2));
            Standee(manager, Pitch, 3f, FrogChapter, "Smug", When(Chapter, CompareOp.GreaterOrEqual, 3), QuestIs(FrogQuest, QuestState.Inactive));

            // Barks at the Courtyard: she mutters every 8 s, following the quest.
            var barker = DialogueUIBuilder.CreateBark(hallway.visual, manager, BarkConversation, 1.9f);
            var barkerSo = new SerializedObject(barker);
            barkerSo.FindProperty("m_When").enumValueIndex = (int)BarkTriggerEvent.Interval;
            barkerSo.FindProperty("m_IntervalMin").floatValue = 6f; // every 6–10 s, at random
            barkerSo.FindProperty("m_IntervalMax").floatValue = 10f;
            barkerSo.FindProperty("m_Chance").floatValue = 0.8f;
            barkerSo.FindProperty("m_Range").floatValue = 9f; // only when Harry is close (the listener is set below)
            barkerSo.ApplyModifiedPropertiesWithoutUndo();
            // A second kind of bark, same Malfoy and bubble: a parting line when any talk ends (only if Harry is near).
            var parting = DialogueUIBuilder.CreateBark(hallway.visual, manager, PartingBarkConversation, 1.9f);
            var partingSo = new SerializedObject(parting);
            partingSo.FindProperty("m_When").enumValueIndex = (int)BarkTriggerEvent.AfterConversation;
            partingSo.FindProperty("m_Cooldown").floatValue = 0f;
            partingSo.FindProperty("m_Range").floatValue = 9f;
            partingSo.ApplyModifiedPropertiesWithoutUndo();

            // The ball: her gown appears and the camera cuts in (sequences of "5. Yule Ball: The Ball").
            var gown = Billboard("Malfoy (gown)", AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/Yule/Malfoy_Embarrassed.png"), ball.visual.transform, ball.sprite.transform.localPosition);
            gown.SetActive(false);
            var closeUp = new GameObject("Ball Close-Up").transform;
            closeUp.SetPositionAndRotation(GreatHall.Position + new Vector3(0f, 2.2f, -3.2f), Quaternion.Euler(15f, 0f, 0f));
            var danceFloor = new GameObject("Dance Floor").transform;
            danceFloor.SetPositionAndRotation(GreatHall.Position + new Vector3(-2f, 4f, -7f), Quaternion.Euler(25f, 15f, 0f));

            var player = new GameObject("Harry (player)");
            player.transform.position = DiagonAlley.Position + new Vector3(0f, 0f, -4f);
            SetField(barker, "m_Listener", player.transform);
            SetField(parting, "m_Listener", player.transform);
            Billboard("Sprite", Portrait("Harry", "Neutral"), player.transform, new Vector3(0f, 1.3f, 0f));
            var mover = player.AddComponent<DialogueSampleMover>();
            SetField(mover, "m_Manager", manager);
            SetField(mover, "m_Speed", 6f);
            var interactor = player.AddComponent<DialogueInteractor>();
            var (promptRoot, promptText) = DialogueUIBuilder.CreatePrompt(canvas);
            SetField(interactor, "m_Manager", manager);
            SetField(interactor, "m_PromptRoot", promptRoot);
            SetField(interactor, "m_PromptText", promptText);

            Camera camera = null; // the new scene's own camera
            foreach (var root in scene.GetRootGameObjects())
                if (camera == null) camera = root.GetComponentInChildren<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
            camera.transform.SetPositionAndRotation(player.transform.position + CameraOffset, CameraRotation);
            var follow = camera.gameObject.AddComponent<DialogueSampleFollowCamera>();
            SetField(follow, "m_Target", player.transform);
            SetField(follow, "m_Manager", manager);
            SetField(follow, "m_Offset", CameraOffset);

            var sequencer = manager.gameObject.AddComponent<DialogueSequencer>();
            var so = new SerializedObject(sequencer);
            so.FindProperty("m_Manager").objectReferenceValue = manager;
            so.FindProperty("m_Camera").objectReferenceValue = camera.transform;
            var targets = so.FindProperty("m_Targets");
            var named = new (string name, GameObject go)[] { ("BallMalfoy", ball.sprite), ("BallGown", gown), ("BallCloseUp", closeUp.gameObject), ("DanceFloor", danceFloor.gameObject) };
            targets.arraySize = named.Length;
            for (int i = 0; i < named.Length; i++)
            {
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = named[i].name;
                targets.GetArrayElementAtIndex(i).FindPropertyRelative("gameObject").objectReferenceValue = named[i].go;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var save = manager.gameObject.AddComponent<DialogueSaveHotkeys>();
            SetField(save, "m_Manager", manager);
            SetField(save, "m_Slot", "malfoy_saga");
            SetField(save, "m_Note", hint.note);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Malfoy Saga] Scene saved: {ScenePath}. Press Play: WASD to walk, E to talk where Malfoy waits (\"!\"), J for the quest log, F5 / F9 to save / load.");
        }

        private static readonly Vector3 CameraOffset = new Vector3(0f, 11f, -11f);
        private static readonly Quaternion CameraRotation = Quaternion.Euler(45f, 0f, 0f);
        private static readonly Quaternion BillboardRotation = Quaternion.Euler(45f, 0f, 0f); // faces the camera

        private static void Pad(Place place)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = place.Name;
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.position = place.Position;
            pad.transform.localScale = new Vector3(9f, 0.05f, 9f);
            var renderer = pad.GetComponent<Renderer>();
            renderer.sharedMaterial = Material(place.Name, place.Color, renderer.sharedMaterial);
            var label = new GameObject("Label", typeof(TextMesh));
            label.transform.position = place.Position + new Vector3(0f, 0.1f, -4f);
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var text = label.GetComponent<TextMesh>();
            text.text = place.Name;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.15f;
            text.color = new Color(1f, 1f, 1f, 0.85f);
        }

        /// <summary>Malfoy waiting at <paramref name="place"/> for <paramref name="conversation"/>: a trigger with the conditions, a "!" and her sprite shown only while it can start.</summary>
        private static (DialogueTrigger trigger, GameObject visual, GameObject sprite) Standee(DialogueManager manager, Place place, float offsetX, string conversation, string emotion, params DialogueCondition[] conditions)
        {
            var root = new GameObject($"Malfoy — {conversation}");
            root.transform.position = place.Position + new Vector3(offsetX, 0f, 0.5f);
            var trigger = root.AddComponent<DialogueTrigger>();
            trigger.Manager = manager;
            trigger.Conversation = conversation;
            trigger.Actor = "Malfoy";
            trigger.UseRange = 2.2f;
            trigger.Conditions.AddRange(conditions);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sprite = Billboard("Sprite", MalfoySprite(emotion), visual.transform, new Vector3(0f, 1.3f, 0f));
            var bang = new GameObject("!", typeof(TextMesh));
            bang.transform.SetParent(visual.transform, false);
            bang.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            bang.transform.rotation = BillboardRotation;
            var text = bang.GetComponent<TextMesh>();
            text.text = "!";
            text.fontSize = 64;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.3f);
            var marker = root.AddComponent<DialogueTriggerMarker>();
            marker.Trigger = trigger;
            marker.Visual = visual;
            return (trigger, visual, sprite);
        }

        private static Sprite MalfoySprite(string emotion)
        {
            if (emotion.StartsWith("Older_")) return AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/Older/Malfoy_{emotion.Substring(6)}.png");
            return Portrait("Malfoy", emotion);
        }

        private static GameObject Billboard(string name, Sprite sprite, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = BillboardRotation;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (sprite == null) Debug.LogError($"[Malfoy Saga] No sprite for '{parent?.name}/{name}': it would render nothing.");
            return go;
        }

        /// <summary>The controls line at the bottom left, and a note above it for "Saved" / "Loaded".</summary>
        private static (Text help, Text note) HelpLine(Transform canvas, DialogueManager manager)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text Line(string name, float y, string content)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(24f, y);
                rect.sizeDelta = new Vector2(900f, 40f);
                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = 24;
                text.color = new Color(1f, 1f, 1f, 0.8f);
                text.raycastTarget = false;
                text.text = content;
                go.AddComponent<Shadow>();
                return text;
            }
            var help = Line("Controls", 16f, "WASD walk   ·   E talk (where Malfoy waits, \"!\")   ·   J quest log   ·   F5 save   ·   F9 load");
            var note = Line("Save Note", 56f, string.Empty);
            note.color = new Color(1f, 0.85f, 0.45f);
            note.gameObject.SetActive(false);
            return (help, note);
        }

        /// <summary>A coloured copy of the primitive's own material (works in any render pipeline), saved as an asset.</summary>
        private static Material Material(string name, Color color, Material template)
        {
            System.IO.Directory.CreateDirectory(Folder + "/Materials");
            string path = $"{Folder}/Materials/{name.Replace(" ", "_").Replace("(", "").Replace(")", "")}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"[Malfoy Saga] {target.GetType().Name} has no field '{field}'."); return; }
            switch (value)
            {
                case Object reference: property.objectReferenceValue = reference; break;
                case string text: property.stringValue = text; break;
                case float number: property.floatValue = number; break;
                case Vector3 vector: property.vector3Value = vector; break;
                default: Debug.LogError($"[Malfoy Saga] {nameof(SetField)}: unsupported value type {value?.GetType().Name} for '{field}'."); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ chapters

        private static void FirstMeet(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, FirstMeetChapter, "Madam Malkin's, then the Great Hall. Will Harry take her hand this time?", cast);
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
            var suit = b.Say(8, 0, "Malfoy", ("Surprised", "Your <shake>FRIEND</shake>?"), ("Pout", "...Hmph.<pause 0.6> Suit yourself."));
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
            var regret = b.Say(12, 0, "Malfoy", ("Sad", "Huh...?"), ("Sad", "You..."), ("Embarrassed", "<shout>Y-you'll regret this!!!!</shout>"));
            b.Link(refuse, regret);
            var warm = b.Say(12, 1, "Malfoy",
                ("Surprised", "!!"),
                ("Embarrassed", "I-it's not like I wanted to shake your hand or anything!"),
                ("Pout", "...Your hand is warm. That's disgusting. Hmph."));
            b.Link(shake, warm);
        }

        private static void FlyingLesson(DialogueDatabase db, Dictionary<string, Actor> cast)
        {
            var b = new Writer(db, FlyingChapter, "Harry is a natural on a broom. Malfoy is... not taking it well.", cast);
            var lesson = b.Say(1, 0, "Malfoy", ("Pout", "<whisper>(How is he so good?!)</whisper>"), ("Angry", "UGH, HE'S SO ANNOYING!!!"));
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
            var eek = b.Say(7, 1, "Malfoy", ("Scared", "<speed 80><shake>EEEEEEEEEEEEEEK!!!!</shake></speed>"));
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
            var b = new Writer(db, ForestChapter, "Detention in the Forbidden Forest. Someone is very brave. Out loud.", cast);
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
            var b = new Writer(db, MissionChapter, "Orders are orders. Surveillance is surveillance. Treacle tart is... surveillance.", cast);
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
            var tart = b.Say(5, 0, "Malfoy", ("Happy", "...Thanks."), ("Pout", "Here. Take my <err id=\"t\">heart</err><fix id=\"t\" text=\"treacle tart\">.<pause 0.4> Don't read into it."));
            b.Link(sit, tart);
            var notFollowing = b.Say(5, 1, "Malfoy", ("Embarrassed", "I'm NOT following you! I just...<pause 0.5> <wave>happen</wave> to be going the same way!"));
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
            var b = new Writer(db, InterviewChapter, "Rita Skeeter wants to know who Malfoy despises most. The ending depends on Malfoy.affection.", cast);
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
            hostileEnd.actions.Add(ChapterTo(9)); // she hates you: no epilogue
            hostileEnd.actions.Add(new SetQuestStateAction { quest = SagaQuest, state = QuestState.Failed });
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
            var b = new Writer(db, HallwayChapter, "Play it as often as you like: what Malfoy says depends on Malfoy.annoyance, .affection and .pride — and changes them.", cast);

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
            // Every greeting leads to the same four replies: one hub instead of 4 × 4 crossing links.
            var replies = b.Hub(2, 1.5f, "replies");
            foreach (var greeting in new[] { hostile, shy, smug, cold }) b.Link(greeting, replies);
            foreach (var reply in new[] { compliment, tease, invite, ignore }) b.Link(replies, reply);

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

        // ------------------------------------------------------------------ epilogue (her older look: Older_* emotions; a local character)

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
            var shout = b.Say(5, 0, "Malfoy", ("Older_Angry", "BE QUIET, YOU STUPID MIRROR!!!"));
            s_ShoutEntryId = shout.id;
            b.Link(boy, shout);
            var knock = b.Choice(6, 0, "(Knock) Ready, Malfoy?", "Happy", menu: "Knock on the door");
            var wait = b.Choice(6, 1, "(Wait outside a little longer.)", menu: "Wait outside");
            b.Link(shout, knock);
            b.Link(shout, wait);
            var look = b.Say(7, 0, "Malfoy", ("Older_Pout", "...Don't look at me like that."), ("Older_Smug", "I know. I look amazing."));
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
            var b = new Writer(db, MorningChapter, "The morning after. Toothbrushes, a bathroom mirror (a quiet one), and something Malfoy finally admits.", cast);
            // If the talking mirror scene was played, she has a warning first (Entry Seen in ANOTHER conversation).
            var warning = b.Say(1, 1, "Malfoy", ("Older_Pout", "And if that mirror says ONE word about last night..."));
            b.Link(b.Start, warning, LinkPriority.High).conditions.Add(new EntrySeenCondition { conversation = MirrorChapter, entryId = s_ShoutEntryId, seen = true });
            var morning = b.Say(2, 0, "Malfoy",
                ("Neutral", "Morning, Potter."),
                ("Older_Smug", "Guess you were right."),
                ("Embarrassed", "We were never going to be just \"friends\", were we?"));
            b.Link(b.Start, morning, LinkPriority.Low);
            b.Link(warning, morning);
            var choke = b.Choice(3, 0, "(Choke on the toothpaste.)", "Surprised", menu: "Choke on the toothpaste");
            var grin = b.Choice(3, 1, "I'd never be friends with someone like you, Malfoy.", "Happy", menu: "Quote your first year");
            b.Link(morning, choke);
            b.Link(morning, grin);
            var idiot = b.Say(4, 0, "Malfoy", ("Older_Smug", "Hmph. Idiot."));
            b.Link(choke, idiot);
            var hit = b.Say(4, 1, "Malfoy", ("Older_Angry", "Say that again and I'll hex your toothbrush."), ("Older_Pout", "...Idiot."));
            b.Link(grin, hit);
            foreach (var last in new[] { idiot, hit })
            {
                last.actions.Add(ChapterTo(9));
                last.actions.Add(new SetQuestStateAction { quest = SagaQuest, state = QuestState.Completed });
            }
        }

        // ------------------------------------------------------------------ helpers

        internal const string Annoyance = "Malfoy.annoyance";
        internal const string Pride = "Malfoy.pride";

        internal static SetVariableAction AddTo(string variable, int amount) =>
            new SetVariableAction { variable = variable, mode = SetVariableAction.Mode.Add, value = amount.ToString() };

        internal static VariableCondition When(string variable, CompareOp op, int value) =>
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

        internal static Sprite Portrait(string actor, string emotion) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/{actor}_{emotion}.png");

        /// <summary>Portraits are plain PNGs: make them single sprites (once).</summary>
        internal static void ImportPortraits()
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
        internal sealed class Writer
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

            /// <summary>A hub: links into it count as links to each of its targets (merges many → many).</summary>
            public DialogueEntry Hub(float column, float row, string title)
            {
                var entry = m_Conversation.AddEntry(m_Cast["Harry"].id, title);
                entry.position = new Vector2(column * 300f + 60f, row * 190f + 40f);
                entry.isPassthrough = true;
                entry.isHub = true;
                return entry;
            }

            public DialogueLink Link(DialogueEntry from, DialogueEntry to, LinkPriority priority = LinkPriority.Normal) =>
                m_Conversation.Link(from, to, priority);
        }
    }
}
