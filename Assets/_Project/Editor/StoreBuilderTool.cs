using System.IO;
using Game.Gameplay;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Store → Build Small Store In Open Scene
    ///   A small PS1-style convenience store around the player (player = just inside the door):
    ///   floor, walls with an entrance, ceiling with fluorescent lights, a "МАГАЗИН 24" sign,
    ///   14 shelves (6 along the back wall + 2 islands) filled with every product,
    ///   a checkout counter with a register and the shopping "cart".
    ///   Removes the old test objects (Shelves, ProductShelves, ShoppingTestItems, Cart)
    ///   and hides test boxes of the Level that would stand inside the store.
    ///   Run Tools → Products → 1. Make Survival Products first (done automatically from the command line).
    ///   Safe to run again: the "Store" object is rebuilt.
    ///
    /// Size: inside 14 m wide (x), 10 m deep (z), 3.2 m high. Entrance in the middle of the front wall.
    /// </summary>
    public static class StoreBuilderTool
    {
        const string TexFolder   = "Assets/_Project/Art/Textures/Store";
        const string MatFolder   = "Assets/_Project/Art/Materials/Store";
        const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        const string MainScene   = "Assets/_Project/Scenes/Main.unity";

        const float HalfW = 7f, Front = -1f, Back = 9f, Height = 3.2f, Wall = 0.2f, DoorHalf = 1.1f;

        // Back wall (left → right), then the two islands. null = empty shelf (put anything on it).
        static readonly (string id, int stock)[] BackWall =
        {
            ("water_jug", 3), ("canned_meat", 8), ("buckwheat", 6),
            ("noodles", 8), ("toilet_paper", 4), ("first_aid", 2),
        };
        static readonly (string id, int stock)[] Islands =
        {
            ("batteries", 6), ("candles", 6), ("matches", 12), (null, 0),   // left island
            ("banana", 6), ("soda", 6), ("apple", 6), ("bread", 4),         // right island
        };

        [MenuItem("Tools/Store/Build Small Store In Open Scene", priority = 70)]
        public static void BuildMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene &&
                !EditorUtility.DisplayDialog("Store", "This edits the shared Main.unity scene. Tell your team first.",
                    "Build in Main", "Cancel"))
                return;
            Build();
        }

        /// <summary>
        /// Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.StoreBuilderTool.BuildFromCommandLine
        ///       -storeScene Assets/_Project/Scenes/Dev/user.unity
        /// </summary>
        public static void BuildFromCommandLine()
        {
            SurvivalProductsTool.MakeProducts();
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-storeScene");
            if (i >= 0 && i + 1 < args.Length) EditorSceneManager.OpenScene(args[i + 1]);
            Build();
        }

        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            AssetDatabase.Refresh();
            EnsureFolder(MatFolder);
            SetUpTextures();

            // Old test layout out of the way
            foreach (string n in new[] { "Store", "Shelves", "ProductShelves", "ShoppingTestItems", "Cart" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            var root = new GameObject("Store").transform;

            // Materials: projected textures (no UVs needed), 1 texture repeat per metre
            Material floor   = Mat("M_StoreFloor", "T_StoreFloor", 1f);
            Material wall    = Mat("M_StoreWall", "T_StoreWall", 1f);
            Material ceiling = Mat("M_StoreCeiling", "T_StoreCeiling", 1f);
            Material wood    = Mat("M_StoreWood", "T_StoreWood", 2f);
            Material cartMat = Mat("M_StoreCart", "T_StoreCart", 6f);
            Material light   = Mat("M_StoreLight", "T_StoreLight", 4f, ambient: 2f);
            Material sign    = Mat("M_StoreSign", "T_StoreSign", 0f);

            float depth = Back - Front, midZ = (Back + Front) / 2f;

            // ---- Room
            var room = Group(root, "Room");
            Box(room, "Floor", new Vector3(0, -0.045f, midZ), new Vector3(HalfW * 2 + Wall * 2, 0.1f, depth + Wall * 2), floor);
            Box(room, "Ceiling", new Vector3(0, Height + 0.05f, midZ), new Vector3(HalfW * 2 + Wall * 2, 0.1f, depth + Wall * 2), ceiling);
            Box(room, "Wall_Back", new Vector3(0, Height / 2, Back + Wall / 2), new Vector3(HalfW * 2 + Wall * 2, Height, Wall), wall);
            Box(room, "Wall_Left", new Vector3(-HalfW - Wall / 2, Height / 2, midZ), new Vector3(Wall, Height, depth), wall);
            Box(room, "Wall_Right", new Vector3(HalfW + Wall / 2, Height / 2, midZ), new Vector3(Wall, Height, depth), wall);
            // Front wall with the door gap in the middle
            float sideW = HalfW - DoorHalf + Wall;
            Box(room, "Wall_Front_L", new Vector3(-(DoorHalf + sideW / 2), Height / 2, Front - Wall / 2), new Vector3(sideW, Height, Wall), wall);
            Box(room, "Wall_Front_R", new Vector3(DoorHalf + sideW / 2, Height / 2, Front - Wall / 2), new Vector3(sideW, Height, Wall), wall);
            Box(room, "Wall_Front_Top", new Vector3(0, (2.4f + Height) / 2, Front - Wall / 2), new Vector3(DoorHalf * 2, Height - 2.4f, Wall), wall);

            // Sign above the door, outside
            var signGo = Box(room, "Sign_Magazin24", new Vector3(0, 2.8f, Front - Wall - 0.03f), new Vector3(2.4f, 0.4f, 0.04f), sign);
            Object.DestroyImmediate(signGo.GetComponent<Collider>()); // the cube's -Z face (outside) shows the sign

            // ---- Fluorescent lights (fixture + point light), 2 rows x 3
            var lights = Group(root, "Lights");
            foreach (float z in new[] { 1.5f, 6.2f })
            foreach (float x in new[] { -4.2f, 0f, 4.2f })
            {
                var fixture = Box(lights, "LightPanel", new Vector3(x, Height - 0.03f, z), new Vector3(1.2f, 0.05f, 0.3f), light);
                Object.DestroyImmediate(fixture.GetComponent<Collider>());

                var l = new GameObject("PointLight").AddComponent<Light>();
                l.transform.SetParent(fixture.transform.parent, false);
                l.transform.position = new Vector3(x, Height - 0.25f, z);
                l.type = LightType.Point;
                l.range = 8f;
                l.intensity = 2.2f;
                l.color = new Color(0.92f, 1f, 0.9f); // cold, slightly green fluorescent
                l.shadows = LightShadows.None;
            }

            // ---- Shelves
            var shelves = Group(root, "Shelves");
            const float spacing = 2.1f;
            for (int i = 0; i < BackWall.Length; i++)
            {
                float x = (i - (BackWall.Length - 1) / 2f) * spacing;
                AddShelf(shelves, BackWall[i], new Vector3(x, 0, Back - 0.3f), Vector3.back);
            }
            // Islands: back-to-back pairs. Index 0-3 left island, 4-7 right island;
            // in each island: [0] [1] face the entrance, [2] [3] face the back wall.
            const float islandZ = 4.2f;
            for (int i = 0; i < Islands.Length; i++)
            {
                int island = i / 4, k = i % 4;
                float baseX = island == 0 ? -3.3f : 3.3f;
                float x = baseX + (k % 2 == 0 ? -0.9f : 0.9f);
                bool facesEntrance = k < 2;
                float z = islandZ + (facesEntrance ? -0.26f : 0.26f);
                AddShelf(shelves, Islands[i], new Vector3(x, 0, z), facesEntrance ? Vector3.back : Vector3.forward);
            }

            // ---- Checkout: counter, register, cart
            var checkout = Group(root, "Checkout");
            Box(checkout, "Counter", new Vector3(4.6f, 0.5f, 0.9f), new Vector3(0.8f, 1f, 2.4f), wood);
            var register = Box(checkout, "Register", new Vector3(4.6f, 1.15f, 0.5f), new Vector3(0.45f, 0.3f, 0.4f), ceiling);
            var regUse = register.AddComponent<Game.Interaction.Interactable>();
            var rso = new SerializedObject(regUse);
            rso.FindProperty("displayName").stringValue = "Register";
            rso.FindProperty("prompt").stringValue = "Pay";
            rso.ApplyModifiedPropertiesWithoutUndo();

            var cart = Box(root, "Cart", new Vector3(2.8f, 0.35f, 0.6f), new Vector3(0.9f, 0.7f, 0.6f), cartMat);
            var slot = cart.AddComponent<Game.Interaction.ItemSlot>();
            var cso = new SerializedObject(slot);
            cso.FindProperty("slotId").stringValue = "cart";
            cso.FindProperty("displayName").stringValue = "Cart";
            cso.FindProperty("prompt").stringValue = "Put in cart:";
            cso.FindProperty("consumeItems").boolValue = true;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // ---- Player just inside the door, looking into the store
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Move player");
                player.transform.SetPositionAndRotation(new Vector3(0, player.transform.position.y, 0.2f),
                    Quaternion.LookRotation(Vector3.forward));
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            }

            HideLevelBoxesInside();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Store");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = root.gameObject;
            Debug.Log("[Store] Built a 14 x 10 m store with 14 shelves, checkout and cart. Press Play.");
        }

        // ------------------------------------------------------------------

        static void AddShelf(Transform parent, (string id, int stock) entry, Vector3 pos, Vector3 facing)
        {
            ItemData data = null;
            if (entry.id != null)
            {
                data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_{entry.id}.asset");
                if (data == null)
                    Debug.LogWarning($"[Store] No Item_{entry.id}.asset — run Tools → Products → 1. Make Survival Products. " +
                                     "This shelf stays empty.");
            }

            var go = new GameObject(data != null ? $"Shelf_{data.id}" : "Shelf_empty");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));

            var shelf = go.AddComponent<Shelf>();
            var so = new SerializedObject(shelf);
            so.FindProperty("item").objectReferenceValue = data;
            so.FindProperty("stock").intValue = data != null ? entry.stock : 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Test boxes of the Level standing inside the store are switched off (not deleted).</summary>
        static void HideLevelBoxesInside()
        {
            var level = GameObject.Find("Level");
            if (level == null) return;
            var inside = new Bounds(new Vector3(0, 1.5f, (Back + Front) / 2f), new Vector3(HalfW * 2 + 1, 4, Back - Front + 1));
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
            {
                if (t == level.transform || t.name == "Ground") continue;
                if (!t.TryGetComponent(out Renderer r) || !inside.Intersects(r.bounds)) continue;
                Undo.RecordObject(t.gameObject, "Hide box");
                t.gameObject.SetActive(false);
            }
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static void SetUpTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }

        /// <param name="tilesPerMetre">0 = use the mesh UVs (e.g. the sign), otherwise projected texture.</param>
        static Material Mat(string name, string texture, float tilesPerMetre, float ambient = 1f)
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
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_AmbientBoost")) mat.SetFloat("_AmbientBoost", ambient);

            bool project = tilesPerMetre > 0f;
            if (mat.HasProperty("_Triplanar"))
            {
                mat.SetFloat("_Triplanar", project ? 1f : 0f);
                mat.SetFloat("_TriplanarScale", tilesPerMetre);
                if (project) mat.EnableKeyword("_TRIPLANAR");
                else mat.DisableKeyword("_TRIPLANAR");
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
