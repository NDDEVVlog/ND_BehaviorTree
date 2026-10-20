using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND_BehaviorTree.Editor.Graph;
using ND_BehaviorTree.GOAP;
using ND_BehaviorTree.Story;
using ND_BehaviorTree.Story.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ND_BehaviorTree.Demo.Plugins.EditorTools
{
    /// <summary>
    /// Builds TestZone4: one scene showing both plugins side by side, with their trees and blackboards.
    ///   Left  — Story Nodes: a villager wanders (Random of Blocks), picks a mood (Set Text) and reacts (Switch → Cases).
    ///   Right — GOAP: a woodcutter keeps the village stockpile full (the village burns wood over time) and eats when
    ///           health gets low; the action subtrees are Story Blocks (moves to / says / wait).
    /// Re-run to rebuild everything from scratch (asks before overwriting).
    /// </summary>
    public static class PluginShowcaseBuilder
    {
        private const string Root = "Assets/ND_BehaviorTree/DEMO/TestZone4_Plugins";
        private const string ScenePath = Root + "/PluginShowcase.unity";
        private const string NavMeshPath = Root + "/PluginShowcase_NavMesh.asset";
        private const string MaterialFolder = Root + "/Materials";
        private const string VillagerTreePath = Root + "/Villager_Story.asset";
        private const string WoodcutterTreePath = Root + "/Woodcutter_GOAP.asset";

        [MenuItem("ND_BehaviorTree/Demo/Build Plugin Showcase Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PluginShowcaseBuilder] Exit Play mode first.");
                return;
            }
            if ((File.Exists(ScenePath) || File.Exists(VillagerTreePath) || File.Exists(WoodcutterTreePath))
                && !EditorUtility.DisplayDialog("Plugin showcase", $"Rebuild the scene and both trees in {Root} from scratch?", "Rebuild", "Cancel"))
                return;

            var villagerTree = BuildVillagerTree(out var villagerKeys);
            var woodcutterTree = BuildWoodcutterTree(out var woodKeys);
            if (villagerTree == null || woodcutterTree == null) return;
            BuildScene(villagerTree, villagerKeys, woodcutterTree, woodKeys);
        }

        // ================================================================== trees

        private sealed class VillagerKeys { public Key SpotA, SpotB, SpotC; }
        private sealed class WoodKeys { public Key AxeSpot, TreeSpot, StockpileSpot, FoodSpot; }

        /// <summary>Story Nodes: Random of walk Blocks → Random mood (Set Text) → Switch on mood → reaction Blocks.</summary>
        private static BehaviorTree BuildVillagerTree(out VillagerKeys keys)
        {
            var tree = NewTree(VillagerTreePath, out var bb);
            keys = new VillagerKeys
            {
                SpotA = AddKey<GameObjectKey>(bb, "SpotA"),
                SpotB = AddKey<GameObjectKey>(bb, "SpotB"),
                SpotC = AddKey<GameObjectKey>(bb, "SpotC"),
            };
            var mood = AddKey<StringKey>(bb, "Mood");

            var graph = new BehaviorTreeGraphService(tree);
            var day = N<SequenceNode>(graph, 600, 260, "Villager Day");
            graph.Connect(tree.rootNode, day);

            // 1. Where to go (weights: the market is twice as likely)
            var walk = N<StoryRandomNode>(graph, 150, 440);
            graph.Connect(day, walk);
            WalkBlock(graph, walk, -150, keys.SpotA, "The well. Fresh water!", 1f);
            WalkBlock(graph, walk, 150, keys.SpotB, "The market is busy today.", 2f);
            WalkBlock(graph, walk, 450, keys.SpotC, "My little garden.", 1f);

            // 2. Pick a mood
            var pickMood = N<StoryRandomNode>(graph, 700, 440);
            graph.Connect(day, pickMood);
            foreach (var (m, x) in new[] { ("Happy", 600f), ("Tired", 820f), ("Curious", 1040f) })
            {
                var block = N<StoryBlockNode>(graph, x, 620);
                graph.Connect(pickMood, block);
                var set = Own<SetTextNode>(graph, block);
                set.variableKey = mood;
                set.text = m;
            }

            // 3. React to it: Switch generates one Case per entry (+ Default)
            var sw = N<StorySwitchNode>(graph, 1350, 440);
            sw.valueKey = mood;
            sw.cases = new List<string> { "Happy", "Tired" };
            sw.includeDefault = true;
            graph.Connect(day, sw);
            StorySwitchCases.Sync(graph, sw);
            CaseBranch(graph, sw, "Happy", "What a lovely day!", 1.5f);
            CaseBranch(graph, sw, "Tired", "I need a nap...", 3f);
            CaseBranch(graph, sw, null, "Hmm, what's that over there?", 1.5f);

            Save(tree, bb);
            return tree;
        }

        private static void WalkBlock(BehaviorTreeGraphService graph, Node parent, float x, Key spot, string line, float weight)
        {
            var block = N<StoryBlockNode>(graph, x, 620);
            block.weight = weight;
            graph.Connect(parent, block);
            var move = Own<MoveToNode>(graph, block);
            move.targetKey = spot;
            move.stoppingDistance = 0.6f;
            Own<SaysNode>(graph, block, says => { says.message = line; says.duration = 2f; says.logToConsole = false; });
            Own<WaitForNode>(graph, block, w => w.seconds = 1f);
        }

        private static void CaseBranch(BehaviorTreeGraphService graph, StorySwitchNode sw, string caseValue, string line, float seconds)
        {
            var caseNode = sw.children.OfType<StoryCaseNode>().First(c => caseValue == null ? c.isDefault : c.caseValue == caseValue);
            var block = N<StoryBlockNode>(graph, caseNode.position.x, caseNode.position.y + 180);
            graph.Connect(caseNode, block);
            Own<SaysNode>(graph, block, says => { says.message = line; says.duration = seconds; says.logToConsole = false; });
            Own<WaitForNode>(graph, block, w => w.seconds = seconds);
        }

        /// <summary>GOAP: planner with two goals; each action's work is a Story Block (moves to / says / wait).</summary>
        private static BehaviorTree BuildWoodcutterTree(out WoodKeys keys)
        {
            var tree = NewTree(WoodcutterTreePath, out var bb);
            var hasAxe = AddKey<BoolKey>(bb, "HasAxe");
            var carrying = AddKey<BoolKey>(bb, "Carrying");
            var health = AddKey<IntKey>(bb, "Health");
            ((IntKey)health).SetValue(100);
            var stockpile = AddKey<IntKey>(bb, "Stockpile");
            keys = new WoodKeys
            {
                AxeSpot = AddKey<GameObjectKey>(bb, "AxeSpot"),
                TreeSpot = AddKey<GameObjectKey>(bb, "TreeSpot"),
                StockpileSpot = AddKey<GameObjectKey>(bb, "StockpileSpot"),
                FoodSpot = AddKey<GameObjectKey>(bb, "FoodSpot"),
            };

            var graph = new BehaviorTreeGraphService(tree);
            var loop = N<SequenceNode>(graph, 700, 260, "Work Loop");
            graph.Connect(tree.rootNode, loop);

            var planner = N<GOAPPlannerNode>(graph, 500, 440, "Woodcutter Planner");
            planner.logPlans = true;
            planner.goals = new List<GoapGoal>
            {
                new GoapGoal { name = "Stay healthy", priority = 10, conditions = { Cond(health, ComparisonType.GreaterEqual, i: 40) } },
                new GoapGoal { name = "Keep the stockpile full", priority = 1, conditions = { Cond(stockpile, ComparisonType.GreaterEqual, i: 30) } },
            };
            graph.Connect(loop, planner);

            GoapAction(graph, planner, 0, "Get Axe", keys.AxeSpot, "Got my axe.", 0.5f,
                new[] { Cond(hasAxe, ComparisonType.Equal, b: false) }, Set(hasAxe, b: true));
            GoapAction(graph, planner, 1, "Chop Tree", keys.TreeSpot, "Chop! Chop!", 2f,
                new[] { Cond(hasAxe, ComparisonType.Equal, b: true), Cond(carrying, ComparisonType.Equal, b: false), Cond(health, ComparisonType.GreaterEqual, i: 20) },
                Set(carrying, b: true), Add(health, -20));
            GoapAction(graph, planner, 2, "Deliver Logs", keys.StockpileSpot, "+10 wood for the village.", 0.5f,
                new[] { Cond(carrying, ComparisonType.Equal, b: true) }, Set(carrying, b: false), Add(stockpile, 10));
            GoapAction(graph, planner, 3, "Eat", keys.FoodSpot, "Lunch break!", 1.5f,
                new KeyCondition[0], Set(health, i: 100));

            // When every goal holds the planner succeeds at once: take a short break before checking again.
            var rest = N<StoryBlockNode>(graph, 1000, 440);
            graph.Connect(loop, rest);
            Own<SaysNode>(graph, rest, says => { says.message = "Stockpile is full. Break time."; says.duration = 2f; says.logToConsole = false; });
            Own<WaitForNode>(graph, rest, w => w.seconds = 3f);

            Save(tree, bb);
            return tree;
        }

        private static void GoapAction(BehaviorTreeGraphService graph, GOAPPlannerNode planner, int column, string title, Key spot, string line, float seconds,
            KeyCondition[] preconditions, params KeyEffect[] effects)
        {
            float x = 100 + column * 330;
            var action = N<GOAPActionNode>(graph, x, 640, title);
            action.preconditions = preconditions.ToList();
            action.effects = effects.ToList();
            graph.Connect(planner, action);

            var block = N<StoryBlockNode>(graph, x, 860);
            graph.Connect(action, block);
            var move = Own<MoveToNode>(graph, block);
            move.targetKey = spot;
            move.stoppingDistance = 0.6f;
            Own<SaysNode>(graph, block, says => { says.message = line; says.duration = Mathf.Max(1.5f, seconds); says.logToConsole = false; });
            Own<WaitForNode>(graph, block, w => w.seconds = seconds);
        }

        // ================================================================== scene

        private static void BuildScene(BehaviorTree villagerTree, VillagerKeys vk, BehaviorTree woodTree, WoodKeys wk)
        {
            // Built additively and closed afterwards: the scene you have open is left alone.
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var mats = new Materials();

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 50f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 19f, -17f), Quaternion.Euler(50f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // Ground (the only NavMesh geometry: props have no colliders, so agents walk right onto their spots)
            var level = new GameObject("Level");
            Box(level, "Ground", new Vector3(0f, -0.25f, 0f), new Vector3(32f, 0.5f, 22f), mats.ground, collider: true);
            Box(level, "Divider", new Vector3(0f, 0.01f, 0f), new Vector3(0.15f, 0.02f, 22f), mats.divider, collider: false);
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);

            Label(null, "STORY NODES", new Vector3(-8f, 0.05f, 9.5f), 0.35f, Color.white);
            Label(null, "Villager: Random walk → mood → Switch", new Vector3(-8f, 0.05f, 8.4f), 0.18f, new Color(0.8f, 0.85f, 0.95f));
            Label(null, "GOAP", new Vector3(8f, 0.05f, 9.5f), 0.35f, Color.white);
            Label(null, "Woodcutter: keep stockpile ≥ 30, stay healthy", new Vector3(8f, 0.05f, 8.4f), 0.18f, new Color(0.8f, 0.85f, 0.95f));

            // ---- Story side
            var well = Spot("Well", new Vector3(-12f, 0f, 4f), mats.water);
            var market = Spot("Market", new Vector3(-4f, 0f, 4f), mats.market);
            var garden = Spot("Garden", new Vector3(-8f, 0f, -5f), mats.garden);
            var villager = Agent("Villager", new Vector3(-8f, 0f, 0f), mats.villager, villagerTree,
                (vk.SpotA, well), (vk.SpotB, market), (vk.SpotC, garden));

            // ---- GOAP side
            var axe = Spot("Axe Rack", new Vector3(4f, 0f, 5f), mats.axe);
            var treeSpot = Spot("Tree", new Vector3(12f, 0f, 5f), mats.garden);
            var trunk = Cylinder(treeSpot, "Trunk", new Vector3(0f, 1f, 1.2f), new Vector3(0.6f, 1f, 0.6f), mats.trunk);
            Sphere(trunk.transform.parent.gameObject, "Leaves", new Vector3(0f, 2.6f, 1.2f), 2.2f, mats.leaves);
            var stockpile = Spot("Stockpile", new Vector3(4f, 0f, -5f), mats.trunk);
            var food = Spot("Food", new Vector3(12f, 0f, -5f), mats.food);
            var woodcutter = Agent("Woodcutter", new Vector3(8f, 0f, 0f), mats.woodcutter, woodTree,
                (wk.AxeSpot, axe), (wk.TreeSpot, treeSpot), (wk.StockpileSpot, stockpile), (wk.FoodSpot, food));

            var sign = Label(stockpile, "Stockpile: 0", new Vector3(0f, 0.05f, -1.4f), 0.22f, new Color(1f, 0.85f, 0.4f));
            var pile = stockpile.AddComponent<WoodStockpile>();
            Set(pile, "woodcutter", woodcutter.GetComponent<BehaviorTreeRunner>());
            Set(pile, "label", sign);

            // ---- Speech bubbles for Story "says"
            var speech = new GameObject("Story Speech").AddComponent<StorySpeechRenderer>();
            Set(speech, "targetCamera", cam);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, removeScene: true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PluginShowcaseBuilder] Built {ScenePath} (not opened). Open it and press Play; open {VillagerTreePath} / {WoodcutterTreePath} with Live Debug to watch them think.");
        }

        private static GameObject Spot(string name, Vector3 position, Material material)
        {
            var spot = new GameObject(name);
            spot.transform.position = position;
            Cylinder(spot, "Pad", new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.02f, 1.6f), material);
            Label(spot, name, new Vector3(0f, 0.05f, -1.1f), 0.2f, Color.white);
            return spot;
        }

        private static GameObject Agent(string name, Vector3 position, Material material, BehaviorTree tree, params (Key key, GameObject target)[] targets)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.position = position + Vector3.up;
            body.GetComponent<Renderer>().sharedMaterial = material;
            Box(body, "Facing", new Vector3(0f, 0.45f, 0.45f), new Vector3(0.3f, 0.15f, 0.3f), material, collider: false);
            var agent = body.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f;
            agent.speed = 3.5f;
            agent.angularSpeed = 540f;
            agent.acceleration = 16f;
            agent.radius = 0.4f;
            var runner = body.AddComponent<BehaviorTreeRunner>();
            runner.treeAsset = tree;
            runner.overrides = targets.Select(t => new KeyOverride { keyName = t.key.keyName, data = new OverrideDataGameObject { value = t.target } }).ToList();
            EditorUtility.SetDirty(runner);
            return body;
        }

        // ================================================================== tree helpers

        private static BehaviorTree NewTree(string path, out Blackboard blackboard)
        {
            string bbPath = path.Replace(".asset", "_Blackboard.asset");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(bbPath);
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(Path.GetDirectoryName(Root).Replace('\\', '/'), Path.GetFileName(Root));

            var tree = ScriptableObject.CreateInstance<BehaviorTree>();
            AssetDatabase.CreateAsset(tree, path);
            tree.EditorInit();
            blackboard = ScriptableObject.CreateInstance<Blackboard>();
            AssetDatabase.CreateAsset(blackboard, bbPath);
            tree.blackboard = blackboard;
            EditorUtility.SetDirty(tree);
            GraphHistory.For(tree).Clear();
            return tree;
        }

        private static T AddKey<T>(Blackboard blackboard, string name) where T : Key
        {
            var key = ScriptableObject.CreateInstance<T>();
            key.name = typeof(T).Name;
            key.keyName = name;
            AssetDatabase.AddObjectToAsset(key, blackboard);
            blackboard.keys.Add(key);
            EditorUtility.SetDirty(blackboard);
            return key;
        }

        private static void Save(BehaviorTree tree, Blackboard blackboard)
        {
            foreach (var node in tree.nodes) if (node != null) EditorUtility.SetDirty(node);
            foreach (var key in blackboard.keys) EditorUtility.SetDirty(key);
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(blackboard);
            AssetDatabase.SaveAssets();
            GraphHistory.For(tree).Clear(); // a fresh asset: nothing to undo
        }

        private static T N<T>(BehaviorTreeGraphService graph, float x, float y, string title = null) where T : Node
        {
            var node = (T)graph.CreateNode(typeof(T), new Vector2(x, y));
            if (title != null) node.name = node.typeName = title;
            return node;
        }

        /// <summary>An action owned by a Block (drawn inside it).</summary>
        private static T Own<T>(BehaviorTreeGraphService graph, StoryBlockNode block, System.Action<T> setup = null) where T : Node
        {
            var node = (T)graph.CreateNode(typeof(T), block.position.position + new Vector2(0f, 60f + 40f * block.children.Count));
            graph.Connect(block, node, allowOwned: true);
            setup?.Invoke(node);
            return node;
        }

        private static KeyCondition Cond(Key key, ComparisonType op, bool b = false, int i = 0) =>
            new KeyCondition { key = key, comparison = op, value = new GoapValue { boolValue = b, intValue = i } };

        private static KeyEffect Set(Key key, bool b = false, int i = 0) =>
            new KeyEffect { key = key, operation = GoapEffectOperation.Set, value = new GoapValue { boolValue = b, intValue = i } };

        private static KeyEffect Add(Key key, int amount) =>
            new KeyEffect { key = key, operation = GoapEffectOperation.Add, value = new GoapValue { intValue = amount } };

        // ================================================================== scene helpers

        private sealed class Materials
        {
            public readonly Material ground = Make("Showcase_Ground", new Color(0.33f, 0.42f, 0.3f));
            public readonly Material divider = Make("Showcase_Divider", new Color(0.9f, 0.9f, 0.9f));
            public readonly Material water = Make("Showcase_Water", new Color(0.25f, 0.55f, 0.9f));
            public readonly Material market = Make("Showcase_Market", new Color(0.9f, 0.6f, 0.2f));
            public readonly Material garden = Make("Showcase_Garden", new Color(0.35f, 0.75f, 0.35f));
            public readonly Material axe = Make("Showcase_Axe", new Color(0.6f, 0.6f, 0.65f));
            public readonly Material trunk = Make("Showcase_Trunk", new Color(0.45f, 0.3f, 0.18f));
            public readonly Material leaves = Make("Showcase_Leaves", new Color(0.2f, 0.55f, 0.25f));
            public readonly Material food = Make("Showcase_Food", new Color(0.85f, 0.25f, 0.25f));
            public readonly Material villager = Make("Showcase_Villager", new Color(0.3f, 0.6f, 1f));
            public readonly Material woodcutter = Make("Showcase_Woodcutter", new Color(1f, 0.55f, 0.15f));

            private static Material Make(string name, Color color)
            {
                if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Root, "Materials");
                string path = $"{MaterialFolder}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.color = color;
                EditorUtility.SetDirty(mat);
                return mat;
            }
        }

        private static GameObject Primitive(PrimitiveType type, GameObject parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Box(GameObject parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collider) =>
            Primitive(PrimitiveType.Cube, parent, name, localPosition, scale, material, collider);

        private static GameObject Cylinder(GameObject parent, string name, Vector3 localPosition, Vector3 scale, Material material) =>
            Primitive(PrimitiveType.Cylinder, parent, name, localPosition, scale, material, collider: false);

        private static GameObject Sphere(GameObject parent, string name, Vector3 localPosition, float size, Material material) =>
            Primitive(PrimitiveType.Sphere, parent, name, localPosition, Vector3.one * size, material, collider: false);

        private static TextMesh Label(GameObject parent, string text, Vector3 localPosition, float size, Color color)
        {
            var go = new GameObject($"Label ({text})");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // flat on the ground, readable from the camera
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            return mesh;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[PluginShowcaseBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
