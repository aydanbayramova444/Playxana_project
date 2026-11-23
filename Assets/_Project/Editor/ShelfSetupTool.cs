using Game.Gameplay;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Shelves → Add Test Shelves (Current Dev Scene)
    ///   Adds 4 shelves (banana 3, soda 2, apple 2, bread 1) to the right of the player
    ///   and removes the loose "ShoppingTestItems" — items now come from shelves.
    ///   Run "Tools → Shopping List → Set Up Everything" first (it makes the ItemData assets).
    ///   Never touches Main.unity. Safe to run again (rebuilds the "Shelves" group).
    /// </summary>
    public static class ShelfSetupTool
    {
        const string ItemsFolder = "Assets/_Project/ScriptableObjects/Items";
        const string MainScene   = "Assets/_Project/Scenes/Main.unity";

        static readonly (string id, int stock)[] Shelves =
        {
            ("banana", 6),
            ("soda",   4),
            ("apple",  5),
            ("bread",  3),
        };

        [MenuItem("Tools/Shelves/Add Test Shelves (Current Dev Scene)", priority = 40)]
        public static void AddTestShelves() => BuildShelves(allowMain: false);

        /// <summary>Builds the shelves in the active scene. Main.unity only when allowMain is true.</summary>
        public static void BuildShelves(bool allowMain)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == MainScene && !allowMain)
            {
                EditorUtility.DisplayDialog("Shelves", "This tool does not edit Main.unity.\n" +
                    "Open your dev scene (Scenes/Dev/<you>.unity) first.", "OK");
                return;
            }

            // Rebuild from scratch each time.
            var old = GameObject.Find("Shelves");
            if (old != null) Object.DestroyImmediate(old);
            var loose = GameObject.Find("ShoppingTestItems");
            if (loose != null) Object.DestroyImmediate(loose);

            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            Vector3 fwd = player != null ? player.transform.forward : Vector3.forward;
            Vector3 right = player != null ? player.transform.right : Vector3.right;

            var group = new GameObject("Shelves").transform;

            for (int i = 0; i < Shelves.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_{Shelves[i].id}.asset");
                if (data == null)
                {
                    Debug.LogWarning($"[Shelves] No ItemData for '{Shelves[i].id}'. " +
                                     "Run Tools → Shopping List → Set Up Everything first.");
                    continue;
                }

                // Empty object on the floor: Shelf builds its frame and boards on Play.
                // Shelves stand in a row to the player's right, front facing the player's path.
                var shelf = new GameObject($"Shelf_{data.id}");
                shelf.transform.SetParent(group, false);
                shelf.transform.position = new Vector3(0, origin.y, 0) +
                                           Vector3.ProjectOnPlane(origin + right * 3f + fwd * (1f + i * 1.9f), Vector3.up);
                shelf.transform.rotation = Quaternion.LookRotation(-right);

                var comp = shelf.AddComponent<Shelf>();
                var so = new SerializedObject(comp);
                so.FindProperty("item").objectReferenceValue = data;
                so.FindProperty("stock").intValue = Shelves[i].stock;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Shelves] Added test shelves. Press Play: take items with E, watch the stock labels.");
        }
    }
}
