using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Store → Build Surroundings (street, buildings, cars)
    ///   Low-poly PS1 world around the store (built by Tools → Store → Build Small Store):
    ///   road + sidewalks + street lamps in front, two panel apartment blocks across the road,
    ///   rusty garages on the left, a small brick building on the right, concrete fence,
    ///   dumpster and pine trees behind, abandoned cars, bus stop, power poles,
    ///   an orange end-of-the-world sky with haze, and invisible walls at the edges.
    ///   Safe to run again: the "Surroundings" object is rebuilt.
    ///
    /// Store (for reference): x -7.2..7.2, z -1.2..9.2, entrance at z = -1 facing -z (the street).
    /// </summary>
    public static class SurroundingsBuilderTool
    {
        const string TexFolder   = "Assets/_Project/Art/Textures/Environment";
        const string MatFolder   = "Assets/_Project/Art/Materials/Environment";
        const string MeshFolder  = "Assets/_Project/Art/Models/Environment";
        const string MainScene   = "Assets/_Project/Scenes/Main.unity";

        internal static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        [MenuItem("Tools/Store/Build Surroundings (street, buildings, cars)", priority = 71)]
        public static void BuildMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene &&
                !EditorUtility.DisplayDialog("Surroundings", "This edits the shared Main.unity scene. Tell your team first.",
                    "Build in Main", "Cancel"))
                return;
            Build();
        }

        /// <summary>
        /// Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.SurroundingsBuilderTool.BuildFromCommandLine
        ///       -envScene Assets/_Project/Scenes/Dev/user.unity
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-envScene");
            if (i >= 0 && i + 1 < args.Length) EditorSceneManager.OpenScene(args[i + 1]);
            Build();
        }

        /// <summary>
        /// Rebuilds only Surroundings/Street (road, sidewalks, ground) in -envScene, keeping everything else.
        /// -executeMethod Game.EditorTools.SurroundingsBuilderTool.RebuildStreetFromCommandLine -envScene path
        /// </summary>
        [MenuItem("Tools/Store/Rebuild Street Only (fix road smearing)", priority = 74)]
        public static void RebuildStreetMenu() => RebuildStreetFromCommandLine();

        public static void RebuildStreetFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-envScene");
            var scene = i >= 0 && i + 1 < args.Length ? EditorSceneManager.OpenScene(args[i + 1]) : EditorSceneManager.GetActiveScene();
            var root = GameObject.Find("Surroundings");
            if (root == null) { Debug.LogWarning("[Surroundings] Nothing to rebuild."); return; }
            Mats.Clear();
            var old = root.transform.Find("Street");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var street = Group(root.transform, "Street");
            street.SetSiblingIndex(0);
            BuildGroundAndStreet(street);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Surroundings] Street rebuilt from 10 m tiles.");
        }

        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            AssetDatabase.Refresh();
            EnsureFolder(MatFolder);
            EnsureFolder(MeshFolder);
            SetUpTextures();
            Mats.Clear();

            var old = GameObject.Find("Surroundings");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Surroundings").transform;

            // The old test ground is replaced by our own
            var level = GameObject.Find("Level");
            if (level != null)
                foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
                    if (t != level.transform && t.GetComponent<Renderer>() != null) t.gameObject.SetActive(false);

            BuildGroundAndStreet(Group(root, "Street"));
            BuildBuildings(Group(root, "Buildings"));
            BuildBehindStore(Group(root, "Backyard"));
            BuildCars(Group(root, "Cars"));
            BuildStreetProps(Group(root, "Props"));
            BuildTrees(Group(root, "Trees"));
            BuildBounds(Group(root, "InvisibleWalls"));
            SetUpSkyAndLight();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Surroundings");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Surroundings] Built street, buildings, cars, trees and the apocalypse sky around the store.");
        }

        // ------------------------------------------------------------------ parts

        static void BuildGroundAndStreet(Transform p)
        {
            Tiles(p, "Ground", new Vector3(0, -0.12f, -6), new Vector3(110, 0.2f, 90), M("Ground", 0.5f));

            // Near sidewalk (store side) and far sidewalk, 12 cm high
            Tiles(p, "Sidewalk_Near", new Vector3(0, 0.06f, -2.6f), new Vector3(90, 0.12f, 2.8f), M("Sidewalk", 1f));
            Tiles(p, "Sidewalk_Far", new Vector3(0, 0.06f, -13.4f), new Vector3(90, 0.12f, 2.8f), M("Sidewalk", 1f));

            // Road
            Tiles(p, "Road", new Vector3(0, -0.05f, -8f), new Vector3(90, 0.1f, 8f), M("Asphalt", 0.7f));
            var paint = M("RoadPaint", 2f);
            for (float x = -42f; x <= 42f; x += 5f)
                NoCollider(Box(p, "LaneDash", new Vector3(x, 0.003f, -8f), new Vector3(2.2f, 0.01f, 0.15f), paint));
            // Pedestrian crossing in front of the store
            for (float x = -1.6f; x <= 1.6f; x += 0.8f)
                NoCollider(Box(p, "Zebra", new Vector3(x, 0.003f, -8f), new Vector3(0.45f, 0.01f, 7.6f), paint));
        }

        static void BuildBuildings(Transform p)
        {
            Material panel = M("PanelBlock", 1f / 6f), roof = M("Roof", 0.5f), brick = M("Brick", 2f),
                     garage = M("Garage", 1f), metal = M("PoleMetal", 1f);

            // Two 5-floor panel blocks across the road
            Building(p, "PanelBlock_A", new Vector3(-15, 0, -23), new Vector3(24, 15, 11), panel, roof);
            Building(p, "PanelBlock_B", new Vector3(16, 0, -22), new Vector3(18, 12, 10), panel, roof);
            // Entrances (dark doors with a small concrete roof) facing the street
            foreach (float x in new[] { -21f, -9f, 16f })
            {
                float z = x < 0 ? -17.45f : -16.95f;
                NoCollider(Box(p, "Door", new Vector3(x, 1.1f, z), new Vector3(1.4f, 2.2f, 0.1f), M("CarWindow", 1f)));
                Box(p, "DoorRoof", new Vector3(x, 2.5f, z - 0.6f), new Vector3(2.2f, 0.15f, 1.3f), M("Sidewalk", 1f));
            }

            // Row of garages left of the store, doors to the street
            for (int i = 0; i < 4; i++)
            {
                float x = -11f - i * 3.8f;
                Box(p, "Garage", new Vector3(x, 1.3f, 3f), new Vector3(3.6f, 2.6f, 6f), garage);
                Box(p, "GarageRoof", new Vector3(x, 2.68f, 3f), new Vector3(3.8f, 0.16f, 6.3f), roof);
                NoCollider(Box(p, "GarageDoor", new Vector3(x, 1.05f, -0.02f), new Vector3(2.8f, 2.1f, 0.06f), metal));
            }

            // Small brick building right of the store (pharmacy / office)
            Building(p, "BrickHouse", new Vector3(15, 0, 4), new Vector3(12, 7, 10), brick, roof);
            NoCollider(Box(p, "BrickHouse_Door", new Vector3(13, 1.1f, -1.03f), new Vector3(1.4f, 2.2f, 0.1f), M("CarWindow", 1f)));
            foreach (float x in new[] { 10.5f, 17f, 19.5f })
            foreach (float y in new[] { 1.6f, 4.8f })
                NoCollider(Box(p, "Window", new Vector3(x, y, -1.03f), new Vector3(1.3f, 1.3f, 0.06f), M("CarWindow", 1f)));
        }

        internal static void Building(Transform p, string name, Vector3 floorCenter, Vector3 size, Material walls, Material roof)
        {
            Box(p, name, floorCenter + Vector3.up * size.y / 2f, size, walls);
            Box(p, name + "_Roof", floorCenter + Vector3.up * (size.y + 0.15f),
                new Vector3(size.x + 0.4f, 0.3f, size.z + 0.4f), roof);
        }

        static void BuildBehindStore(Transform p)
        {
            var fence = M("ConcreteFence", 0.5f);
            Box(p, "ConcreteFence", new Vector3(-2f, 1.1f, 14.5f), new Vector3(50f, 2.2f, 0.15f), fence);

            // Dumpster + trash bags
            Box(p, "Dumpster", new Vector3(4.5f, 0.65f, 11f), new Vector3(2f, 1.3f, 1.2f), M("Dumpster", 1f));
            var bag = M("TrashBag", 2f);
            foreach (var pos in new[] { new Vector3(6.2f, 0.3f, 11.4f), new Vector3(6.6f, 0.25f, 10.6f), new Vector3(2.9f, 0.3f, 11.6f) })
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "TrashBag";
                s.transform.SetParent(p, false);
                s.transform.position = pos;
                s.transform.localScale = new Vector3(0.7f, 0.55f, 0.6f);
                s.GetComponent<Renderer>().sharedMaterial = bag;
                s.isStatic = true;
            }
            // Wooden pallets / boxes
            Box(p, "Crates", new Vector3(-4f, 0.4f, 11f), new Vector3(1.2f, 0.8f, 1.2f), M("Bark", 2f));
            Box(p, "Crates", new Vector3(-4.3f, 1.1f, 11.1f), new Vector3(0.8f, 0.6f, 0.8f), M("Bark", 2f))
                .transform.rotation = Quaternion.Euler(0, 20, 0);
        }

        static void BuildCars(Transform p)
        {
            Car(p, new Vector3(10f, 0f, -6f), 90f, "CarRed");          // parked by the curb
            Car(p, new Vector3(-6f, 0f, -9.2f), 115f, "CarBlue");      // crashed across the lane
            Car(p, new Vector3(-24f, 0f, -6f), 90f, "CarWhite");
            Car(p, new Vector3(25f, 0f, -10f), 270f, "CarRed");
        }

        internal static Transform Car(Transform p, Vector3 pos, float yaw, string paint)
        {
            var car = new GameObject("Car").transform;
            car.SetParent(p, false);
            car.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            Material body = M(paint, 2f), glass = M("CarWindow", 2f), rubber = M("Rubber", 2f);

            Local(car, "Body", new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.7f, 4.2f), body);
            Local(car, "Cabin", new Vector3(0, 1.22f, -0.2f), new Vector3(1.6f, 0.6f, 2.1f), glass);
            Local(car, "CabinRoof", new Vector3(0, 1.54f, -0.2f), new Vector3(1.62f, 0.06f, 1.9f), body);
            foreach (float x in new[] { -0.88f, 0.88f })
            foreach (float z in new[] { -1.35f, 1.35f })
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                w.name = "Wheel";
                Object.DestroyImmediate(w.GetComponent<Collider>());
                w.transform.SetParent(car, false);
                w.transform.localPosition = new Vector3(x, 0.33f, z);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                w.transform.localScale = new Vector3(0.66f, 0.12f, 0.66f);
                w.GetComponent<Renderer>().sharedMaterial = rubber;
                w.isStatic = true;
            }
            return car;
        }

        static void BuildStreetProps(Transform p)
        {
            Material metal = M("PoleMetal", 1f), glow = M("LampGlow", 1f, ambient: 2f), concrete = M("Sidewalk", 1f);

            // Street lamps on the store side (warm, dim)
            foreach (float x in new[] { -20f, -8f, 8f, 20f })
            {
                Box(p, "LampPole", new Vector3(x, 2.5f, -3.7f), new Vector3(0.14f, 5f, 0.14f), metal);
                NoCollider(Box(p, "LampArm", new Vector3(x, 4.95f, -4.3f), new Vector3(0.1f, 0.1f, 1.3f), metal));
                NoCollider(Box(p, "LampHead", new Vector3(x, 4.85f, -4.9f), new Vector3(0.35f, 0.12f, 0.6f), glow));
                var l = new GameObject("StreetLight").AddComponent<Light>();
                l.transform.SetParent(p, false);
                l.transform.position = new Vector3(x, 4.6f, -4.9f);
                l.type = LightType.Point;
                l.range = 11f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.65f, 0.35f);
                l.shadows = LightShadows.None;
            }

            // Concrete power poles on the far side
            foreach (float x in new[] { -32f, -12f, 8f, 28f })
            {
                Box(p, "PowerPole", new Vector3(x, 4f, -14.4f), new Vector3(0.25f, 8f, 0.25f), concrete);
                NoCollider(Box(p, "PowerPoleBar", new Vector3(x, 7.5f, -14.4f), new Vector3(0.12f, 0.12f, 1.6f), metal));
            }

            // Bus stop on the far sidewalk
            Box(p, "BusStop_Back", new Vector3(3f, 1.25f, -14.6f), new Vector3(3.2f, 2.3f, 0.08f), metal);
            Box(p, "BusStop_Roof", new Vector3(3f, 2.45f, -14f), new Vector3(3.4f, 0.1f, 1.5f), metal);
            Box(p, "BusStop_Bench", new Vector3(3f, 0.45f, -14.3f), new Vector3(2.6f, 0.08f, 0.45f), M("Bark", 2f));
            foreach (float x in new[] { 1.5f, 4.5f })
                Box(p, "BusStop_Post", new Vector3(x, 1.25f, -13.4f), new Vector3(0.08f, 2.4f, 0.08f), metal);
        }

        static void BuildTrees(Transform p)
        {
            Mesh pine = PineMesh();
            Material bark = M("Bark", 2f), needles = M("Pine", 1.5f);
            var spots = new[]
            {
                (-9f, 11.5f, 5.5f), (-6f, 12.8f, 4.2f), (9.5f, 12f, 6f), (22f, 11f, 7f), (-26f, 7f, 6.5f),
                (-30f, 1f, 5f), (29f, -1f, 6f), (-34f, -19f, 7f), (33f, -18f, 6.5f), (0f, -27f, 8f),
                (-2f, 12.5f, 4.8f), (13f, 12.6f, 5f),
            };
            int n = 0;
            foreach (var (x, z, h) in spots)
            {
                var go = new GameObject($"Pine_{n++}");
                go.transform.SetParent(p, false);
                go.transform.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, n * 47f, 0));
                go.transform.localScale = Vector3.one * h / 4f; // mesh is 4 m tall
                go.AddComponent<MeshFilter>().sharedMesh = pine;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, needles };
                var col = go.AddComponent<CapsuleCollider>();       // trunk only
                col.radius = 0.15f;
                col.height = 4f;
                col.center = new Vector3(0, 2f, 0);
                go.isStatic = true;
            }
        }

        /// <summary>Keeps the player in the playable area (street block around the store).</summary>
        static void BuildBounds(Transform p)
        {
            void Wall(string name, Vector3 c, Vector3 s)
            {
                var go = new GameObject(name);
                go.transform.SetParent(p, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            Wall("Bound_North", new Vector3(0, 3, 14.6f), new Vector3(90, 6, 0.5f));
            Wall("Bound_South", new Vector3(0, 3, -16.5f), new Vector3(90, 6, 0.5f)); // in front of the far buildings
            Wall("Bound_West", new Vector3(-38, 3, -1), new Vector3(0.5f, 6, 32));
            Wall("Bound_East", new Vector3(38, 3, -1), new Vector3(0.5f, 6, 32));
        }

        /// <summary>
        /// Orange end-of-the-world sky, low warm sun, warm haze.
        /// Colours are picked to survive the 4-level PixelDither (dark sunset tones turn teal there),
        /// and the ambient light is set directly (Trilight) instead of being taken from the sky.
        /// </summary>
        public static void SetUpSkyAndLight()
        {
            string skyPath = $"{MatFolder}/M_SkyApocalypse.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            Shader gradient = Shader.Find("Game/Sky Gradient");
            if (sky == null)
            {
                sky = new Material(gradient);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            if (gradient != null) sky.shader = gradient;
            sky.SetColor("_TopColor", new Color(0.36f, 0.14f, 0.1f));
            sky.SetColor("_HorizonColor", new Color(0.98f, 0.55f, 0.28f));
            sky.SetColor("_BottomColor", new Color(0.4f, 0.27f, 0.2f));
            sky.SetFloat("_Exponent", 0.7f);
            sky.SetColor("_SunColor", new Color(1f, 0.9f, 0.6f));
            sky.SetFloat("_SunSize", 0.025f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            // Ambient light: warm sky, neutral sides, dark ground (not computed from the sky)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.48f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.42f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.21f, 0.19f);

            // Haze in the distance, same colour as the horizon so buildings fade into the sky
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.85f, 0.5f, 0.3f);
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 90f;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                Undo.RecordObject(l, "Sun");
                Undo.RecordObject(l.transform, "Sun");
                l.color = new Color(1f, 0.78f, 0.58f);   // warm, but not red
                l.intensity = 1.3f;
                l.transform.rotation = Quaternion.Euler(22f, -35f, 0f); // low sun from behind the store
                break;
            }
        }

        /// <summary>Tools → Store → Fix Sky And Light: only redoes the sky / ambient / fog / sun.</summary>
        [MenuItem("Tools/Store/Fix Sky And Light (open scene)", priority = 72)]
        public static void FixSkyMenu()
        {
            EnsureFolder(MatFolder);
            SetUpSkyAndLight();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Surroundings] Sky, ambient light, fog and sun updated.");
        }

        /// <summary>Command line: -executeMethod ...SurroundingsBuilderTool.FixSkyFromCommandLine -envScene path</summary>
        public static void FixSkyFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-envScene");
            if (i >= 0 && i + 1 < args.Length) EditorSceneManager.OpenScene(args[i + 1]);
            FixSkyMenu();
        }

        // ------------------------------------------------------------------ low-poly pine mesh

        /// <summary>Hexagonal trunk + 3 hexagonal cones, flat shaded (PS1 style). 4 m tall, 2 submeshes.</summary>
        internal static Mesh PineMesh()
        {
            string path = $"{MeshFolder}/Pine_LowPoly.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var verts = new List<Vector3>();
            var trunk = new List<int>();
            var leaves = new List<int>();
            const int sides = 6;

            void Tri(List<int> sub, Vector3 a, Vector3 b, Vector3 c)
            {
                sub.Add(verts.Count); verts.Add(a);
                sub.Add(verts.Count); verts.Add(b);
                sub.Add(verts.Count); verts.Add(c);
            }
            Vector3 Ring(float r, float y, int i) =>
                new Vector3(Mathf.Cos(i * Mathf.PI * 2 / sides) * r, y, Mathf.Sin(i * Mathf.PI * 2 / sides) * r);

            // trunk 0 .. 1 m
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(0.15f, 0, i), b = Ring(0.15f, 0, i + 1), c = Ring(0.12f, 1.1f, i), d = Ring(0.12f, 1.1f, i + 1);
                Tri(trunk, a, c, b); Tri(trunk, b, c, d);
            }
            // cones: (bottom y, top y, radius)
            foreach (var (y0, y1, r) in new[] { (0.8f, 2.4f, 1.3f), (1.8f, 3.3f, 1.0f), (2.7f, 4.0f, 0.7f) })
            {
                var top = new Vector3(0, y1, 0);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = Ring(r, y0, i), b = Ring(r, y0, i + 1);
                    Tri(leaves, a, top, b);                  // side
                    Tri(leaves, a, b, new Vector3(0, y0, 0)); // underside
                }
            }

            var mesh = new Mesh { name = "Pine_LowPoly" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunk, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateNormals(); // every triangle has its own vertices → flat shading
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ helpers

        internal static Material M(string texture, float tilesPerMetre, float ambient = 1f)
        {
            string key = $"{texture}_{tilesPerMetre}";
            if (Mats.TryGetValue(key, out var cached)) return cached;

            string path = $"{MatFolder}/M_{texture}_{tilesPerMetre:0.##}.mat".Replace(',', '.');
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Game/PS1 Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/T_{texture}.png");
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_AmbientBoost")) mat.SetFloat("_AmbientBoost", ambient);
            if (mat.HasProperty("_Triplanar"))
            {
                mat.SetFloat("_Triplanar", 1f);
                mat.SetFloat("_TriplanarScale", tilesPerMetre);
                mat.EnableKeyword("_TRIPLANAR");
            }
            EditorUtility.SetDirty(mat);
            Mats[key] = mat;
            return mat;
        }

        internal static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        internal static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        /// <summary>
        /// Like Box, but cut into tiles of about <paramref name="tile"/> metres (X and Z).
        /// Big single polygons get smeared by the PS1 vertex snapping; small tiles don't.
        /// Keep size / tile × texture-tiles-per-metre a whole number so the pattern has no seams.
        /// </summary>
        internal static GameObject Tiles(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
                                         float tile = 10f)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / tile)), nz = Mathf.Max(1, Mathf.RoundToInt(size.z / tile));
            float sx = size.x / nx, sz = size.z / nz;
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                Box(group, name, new Vector3(center.x - size.x / 2f + sx * (i + 0.5f), center.y,
                    center.z - size.z / 2f + sz * (j + 0.5f)), new Vector3(sx, size.y, sz), mat);
            return group.gameObject;
        }

        internal static GameObject Local(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = Box(parent, name, Vector3.zero, size, mat);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        internal static GameObject NoCollider(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static void SetUpTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                if (ti.filterMode == FilterMode.Point && !ti.mipmapEnabled) continue;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
