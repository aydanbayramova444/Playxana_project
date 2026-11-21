using System.Collections.Generic;
using Game.Level2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using S = Game.EditorTools.SurroundingsBuilderTool;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Level 2 → Build Level 2 Scene (run to the bomb shelter)
    ///   Scenes/Level2.unity: a 200 m apocalypse street, 12 chaotic cars, wrecks and barriers,
    ///   "УБЕЖИЩЕ ↑" road signs, the bomb shelter at the end, Level2Manager (60 s, 4 hits = run over).
    ///   Also adds the scene to Build Profiles.
    /// Tools → Level 2 → Add Store Exit To Open Scene
    ///   Door trigger in the store: with the full shopping list, walking out loads Level 2.
    /// Both are safe to run again.
    /// </summary>
    public static class Level2BuilderTool
    {
        const string ScenePath   = "Assets/_Project/Scenes/Level2.unity";
        const string PlayerPath  = "Assets/_Project/Prefabs/Player.prefab";
        const string EnvTex      = "Assets/_Project/Art/Textures/Environment";
        const string StoreTex    = "Assets/_Project/Art/Textures/Store";
        const string MatFolder   = "Assets/_Project/Art/Materials/Environment";
        const string StoreScene  = "Assets/_Project/Scenes/Dev/user.unity";

        const float RoadStart = -15f, ShelterZ = 205f, HalfRoad = 5f, SidewalkOuter = 8f;

        [MenuItem("Tools/Level 2/Build Level 2 Scene (run to the shelter)", priority = 80)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Tools/Level 2/Add Store Exit To Open Scene", priority = 81)]
        public static void AddStoreExitMenu() => AddStoreExit(EditorSceneManager.GetActiveScene());

        /// <summary>-executeMethod Game.EditorTools.Level2BuilderTool.BuildAllFromCommandLine</summary>
        public static void BuildAllFromCommandLine()
        {
            Build();
            AddStoreExit(EditorSceneManager.OpenScene(StoreScene));
        }

        // ------------------------------------------------------------------ Level 2 scene

        public static void Build()
        {
            AssetDatabase.Refresh();
            S.EnsureFolder(MatFolder);
            S.SetUpTextures();
            SetUpSignTextures();
            S.Mats.Clear();
            Random.InitState(2026); // same layout every time it is built

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            var root = new GameObject("Level2").transform;
            BuildStreet(S.Group(root, "Street"));
            BuildBuildings(S.Group(root, "Buildings"));
            BuildObstacles(S.Group(root, "Obstacles"));
            BuildSigns(S.Group(root, "Signs"));
            BuildShelter(S.Group(root, "Shelter"));
            BuildMovingCars(S.Group(root, "ChaosCars"));
            BuildBounds(S.Group(root, "InvisibleWalls"));
            S.SetUpSkyAndLight();

            // Player on the sidewalk in front of the store, looking up the street
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.transform.SetPositionAndRotation(new Vector3(6.5f, 0.15f, -6f), Quaternion.identity);
            }
            else Debug.LogWarning("[Level2] Player prefab not found at " + PlayerPath);

            new GameObject("Level2Manager").AddComponent<Level2Manager>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath);
            Debug.Log("[Level2] Built " + ScenePath + ". Run to the shelter: 60 s, 4 hits = game over.");
        }

        static void BuildStreet(Transform p)
        {
            float len = ShelterZ - RoadStart, mid = (ShelterZ + RoadStart) / 2f;
            S.Box(p, "Ground", new Vector3(0, -0.12f, mid), new Vector3(90, 0.2f, len + 60), S.M("Ground", 0.5f));
            S.Box(p, "Road", new Vector3(0, -0.05f, mid), new Vector3(HalfRoad * 2, 0.1f, len), S.M("Asphalt", 0.7f));
            float sw = SidewalkOuter - HalfRoad;
            foreach (float side in new[] { -1f, 1f })
                S.Box(p, "Sidewalk", new Vector3(side * (HalfRoad + sw / 2f), 0.06f, mid), new Vector3(sw, 0.12f, len), S.M("Sidewalk", 1f));

            var paint = S.M("RoadPaint", 2f);
            for (float z = RoadStart + 2f; z < ShelterZ - 3f; z += 5f)
                S.NoCollider(S.Box(p, "LaneDash", new Vector3(0, 0.003f, z), new Vector3(0.15f, 0.01f, 2.2f), paint));
        }

        static void BuildBuildings(Transform p)
        {
            Material panel = S.M("PanelBlock", 1f / 6f), roof = S.M("Roof", 0.5f), brick = S.M("Brick", 2f),
                     garage = S.M("Garage", 1f), fence = S.M("ConcreteFence", 0.5f);

            // The store you just left, behind the start
            S.Building(p, "Store_Back", new Vector3(0, 0, -21f), new Vector3(16, 3.4f, 8), S.M("Sidewalk", 1f), roof);
            var sign = S.Box(p, "Store_Sign", new Vector3(0, 2.9f, -16.97f), new Vector3(2.4f, 0.4f, 0.04f),
                SignMat("M_StoreSign_UV", $"{StoreTex}/T_StoreSign.png"));
            S.NoCollider(sign).transform.rotation = Quaternion.Euler(0, 180, 0); // faces the player at the start

            // Both sides of the street: a mix of panel blocks, brick houses, garages and fence
            foreach (float side in new[] { -1f, 1f })
            {
                float z = RoadStart + 5f;
                int i = side < 0 ? 0 : 2;
                while (z < ShelterZ - 15f)
                {
                    int kind = i++ % 4;
                    float x = side * (SidewalkOuter + 6.5f);
                    if (kind == 0)
                    {
                        S.Building(p, "PanelBlock", new Vector3(x, 0, z + 12f), new Vector3(10, 15, 24), panel, roof);
                        z += 30f;
                    }
                    else if (kind == 1)
                    {
                        S.Building(p, "BrickHouse", new Vector3(x, 0, z + 7f), new Vector3(10, 7, 14), brick, roof);
                        z += 19f;
                    }
                    else if (kind == 2)
                    {
                        for (int g = 0; g < 4; g++)
                            S.Building(p, "Garage", new Vector3(side * (SidewalkOuter + 4f), 0, z + 2f + g * 3.8f),
                                new Vector3(6, 2.6f, 3.6f), garage, roof);
                        z += 18f;
                    }
                    else
                    {
                        S.Box(p, "Fence", new Vector3(side * (SidewalkOuter + 0.4f), 1.1f, z + 6f), new Vector3(0.15f, 2.2f, 12f), fence);
                        z += 14f;
                    }
                }
            }

            // A few pines between buildings
            Mesh pine = S.PineMesh();
            Material bark = S.M("Bark", 2f), needles = S.M("Pine", 1.5f);
            for (int t = 0; t < 10; t++)
            {
                float side = t % 2 == 0 ? -1f : 1f;
                var go = new GameObject($"Pine_{t}");
                go.transform.SetParent(p, false);
                go.transform.position = new Vector3(side * Random.Range(SidewalkOuter + 1f, SidewalkOuter + 2.5f), 0,
                    Random.Range(RoadStart + 10f, ShelterZ - 20f));
                go.transform.localScale = Vector3.one * Random.Range(1.1f, 1.7f);
                go.AddComponent<MeshFilter>().sharedMesh = pine;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, needles };
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.15f; col.height = 4f; col.center = new Vector3(0, 2f, 0);
                go.isStatic = true;
            }
        }

        /// <summary>Wrecked cars and concrete barriers to run around (they don't move).</summary>
        static void BuildObstacles(Transform p)
        {
            string[] paints = { "CarRed", "CarBlue", "CarWhite" };
            var wrecks = new[]
            {
                (new Vector3(-6.5f, 0, 30f), 10f), (new Vector3(3.5f, 0, 58f), 160f), (new Vector3(6.2f, 0, 92f), 85f),
                (new Vector3(-2f, 0, 120f), 40f), (new Vector3(-6.4f, 0, 150f), 175f), (new Vector3(2.5f, 0, 176f), 300f),
            };
            for (int i = 0; i < wrecks.Length; i++)
                S.Car(p, wrecks[i].Item1, wrecks[i].Item2, paints[i % 3]).name = "WreckedCar";

            Material hazard = S.M("Hazard", 2f);
            foreach (var pos in new[] { new Vector3(0.5f, 0.45f, 45f), new Vector3(-3.5f, 0.45f, 105f), new Vector3(4f, 0.45f, 135f), new Vector3(-1f, 0.45f, 165f) })
                S.Box(p, "Barrier", pos, new Vector3(2.2f, 0.9f, 0.5f), hazard).transform.rotation =
                    Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
        }

        static void BuildSigns(Transform p)
        {
            Material metal = S.M("PoleMetal", 1f);
            Material arrow = SignMat("M_ArrowSign_UV", $"{EnvTex}/T_ArrowSign.png");
            for (float z = 15f; z < ShelterZ - 10f; z += 48f)
            {
                float x = HalfRoad + 1.2f;
                S.Box(p, "SignPole", new Vector3(x, 1.3f, z), new Vector3(0.08f, 2.6f, 0.08f), metal);
                S.NoCollider(S.Box(p, "Sign_Shelter", new Vector3(x, 2.5f, z - 0.06f), new Vector3(1.1f, 0.55f, 0.03f), arrow));
            }
        }

        static void BuildShelter(Transform p)
        {
            Material concrete = S.M("Sidewalk", 1f), door = SignMat("M_BlastDoor_UV", $"{EnvTex}/T_BlastDoor.png"),
                     sign = SignMat("M_ShelterSign_UV", $"{EnvTex}/T_ShelterSign.png"), hazard = S.M("Hazard", 2f);

            float front = ShelterZ + 3f;
            S.Box(p, "Bunker", new Vector3(0, 1.75f, front + 4f), new Vector3(16, 3.5f, 8), concrete);
            S.Box(p, "Bunker_Roof", new Vector3(0, 3.7f, front + 4f), new Vector3(17, 0.4f, 9), S.M("Ground", 0.5f));
            S.NoCollider(S.Box(p, "BlastDoor", new Vector3(0, 1.2f, front - 0.05f), new Vector3(2.4f, 2.4f, 0.1f), door));
            S.NoCollider(S.Box(p, "Sign_Ubezhishche", new Vector3(0, 2.95f, front - 0.06f), new Vector3(3.2f, 0.8f, 0.04f), sign));
            foreach (float x in new[] { -2.6f, 2.6f })
                S.Box(p, "EntranceBarrier", new Vector3(x, 0.5f, front - 1.5f), new Vector3(0.4f, 1f, 2.5f), hazard);

            // Walk into the doorway = saved
            var goal = new GameObject("ShelterGoal");
            goal.transform.SetParent(p, false);
            goal.transform.position = new Vector3(0, 1.3f, front - 0.8f);
            var box = goal.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.6f, 2.6f, 1.6f);
            goal.AddComponent<Rigidbody>().isKinematic = true; // so the CharacterController triggers it reliably
            goal.AddComponent<ShelterGoal>();

            // Glow over the door so it's easy to spot in the haze
            var l = new GameObject("ShelterLight").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.position = new Vector3(0, 3.3f, front - 1.5f);
            l.type = LightType.Point;
            l.color = new Color(0.5f, 1f, 0.55f);
            l.intensity = 3f;
            l.range = 9f;
        }

        static void BuildMovingCars(Transform p)
        {
            string[] paints = { "CarRed", "CarBlue", "CarWhite" };
            float[] lanes = { -3.5f, -1.5f, 1.5f, 3.5f };
            for (int i = 0; i < 12; i++)
            {
                float z = Mathf.Lerp(28f, ShelterZ - 12f, i / 11f) + Random.Range(-4f, 4f);
                float yaw = Random.value < 0.5f ? 0f : 180f;
                Transform car = S.Car(p, new Vector3(lanes[i % lanes.Length], 0, z), yaw, paints[i % 3]);
                car.name = $"ChaosCar_{i + 1}";
                foreach (var t in car.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false; // they move!

                car.gameObject.AddComponent<Rigidbody>().isKinematic = true;
                var hit = car.gameObject.AddComponent<BoxCollider>(); // bigger trigger = hit zone
                hit.isTrigger = true;
                hit.size = new Vector3(2.3f, 1.8f, 4.8f);
                hit.center = new Vector3(0, 0.9f, 0);

                var chaos = car.gameObject.AddComponent<ChaosCar>();
                chaos.minZ = RoadStart;
                chaos.maxZ = ShelterZ - 8f;
                chaos.chaos = Random.Range(0.4f, 0.9f);
            }
        }

        static void BuildBounds(Transform p)
        {
            void Wall(string name, Vector3 c, Vector3 s)
            {
                var go = new GameObject(name);
                go.transform.SetParent(p, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            float len = ShelterZ - RoadStart + 20f, mid = (ShelterZ + RoadStart) / 2f;
            Wall("Bound_Left", new Vector3(-SidewalkOuter - 0.3f, 3, mid), new Vector3(0.5f, 6, len));
            Wall("Bound_Right", new Vector3(SidewalkOuter + 0.3f, 3, mid), new Vector3(0.5f, 6, len));
            Wall("Bound_Back", new Vector3(0, 3, -16.8f), new Vector3(20, 6, 0.5f));
        }

        // ------------------------------------------------------------------ Level 1 door

        public static void AddStoreExit(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("StoreExit");
            if (old != null) Object.DestroyImmediate(old);

            // Just outside the store door (store front wall at z = -1, door 2.2 m wide)
            var exit = new GameObject("StoreExit");
            exit.transform.position = new Vector3(0, 1.2f, -1.9f);
            var box = exit.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.4f, 2.4f, 0.8f);
            exit.AddComponent<Rigidbody>().isKinematic = true;
            exit.AddComponent<StoreExit>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Level2] Store exit added to {scene.path}: with the full list, walk out of the door → Level 2.");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Material that uses the cube's own UVs (for signs/doors: the picture shows once on the face).</summary>
        static Material SignMat(string name, string texPath)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Game/PS1 Lit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Triplanar")) { mat.SetFloat("_Triplanar", 0f); mat.DisableKeyword("_TRIPLANAR"); }
            if (mat.HasProperty("_AmbientBoost")) mat.SetFloat("_AmbientBoost", 1.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetUpSignTextures()
        {
            foreach (string t in new[] { $"{StoreTex}/T_StoreSign.png" })
                if (AssetImporter.GetAtPath(t) is TextureImporter ti && (ti.filterMode != FilterMode.Point || ti.mipmapEnabled))
                {
                    ti.filterMode = FilterMode.Point;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
        }

        static void AddToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
