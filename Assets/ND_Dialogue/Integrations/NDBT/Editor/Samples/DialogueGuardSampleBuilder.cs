using System.Collections.Generic;
using ND.Localization;
using ND_BehaviorTree;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ND.Dialogue.NDBT.Editor.Samples
{
    /// <summary>
    /// Builds a complete, playable dialogue setup in Assets/ND_Dialogue/Samples/GuardSample (rebuilt from scratch each
    /// time): locales en/vi, a world blackboard and a guard blackboard (as its child), a database with three kinds of
    /// characters (player, a BlackboardActor guard, a plain merchant with an attribute), two conversations using
    /// quests, conditions, priorities, vector/colour/object values, events, plain text and localized text, a colour
    /// table for a region-dependent emotion tint, and a scene with the debug view and an event listener.
    /// </summary>
    public static class DialogueGuardSampleBuilder
    {
        public const string DefaultFolder = "Assets/ND_Dialogue/Samples/GuardSample";
        public const string SceneFile = "GuardSample.unity";
        public const string DatabaseFile = "GuardSample_Dialogue.asset";
        public const string LocalizationFile = "GuardSample_Localization.asset";

        /// <summary>Folder of the build in progress (DefaultFolder for the menu; tests build elsewhere).</summary>
        private static string s_Folder = DefaultFolder;
        private static string Folder => s_Folder;
        public static string ScenePathIn(string folder) => $"{folder}/{SceneFile}";
        private static string ScenePath => ScenePathIn(s_Folder);
        private static string DatabasePath => $"{s_Folder}/{DatabaseFile}";
        private static string LocalizationPath => $"{s_Folder}/{LocalizationFile}";

        [MenuItem("ND_BehaviorTree/Samples/Build Dialogue Guard Sample")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Guard Sample] Leave Play mode first.");
                return;
            }
            var database = Build();
            EditorSceneManager.OpenScene(ScenePathIn(DefaultFolder));
            Selection.activeObject = database;
            Debug.Log($"[Guard Sample] Built in {DefaultFolder}. Press Play, or double-click the database to open the Dialogue Editor.", database);
        }

        /// <summary>Builds every asset and the scene; returns the database.</summary>
        public static DialogueDatabase Build() => Build(DefaultFolder, NewSceneMode.Single, out _);

        /// <summary>
        /// Builds into <paramref name="folder"/> (deleted first: generated content only). <paramref name="sceneMode"/>
        /// Additive leaves the open scenes alone (tests); the new scene is returned saved and loaded.
        /// </summary>
        public static DialogueDatabase Build(string folder, NewSceneMode sceneMode, out UnityEngine.SceneManagement.Scene scene)
        {
            s_Folder = folder;
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder); // generated content only
            EnsureFolder(Folder);

            // ---------------------------------------------------------------- localization
            var en = Create<Locale>("Locale_en", l => { l.code = "en"; l.displayName = "English"; });
            var vi = Create<Locale>("Locale_vi", l => { l.code = "vi"; l.displayName = "Tiếng Việt"; l.fallback = en; });
            var text = Create<StringTable>("GuardSample_Text", t => t.tableName = "GuardSample");
            var colors = Create<ColorTable>("GuardSample_Colors", t => t.tableName = "GuardSampleColors");
            Create<LocalizationSettings>("GuardSample_Localization", s =>
            {
                s.locales = new List<Locale> { en, vi };
                s.defaultLocale = en;
                s.tables = new List<LocalizationTable> { text, colors };
                s.playerPrefsKey = string.Empty; // a sample always starts in English
                s.startupSelectors = new List<IStartupLocaleSelector>();
            });

            // ---------------------------------------------------------------- blackboards (world + guard as its child)
            var world = Create<Blackboard>("World_Blackboard");
            AddKey<BoolKey, bool>(world, "player.has_old_key", false);
            AddKey<IntKey, int>(world, "player.gold", 10);
            AddKey<FloatKey, float>(world, "world.time_of_day", 8f);
            AddKey<IntKey, int>(world, "quest.key", (int)QuestState.Inactive);
            AddKey<Vector3Key, Vector3>(world, "world.gate_position", new Vector3(0f, 0f, 5f));
            AddKey<IntKey, int>(world, "Merchant.mood", 0); // a plain character's attribute lives in the world as "Actor.attribute"

            var guardBoard = Create<Blackboard>("Guard_Blackboard", b => b.parent = world);
            AddKey<FloatKey, float>(guardBoard, "trust", 0f);
            AddKey<BoolKey, bool>(guardBoard, "met", false);
            AddKey<FloatKey, float>(guardBoard, "alertness", 0.2f);
            AddKey<Vector3Key, Vector3>(guardBoard, "homePosition", new Vector3(0f, 0f, 4f));
            AddKey<ColorKey, Color>(guardBoard, "bannerColor", new Color(0.2f, 0.4f, 0.9f));
            AddKey<TransformKey, Transform>(guardBoard, "target", null);

            var source = Create<BlackboardSchemaSource>("GuardSample_World", s => s.blackboard = world);

            // ---------------------------------------------------------------- database & characters
            var db = Create<DialogueDatabase>("GuardSample_Dialogue");
            db.textTable = text;
            db.schemaSource = source;

            var player = db.AddActor("Player", isPlayer: true);
            player.displayName.Text = "You";
            player.emotions.Add(new ActorEmotion { id = "Neutral" });

            var guard = db.AddActor(new BlackboardActor { name = "Guard", blackboard = guardBoard });
            guard.displayName.Text = "Gate Guard";
            guard.emotions.Add(new ActorEmotion { id = "Neutral", tint = Color.white });
            var angry = new ActorEmotion { id = "Angry", tint = new Color(1f, 0.55f, 0.55f) };
            long angryTint = colors.AddEntry("guard.angry");
            colors.SetValue(angryTint, "en", Color.red);                     // red reads as anger here…
            colors.SetValue(angryTint, "vi", new Color(1f, 0.6f, 0.1f));      // …another region gets orange instead
            angry.localizedTint.SetReference(colors, angryTint);
            guard.emotions.Add(angry);
            guard.emotions.Add(new ActorEmotion { id = "Happy", tint = new Color(0.6f, 1f, 0.6f) });

            var merchant = db.AddActor("Merchant");
            merchant.displayName.Text = "Old Merchant";
            merchant.attributes.Add(new VariableDefinition { name = "mood", initialValue = VariableValue.Int(0), description = "Goes up when you buy something." });
            merchant.emotions.Add(new ActorEmotion { id = "Neutral" });

            db.events.Add(new EventDefinition { name = "open_gate", description = "The guard lets the player through." });
            db.events.Add(new EventDefinition { name = "give_item", argumentHint = "item id", description = "The player receives an item." });
            db.quests.Add(KeyQuestJournal()); // the quest itself is the int key "quest.key" of the world blackboard

            BuildGateConversation(db, text, player, guard);
            BuildMerchantConversation(db, player, merchant);

            var issues = db.Validate();
            if (issues.Count > 0) Debug.LogError("[Guard Sample] The sample does not validate:\n  - " + string.Join("\n  - ", issues), db);

            EditorUtility.SetDirty(db);
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(colors);
            AssetDatabase.SaveAssets();

            scene = BuildScene(sceneMode);
            var built = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
            s_Folder = DefaultFolder;
            return built;
        }

        // ---------------------------------------------------------------- conversations

        /// <summary>Quest giver: first meeting with a choice, waiting, completion (high priority), and thanks afterwards. Localized en/vi.</summary>
        private static void BuildGateConversation(DialogueDatabase db, StringTable table, Actor player, Actor guard)
        {
            var c = db.AddConversation("Gate Guard");
            c.description = "Quest giver. Localized lines (en/vi), quest state, character attributes, link priority, an event.";
            var start = c.StartEntry;
            start.position = new Vector2(0, 260);

            DialogueEntry Say(Actor actor, Vector2 at, string en, string vi, string emotion = null)
            {
                var e = c.AddEntry(actor.id);
                e.position = at;
                e.emotion = emotion;
                e.text.Text = en; // the writer's note
                long id = table.AddEntry($"gate.{e.id}");
                table.SetValue(id, "en", en);
                table.SetValue(id, "vi", vi);
                e.text.SetReference(table, id);
                return e;
            }

            var halt = Say(guard, new Vector2(300, 0), "Halt! Nobody passes this gate.", "Đứng lại! Không ai được qua cổng này.");
            halt.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Inactive });
            halt.conditions.Add(new VariableCondition { variable = "Guard.met", value = "false" });
            halt.actions.Add(new SetVariableAction { variable = "Guard.met", value = "true" });

            var again = Say(guard, new Vector2(300, 130), "You again. Looking for work or not?", "Lại là cậu. Có tìm việc không?");
            again.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Inactive });
            again.conditions.Add(new VariableCondition { variable = "Guard.met", value = "true" });

            var work = Say(player, new Vector2(620, 0), "I'm looking for work.", "Tôi đang tìm việc.");
            var rude = Say(player, new Vector2(620, 130), "Out of my way!", "Tránh ra!");
            var task = Say(guard, new Vector2(940, 0), "Find my old key in the market and I'll let you through.", "Tìm giúp tôi chiếc chìa khóa cũ ở chợ, tôi sẽ cho cậu qua.");
            task.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Active });
            task.actions.Add(new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "1" });
            var tone = Say(guard, new Vector2(940, 130), "Watch your tone, stranger.", "Ăn nói cho cẩn thận.", "Angry");
            tone.actions.Add(new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "-1" });
            tone.actions.Add(new SetVariableAction { variable = "Guard.alertness", value = "0.9" });

            var waiting = Say(guard, new Vector2(300, 300), "Still no key? Try the merchant.", "Vẫn chưa có chìa à? Thử hỏi ông lái buôn xem.");
            waiting.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });

            var found = Say(guard, new Vector2(300, 420), "My key! Thank you. The gate is yours.", "Chìa khóa của tôi! Cảm ơn. Cậu qua đi.", "Happy");
            found.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Active });
            found.conditions.Add(new VariableCondition { variable = "player.has_old_key", value = "true" });
            found.actions.Add(new SetQuestStateAction { quest = "key", state = QuestState.Completed });
            found.actions.Add(new SetVariableAction { variable = "Guard.trust", mode = SetVariableAction.Mode.Add, value = "2" });
            found.actions.Add(new SetVariableAction { variable = "Guard.bannerColor", value = "#33CC55" });
            found.actions.Add(new RaiseEventAction { eventName = "open_gate" });

            var friend = Say(guard, new Vector2(300, 560), "Welcome back, friend. (trust [var=Guard.trust])", "Chào mừng trở lại, bạn tôi. (tin tưởng [var=Guard.trust])", "Happy");
            friend.conditions.Add(new QuestStateCondition { quest = "key", state = QuestState.Completed });

            // Which greeting plays is decided on the branches out of START (shown on the edges).
            Branch(c, start, halt);
            Branch(c, start, again);
            Branch(c, start, waiting);
            Branch(c, start, found, LinkPriority.High); // tried before "waiting": both need the quest active
            Branch(c, start, friend);
            c.Link(halt, work); c.Link(halt, rude);
            c.Link(again, work); c.Link(again, rude);
            c.Link(work, task);
            c.Link(rude, tone);
        }

        /// <summary>Shop: plain (not yet localized) text, an int comparison, object/vector-free numbers, an event with an argument.</summary>
        private static void BuildMerchantConversation(DialogueDatabase db, Actor player, Actor merchant)
        {
            var c = db.AddConversation("Merchant");
            c.description = "Plain text (connect it to the table later with Settings → Localize all). Gold check, a purchase, the merchant's attribute.";
            var start = c.StartEntry;
            start.position = new Vector2(0, 120);

            DialogueEntry Say(Actor actor, Vector2 at, string line, string menu = null)
            {
                var e = c.AddEntry(actor.id);
                e.position = at;
                e.text.Text = line;
                if (menu != null) e.menuText.Text = menu;
                return e;
            }

            var hello = Say(merchant, new Vector2(300, 120), "Fresh goods! You have [var=player.gold] gold.");
            var buy = Say(player, new Vector2(620, 0), "I'll take that rusty old key.", "Buy the old key (5 gold)");
            buy.conditions.Add(new VariableCondition { variable = "player.gold", op = CompareOp.GreaterOrEqual, value = "5" });
            buy.conditions.Add(new VariableCondition { variable = "player.has_old_key", value = "false" });
            var look = Say(player, new Vector2(620, 240), "Just looking.");
            var sold = Say(merchant, new Vector2(940, 0), "Here you go. Careful, it's rusty. (mood [var=Merchant.mood])");
            sold.actions.Add(new SetVariableAction { variable = "player.gold", mode = SetVariableAction.Mode.Add, value = "-5" });
            sold.actions.Add(new SetVariableAction { variable = "player.has_old_key", value = "true" });
            sold.actions.Add(new SetVariableAction { variable = "Merchant.mood", mode = SetVariableAction.Mode.Add, value = "1" });
            sold.actions.Add(new RaiseEventAction { eventName = "give_item", argument = "old_key" });
            var bye = Say(merchant, new Vector2(940, 240), "Suit yourself.");

            c.Link(start, hello);
            Branch(c, hello, buy); c.Link(hello, look);
            c.Link(buy, sold);
            c.Link(look, bye);
        }

        /// <summary>The quest log's view of "key": a title, a summary and three steps driven by the world's values.</summary>
        private static QuestDefinition KeyQuestJournal()
        {
            var started = new QuestStateCondition { quest = "key", op = CompareOp.NotEqual, state = QuestState.Inactive };
            var journal = new QuestDefinition
            {
                name = "key",
                group = "Main",
                description = "Journal of the world's quest.key (the state lives in World_Blackboard).",
            };
            journal.title.Text = "The Old Key";
            journal.summary.Text = "The gate guard won't let anyone through. He lost an old key somewhere in the market.";
            journal.steps.Add(Step("Ask the gate guard how to get through", null,
                new QuestStateCondition { quest = "key", op = CompareOp.NotEqual, state = QuestState.Inactive }));
            journal.steps.Add(Step("Find the old key (the merchant might have one)", started,
                new VariableCondition { variable = "player.has_old_key", value = "true" }));
            journal.steps.Add(Step("Bring the key back to the gate guard",
                new VariableCondition { variable = "player.has_old_key", value = "true" }, null)); // ticked when the quest completes
            return journal;
        }

        private static QuestStep Step(string text, DialogueCondition visibleWhen, DialogueCondition doneWhen)
        {
            var step = new QuestStep();
            step.text.Text = text;
            if (visibleWhen != null) step.visibleWhen.Add(visibleWhen);
            if (doneWhen != null) step.doneWhen.Add(doneWhen);
            return step;
        }

        /// <summary>Links <paramref name="from"/> → <paramref name="to"/> and moves <paramref name="to"/>'s conditions onto the link (a per-branch rule, drawn on the edge).</summary>
        private static DialogueLink Branch(Conversation c, DialogueEntry from, DialogueEntry to, LinkPriority priority = LinkPriority.Normal)
        {
            var link = c.Link(from, to, priority);
            link.conditions.AddRange(to.conditions);
            to.conditions.Clear();
            return link;
        }

        // ---------------------------------------------------------------- scene

        private static UnityEngine.SceneManagement.Scene BuildScene(NewSceneMode sceneMode)
        {
            var previousActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, sceneMode);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene); // new GameObjects go to the active scene
            // Load the assets AFTER creating the scene: a Single-mode NewScene unloads assets it considers unused,
            // which turns earlier C# references into destroyed objects that serialize as null.
            var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
            var localization = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationPath);
            if (db == null || localization == null)
            {
                Debug.LogError($"[Guard Sample] Assets missing after creation: database={(db != null)}, localization={(localization != null)}.");
                return scene;
            }

            var loc = new GameObject("Localization");
            var bootstrap = loc.AddComponent<LocalizationBootstrap>();
            SetField(bootstrap, "m_Settings", localization);

            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Gate (hidden by the open_gate event)";
            gate.transform.position = new Vector3(0f, 0.5f, 5f);
            gate.transform.localScale = new Vector3(4f, 1f, 0.3f);

            // The real UI (Standard Dialogue UI) driven by the manager; the debug view adds conversation buttons,
            // language switches and the live state panel on top of the same runner and world.
            var manager = ND.Dialogue.Editor.DialogueUIBuilder.CreateSetup(db);
            var dialogue = manager.gameObject;
            var view = dialogue.AddComponent<DialogueDebugView>();
            SetField(view, "m_Manager", manager);

            // Quest HUD: the tracker (top right, with "Quest started" toasts) and the journal window (J).
            var questCanvas = ND.Dialogue.Editor.DialogueUIBuilder.CanvasOf(manager);
            ND.Dialogue.Editor.DialogueUIBuilder.CreateQuestTracker(questCanvas, manager);
            ND.Dialogue.Editor.DialogueUIBuilder.CreateQuestLog(questCanvas, manager);

            // A small yard: walk with WASD, press E near someone to talk.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            var guardNpc = Character("Guard (NPC)", PrimitiveType.Capsule, new Vector3(0f, 1f, 3.6f), new Color(0.35f, 0.55f, 0.95f));
            var guardTrigger = guardNpc.AddComponent<DialogueTrigger>();
            guardTrigger.Manager = manager;
            guardTrigger.Conversation = "Gate Guard";
            guardTrigger.Actor = "Guard";

            var merchantNpc = Character("Merchant (NPC)", PrimitiveType.Cylinder, new Vector3(-5f, 1f, -1f), new Color(0.95f, 0.7f, 0.3f));
            var merchantTrigger = merchantNpc.AddComponent<DialogueTrigger>();
            merchantTrigger.Manager = manager;
            merchantTrigger.Conversation = "Merchant";
            merchantTrigger.Actor = "Merchant";

            var playerGo = Character("Player", PrimitiveType.Capsule, new Vector3(0f, 1f, -3f), new Color(0.9f, 0.9f, 0.9f));
            var mover = playerGo.AddComponent<DialogueSampleMover>();
            SetField(mover, "m_Manager", manager);
            var interactor = playerGo.AddComponent<DialogueInteractor>();
            var (promptRoot, promptText) = ND.Dialogue.Editor.DialogueUIBuilder.CreatePrompt(manager.View.transform.root);
            SetField(interactor, "m_Manager", manager);
            SetField(interactor, "m_PromptRoot", promptRoot);
            SetField(interactor, "m_PromptText", promptText);

            Camera camera = null; // the new scene's own camera (Camera.main may belong to another open scene)
            foreach (var root in scene.GetRootGameObjects())
                if (camera == null) camera = root.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 11f, -10f);
                camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            }

            var listener = dialogue.AddComponent<DialogueEventListener>();
            SetField(listener, "m_Database", db);
            var open = new DialogueEventListener.Binding { eventName = "open_gate" };
            UnityEventTools.AddBoolPersistentListener(open.response, gate.SetActive, false);
            listener.Bindings.Add(open);

            EditorSceneManager.SaveScene(scene, ScenePath);
            VerifySceneReferences(manager, bootstrap, db, localization);
            if (sceneMode == NewSceneMode.Additive && previousActive.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousActive);
            return scene;
        }

        /// <summary>Reads the wired fields back from the saved scene objects: a null here means the sample is broken.</summary>
        private static void VerifySceneReferences(DialogueManager manager, LocalizationBootstrap bootstrap, DialogueDatabase db, LocalizationSettings localization)
        {
            var problems = new List<string>();
            void Expect(Object target, string field, Object expected)
            {
                var actual = new SerializedObject(target).FindProperty(field)?.objectReferenceValue;
                if (actual != expected) problems.Add($"{target.GetType().Name}.{field} = {(actual != null ? actual.name : "null")} (expected {expected.name})");
            }
            Expect(manager, "m_Database", db);
            Expect(bootstrap, "m_Settings", localization);
            if (problems.Count > 0) Debug.LogError("[Guard Sample] The scene was saved with broken references:\n  - " + string.Join("\n  - ", problems));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A coloured primitive standing on the ground (its collider is removed: interaction is by distance).</summary>
        private static GameObject Character(string objectName, PrimitiveType shape, Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = objectName;
            go.transform.position = position;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { color = color, name = objectName + " Material" };
            AssetDatabase.CreateAsset(material, $"{Folder}/{objectName.Replace(" ", "_").Replace("(", "").Replace(")", "")}.mat");
            renderer.sharedMaterial = material;
            return go;
        }

        private static T Create<T>(string assetName, System.Action<T> setup = null) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            setup?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, $"{Folder}/{assetName}.asset");
            return asset;
        }

        /// <summary>Adds a key as a sub-asset of the blackboard (how the blackboard editor stores keys).</summary>
        private static void AddKey<TKey, TValue>(Blackboard board, string keyName, TValue value) where TKey : Key<TValue>
        {
            var key = ScriptableObject.CreateInstance<TKey>();
            key.name = keyName;
            key.keyName = keyName;
            key.SetValue(value);
            AssetDatabase.AddObjectToAsset(key, board);
            board.keys.Add(key);
            EditorUtility.SetDirty(board);
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Guard Sample] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case string s: prop.stringValue = s; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
