using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Level → Create Walk Platform Scene
    ///
    /// Makes a NEW personal dev scene (Scenes/Dev/Platform_&lt;user&gt;.unity) with:
    ///   - a store-sized floor with a checker pattern (easy to see you are moving)
    ///   - 4 walls with an entrance gap, so the player can't fall off
    ///   - a light and the Player prefab at the entrance
    ///
    /// Grey-box only: replace it with real store models later.
    /// Change the size in the constants below.
    /// </summary>
    public static class WalkPlatformTool
    {
        const float Width      = 20f;   // left-right (x), metres
        const float Depth      = 16f;   // front-back (z), metres
        const float WallHeight = 3f;
        const float WallThick  = 0.2f;
        const float DoorWidth  = 2.4f;  // gap in the front wall

        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string MaterialFolder   = "Assets/_Project/Art/Materials";
        const string TextureFolder    = "Assets/_Project/Art/Textures";
        const string DevSceneFolder   = "Assets/_Project/Scenes/Dev";

        [MenuItem("Tools/Level/Create Walk Platform Scene", priority = 10)]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string scenePath = $"{DevSceneFolder}/Platform_{SafeName(Environment.UserName)}.unity";

            if (File.Exists(scenePath) &&
                !EditorUtility.DisplayDialog("Walk Platform",
                    scenePath + " already exists.\nReplace it with a fresh one?", "Replace", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            BuildPlatform();
            SpawnPlayer();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Level] Walk platform scene created: {scenePath}. Press Play and walk around!");
        }

        // ---------- Building ----------

        static void BuildPlatform()
        {
            Material floorMat = GetMaterial("M_Floor_Checker", new Color(0.75f, 0.75f, 0.75f), checker: true);
            Material wallMat  = GetMaterial("M_Wall_Blockout", new Color(0.88f, 0.86f, 0.80f), checker: false);

            var root = new GameObject("WalkPlatform");

            // Floor: top surface at y = 0
            Block("Floor", root.transform,
                new Vector3(0f, -0.1f, 0f), new Vector3(Width, 0.2f, Depth), floorMat);

            float halfW = Width / 2f, halfD = Depth / 2f, y = WallHeight / 2f;

            // Back, left and right walls
            Block("Wall_Back", root.transform,
                new Vector3(0f, y, halfD), new Vector3(Width, WallHeight, WallThick), wallMat);
            Block("Wall_Left", root.transform,
                new Vector3(-halfW, y, 0f), new Vector3(WallThick, WallHeight, Depth), wallMat);
            Block("Wall_Right", root.transform,
                new Vector3(halfW, y, 0f), new Vector3(WallThick, WallHeight, Depth), wallMat);

            // Front wall in two parts, with the entrance gap in the middle
            float sideLen = (Width - DoorWidth) / 2f;
            float sideX = DoorWidth / 2f + sideLen / 2f;
            Block("Wall_Front_L", root.transform,
                new Vector3(-sideX, y, -halfD), new Vector3(sideLen, WallHeight, WallThick), wallMat);
            Block("Wall_Front_R", root.transform,
                new Vector3(sideX, y, -halfD), new Vector3(sideLen, WallHeight, WallThick), wallMat);

            // Empty marker where the entrance is (useful for the "Lock the door" task later)
            var door = new GameObject("EntrancePoint");
            door.transform.SetParent(root.transform, false);
            door.transform.position = new Vector3(0f, 0f, -halfD);

            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
            foreach (Transform t in root.transform)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(root));
        }

        static void Block(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Level] Player prefab not found at " + PlayerPrefabPath +
                                 ". Drag your player into the scene by hand.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Just inside the entrance, looking into the store
            player.transform.position = new Vector3(0f, 0.05f, -Depth / 2f + 1.5f);
            player.transform.rotation = Quaternion.identity;
        }

        // ---------- Materials ----------

        static Material GetMaterial(string name, Color color, bool checker)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };

            if (checker)
            {
                Texture2D tex = GetCheckerTexture();
                mat.mainTexture = tex;
                // One checker tile = 2 x 2 metres
                mat.mainTextureScale = new Vector2(Width / 2f, Depth / 2f);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Texture2D GetCheckerTexture()
        {
            EnsureFolder(TextureFolder);
            string path = $"{TextureFolder}/T_Checker.asset";

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;

            const int size = 16;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // crisp pixels (fits the PS1 look)
                wrapMode = TextureWrapMode.Repeat,
                name = "T_Checker"
            };

            Color light = Color.white, dark = new Color(0.78f, 0.78f, 0.78f);
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                    tex.SetPixel(x, z, (x < size / 2) == (z < size / 2) ? light : dark);
            tex.Apply();

            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // ---------- Helpers ----------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "Me" : s.Replace(' ', '_');
        }
    }
}
