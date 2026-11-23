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

        // Supplies you carried here from the store: (item id, how many)
        static readonly (string id, int stock, Vector3 pos, float yaw)[] Supplies =
        {
            ("canned_meat", 4, new Vector3(-4.5f, 0, 4.45f), 180f),
            ("water_jug",   3, new Vector3(-1.5f, 0, 4.45f), 180f),
            ("buckwheat",   3, new Vector3( 1.5f, 0, 4.45f), 180f),
            ("first_aid",   2, new Vector3( 4.5f, 0, 4.45f), 180f),
            ("batteries",   2, new Vector3( 6.45f, 0, 1.2f), -90f),
        };

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
            Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(2 * HalfW + 2 * Wall, 0.2f, 2 * HalfD + 2 * Wall), floor);
            Box(room, "Ceiling", new Vector3(0, Height + 0.1f, 0), new Vector3(2 * HalfW + 2 * Wall, 0.2f, 2 * HalfD + 2 * Wall), ceiling);
            Box(room, "Wall_Back", new Vector3(0, Height / 2, HalfD + Wall / 2), new Vector3(2 * HalfW + 2 * Wall, Height, Wall), wall);
            Box(room, "Wall_Left", new Vector3(-HalfW - Wall / 2, Height / 2, 0), new Vector3(Wall, Height, 2 * HalfD), wall);
            Box(room, "Wall_Right", new Vector3(HalfW + Wall / 2, Height / 2, 0), new Vector3(Wall, Height, 2 * HalfD), wall);
            // Front wall with a 2 m doorway in the middle
            float seg = HalfW - 1f + Wall;
            Box(room, "Wall_Front_L", new Vector3(-1f - seg / 2, Height / 2, -HalfD - Wall / 2), new Vector3(seg, Height, Wall), wall);
            Box(room, "Wall_Front_R", new Vector3(1f + seg / 2, Height / 2, -HalfD - Wall / 2), new Vector3(seg, Height, Wall), wall);
            Box(room, "Door_Lintel", new Vector3(0, 2.6f, -HalfD - Wall / 2), new Vector3(2f, 0.8f, Wall), wall);
            // Heavy blast door, swung open against the wall
            Box(room, "BlastDoor", new Vector3(-1.9f, 1.1f, -HalfD + 0.15f), new Vector3(1.9f, 2.2f, 0.15f), metal);

            // Entrance corridor (intruders come in from its dark end)
            var corridor = Group(room, "Corridor");
            Box(corridor, "Floor", new Vector3(0, -0.1f, -8.15f), new Vector3(2.6f, 0.2f, 6.3f), floor);
            Box(corridor, "Ceiling", new Vector3(0, 2.3f, -8.15f), new Vector3(2.6f, 0.2f, 6.3f), ceiling);
            Box(corridor, "Wall_L", new Vector3(-1.15f, 1.1f, -8.15f), new Vector3(Wall, 2.2f, 6.3f), wall);
            Box(corridor, "Wall_R", new Vector3(1.15f, 1.1f, -8.15f), new Vector3(Wall, 2.2f, 6.3f), wall);
            Box(corridor, "Wall_End", new Vector3(0, 1.1f, -11.45f), new Vector3(2.6f, 2.2f, Wall), wall);

            // ---------- Furniture (also blocks the NavMesh) ----------
            var props = Group(room, "Props");
            Box(props, "Pillar_L", new Vector3(-2.8f, Height / 2, 0.3f), new Vector3(0.6f, Height, 0.6f), wall);
            Box(props, "Pillar_R", new Vector3(2.8f, Height / 2, 0.3f), new Vector3(0.6f, Height, 0.6f), wall);
            foreach (float z in new[] { -3.6f, -1.2f })
            {
                Box(props, "Cot_Frame", new Vector3(-6.2f, 0.25f, z), new Vector3(0.9f, 0.1f, 2f), metal);
                Box(props, "Cot_Mattress", new Vector3(-6.2f, 0.36f, z), new Vector3(0.8f, 0.12f, 1.9f), cloth);
            }
            Box(props, "Crate_1", new Vector3(5.8f, 0.45f, -3.9f), new Vector3(0.9f, 0.9f, 0.9f), wood);
            Box(props, "Crate_2", new Vector3(5.9f, 1.15f, -3.85f), new Vector3(0.6f, 0.5f, 0.6f), wood);
            Box(props, "Crate_3", new Vector3(4.8f, 0.35f, -4.2f), new Vector3(0.7f, 0.7f, 0.7f), wood);
            Box(props, "Table", new Vector3(3.2f, 0.75f, -2.2f), new Vector3(1.4f, 0.08f, 0.8f), wood);
            foreach (var p in new[] { new Vector3(2.6f, 0.37f, -2.5f), new Vector3(3.8f, 0.37f, -2.5f),
                                      new Vector3(2.6f, 0.37f, -1.9f), new Vector3(3.8f, 0.37f, -1.9f) })
                Box(props, "TableLeg", p, new Vector3(0.08f, 0.74f, 0.08f), wood);
            Box(props, "Barrel_1", new Vector3(-6.3f, 0.5f, 3.9f), new Vector3(0.7f, 1f, 0.7f), metal, PrimitiveType.Cylinder);
            Box(props, "Barrel_2", new Vector3(-5.5f, 0.5f, 4.2f), new Vector3(0.7f, 1f, 0.7f), metal, PrimitiveType.Cylinder);

            // ---------- NavMesh (before shelves and player: shelves cut themselves out at runtime) ----------
            var navGo = new GameObject("ShelterNavMesh");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            EnsureFolder(NavFolder);
            string navPath = $"{NavFolder}/Shelter_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

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
                Set(so, "frameColor", new Color(0.36f, 0.37f, 0.34f));  // dull army metal
                Set(so, "boardColor", new Color(0.33f, 0.25f, 0.17f));  // dark wood
                Set(so, "signStripe", new Color(0.32f, 0.36f, 0.18f));  // olive instead of shop red
                so.ApplyModifiedPropertiesWithoutUndo();
                shelfCount++;
            }

            // ---------- Light & atmosphere ----------
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.10f);
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
