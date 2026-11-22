using System.Collections.Generic;
using System.IO;
using Game.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Shelter → Create Shelter Scene (Level 3)
    ///   Builds Scenes/Shelter.unity: a concrete bomb shelter with a short entrance corridor,
    ///   supply shelves (survival products), cots, crates, flickering lamps, fog and the PS1 look,
    ///   bakes the NavMesh, places the Player and the ShelterDirector, adds the scene to Build Profiles.
    ///   Safe to run again: the scene is rebuilt from scratch (never touches other scenes).
    /// </summary>
    public static class ShelterSceneTool
    {
        const string ScenePath   = "Assets/_Project/Scenes/Shelter.unity";
        const string MatFolder   = "Assets/_Project/Art/Materials/Shelter";
        const string NavFolder   = "Assets/_Project/Scenes/NavMesh";
        const string PlayerPath  = "Assets/_Project/Prefabs/Player.prefab";
        const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        const string VolumePath  = "Assets/_Project/Scenes/Dev/user/Global Volume Profile.asset";
        const string TexEnv      = "Assets/_Project/Art/Textures/Environment";
        const string TexStore    = "Assets/_Project/Art/Textures/Store";

        // Room: x -7..7, z -5..5, 3 m high. Entrance corridor goes out of the front wall (z = -5) to z = -11.
        const float HalfW = 7f, HalfD = 5f, Height = 3f, Wall = 0.3f;
        const float SideDoorZ = -1.5f; // second way in: maintenance tunnel out of the right wall

        // Supplies you carried here from the store: (item id, how many)
        static readonly (string id, int stock, Vector3 pos, float yaw)[] Supplies =
        {
            ("canned_meat", 4, new Vector3(-4.5f, 0, 4.45f), 180f),
            ("water_jug",   3, new Vector3(-1.5f, 0, 4.45f), 180f),
            ("buckwheat",   3, new Vector3( 1.5f, 0, 4.45f), 180f),
            ("first_aid",   2, new Vector3( 4.5f, 0, 4.45f), 180f),
            ("batteries",   2, new Vector3( 6.45f, 0, 1.2f), -90f),
        };

        /// <summary>
        /// Bakes ONLY the floor level (everything below 2 m): with "All" the roofs of the bunker, corridor and
        /// tunnel got NavMesh too, and intruders up there stole supplies unseen through the ceiling.
        /// </summary>
        static void BakeFloorNavMesh(NavMeshSurface surface)
        {
            surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(3f, 0.75f, -3f);    // room x -7..7, tunnel to x 13.3; corridor to z -11.5
            surface.size = new Vector3(22f, 2.5f, 18f);     // y -0.5 .. 2.0: floor, crates and tables, no roofs
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            EnsureFolder(NavFolder);
            string navPath = $"{NavFolder}/Shelter_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        }

        [MenuItem("Tools/Shelter/Rebake NavMesh (Floor Only)", priority = 81)]
        public static void RebakeOpenScene()
        {
            var surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface == null || EditorSceneManager.GetActiveScene().name != "Shelter")
            {
                EditorUtility.DisplayDialog("Shelter", "Open Scenes/Shelter.unity first.", "OK");
                return;
            }
            BakeFloorNavMesh(surface);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            EditorSceneManager.SaveScene(surface.gameObject.scene);
            Debug.Log("[Shelter] NavMesh rebaked: floor only, no roofs.");
        }

        [MenuItem("Tools/Shelter/Create Shelter Scene (Level 3)", priority = 80)]
        public static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Shelter", "Press Stop (■) first, then run this again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(MatFolder);

            Material floor   = Mat("M_ShelterFloor",   $"{TexEnv}/T_Asphalt.png",       1f, new Color(0.55f, 0.56f, 0.52f));
            Material wall    = Mat("M_ShelterWall",    $"{TexEnv}/T_ConcreteFence.png", 0.7f, new Color(0.50f, 0.53f, 0.47f));
            Material ceiling = Mat("M_ShelterCeiling", $"{TexStore}/T_StoreCeiling.png", 1f, new Color(0.35f, 0.36f, 0.33f));
            Material wood    = Mat("M_ShelterWood",    $"{TexStore}/T_StoreWood.png",   2f, new Color(0.60f, 0.52f, 0.45f));
            Material metal   = Mat("M_ShelterMetal",   $"{TexEnv}/T_PoleMetal.png",     2f, new Color(0.55f, 0.50f, 0.45f));
            Material cloth   = Mat("M_ShelterCloth",   $"{TexEnv}/T_Rubber.png",        3f, new Color(0.45f, 0.47f, 0.35f));
            Material lamp    = Mat("M_ShelterLamp",    $"{TexEnv}/T_LampGlow.png",      4f, Color.white, ambient: 2f);

            // ---------- Room ----------
            var room = Group(null, "Shelter");
            Material olive  = Mat("M_ShelterOlive",  $"{TexEnv}/T_PoleMetal.png", 2f, new Color(0.33f, 0.36f, 0.22f));
            Material yellow = Mat("M_ShelterHazard", $"{TexEnv}/T_PoleMetal.png", 2f, new Color(0.85f, 0.7f, 0.1f));
            Material black  = Mat("M_ShelterBlack",  $"{TexEnv}/T_Rubber.png",    2f, new Color(0.08f, 0.08f, 0.08f));
            Material sand   = Mat("M_ShelterSand",   $"{TexEnv}/T_Rubber.png",    3f, new Color(0.55f, 0.48f, 0.33f));

            Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(2 * HalfW + 2 * Wall, 0.2f, 2 * HalfD + 2 * Wall), floor);
            Box(room, "Ceiling", new Vector3(0, Height + 0.1f, 0), new Vector3(2 * HalfW + 2 * Wall, 0.2f, 2 * HalfD + 2 * Wall), ceiling);
            Box(room, "Wall_Back", new Vector3(0, Height / 2, HalfD + Wall / 2), new Vector3(2 * HalfW + 2 * Wall, Height, Wall), wall);
            Box(room, "Wall_Left", new Vector3(-HalfW - Wall / 2, Height / 2, 0), new Vector3(Wall, Height, 2 * HalfD), wall);
            // Right wall with a 2 m opening to the maintenance tunnel (second way in), centred at z = SideDoorZ
            float lowLen = SideDoorZ - 1f + HalfD, highLen = HalfD - (SideDoorZ + 1f);
            Box(room, "Wall_Right_Front", new Vector3(HalfW + Wall / 2, Height / 2, -HalfD + lowLen / 2), new Vector3(Wall, Height, lowLen), wall);
            Box(room, "Wall_Right_Back", new Vector3(HalfW + Wall / 2, Height / 2, HalfD - highLen / 2), new Vector3(Wall, Height, highLen), wall);
            Box(room, "SideDoor_Lintel", new Vector3(HalfW + Wall / 2, 2.6f, SideDoorZ), new Vector3(Wall, 0.8f, 2f), wall);
            // Front wall with a 2 m doorway in the middle
            float seg = HalfW - 1f + Wall;
            Box(room, "Wall_Front_L", new Vector3(-1f - seg / 2, Height / 2, -HalfD - Wall / 2), new Vector3(seg, Height, Wall), wall);
            Box(room, "Wall_Front_R", new Vector3(1f + seg / 2, Height / 2, -HalfD - Wall / 2), new Vector3(seg, Height, Wall), wall);
            Box(room, "Door_Lintel", new Vector3(0, 2.6f, -HalfD - Wall / 2), new Vector3(2f, 0.8f, Wall), wall);
            // Heavy blast door, swung open against the wall, and hazard stripes on the door frames
            Box(room, "BlastDoor", new Vector3(-1.9f, 1.1f, -HalfD + 0.15f), new Vector3(1.9f, 2.2f, 0.15f), olive);
            HazardFrame(room, new Vector3(0, 0, -HalfD + 0.01f), Vector3.right, yellow, black);
            HazardFrame(room, new Vector3(HalfW - 0.01f, 0, SideDoorZ), Vector3.forward, yellow, black);

            // Main entrance corridor (one way in)
            var corridor = Group(room, "Corridor");
            Box(corridor, "Floor", new Vector3(0, -0.1f, -8.15f), new Vector3(2.6f, 0.2f, 6.3f), floor);
            Box(corridor, "Ceiling", new Vector3(0, 2.3f, -8.15f), new Vector3(2.6f, 0.2f, 6.3f), ceiling);
            Box(corridor, "Wall_L", new Vector3(-1.15f, 1.1f, -8.15f), new Vector3(Wall, 2.2f, 6.3f), wall);
            Box(corridor, "Wall_R", new Vector3(1.15f, 1.1f, -8.15f), new Vector3(Wall, 2.2f, 6.3f), wall);
            Box(corridor, "Wall_End", new Vector3(0, 1.1f, -11.45f), new Vector3(2.6f, 2.2f, Wall), wall);

            // Maintenance tunnel (the second way in), going out of the right wall
            var tunnel = Group(room, "MaintenanceTunnel");
            float tx = HalfW + 3f;
            Box(tunnel, "Floor", new Vector3(tx, -0.1f, SideDoorZ), new Vector3(6.3f, 0.2f, 2.6f), floor);
            Box(tunnel, "Ceiling", new Vector3(tx, 2.3f, SideDoorZ), new Vector3(6.3f, 0.2f, 2.6f), ceiling);
            Box(tunnel, "Wall_A", new Vector3(tx, 1.1f, SideDoorZ - 1.15f), new Vector3(6.3f, 2.2f, Wall), wall);
            Box(tunnel, "Wall_B", new Vector3(tx, 1.1f, SideDoorZ + 1.15f), new Vector3(6.3f, 2.2f, Wall), wall);
            Box(tunnel, "Wall_End", new Vector3(HalfW + 6.3f, 1.1f, SideDoorZ), new Vector3(Wall, 2.2f, 2.6f), wall);
            foreach (float off in new[] { -0.8f, -0.55f })
                Box(tunnel, "Pipe", new Vector3(tx, 1.9f, SideDoorZ + off), new Vector3(0.14f, 3.1f, 0.14f), olive, PrimitiveType.Cylinder, Quaternion.Euler(0, 0, 90));

            // ---------- Bunker furniture (blocks the NavMesh) ----------
            var props = Group(room, "Props");
            Box(props, "Pillar_L", new Vector3(-2.8f, Height / 2, 0.3f), new Vector3(0.6f, Height, 0.6f), wall);
            Box(props, "Pillar_R", new Vector3(2.8f, Height / 2, 0.3f), new Vector3(0.6f, Height, 0.6f), wall);

            // Two-level bunk beds along the left wall
            foreach (float z in new[] { -3.5f, -1.1f })
            {
                foreach (var c in new[] { new Vector3(-6.75f, 0, z - 0.95f), new Vector3(-5.85f, 0, z - 0.95f), new Vector3(-6.75f, 0, z + 0.95f), new Vector3(-5.85f, 0, z + 0.95f) })
                    Box(props, "Bunk_Post", c + Vector3.up * 0.9f, new Vector3(0.06f, 1.8f, 0.06f), olive);
                foreach (float h in new[] { 0.4f, 1.4f })
                {
                    Box(props, "Bunk_Frame", new Vector3(-6.3f, h, z), new Vector3(0.95f, 0.08f, 2f), olive);
                    Box(props, "Bunk_Mattress", new Vector3(-6.3f, h + 0.09f, z), new Vector3(0.85f, 0.1f, 1.9f), cloth);
                    Box(props, "Bunk_Blanket", new Vector3(-6.3f, h + 0.15f, z + 0.3f), new Vector3(0.86f, 0.03f, 1.1f), olive);
                }
            }
            // Lockers
            for (int i = 0; i < 3; i++)
            {
                float z = 1.2f + i * 0.55f;
                Box(props, "Locker", new Vector3(-6.75f, 0.95f, z), new Vector3(0.5f, 1.9f, 0.52f), olive);
                Box(props, "Locker_Vent", new Vector3(-6.49f, 1.6f, z), new Vector3(0.01f, 0.15f, 0.3f), black);
            }
            // Big water tanks in the back-left corner
            Box(props, "WaterTank_1", new Vector3(-6.2f, 0.8f, 4.1f), new Vector3(0.9f, 1.6f, 0.9f), metal, PrimitiveType.Cylinder);
            Box(props, "WaterTank_2", new Vector3(-5.2f, 0.8f, 4.3f), new Vector3(0.8f, 1.6f, 0.8f), metal, PrimitiveType.Cylinder);
            // Generator + fuel cans in the front-right corner
            Box(props, "Generator", new Vector3(5.7f, 0.45f, -4.2f), new Vector3(1.2f, 0.9f, 0.7f), olive);
            Box(props, "Generator_Exhaust", new Vector3(6.1f, 1.5f, -4.35f), new Vector3(0.12f, 1.2f, 0.12f), black, PrimitiveType.Cylinder);
            Box(props, "FuelCan_1", new Vector3(4.7f, 0.25f, -4.4f), new Vector3(0.3f, 0.5f, 0.2f), Mat("M_ShelterRed", $"{TexEnv}/T_PoleMetal.png", 2f, new Color(0.6f, 0.1f, 0.08f)));
            Box(props, "Crate", new Vector3(4.6f, 0.35f, -3.6f), new Vector3(0.7f, 0.7f, 0.7f), wood);
            // Radio table
            Box(props, "Table", new Vector3(3.2f, 0.75f, -2.2f), new Vector3(1.4f, 0.08f, 0.8f), wood);
            foreach (var p in new[] { new Vector3(2.6f, 0.37f, -2.5f), new Vector3(3.8f, 0.37f, -2.5f),
                                      new Vector3(2.6f, 0.37f, -1.9f), new Vector3(3.8f, 0.37f, -1.9f) })
                Box(props, "TableLeg", p, new Vector3(0.08f, 0.74f, 0.08f), wood);
            Box(props, "Radio", new Vector3(3.0f, 0.93f, -2.3f), new Vector3(0.5f, 0.28f, 0.3f), olive);
            Box(props, "Radio_Antenna", new Vector3(3.2f, 1.3f, -2.35f), new Vector3(0.015f, 0.5f, 0.015f), black);
            // Sandbags by the main door
            foreach (float x in new[] { -2.4f, 2.4f })
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 3 - row; i++)
                        Box(props, "Sandbag", new Vector3(x + (i - (2 - row) / 2f) * 0.55f * Mathf.Sign(x), 0.14f + row * 0.24f, -4.55f),
                            new Vector3(0.55f, 0.25f, 0.4f), sand);
            // Ventilation ducts under the ceiling (no colliders: they're overhead)
            foreach (float x in new[] { -3.9f, 3.9f })
            {
                var duct = Box(props, "VentDuct", new Vector3(x, Height - 0.3f, 0), new Vector3(0.6f, 0.4f, 2 * HalfD), metal);
                Object.DestroyImmediate(duct.GetComponent<Collider>());
            }
            // Stencilled signs
            Stencil(room, "SHELTER 7", new Vector3(0, 2.55f, HalfD - 0.01f), 0f, 9f, new Color(0.85f, 0.75f, 0.35f));
            Stencil(room, "RADIATION\nHAZARD", new Vector3(-HalfW + 0.01f, 2.3f, 3.0f), -90f, 4f, new Color(0.95f, 0.8f, 0.1f));
            Stencil(room, "MAINTENANCE", new Vector3(HalfW - 0.01f, 2.62f, SideDoorZ), 90f, 2.2f, new Color(0.85f, 0.75f, 0.35f));
            Stencil(room, "RATION: 1 CAN / DAY", new Vector3(-HalfW + 0.01f, 1.6f, 0.4f), -90f, 2.6f, new Color(0.9f, 0.88f, 0.8f));

            // ---------- NavMesh (before shelves and player: shelves cut themselves out at runtime) ----------
            var navGo = new GameObject("ShelterNavMesh");
            BakeFloorNavMesh(navGo.AddComponent<NavMeshSurface>());

            // ---------- Supplies ----------
            var shelves = Group(room, "SupplyShelves");
            int shelfCount = 0;
            foreach (var s in Supplies)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_{s.id}.asset");
                if (data == null)
                {
                    Debug.LogWarning($"[Shelter] No ItemData '{s.id}' — run Tools → Store → survival products first.");
                    continue;
                }
                var go = new GameObject($"Shelf_{s.id}");
                go.transform.SetParent(shelves, false);
                go.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0, s.yaw, 0));
                var shelf = go.AddComponent<Shelf>();
                var so = new SerializedObject(shelf);
                so.FindProperty("item").objectReferenceValue = data;
                so.FindProperty("stock").intValue = s.stock;
                so.FindProperty("bunkerRack").boolValue = true;        // military storage rack, not a shop shelf
                so.FindProperty("lockItemsOnShelf").boolValue = true;  // E never takes supplies off the rack by accident
                Set(so, "frameColor", new Color(0.30f, 0.33f, 0.20f));  // olive drab paint
                Set(so, "boardColor", new Color(0.24f, 0.25f, 0.20f));  // grey-green steel boards
                Set(so, "signColor", new Color(0.12f, 0.13f, 0.09f));
                Set(so, "signStripe", new Color(0.85f, 0.7f, 0.1f));   // hazard yellow
                so.ApplyModifiedPropertiesWithoutUndo();
                shelfCount++;
            }

            // ---------- Light & atmosphere ----------
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.03f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 17f;

            var lightsRoot = Group(room, "Lights");
            var flicker = new List<Light>();
            foreach (var p in new[] { new Vector3(-4.2f, 0, 1.5f), new Vector3(0f, 0, 2.5f), new Vector3(4.2f, 0, 1.5f), new Vector3(0f, 0, -2.5f) })
            {
                var fixture = Box(lightsRoot, "Lamp", new Vector3(p.x, Height - 0.05f, p.z), new Vector3(0.5f, 0.08f, 0.5f), lamp);
                Object.DestroyImmediate(fixture.GetComponent<Collider>());
                flicker.Add(PointLight(lightsRoot, new Vector3(p.x, Height - 0.3f, p.z), new Color(1f, 0.86f, 0.62f), 2.2f, 7f));
            }
            // Red emergency light over the doorway + a faint one deep in the corridor
            PointLight(lightsRoot, new Vector3(0, 2.1f, -5.6f), new Color(1f, 0.12f, 0.06f), 1.6f, 5f);
            PointLight(lightsRoot, new Vector3(0, 1.9f, -10.4f), new Color(0.6f, 0.1f, 0.05f), 0.8f, 4f);
            // Amber light over the maintenance opening + a faint red one deep in the tunnel
            PointLight(lightsRoot, new Vector3(HalfW - 0.6f, 2.1f, SideDoorZ), new Color(1f, 0.55f, 0.1f), 1.4f, 5f);
            PointLight(lightsRoot, new Vector3(HalfW + 5.4f, 1.9f, SideDoorZ), new Color(0.6f, 0.1f, 0.05f), 0.8f, 4f);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null)
            {
                var vol = new GameObject("Global Volume").AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile; // same film grain / colour grading as the store
            }

            // ---------- Entrance, player, director ----------
            var entrance = new GameObject("Intruder_Entrance").transform;
            entrance.SetPositionAndRotation(Snap(new Vector3(0, 0, -10.4f)), Quaternion.identity); // faces into the room
            var entrance2 = new GameObject("Intruder_Entrance_Tunnel").transform;
            entrance2.SetPositionAndRotation(Snap(new Vector3(HalfW + 5.4f, 0, SideDoorZ)), Quaternion.Euler(0, -90f, 0));

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.transform.SetPositionAndRotation(new Vector3(0, 0, 1.5f), Quaternion.Euler(0, 180f, 0)); // looking at the door
                var camData = player.GetComponentInChildren<UniversalAdditionalCameraData>();
                if (camData != null) camData.renderPostProcessing = true;
            }
            else Debug.LogWarning("[Shelter] Player prefab not found: " + PlayerPath);

            var director = new GameObject("ShelterDirector").AddComponent<ShelterDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("entrance").objectReferenceValue = entrance;
            dso.FindProperty("exit").objectReferenceValue = entrance;
            var entrancesProp = dso.FindProperty("entrances"); // groups come in through either door
            entrancesProp.arraySize = 2;
            entrancesProp.GetArrayElementAtIndex(0).objectReferenceValue = entrance;
            entrancesProp.GetArrayElementAtIndex(1).objectReferenceValue = entrance2;
            var modelsProp = dso.FindProperty("characterModels");
            var models = FindCharacterModels();
            modelsProp.arraySize = models.Count;
            for (int i = 0; i < models.Count; i++) modelsProp.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            var lightsProp = dso.FindProperty("flickerLights");
            lightsProp.arraySize = flicker.Count;
            for (int i = 0; i < flicker.Count; i++) lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = flicker[i];
            dso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Add to Build Profiles (after the existing scenes) so SceneLoader.Load("Shelter") works.
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!build.Exists(b => b.path == ScenePath))
            {
                build.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = build.ToArray();
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"[Shelter] {ScenePath} built: {shelfCount} supply shelves, NavMesh baked. Press Play.");
        }

        // ---------- Helpers ----------

        /// <summary>The rigged low-poly men in Assets/Floreswa/Models (male01_1 ... male03_3).</summary>
        public static List<GameObject> FindCharacterModels()
        {
            var list = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("male0 t:Model", new[] { "Assets/Floreswa/Models" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null) list.Add(go);
            }
            if (list.Count == 0) Debug.LogWarning("[Shelter] No character models in Assets/Floreswa/Models — NPCs use simple shapes.");
            return list;
        }

        static void Set(SerializedObject so, string prop, Color c)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.colorValue = c;
        }

        static Light PointLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var l = new GameObject("PointLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None; // cheap: fine on a laptop
            return l;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        /// <summary>Yellow/black hazard stripes around a 2 m wide, 2.2 m high doorway.</summary>
        static void HazardFrame(Transform parent, Vector3 bottomCentre, Vector3 across, Material yellow, Material black)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 9; i++)
                {
                    Vector3 size = across * 0.04f + Vector3.up * 0.24f + Vector3.Cross(across, Vector3.up).normalized * 0.32f;
                    size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    var go = Box(parent, "Hazard", bottomCentre + across * (side * 1.02f) + Vector3.up * (0.12f + i * 0.24f),
                        size, i % 2 == 0 ? yellow : black);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                }
        }

        /// <summary>Stencil-style painted text on a wall (TextMeshPro, read from inside the room).</summary>
        static void Stencil(Transform parent, string text, Vector3 pos, float yaw, float size, Color color)
        {
            var go = new GameObject("Stencil_" + text.Split('\n')[0], typeof(TMPro.TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var t = go.GetComponent<TMPro.TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase;
            t.characterSpacing = 12;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
            t.color = new Color(color.r, color.g, color.b, 0.85f);
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
            PrimitiveType type, Quaternion rotation)
        {
            var go = Box(parent, name, pos, size, mat, type);
            go.transform.rotation = rotation;
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
            PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static Vector3 Snap(Vector3 p) =>
            NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        /// <summary>PS1-look material (same shader as the store), texture projected so primitives need no UVs.</summary>
        static Material Mat(string name, string texturePath, float tilesPerMetre, Color tint, float ambient = 1f)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Game/PS1 Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_AmbientBoost")) mat.SetFloat("_AmbientBoost", ambient);
            if (mat.HasProperty("_Triplanar"))
            {
                mat.SetFloat("_Triplanar", 1f);
                mat.SetFloat("_TriplanarScale", tilesPerMetre);
                mat.EnableKeyword("_TRIPLANAR");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
