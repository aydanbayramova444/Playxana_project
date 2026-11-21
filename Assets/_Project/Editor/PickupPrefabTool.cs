using Game.Gameplay;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Level → Make Store Pickup Prefabs
    ///   Builds Prefabs/Pickups/Pickup_*.prefab from the Ultimate Food Pack:
    ///   real-world size, a simple BoxCollider, a Rigidbody and the Pickup script (with an item id).
    ///   The original pack is not changed. Safe to run again (rebuilds the prefabs).
    ///
    /// Tools → Level → Add Pickup Showcase
    ///   Puts one of each on a long table at the back of the walk platform, to try them out.
    ///
    /// Add more items in the Items list below.
    /// </summary>
    public static class PickupPrefabTool
    {
        const string PackPrefabs = "Assets/Ultimate_Food_Pack/Prefabs";
        const string OutFolder   = "Assets/_Project/Prefabs/Pickups";

        // model name in the pack, item id, name shown to the player, biggest side in metres
        static readonly (string model, string id, string name, float size)[] Items =
        {
            ("Soda",               "soda",          "Soda",          0.24f),
            ("Bottle1",            "water",         "Water Bottle",  0.30f),
            ("Bottle2",            "juice",         "Juice",         0.30f),
            ("KetchupBottle",      "ketchup",       "Ketchup",       0.20f),
            ("MustardBottle",      "mustard",       "Mustard",       0.20f),
            ("MayoBottle",         "mayo",          "Mayo",          0.20f),
            ("ChocolateBar",       "chocolate",     "Chocolate Bar", 0.16f),
            ("Bread",              "bread",         "Bread",         0.30f),
            ("Banana",             "banana",        "Banana",        0.20f),
            ("Apple",              "apple",         "Apple",         0.08f),
            ("Orange",             "orange",        "Orange",        0.08f),
            ("Tomato",             "tomato",        "Tomato",        0.08f),
            ("PeanutButter",       "peanut_butter", "Peanut Butter", 0.13f),
            ("Jar_Large",          "jar",           "Jar",           0.16f),
            ("Donut1",             "donut",         "Donut",         0.10f),
            ("Cupcake",            "cupcake",       "Cupcake",       0.09f),
            ("Croissant",          "croissant",     "Croissant",     0.16f),
            ("Cheese_Singles",     "cheese",        "Cheese",        0.10f),
            ("Popsicle_Chocolate", "popsicle",      "Popsicle",      0.16f),
            ("Fish",               "fish",          "Fish",          0.35f),
            ("Steak",              "steak",         "Steak",         0.22f),
            ("Pumpkin",            "pumpkin",       "Pumpkin",       0.30f),
            ("Coconut",            "coconut",       "Coconut",       0.13f),
            ("Eggplant",           "eggplant",      "Eggplant",      0.20f),
            ("Carrot",             "carrot",        "Carrot",        0.18f),
        };

        public static string PrefabPath(string id) => $"{OutFolder}/Pickup_{id}.prefab";

        // ---------- Build prefabs ----------

        [MenuItem("Tools/Level/Make Store Pickup Prefabs", priority = 20)]
        static void MakePrefabsMenu() => MakePrefabs(showDialog: true);

        /// <summary>One click: pickup prefabs + test objects + food table in the OPEN scene.</summary>
        [MenuItem("Tools/Level/Add Interaction Demo (all in one)", priority = 0)]
        static void AddEverything()
        {
            if (Object.FindAnyObjectByType<PlayerController>() == null)
            {
                EditorUtility.DisplayDialog("Interaction Demo",
                    "No player in this scene. Open a scene with the Player (e.g. Main).", "OK");
                return;
            }

            MakePrefabs(showDialog: false);
            InteractionTestTool.AddTestObjects();
            AddShowcase();

            EditorUtility.DisplayDialog("Interaction Demo",
                "Added to this scene:\n" +
                "- InteractionTest (shelf, basket, bin, register, mop, spill, door lock)\n" +
                "- PickupShowcase (table with 25 foods)\n\n" +
                "Press Play and press E on things. Save with Ctrl+S.", "OK");
        }

        public static void MakePrefabs(bool showDialog)
        {
            EnsureFolder(OutFolder);
            int made = 0;

            foreach (var item in Items)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabs}/{item.model}.prefab");
                if (src == null)
                {
                    Debug.LogWarning($"[Pickups] Not found: {PackPrefabs}/{item.model}.prefab");
                    continue;
                }

                var root = new GameObject("Pickup_" + item.id);

                // The visible model (copy of the pack prefab, without its heavy MeshColliders)
                var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                model.name = "Model";
                foreach (var c in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
                model.transform.SetParent(root.transform, false);

                // Scale to real size
                Bounds b = RendererBounds(model);
                float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (biggest > 0.0001f)
                    model.transform.localScale *= item.size / biggest;

                // Center the model on the root, bottom at y = 0
                b = RendererBounds(model);
                model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                b = RendererBounds(model);

                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.interpolation = RigidbodyInterpolation.None; // interpolated + held = item lags behind
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // small items: don't fall through

                var pickup = root.AddComponent<Game.Interaction.Pickup>();
                var so = new SerializedObject(pickup);
                so.FindProperty("itemId").stringValue = item.id;
                so.FindProperty("displayName").stringValue = item.name;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(item.id));
                Object.DestroyImmediate(root);
                made++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Pickups] Made {made} pickup prefabs in {OutFolder}");
            if (showDialog) EditorUtility.DisplayDialog("Pickup Prefabs",
                $"Made {made} pickup prefabs in\n{OutFolder}\n\nDrag them into any scene, " +
                "or use Tools → Level → Add Pickup Showcase.", "OK");
        }

        // ---------- Showcase in the scene ----------

        [MenuItem("Tools/Level/Add Pickup Showcase", priority = 21)]
        static void AddShowcase()
        {
            if (Object.FindAnyObjectByType<PlayerController>() == null)
            {
                EditorUtility.DisplayDialog("Pickup Showcase",
                    "No player in this scene.\nFirst use Tools → Level → Create Walk Platform Scene.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Items[0].id)) == null)
                MakePrefabs(showDialog: false);

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player.GetComponent<PlayerInteractor>() == null)
                Undo.AddComponent<PlayerInteractor>(player.gameObject);

            var old = GameObject.Find("PickupShowcase");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("PickupShowcase");
            Undo.RegisterCreatedObjectUndo(root, "Add Pickup Showcase");

            // Long table along the back wall
            const float tableLen = 16f, tableHeight = 0.8f, z = 6.6f;
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Display Table";
            table.transform.SetParent(root.transform, false);
            table.transform.position = new Vector3(0f, tableHeight / 2f, z);
            table.transform.localScale = new Vector3(tableLen, tableHeight, 0.9f);
            var tableMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Blockout/M_Shelf.mat");
            if (tableMat != null) table.GetComponent<MeshRenderer>().sharedMaterial = tableMat;

            for (int i = 0; i < Items.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Items[i].id));
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                float x = Mathf.Lerp(-tableLen / 2f + 0.4f, tableLen / 2f - 0.4f, i / (Items.Length - 1f));
                go.transform.position = new Vector3(x, tableHeight + 0.01f, z);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Pickups] Showcase added on the back table. Press Play and press E on any item.");
        }

        // ---------- Helpers ----------

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
