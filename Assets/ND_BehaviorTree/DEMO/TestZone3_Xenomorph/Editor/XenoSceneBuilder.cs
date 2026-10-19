using System.IO;
using ND_BehaviorTree.Demo.Xeno;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ND_BehaviorTree.Demo.Xeno.EditorTools
{
    /// <summary>
    /// Builds TestZone3's scene: a small station (4 rooms), lockers, vents, the player, the director and the alien,
    /// all wired, with a baked NavMesh. Re-run it to rebuild the scene from scratch (it asks before overwriting).
    /// </summary>
    public static class XenoSceneBuilder
    {
        private const string Root = "Assets/ND_BehaviorTree/DEMO/TestZone3_Xenomorph";
        private const string ScenePath = Root + "/XenoTest.unity";
        private const string NavMeshPath = Root + "/XenoTest_NavMesh.asset";
        private const string MaterialFolder = Root + "/Materials";
        private const string TreePath = Root + "/Xenomorph.asset";

        [MenuItem("ND_BehaviorTree/Demo/Build Xenomorph Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[XenoSceneBuilder] Exit Play mode first.");
                return;
            }
            var tree = AssetDatabase.LoadAssetAtPath<BehaviorTree>(TreePath);
            if (tree == null)
            {
                Debug.LogError($"[XenoSceneBuilder] Behavior tree not found at {TreePath}.");
                return;
            }
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Xenomorph test scene", $"{ScenePath} exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = new Materials();

            // ---------------- Camera & light
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 19.5f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 40f, 0f), Quaternion.Euler(90f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

            // ---------------- Level (everything under it is baked into the NavMesh)
            var level = new GameObject("Level");
            Box(level, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(36f, 0.5f, 36f), mats.floor);

            // Outer walls
            Wall(level, new Vector3(0f, 0f, 18f), new Vector3(36.5f, 0f, 0.5f), mats.wall);
            Wall(level, new Vector3(0f, 0f, -18f), new Vector3(36.5f, 0f, 0.5f), mats.wall);
            Wall(level, new Vector3(18f, 0f, 0f), new Vector3(0.5f, 0f, 36.5f), mats.wall);
            Wall(level, new Vector3(-18f, 0f, 0f), new Vector3(0.5f, 0f, 36.5f), mats.wall);
            // Horizontal wall at z = 4 with a door between x = -4 and 2
            Wall(level, new Vector3(-11f, 0f, 4f), new Vector3(14f, 0f, 0.5f), mats.wall);
            Wall(level, new Vector3(10f, 0f, 4f), new Vector3(16f, 0f, 0.5f), mats.wall);
            // Vertical wall at x = 0 (south half) with a door between z = -6 and -2
            Wall(level, new Vector3(0f, 0f, -12f), new Vector3(0.5f, 0f, 12f), mats.wall);
            Wall(level, new Vector3(0f, 0f, 1f), new Vector3(0.5f, 0f, 6f), mats.wall);
            // Vertical wall at x = -8 (north half) with a door between z = 10 and 13
            Wall(level, new Vector3(-8f, 0f, 7f), new Vector3(0.5f, 0f, 6f), mats.wall);
            Wall(level, new Vector3(-8f, 0f, 15.5f), new Vector3(0.5f, 0f, 5f), mats.wall);

            // Cover
            Vector3[] crates = { new(-12f, 0f, -8f), new(-5f, 0f, -13f), new(8f, 0f, -8f), new(12f, 0f, -2f), new(5f, 0f, 10f), new(11f, 0f, 13f), new(-13f, 0f, 9f), new(-3f, 0f, 14f) };
            for (int i = 0; i < crates.Length; i++)
                Box(level, $"Crate_{i}", crates[i] + Vector3.up * 0.75f, new Vector3(1.6f, 1.5f, 1.6f), mats.crate);

            // Lockers (front = the direction the stand point is in)
            var lockers = new GameObject("Lockers");
            lockers.transform.SetParent(level.transform);
            Locker(lockers, "Locker_SW", new Vector3(-17.3f, 0f, -3f), Vector3.right, mats);
            Locker(lockers, "Locker_S", new Vector3(-6f, 0f, -17.3f), Vector3.forward, mats);
            Locker(lockers, "Locker_SE", new Vector3(17.3f, 0f, -10f), Vector3.left, mats);
            Locker(lockers, "Locker_E", new Vector3(6f, 0f, 3.3f), Vector3.back, mats);
            Locker(lockers, "Locker_NE", new Vector3(15f, 0f, 17.3f), Vector3.back, mats);
            Locker(lockers, "Locker_NW", new Vector3(-17.3f, 0f, 14f), Vector3.right, mats);
            Locker(lockers, "Locker_N", new Vector3(-7.3f, 0f, 8f), Vector3.right, mats);

            // ---------------- NavMesh
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);

            // ---------------- Vents (not baked: no colliders)
            var vents = new GameObject("Vents");
            Vector3[] ventPositions = { new(-15f, 0f, -15f), new(15f, 0f, -15f), new(-15f, 0f, 16f), new(15f, 0f, 15f), new(-4f, 0f, 10f), new(-3f, 0f, -2f) };
            for (int i = 0; i < ventPositions.Length; i++)
            {
                var vent = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vent.name = $"Vent_{i}";
                Object.DestroyImmediate(vent.GetComponent<Collider>());
                vent.transform.SetParent(vents.transform);
                vent.transform.position = ventPositions[i] + Vector3.up * 0.02f;
                vent.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
                vent.GetComponent<Renderer>().sharedMaterial = mats.vent;
                vent.AddComponent<XenoVent>();
            }

            // ---------------- Director
            var director = new GameObject("XenoDirector").AddComponent<XenoDirector>();

            // ---------------- Player
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetPositionAndRotation(new Vector3(12f, 1.05f, -14f), Quaternion.identity); // capsule center height
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = spawn.position;
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.GetComponent<Renderer>().sharedMaterial = mats.player;
            var controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.zero;
            controller.height = 2f;
            controller.radius = 0.4f;
            var nose = Box(player, "Facing", new Vector3(0f, 0.4f, 0.45f), new Vector3(0.3f, 0.2f, 0.3f), mats.player, collider: false);
            var flame = Box(player, "Flame", new Vector3(0f, 0f, 3f), new Vector3(1.2f, 0.4f, 5f), mats.flame, collider: false);
            flame.SetActive(false);
            var xenoPlayer = player.AddComponent<XenoPlayer>();

            // ---------------- Alien
            var alien = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            alien.name = "Xenomorph";
            alien.transform.position = ventPositions[2];
            alien.transform.localScale = new Vector3(1f, 1.2f, 1f);
            alien.GetComponent<Renderer>().sharedMaterial = mats.alien;
            var head = Box(alien, "Head", new Vector3(0f, 0.65f, 0.45f), new Vector3(0.45f, 0.25f, 0.8f), mats.alien, collider: false);
            var eye = new GameObject("Eye").transform;
            eye.SetParent(alien.transform, false);
            eye.localPosition = new Vector3(0f, 0.65f, 0.3f);
            var agent = alien.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1.2f;
            agent.height = 2.4f;
            agent.radius = 0.45f;
            agent.speed = 2.2f;
            agent.angularSpeed = 400f;
            agent.acceleration = 14f;
            agent.autoBraking = true;
            var body = alien.AddComponent<XenoBody>();
            var runner = alien.AddComponent<XenoRunner>();

            // ---------------- Wiring
            Set(director, "player", xenoPlayer);
            Set(director, "alien", body);

            Set(xenoPlayer, "controller", controller);
            SetArray(xenoPlayer, "renderers", player.GetComponent<Renderer>(), nose.GetComponent<Renderer>());
            Set(xenoPlayer, "spawnPoint", spawn);
            Set(xenoPlayer, "director", director);
            Set(xenoPlayer, "alien", runner);
            Set(xenoPlayer, "flameVisual", flame);

            Set(body, "agent", agent);
            Set(body, "eye", eye);
            Set(body, "bodyCollider", alien.GetComponent<Collider>());
            SetArray(body, "renderers", alien.GetComponent<Renderer>(), head.GetComponent<Renderer>());
            Set(body, "player", xenoPlayer);
            Set(body, "director", director);

            Set(runner, "body", body);
            runner.treeAsset = tree;
            EditorUtility.SetDirty(runner);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[XenoSceneBuilder] Built {ScenePath}. Press Play; open {TreePath} and turn on Live Debug to watch the alien think.");
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Materials
        {
            public readonly Material floor = Make("Xeno_Floor", new Color(0.32f, 0.33f, 0.35f));
            public readonly Material wall = Make("Xeno_Wall", new Color(0.55f, 0.57f, 0.6f));
            public readonly Material crate = Make("Xeno_Crate", new Color(0.45f, 0.38f, 0.28f));
            public readonly Material locker = Make("Xeno_Locker", new Color(0.2f, 0.3f, 0.45f));
            public readonly Material vent = Make("Xeno_Vent", new Color(0.1f, 0.35f, 0.15f));
            public readonly Material player = Make("Xeno_Player", new Color(0.2f, 0.75f, 1f));
            public readonly Material alien = Make("Xeno_Alien", new Color(0.12f, 0.12f, 0.15f));
            public readonly Material flame = Make("Xeno_Flame", new Color(1f, 0.5f, 0.1f));

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

        private static GameObject Box(GameObject parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void Wall(GameObject level, Vector3 center, Vector3 size, Material material)
        {
            size.y = 2.5f;
            Box(level, "Wall", center + Vector3.up * 1.25f, size, material);
        }

        private static void Locker(GameObject parent, string name, Vector3 position, Vector3 front, Materials mats)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent.transform, false);
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(front));
            var cabinet = Box(root, "Cabinet", new Vector3(0f, 1.1f, 0f), new Vector3(1.1f, 2.2f, 0.9f), mats.locker);
            var hide = new GameObject("HidePoint").transform;
            hide.SetParent(root.transform, false);
            hide.localPosition = new Vector3(0f, 1f, 0f);
            var stand = new GameObject("StandPoint").transform;
            stand.SetParent(root.transform, false);
            stand.localPosition = new Vector3(0f, 0f, 1.3f);
            var spot = root.AddComponent<XenoHidingSpot>();
            Set(spot, "hidePoint", hide);
            Set(spot, "standPoint", stand);
            Set(spot, "doorRenderer", cabinet.GetComponent<Renderer>());
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[XenoSceneBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[XenoSceneBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
