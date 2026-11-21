using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Store → Add Shopper Bots + NavMesh
    ///   1. Bakes a NavMesh (walkable floor) for the open scene: store + street (Farid's NPCs need it).
    ///      Shelves are built on Play, so they cut themselves out of it at runtime (NavMeshObstacle).
    ///   2. Adds an exit point on the sidewalk outside the store door.
    ///   3. Puts 3 Robot Kyle shoppers (Farid's ShopperNPC) in the aisles.
    ///   Safe to run again: "StoreNavMesh", "NPC_Exit" and "ShopperBots" are rebuilt.
    /// </summary>
    public static class ShopperBotsTool
    {
        const string RobotModel = "Assets/UMotionExamples/Miscellaneous/Robot Kyle/Model/Robot Kyle.fbx";
        const string IdleController = "Assets/UMotionExamples/Miscellaneous/AnimationControllers/RobotKyleIdleController.controller";
        const string NavFolder = "Assets/_Project/Scenes/NavMesh";
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        static readonly Vector3[] Spawns =
        {
            new Vector3(-3.3f, 0, 6.6f),  // back aisle, left
            new Vector3(3.3f, 0, 6.6f),   // back aisle, right
            new Vector3(-6f, 0, 2.5f),    // left side aisle
        };

        [MenuItem("Tools/Store/Add Shopper Bots + NavMesh", priority = 73)]
        public static void SetupMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene &&
                !EditorUtility.DisplayDialog("Shopper bots", "This edits the shared Main.unity scene. Tell your team first.",
                    "Add to Main", "Cancel"))
                return;
            Setup();
        }

        /// <summary>-executeMethod Game.EditorTools.ShopperBotsTool.SetupFromCommandLine -npcScene path</summary>
        public static void SetupFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-npcScene");
            if (i >= 0 && i + 1 < args.Length) EditorSceneManager.OpenScene(args[i + 1]);
            Setup();
        }

        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (string n in new[] { "StoreNavMesh", "NPC_Exit", "ShopperBots" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            // ---- 1. NavMesh from all colliders in the scene
            var navGo = new GameObject("StoreNavMesh");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EnsureFolder(NavFolder);
            string navPath = $"{NavFolder}/{scene.name}_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

            // ---- 2. Exit point: sidewalk outside the door
            var exit = new GameObject("NPC_Exit").transform;
            exit.position = Snap(new Vector3(0, 0, -3f));

            // ---- 3. Bots
            var botsRoot = new GameObject("ShopperBots").transform;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RobotModel);
            var idle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleController);
            if (model == null) Debug.LogWarning("[Bots] Robot Kyle model not found — bots will be capsules.");

            for (int i = 0; i < Spawns.Length; i++)
            {
                var bot = new GameObject($"Shopper_{i + 1}");
                bot.transform.SetParent(botsRoot, false);
                bot.transform.SetPositionAndRotation(Snap(Spawns[i]), Quaternion.Euler(0, i * 120f, 0));

                var agent = bot.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 1.8f;
                agent.speed = 2f;
                agent.angularSpeed = 360f;

                var col = bot.AddComponent<CapsuleCollider>(); // the player bumps into them
                col.radius = 0.3f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);

                var npc = bot.AddComponent<ShopperNPC>();
                npc.exitPoint = exit;
                npc.wantsItems = 3;

                AddBody(bot.transform, model, idle);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bots] NavMesh baked ({navPath}), exit point + {Spawns.Length} shopper bots added. Press Play.");
        }

        /// <summary>Robot Kyle (1.8 m tall, idle animation) or a capsule if the model is missing.</summary>
        static void AddBody(Transform bot, GameObject model, RuntimeAnimatorController idle)
        {
            GameObject body;
            if (model != null)
            {
                body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.transform.SetParent(bot, false);

                var bounds = new Bounds(body.transform.position, Vector3.zero);
                foreach (var r in body.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                if (bounds.size.y > 0.01f) body.transform.localScale *= 1.8f / bounds.size.y;

                var anim = body.GetComponent<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>(); // (?? doesn't work with Unity's fake null)
                anim.runtimeAnimatorController = idle;
                anim.applyRootMotion = false; // the NavMeshAgent moves the bot
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(bot, false);
                body.transform.localPosition = new Vector3(0, 0.9f, 0);
                body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            }
            body.name = "Body";
            foreach (var c in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        static Vector3 Snap(Vector3 p) =>
            NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
