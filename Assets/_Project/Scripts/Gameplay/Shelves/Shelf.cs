using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A shop shelf with LIMITED stock of one item.
    ///
    /// On Play it builds itself from code: a base, two side walls, a back panel and
    /// a few wooden boards (levels). Then it puts `stock` copies of item.prefab on the
    /// boards, front row, bottom level first.
    ///
    /// The FRONT of the shelf is transform.forward (blue arrow) — turn it to face the aisle.
    /// The object's position is the middle of the shelf, on the floor.
    ///
    /// Player picks an item (GameEvents.ItemPickedUp) → stock goes down.
    /// NPC shoppers call <see cref="TakeOne"/>.
    /// </summary>
    public class Shelf : MonoBehaviour
    {
        [SerializeField] private ItemData item;

        [Tooltip("How many items the shelf starts with")]
        [Min(0)] [SerializeField] private int stock = 6;

        [Header("Size (metres)")]
        [SerializeField] private float width = 1.6f;
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float depth = 0.5f;
        [Min(1)] [SerializeField] private int levels = 3;

        [Header("Look")]
        [SerializeField] private Color frameColor = new Color(0.82f, 0.84f, 0.86f); // light metal
        [SerializeField] private Color boardColor = new Color(0.62f, 0.45f, 0.28f); // wood
        [SerializeField] private bool showLabel = true;

        const float Thick = 0.04f;    // panels and boards
        const float BaseHeight = 0.12f;

        private readonly List<GameObject> _items = new List<GameObject>();
        private TextMeshPro _label;

        // ---------- Public API (for NPCs, UI ...) ----------

        public ItemData Item => item;
        public string ItemId => item != null ? item.id : "";
        public int Stock => _items.Count;
        public bool IsEmpty => _items.Count == 0;

        /// <summary>NPC takes one item: it disappears from the shelf. False if empty.</summary>
        public bool TakeOne()
        {
            if (IsEmpty) return false;

            int last = _items.Count - 1;
            GameObject taken = _items[last];
            _items.RemoveAt(last);
            if (taken != null) Destroy(taken);

            OnStockChanged();
            return true;
        }

        // ---------- Unity ----------

        private void OnEnable() => GameEvents.ItemPickedUp += OnItemPickedUp;

        private void OnDisable() => GameEvents.ItemPickedUp -= OnItemPickedUp;

        private void Start()
        {
            BuildFrame();
            if (showLabel) _label = CreateLabel();
            SpawnItems();
            UpdateLabel();
        }

        private void LateUpdate()
        {
            // Label always faces the camera.
            Camera cam = Camera.main;
            if (_label != null && cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }

        // ---------- Building the shelf ----------

        /// <summary>Height of the top surface of board i (0 = bottom).</summary>
        private float BoardTop(int i)
        {
            float step = (height - BaseHeight) / levels;
            return BaseHeight + i * step;
        }

        private void BuildFrame()
        {
            var root = new GameObject("Frame").transform;
            root.SetParent(transform, false);

            Material frame = MakeMaterial(frameColor);
            Material wood = MakeMaterial(boardColor);

            float halfW = width / 2f, halfD = depth / 2f;

            // Base (dark, slightly inset)
            Part(root, "Base", new Vector3(0, BaseHeight / 2f, 0),
                new Vector3(width - 2 * Thick, BaseHeight, depth - Thick), MakeMaterial(frameColor * 0.5f));

            // Side walls
            Part(root, "Side_L", new Vector3(-halfW + Thick / 2f, height / 2f, 0), new Vector3(Thick, height, depth), frame);
            Part(root, "Side_R", new Vector3(halfW - Thick / 2f, height / 2f, 0), new Vector3(Thick, height, depth), frame);

            // Back panel
            Part(root, "Back", new Vector3(0, height / 2f, -halfD + Thick / 2f), new Vector3(width, height, Thick), frame);

            // Boards: one per level (items stand on them) + a roof
            for (int i = 0; i <= levels; i++)
            {
                float y = (i == 0 ? BaseHeight : BoardTop(i)) - Thick / 2f;
                if (i == levels) y = height - Thick / 2f;
                Part(root, i == levels ? "Top" : $"Board_{i}", new Vector3(0, y, 0),
                    new Vector3(width - 2 * Thick, Thick, depth - Thick), wood);
            }
        }

        /// <summary>One box part of the frame (keeps its collider so nobody walks through).</summary>
        private static void Part(Transform parent, string partName, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Copies Unity's default material (works in any render pipeline) and colours it.</summary>
        private static Material MakeMaterial(Color color)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(temp.GetComponent<Renderer>().sharedMaterial) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            Destroy(temp);
            return mat;
        }

        // ---------- Items ----------

        /// <summary>Fills the boards from the bottom, items in a row near the front edge.</summary>
        private void SpawnItems()
        {
            if (item == null || item.prefab == null)
            {
                Debug.LogWarning($"[Shelf] '{name}' has no ItemData / prefab.", this);
                return;
            }
            if (item.prefab.GetComponent<Game.Interaction.Pickup>() == null)
                Debug.LogWarning($"[Shelf] Prefab of '{item.id}' has no Pickup — the player can't take it. " +
                                 "Use Prefabs/Pickups/Pickup_*.prefab.", this);

            int perLevel = Mathf.CeilToInt(stock / (float)levels);
            float usable = width - 4 * Thick;
            float frontZ = depth * 0.15f; // a bit in front of the middle: easy to see and reach

            for (int i = 0; i < stock; i++)
            {
                int level = i / perLevel;
                int slot = i % perLevel;
                int onThisLevel = Mathf.Min(perLevel, stock - level * perLevel);

                float x = onThisLevel > 1 ? -usable / 2f + usable * (slot + 0.5f) / onThisLevel : 0f;
                Vector3 local = new Vector3(x, BoardTop(level), frontZ);

                GameObject go = Instantiate(item.prefab, transform.TransformPoint(local), transform.rotation);
                go.name = $"{item.id}_{i}";
                if (go.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true; // stays put until picked up
                _items.Add(go);
            }
        }

        private void OnItemPickedUp(GameObject picked, string itemId)
        {
            if (_items.Remove(picked)) OnStockChanged();
        }

        private void OnStockChanged()
        {
            UpdateLabel();
            GameEvents.RaiseShelfStockChanged(gameObject, ItemId, Stock);
            if (IsEmpty) GameEvents.RaiseShelfEmptied(gameObject, ItemId);
        }

        // ---------- Label ----------

        private TextMeshPro CreateLabel()
        {
            var go = new GameObject("StockLabel", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, height + 0.3f, 0);

            var text = go.GetComponent<TextMeshPro>();
            if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 2.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            string itemName = item != null ? item.displayName : "?";
            _label.text = IsEmpty ? $"{itemName}\n<color=#FF5555>SOLD OUT</color>" : $"{itemName} x{Stock}";
        }

        // ---------- Editor preview ----------

        // The real shelf is built on Play; in the Scene view draw its outline so it can be placed.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(boardColor.r, boardColor.g, boardColor.b, 0.35f);
            Gizmos.DrawCube(new Vector3(0, height / 2f, 0), new Vector3(width, height, depth));
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(new Vector3(0, height / 2f, 0), new Vector3(width, height, depth));
            Gizmos.color = Color.cyan; // front
            Gizmos.DrawLine(new Vector3(0, 0.05f, 0), new Vector3(0, 0.05f, depth));
        }
    }
}
