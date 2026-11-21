using Game.Gameplay;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Level → Add Interaction Test Objects
    ///
    /// Adds grey-box test objects to the OPEN scene (made for the walk platform, 20 x 16 m)
    /// and puts a PlayerInteractor on the player. Try:
    ///   3 sodas  → shelf (3 spots)        Restock
    ///   banana   → basket                 Lost item
    ///   trash    → bin (disappears)       Trash
    ///   register → press E                Check register
    ///   mop + spill → hold E              Clean the spill
    ///   door lock at the entrance → E     Lock up
    /// Watch the Console: every action is logged.
    /// </summary>
    public static class InteractionTestTool
    {
        const string MatFolder = "Assets/_Project/Art/Materials/Blockout";

        [MenuItem("Tools/Level/Add Interaction Test Objects", priority = 11)]
        static void AddTestObjects()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Interaction Test",
                    "No player in this scene.\nFirst use Tools → Level → Create Walk Platform Scene.", "OK");
                return;
            }

            if (player.GetComponent<PlayerInteractor>() == null)
                Undo.AddComponent<PlayerInteractor>(player.gameObject);

            var old = GameObject.Find("InteractionTest");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("InteractionTest");
            Undo.RegisterCreatedObjectUndo(root, "Add Interaction Test Objects");
            root.AddComponent<InteractionTestLogger>();

            // --- Restock: 3 sodas on a crate → shelf with 3 spots
            Model("Crate", root.transform, PrimitiveType.Cube, new Vector3(-4f, 0.3f, -4f),
                new Vector3(0.8f, 0.6f, 0.6f), Mat("Crate", new Color(0.6f, 0.45f, 0.3f)));
            for (int i = 0; i < 3; i++)
                Item("Soda " + (i + 1), root.transform, "soda", "Soda", PrimitiveType.Cylinder,
                    new Vector3(-4.25f + i * 0.25f, 0.85f, -4f), new Vector3(0.12f, 0.12f, 0.12f),
                    Mat("Soda", new Color(0.85f, 0.15f, 0.15f)));

            Receiver("Shelf", root.transform, new Vector3(-7f, 0f, 2f),
                new Vector3(2f, 1f, 0.5f), Mat("Shelf", new Color(0.55f, 0.55f, 0.6f)),
                "shelf_soda", "Shelf", "Put {item} on {target}", new[] { "soda" }, 3, false);

            // --- Lost item: banana → basket
            Item("Banana", root.transform, "banana", "Banana", PrimitiveType.Capsule,
                new Vector3(6f, 0.12f, 5f), new Vector3(0.12f, 0.18f, 0.12f),
                Mat("Banana", new Color(1f, 0.85f, 0.1f)), Quaternion.Euler(0f, 0f, 90f));
            Receiver("Basket", root.transform, new Vector3(2f, 0f, -5f),
                new Vector3(0.6f, 0.3f, 0.45f), Mat("Basket", new Color(0.3f, 0.5f, 0.85f)),
                "basket", "Basket", "Put {item} in {target}", new[] { "banana" }, 1, false);

            // --- Trash → bin
            Item("Trash", root.transform, "trash", "Trash", PrimitiveType.Cube,
                new Vector3(-2f, 0.1f, 4f), Vector3.one * 0.2f, Mat("Trash", new Color(0.4f, 0.4f, 0.35f)));
            Receiver("Trash Bin", root.transform, new Vector3(-8.5f, 0f, -6.5f),
                new Vector3(0.6f, 0.8f, 0.6f), Mat("Bin", new Color(0.2f, 0.35f, 0.2f)),
                "trash_bin", "Trash Bin", "Throw {item} in {target}", new[] { "trash" }, 0, true);

            // --- Register: press E
            var register = Model("Register", root.transform, PrimitiveType.Cube, new Vector3(7f, 0.5f, -3f),
                new Vector3(1f, 1f, 0.6f), Mat("Register", new Color(0.25f, 0.25f, 0.3f)));
            Usable(register, "register", "Register", "Check", 0f, "", "", false);

            // --- Spill: needs the mop, hold E
            Item("Mop", root.transform, "mop", "Mop", PrimitiveType.Cylinder,
                new Vector3(4f, 0.75f, 2f), new Vector3(0.06f, 0.75f, 0.06f), Mat("Mop", new Color(0.9f, 0.9f, 0.85f)));
            var spill = Model("Spill", root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 3f),
                new Vector3(1.4f, 0.01f, 1.4f), Mat("Spill", new Color(0.35f, 0.6f, 1f)));
            Object.DestroyImmediate(spill.GetComponent<Collider>());
            var spillCol = spill.AddComponent<BoxCollider>();
            spillCol.isTrigger = true; // walk over it, but you can still aim at it
            spillCol.size = new Vector3(1f, 20f, 1f);
            Usable(spill, "spill", "Spill", "Clean", 2f, "mop", "Mop", true);

            // --- Door lock next to the entrance
            var doorLock = Model("Door Lock", root.transform, PrimitiveType.Cube, new Vector3(1.6f, 1.2f, -7.85f),
                new Vector3(0.3f, 0.4f, 0.1f), Mat("Lock", new Color(0.9f, 0.6f, 0.1f)));
            Usable(doorLock, "door_lock", "Door", "Lock", 0f, "", "", false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Interaction] Test objects added. Press Play: sodas → shelf, banana → basket, " +
                      "trash → bin, E on register, take mop → hold E on spill, E on door lock.");
        }

        // ---------- Builders ----------

        static GameObject Model(string name, Transform parent, PrimitiveType type, Vector3 pos,
            Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void Item(string name, Transform parent, string id, string display, PrimitiveType type,
            Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = Model(name, parent, type, pos, scale, mat, rot);
            go.AddComponent<Rigidbody>();
            var p = go.AddComponent<Pickup>();
            Set(p, "itemId", id);
            Set(p, "displayName", display);
        }

        /// <summary>Receiver root (unscaled) with a scaled model child and spots on top.</summary>
        static ItemReceiver Receiver(string name, Transform parent, Vector3 floorPos, Vector3 size,
            Material mat, string id, string display, string prompt, string[] accepts, int spotCount, bool consume)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = floorPos;

            var model = Model("Model", root.transform, PrimitiveType.Cube,
                floorPos + Vector3.up * size.y / 2f, size, mat);

            var r = root.AddComponent<ItemReceiver>();
            var so = new SerializedObject(r);
            so.FindProperty("receiverId").stringValue = id;
            so.FindProperty("displayName").stringValue = display;
            so.FindProperty("promptFormat").stringValue = prompt;
            so.FindProperty("consumeItems").boolValue = consume;

            var acc = so.FindProperty("acceptedItemIds");
            acc.arraySize = accepts.Length;
            for (int i = 0; i < accepts.Length; i++) acc.GetArrayElementAtIndex(i).stringValue = accepts[i];

            var spots = so.FindProperty("spots");
            spots.arraySize = spotCount;
            for (int i = 0; i < spotCount; i++)
            {
                var spot = new GameObject("Spot " + (i + 1)).transform;
                spot.SetParent(root.transform, false);
                float x = spotCount == 1 ? 0f : Mathf.Lerp(-size.x / 2f + 0.3f, size.x / 2f - 0.3f, i / (spotCount - 1f));
                spot.position = floorPos + new Vector3(x, size.y, 0f);
                spots.GetArrayElementAtIndex(i).objectReferenceValue = spot;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return r;
        }

        static void Usable(GameObject go, string id, string display, string action, float hold,
            string needId, string needName, bool destroyAfter)
        {
            var u = go.AddComponent<UsableObject>();
            var so = new SerializedObject(u);
            so.FindProperty("objectId").stringValue = id;
            so.FindProperty("displayName").stringValue = display;
            so.FindProperty("actionText").stringValue = action;
            so.FindProperty("holdSeconds").floatValue = hold;
            so.FindProperty("requiredItemId").stringValue = needId;
            so.FindProperty("requiredItemName").stringValue = needName;
            so.FindProperty("destroyAfterUse").boolValue = destroyAfter;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Mat(string name, Color color)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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
