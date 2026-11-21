using System.IO;
using System.Text.RegularExpressions;
using Game.Gameplay;
using Game.Player;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Shopping List → Set Up Everything (My Dev Scene)
    ///   1. ItemData assets (banana, soda, apple, bread) in ScriptableObjects/Items
    ///   2. ShoppingList_Test asset (banana x2, soda, apple, bread)
    ///   3. Prefabs/UI/ShoppingListHUD.prefab (Canvas + top-left panel)
    ///   4. Opens Scenes/Dev/&lt;you&gt;.unity (copy of Main) and adds: tracker, HUD, a "cart",
    ///      a few test items and a PlayerInteractor on the player (scene only, prefab untouched).
    /// Safe to run again: existing assets are reused, scene objects are not duplicated.
    /// Never touches Main.unity.
    /// </summary>
    public static class ShoppingListSetupTool
    {
        const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        const string ListPath    = "Assets/_Project/ScriptableObjects/ShoppingList_Test.asset";
        const string HudFolder   = "Assets/_Project/Prefabs/UI";
        const string HudPath     = HudFolder + "/ShoppingListHUD.prefab";
        const string MainScene   = "Assets/_Project/Scenes/Main.unity";
        const string DevFolder   = "Assets/_Project/Scenes/Dev";
        const string PackFolder  = "Assets/Ultimate_Food_Pack/Prefabs";

        // id (= Pickup.ItemId), name, model in the Food Pack, amount on the test list
        static readonly (string id, string name, string model, int amount)[] Items =
        {
            ("banana", "Banana", "Banana", 2),
            ("soda",   "Soda",   "Soda",   1),
            ("apple",  "Apple",  "Apple",  1),
            ("bread",  "Bread",  "Bread",  1),
        };

        [MenuItem("Tools/Shopping List/Set Up Everything (My Dev Scene)", priority = 30)]
        public static void SetUpEverything()
        {
            ShoppingListData list = CreateItemsAndList();
            GameObject hudPrefab = CreateHudPrefab();
            if (!OpenMyDevScene()) return;
            SetUpScene(list, hudPrefab);
        }

        /// <summary>
        /// Same as above, but into the shared Main.unity: tracker, HUD, cart and shelves
        /// (no loose test items — items come from the shelves).
        /// </summary>
        [MenuItem("Tools/Shopping List/Add To Main Scene", priority = 31)]
        public static void AddToMainScene()
        {
            if (!EditorUtility.DisplayDialog("Add shopping list to Main",
                    "This edits the shared Main.unity scene (tracker, HUD, cart, 4 shelves).\n" +
                    "Tell your team before pushing it, so nobody else edits Main at the same time.",
                    "Add to Main", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AddToMainSceneNoPrompt();
        }

        /// <summary>No dialogs — also for command line: Unity -batchmode -executeMethod
        /// Game.EditorTools.ShoppingListSetupTool.AddToMainSceneNoPrompt -quit</summary>
        public static void AddToMainSceneNoPrompt()
        {
            ShoppingListData list = CreateItemsAndList();
            GameObject hudPrefab = CreateHudPrefab();
            EditorSceneManager.OpenScene(MainScene);

            SetUpScene(list, hudPrefab, allowMain: true, addTestItems: false);
            ShelfSetupTool.BuildShelves(allowMain: true);
        }

        // ---------- 1 + 2: data assets ----------

        static ShoppingListData CreateItemsAndList()
        {
            EnsureFolder(ItemsFolder);

            var list = AssetDatabase.LoadAssetAtPath<ShoppingListData>(ListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<ShoppingListData>();
                AssetDatabase.CreateAsset(list, ListPath);
            }
            list.entries.Clear();

            foreach (var it in Items)
            {
                string path = $"{ItemsFolder}/Item_{it.id}.asset";
                var data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(data, path);
                }

                data.id = it.id;
                data.displayName = it.name;
                // Prefer the teammate's ready pickup prefab, else the raw Food Pack model.
                data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabTool.PrefabPath(it.id));
                if (data.prefab == null)
                    data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackFolder}/{it.model}.prefab");
                EditorUtility.SetDirty(data);

                list.entries.Add(new ShoppingListData.Entry { item = data, amount = it.amount });
            }

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        // ---------- 3: HUD prefab ----------

        static GameObject CreateHudPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (existing != null)
            {
                FixMissingFonts();
                return AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            }

            EnsureFolder(HudFolder);

            var root = new GameObject("ShoppingListHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Top-left panel that grows with its rows.
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(ShoppingListHUD));
            panel.transform.SetParent(root.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);

            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title row
            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(panel.transform, false);
            var titleText = title.GetComponent<TextMeshProUGUI>();
            titleText.text = "<b>Shopping list</b>";
            titleText.font = TMP_Settings.defaultFontAsset; // needs TMP Essentials imported
            titleText.fontSize = 30f;
            titleText.raycastTarget = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>If the HUD prefab was made before TMP Essentials were imported, its text has no font.</summary>
        static void FixMissingFonts()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
            bool changed = false;
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font != null) continue;
                t.font = font;
                changed = true;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------- 4: dev scene ----------

        static bool OpenMyDevScene()
        {
            string user = Regex.Replace(System.Environment.UserName, "[^A-Za-z0-9_-]", "");
            if (string.IsNullOrEmpty(user)) user = "Dev";
            string path = $"{DevFolder}/{user}.unity";

            if (!File.Exists(path))
            {
                EnsureFolder(DevFolder);
                if (!AssetDatabase.CopyAsset(MainScene, path))
                {
                    Debug.LogError($"[ShoppingList] Could not copy {MainScene} to {path}");
                    return false;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(path);
            return true;
        }

        static void SetUpScene(ShoppingListData list, GameObject hudPrefab,
                               bool allowMain = false, bool addTestItems = true)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene && !allowMain)
            {
                Debug.LogError("[ShoppingList] Refusing to edit Main.unity.");
                return;
            }

            // Reload by path: references from before OpenScene can go stale.
            list = AssetDatabase.LoadAssetAtPath<ShoppingListData>(ListPath);
            hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);

            // Tracker
            var tracker = Object.FindAnyObjectByType<ShoppingListTracker>();
            if (tracker == null)
                tracker = new GameObject("ShoppingListTracker").AddComponent<ShoppingListTracker>();
            var tso = new SerializedObject(tracker);
            tso.FindProperty("shoppingList").objectReferenceValue = list;
            tso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tracker);

            // HUD (prefab instance) → point it at the tracker
            var hud = Object.FindAnyObjectByType<ShoppingListHUD>();
            if (hud == null)
            {
                var hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
                hud = hudGo.GetComponentInChildren<ShoppingListHUD>();
            }
            var hso = new SerializedObject(hud);
            hso.FindProperty("tracker").objectReferenceValue = tracker;
            hso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);

            // Player: needs PlayerInteractor to pick things up (added in THIS scene only)
            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            Vector3 fwd = player != null ? player.transform.forward : Vector3.forward;
            Vector3 right = player != null ? player.transform.right : Vector3.right;
            if (player != null && player.GetComponent<PlayerInteractor>() == null)
                player.gameObject.AddComponent<PlayerInteractor>();

            // Cart: a cube with ItemReceiver "cart" that eats items
            if (GameObject.Find("Cart") == null)
            {
                var cart = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cart.name = "Cart";
                cart.transform.position = origin + fwd * 3f + Vector3.up * 0.4f;
                cart.transform.localScale = new Vector3(1f, 0.8f, 0.6f);
                var receiver = cart.AddComponent<ItemReceiver>();
                var rso = new SerializedObject(receiver);
                rso.FindProperty("receiverId").stringValue = "cart";
                rso.FindProperty("displayName").stringValue = "Cart";
                rso.FindProperty("promptFormat").stringValue = "Put {item} in {target}";
                rso.FindProperty("consumeItems").boolValue = true;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Test items in a row to the right of the cart (+ one wrong item: orange)
            if (addTestItems && GameObject.Find("ShoppingTestItems") == null)
            {
                var group = new GameObject("ShoppingTestItems").transform;
                string[] ids = { "banana", "banana", "soda", "apple", "bread", "orange" };
                for (int i = 0; i < ids.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabTool.PrefabPath(ids[i]));
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[ShoppingList] No pickup prefab for '{ids[i]}'. " +
                                         "Run Tools → Level → Make Store Pickup Prefabs, then run this again.");
                        continue;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    go.transform.position = origin + fwd * 2f + right * (1f + i * 0.5f) + Vector3.up * 0.3f;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = tracker;
            Debug.Log($"[ShoppingList] Set up in {scene.path}. Press Play: walk to items, E to pick up, E on the Cart.");
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
