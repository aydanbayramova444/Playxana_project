using System.IO;
using Game.Gameplay;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Products → 1. Make Survival Products
    ///   Builds 3 low-poly products with PS1-style pixel textures (point filter, no mipmaps):
    ///   toilet paper (pack of 4), buckwheat (Гречка), canned meat (Тушёнка).
    ///   → Prefabs/Pickups/Pickup_*.prefab (Game.Interaction.Pickup), ItemData assets,
    ///     and adds them to ShoppingList_Test.
    ///
    /// Tools → Products → 2. Add Product Shelves To Open Scene
    ///   Puts a shelf for each product + one EMPTY shelf (put anything on it) to the player's left.
    ///
    /// Textures live in Art/Textures/Products (drawn small on purpose: 32–64 px, few colours).
    /// Safe to run again: everything is rebuilt / reused.
    /// </summary>
    public static class SurvivalProductsTool
    {
        const string TexFolder    = "Assets/_Project/Art/Textures/Products";
        const string MatFolder    = "Assets/_Project/Art/Materials/Products";
        const string PrefabFolder = "Assets/_Project/Prefabs/Pickups";
        const string ItemsFolder  = "Assets/_Project/ScriptableObjects/Items";
        const string ListPath     = "Assets/_Project/ScriptableObjects/ShoppingList_Test.asset";
        const string MainScene    = "Assets/_Project/Scenes/Main.unity";

        /// <summary>
        /// Size of every product compared to real life (1 = real size). Bigger reads better at PS1
        /// resolution. Max ~1.6: the 5 L water jug must still fit between two shelf boards.
        /// </summary>
        const float ItemSize = 1.5f;

        // id (= Pickup.ItemId = ItemData.id), name in the game, how many a shelf starts with,
        // and whether it is ALWAYS on the test shopping list (the rest come up at random)
        static readonly (string id, string name, int stock, bool onList)[] Products =
        {
            ("toilet_paper",  "Toilet Paper",    4, false),
            ("buckwheat",     "Buckwheat",       6, false),
            ("canned_meat",   "Canned Meat",     8, false),
            ("water_jug",     "Water 5L",        3, true),
            ("batteries",     "Batteries",       6, false),
            ("candles",       "Candles",         6, false),
            ("noodles",       "Instant Noodles", 8, false),
            ("first_aid",     "First Aid Kit",   2, true),
            ("matches",       "Matches",        12, false),
        };

        // ---------------------------------------------------------------- 1. products

        [MenuItem("Tools/Products/1. Make Survival Products", priority = 50)]
        public static void MakeProducts()
        {
            AssetDatabase.Refresh();
            EnsureFolder(MatFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(ItemsFolder);
            SetUpTextures();

            Material buckwheat = Mat("M_Buckwheat", "T_Buckwheat");
            Material canLabel  = Mat("M_CanLabel", "T_CanLabel", smoothness: 0.3f);
            Material canLid    = Mat("M_CanLid", "T_CanLid", smoothness: 0.5f, metallic: 0.6f);
            Material paperSide = Mat("M_ToiletPaperSide", "T_ToiletPaperSide");
            Material paperTop  = Mat("M_ToiletPaperTop", "T_ToiletPaperTop");

            SavePickup("toilet_paper", "Toilet Paper", 0.4f, root =>
            {
                // Pack of 4 rolls (2 x 2)
                const float d = 0.1f, h = 0.1f, gap = 0.051f;
                int n = 0;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var roll = new GameObject($"Roll_{n++}").transform;
                    roll.SetParent(root, false);
                    roll.localPosition = new Vector3(x * gap, 0f, z * gap);
                    Cylinder(roll, "Side", d, h, h / 2f, paperSide);
                    Cylinder(roll, "Top", d + 0.002f, 0.004f, h - 0.001f, paperTop);
                    Cylinder(roll, "Bottom", d + 0.002f, 0.004f, 0.001f, paperTop);
                }
            });

            SavePickup("buckwheat", "Buckwheat", 1f, root =>
            {
                var box = Part(PrimitiveType.Cube, "Pack", root, new Vector3(0f, 0.095f, 0f),
                    new Vector3(0.12f, 0.19f, 0.06f), buckwheat);
                box.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // label faces the aisle
            });

            SavePickup("canned_meat", "Canned Meat", 0.4f, root =>
            {
                const float d = 0.085f, h = 0.105f;
                Cylinder(root, "Label", d, h, h / 2f, canLabel);
                Cylinder(root, "Lid", d + 0.004f, 0.006f, h - 0.002f, canLid);
                Cylinder(root, "Bottom", d + 0.004f, 0.006f, 0.002f, canLid);
            });

            Material waterJug  = Mat("M_WaterJug", "T_WaterJug", smoothness: 0.4f);
            Material waterCap  = Mat("M_WaterCap", "T_WaterCap");
            Material batteries = Mat("M_Batteries", "T_Batteries");
            Material candle    = Mat("M_Candle", "T_Candle");
            Material noodles   = Mat("M_NoodlesSide", "T_NoodlesSide");
            Material noodleLid = Mat("M_NoodlesLid", "T_NoodlesLid");
            Material firstAid  = Mat("M_FirstAid", "T_FirstAid");
            Material matches   = Mat("M_Matches", "T_Matches");
            Material wick      = Mat("M_CanLid", "T_CanLid"); // dark grey bit, reused

            SavePickup("water_jug", "Water 5L", 5f, root =>
            {
                const float d = 0.17f, h = 0.27f;
                Cylinder(root, "Body", d, h, h / 2f, waterJug);
                Cylinder(root, "Shoulder", d * 0.6f, 0.03f, h + 0.012f, waterJug);
                Cylinder(root, "Cap", 0.045f, 0.03f, h + 0.04f, waterCap);
                // handle on top: a small box bridge
                Part(PrimitiveType.Cube, "Handle", root, new Vector3(0f, h + 0.035f, 0.05f),
                    new Vector3(0.03f, 0.05f, 0.05f), waterCap);
            });

            SavePickup("batteries", "Batteries", 0.2f, root =>
            {
                var pack = Part(PrimitiveType.Cube, "Pack", root, new Vector3(0f, 0.07f, 0f),
                    new Vector3(0.09f, 0.14f, 0.02f), batteries);
                pack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            });

            SavePickup("candles", "Candles", 0.3f, root =>
            {
                // three candles in a row
                for (int i = -1; i <= 1; i++)
                {
                    var c = new GameObject($"Candle_{i + 1}").transform;
                    c.SetParent(root, false);
                    c.localPosition = new Vector3(i * 0.032f, 0f, 0f);
                    Cylinder(c, "Wax", 0.026f, 0.16f, 0.08f, candle);
                    Part(PrimitiveType.Cube, "Wick", c, new Vector3(0f, 0.168f, 0f),
                        new Vector3(0.003f, 0.016f, 0.003f), wick);
                }
            });

            SavePickup("noodles", "Instant Noodles", 0.15f, root =>
            {
                // cup: slightly wider at the top
                Cylinder(root, "CupBottom", 0.085f, 0.05f, 0.025f, noodles);
                Cylinder(root, "CupTop", 0.1f, 0.06f, 0.08f, noodles);
                Cylinder(root, "Lid", 0.104f, 0.004f, 0.111f, noodleLid);
            });

            SavePickup("first_aid", "First Aid Kit", 0.8f, root =>
            {
                var box = Part(PrimitiveType.Cube, "Box", root, new Vector3(0f, 0.07f, 0f),
                    new Vector3(0.2f, 0.14f, 0.07f), firstAid);
                box.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Part(PrimitiveType.Cube, "Handle", root, new Vector3(0f, 0.15f, 0f),
                    new Vector3(0.08f, 0.02f, 0.02f), wick);
            });

            SavePickup("matches", "Matches", 0.05f, root =>
            {
                // Big "kitchen matches" box (a real pocket box is too tiny to see at PS1 resolution)
                Part(PrimitiveType.Cube, "Box", root, new Vector3(0f, 0.035f, 0f),
                    new Vector3(0.1f, 0.07f, 0.03f), matches).transform.localRotation =
                    Quaternion.Euler(0f, 180f, 0f);
            });

            foreach (var p in Products) MakeItemData(p.id, p.name);
            AddToShoppingList();

            AssetDatabase.SaveAssets();
            Debug.Log($"[Products] Made {Products.Length} survival products in " + PrefabFolder +
                      ". Next: Tools → Products → 2. Add Product Shelves To Open Scene");
        }

        /// <summary>
        /// Command line (Unity closed): builds the products, then the shelves in -productScene.
        /// Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.SurvivalProductsTool.BuildFromCommandLine
        ///       -productScene Assets/_Project/Scenes/Dev/user.unity
        /// </summary>
        public static void BuildFromCommandLine()
        {
            MakeProducts();
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-productScene");
            if (i < 0 || i + 1 >= args.Length) return;
            EditorSceneManager.OpenScene(args[i + 1]);
            PlaceShelves(EditorSceneManager.GetActiveScene());
        }

        /// <summary>PS1 look: crisp pixels (Point), no mipmaps, no compression blur.</summary>
        static void SetUpTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }

        static Material Mat(string name, string texture, float smoothness = 0f, float metallic = 0f)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Game/PS1 Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{texture}.png");
            if (tex == null) Debug.LogWarning($"[Products] Texture not found: {TexFolder}/{texture}.png");

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            mat.mainTexture = tex;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Builds the model with <paramref name="build"/>, adds collider, Rigidbody and Pickup, saves the prefab.</summary>
        static void SavePickup(string id, string displayName, float mass, System.Action<Transform> build)
        {
            var root = new GameObject("Pickup_" + id);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            build(model);
            model.localScale = Vector3.one * ItemSize; // the collider below is measured after scaling

            // One simple box around the whole model
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.None;                 // held items must not lag
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // small: don't fall through

            var pickup = root.AddComponent<Game.Interaction.Pickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("itemId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Pickup_{id}.prefab");
            Object.DestroyImmediate(root);
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
                               Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // the root has one box for everything
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Unity's cylinder is 1 wide and 2 tall: scale it to diameter / height, centre at y.</summary>
        static GameObject Cylinder(Transform parent, string name, float diameter, float height, float centerY,
                                   Material mat) =>
            Part(PrimitiveType.Cylinder, name, parent, new Vector3(0f, centerY, 0f),
                new Vector3(diameter, height / 2f, diameter), mat);

        static void MakeItemData(string id, string displayName)
        {
            string path = $"{ItemsFolder}/Item_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.id = id;
            data.displayName = displayName;
            data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Pickup_{id}.prefab");
            EditorUtility.SetDirty(data);
        }

        static void AddToShoppingList()
        {
            var list = AssetDatabase.LoadAssetAtPath<ShoppingListData>(ListPath);
            if (list == null) return;

            foreach (var p in Products)
            {
                if (!p.onList) continue;
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_{p.id}.asset");
                if (data == null || list.entries.Exists(e => e != null && e.item == data)) continue;
                list.entries.Add(new ShoppingListData.Entry { item = data, amount = 1 });
            }
            EditorUtility.SetDirty(list);
        }

        // ---------------------------------------------------------------- 2. shelves

        [MenuItem("Tools/Products/2. Add Product Shelves To Open Scene", priority = 51)]
        public static void AddProductShelves()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene &&
                !EditorUtility.DisplayDialog("Product shelves",
                    "This edits the shared Main.unity scene. Tell your team before pushing it.",
                    "Add to Main", "Cancel"))
                return;

            PlaceShelves(scene);
        }

        static void PlaceShelves(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("ProductShelves");
            if (old != null) Object.DestroyImmediate(old);

            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            Vector3 fwd = player != null ? Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized
                                         : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            var group = new GameObject("ProductShelves").transform;

            // Product shelves + one empty shelf at the end, in two rows on the player's LEFT
            // (5 per row) with an aisle between them; all fronts face the player's side.
            const int perRow = 5;
            int count = Products.Length + 1;
            for (int i = 0; i < count; i++)
            {
                bool empty = i == Products.Length;
                ItemData data = empty ? null
                    : AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_{Products[i].id}.asset");
                if (!empty && data == null)
                {
                    Debug.LogWarning("[Products] Run Tools → Products → 1. Make Survival Products first.");
                    Object.DestroyImmediate(group.gameObject);
                    return;
                }

                var shelf = new GameObject(empty ? "Shelf_empty" : $"Shelf_{data.id}");
                shelf.transform.SetParent(group, false);
                int row = i / perRow, col = i % perRow;
                Vector3 pos = origin - right * (3f + row * 3.5f) + fwd * (1f + col * 1.9f);
                shelf.transform.position = new Vector3(pos.x, origin.y, pos.z);
                shelf.transform.rotation = Quaternion.LookRotation(right);

                var comp = shelf.AddComponent<Shelf>();
                var so = new SerializedObject(comp);
                so.FindProperty("item").objectReferenceValue = data;
                so.FindProperty("stock").intValue = empty ? 0 : Products[i].stock;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCreatedObjectUndo(group.gameObject, "Add Product Shelves");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = group.gameObject;
            Debug.Log("[Products] Added product shelves + an empty shelf on the player's left. Press Play.");
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
