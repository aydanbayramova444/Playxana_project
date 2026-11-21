using System.Collections.Generic;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Main Menu → Create Cutscene Scene
    ///   Makes Scenes/Cutscene.unity that plays Videos/Cutscene_Intro.mp4 full screen and then loads the game,
    ///   and wires the order:  MainMenu → (Play) → Cutscene → user.
    ///   - Build Profiles: MainMenu, Cutscene, then the rest.
    ///   - MainMenu's "Game Scene" field = Cutscene.
    ///   Safe to run again.
    /// </summary>
    public static class CutsceneSceneTool
    {
        const string ScenePath    = "Assets/_Project/Scenes/Cutscene.unity";
        const string MenuScene    = "Assets/_Project/Scenes/MainMenu.unity";
        const string VideoPath    = "Assets/_Project/Videos/Cutscene_Intro.mp4";
        const string GameScene    = "user"; // scene loaded after the cutscene

        [MenuItem("Tools/Main Menu/Create Cutscene Scene", priority = 10)]
        public static void CreateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        /// <summary>-executeMethod Game.EditorTools.CutsceneSceneTool.Create</summary>
        public static void Create()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (clip == null) Debug.LogWarning($"[Cutscene] Video not found at {VideoPath}.");

            // ---- 1. The cutscene scene: black camera + video player
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            var cutscene = new GameObject("Cutscene");
            var video = cutscene.AddComponent<VideoPlayer>();
            video.clip = clip;
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.CameraNearPlane;
            video.targetCamera = cam;
            video.aspectRatio = VideoAspectRatio.FitInside;
            video.audioOutputMode = VideoAudioOutputMode.Direct;

            var player = cutscene.AddComponent<CutscenePlayer>();
            var so = new SerializedObject(player);
            so.FindProperty("nextScene").stringValue = GameScene;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // ---- 2. Build order: MainMenu, Cutscene, then everything else as it was
            var scenes = new List<EditorBuildSettingsScene>();
            void Add(string path)
            {
                if (scenes.Exists(s => s.path == path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            Add(MenuScene);
            Add(ScenePath);
            foreach (var s in EditorBuildSettings.scenes) Add(s.path);
            EditorBuildSettings.scenes = scenes.ToArray();

            // ---- 3. Main menu Play → Cutscene
            var menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            foreach (var m in Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mso = new SerializedObject(m);
                mso.FindProperty("gameScene").stringValue = "Cutscene";
                mso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(m);
            }
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            AssetDatabase.SaveAssets();
            Debug.Log("[Cutscene] Created Scenes/Cutscene.unity. Order: MainMenu → Cutscene → " + GameScene +
                      ". Open MainMenu and press Play.");
        }
    }
}
