using System.IO;
using System.Text.RegularExpressions;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Menu: Tools → Project Setup
    ///   1. Create Main Scene + Player — run ONCE by one person, then commit.
    ///   2. Create My Dev Scene        — each teammate runs it to get a personal sandbox
    ///                                   (copy of Main named after the OS user).
    /// </summary>
    public static class ProjectSetupTool
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string DevScenesDir = ScenesDir + "/Dev";
        private const string MainScenePath = ScenesDir + "/Main.unity";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";

        [MenuItem("Tools/Project Setup/1. Create Main Scene + Player", priority = 1)]
        public static void CreateMainScene()
        {
            if (File.Exists(MainScenePath) &&
                !EditorUtility.DisplayDialog("Main scene already exists",
                    $"{MainScenePath} already exists. Overwrite it?", "Overwrite", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(PrefabsDir);

            // Default scene = Main Camera + Directional Light. The camera lives inside the Player
            // prefab (first person), so the default one is removed.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Camera>() != null)
                    Object.DestroyImmediate(go);

            // Ground + a few boxes to have a sense of scale and something to jump on.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            var level = new GameObject("Level");
            ground.transform.SetParent(level.transform, true);
            CreateBox(level.transform, new Vector3(4f, 0.5f, 4f), Vector3.one);
            CreateBox(level.transform, new Vector3(-5f, 1f, 6f), new Vector3(2f, 2f, 2f));
            CreateBox(level.transform, new Vector3(6f, 0.25f, -3f), new Vector3(3f, 0.5f, 3f));

            // Player prefab + instance.
            var playerPrefab = CreatePlayerPrefab();
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log($"[ProjectSetup] Created {MainScenePath} and {PlayerPrefabPath}. Main scene added to Build Profiles.");
        }

        [MenuItem("Tools/Project Setup/2. Create My Dev Scene", priority = 2)]
        private static void CreateMyDevScene()
        {
            if (!File.Exists(MainScenePath))
            {
                EditorUtility.DisplayDialog("No Main scene",
                    "Main scene not found. Run 'Tools/Project Setup/1. Create Main Scene + Player' first " +
                    "(or pull the latest main branch).", "OK");
                return;
            }

            string userName = Regex.Replace(System.Environment.UserName, "[^A-Za-z0-9_-]", "");
            if (string.IsNullOrEmpty(userName)) userName = "Dev";
            string path = $"{DevScenesDir}/{userName}.unity";

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(DevScenesDir);
                AssetDatabase.Refresh();
                if (!AssetDatabase.CopyAsset(MainScenePath, path))
                {
                    Debug.LogError($"[ProjectSetup] Failed to copy {MainScenePath} to {path}");
                    return;
                }
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);

            Debug.Log($"[ProjectSetup] Your dev scene: {path}");
        }

        private static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;
            root.AddComponent<PlayerController>();

            // Visuals only — collision is handled by the CharacterController.
            // Only casts a shadow: the first-person camera sits inside it.
            var body = CreateVisual(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f));
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            // First-person camera at eye height.
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<FirstPersonLook>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateVisual(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            return go;
        }

        private static void CreateBox(Transform parent, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
        }
    }
}
