using Game.Core;
using Game.Interaction;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A shop shelf with LIMITED stock of one item.
    ///
    /// On Play it builds itself from code: a base, two side walls, a back panel and
    /// a few wooden boards (levels), with `itemsPerBoard` spots on each board.
    /// Then it puts `stock` copies of item.prefab on the spots, bottom board first.
    ///
    /// The spots are an <see cref="ItemSlot"/>: the player can take products off AND put
    /// products back on (E while holding one). No Item = an empty shelf that takes anything.
    ///
    /// The FRONT of the shelf is transform.forward (blue arrow) — turn it to face the aisle.
    /// The object's position is the middle of the shelf, on the floor.
    ///
    /// NPC shoppers call <see cref="TakeOne"/>.
    /// </summary>
    public class Shelf : MonoBehaviour
    {
        [Tooltip("What this shelf sells. Empty = empty shelf, the player can put anything on it.")]
        [SerializeField] private ItemData item;

        [Tooltip("How many items the shelf starts with (max = levels x items per board)")]
        [Min(0)] [SerializeField] private int stock = 6;

        [Tooltip("Spots on each board where a product stands")]
        [Min(1)] [SerializeField] private int itemsPerBoard = 4;

        [Header("Size (metres)")]
        [SerializeField] private float width = 1.6f;
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float depth = 0.5f;
        [Min(1)] [SerializeField] private int levels = 3;

        [Header("Look")]
        [SerializeField] private Color frameColor = new Color(0.82f, 0.84f, 0.86f); // light metal
        [SerializeField] private Color boardColor = new Color(0.62f, 0.45f, 0.28f); // wood
        [Tooltip("Products on the shelf can't be picked up by the player (E only puts things back). " +
                 "NPCs can still take them; once off the shelf they can be picked up again. Used in the Shelter.")]
        [SerializeField] private bool lockItemsOnShelf = false;

        [Tooltip("Open military storage rack (4 posts, slatted shelves, cross-braced back) instead of a shop shelf")]
        [SerializeField] private bool bunkerRack = false;

        [Tooltip("Sign on the top front of the shelf: product name + stock")]
        [SerializeField] private bool showLabel = true;
        [SerializeField] private Color signColor = new Color(0.10f, 0.10f, 0.11f);   // dark board
        [SerializeField] private Color signStripe = new Color(0.70f, 0.07f, 0.07f);  // red 24/7 stripe

        const float Thick = 0.04f;    // panels and boards
        const float BaseHeight = 0.12f;

        private ItemSlot _slot;
        private TextMeshPro _label, _count; // name (left) and stock (right) on the sign

        // ---------- Public API (for NPCs, UI ...) ----------

        public ItemData Item => item;
        public string ItemId => item != null ? item.id : "";
        public int Stock => _slot != null ? _slot.Count : 0;
        public int Capacity => levels * itemsPerBoard;
        public bool IsEmpty => Stock == 0;

        /// <summary>NPC takes one item: it disappears from the shelf. False if empty.</summary>
        public bool TakeOne()
        {
            Game.Interaction.Pickup taken = _slot != null ? _slot.TakeLast() : null; // raises ItemRemoved -> label
            if (taken == null) return false;
            Destroy(taken.gameObject);
            return true;
        }

        /// <summary>NPC takes one item off the shelf and gets it (to carry or knock over). Null if empty.</summary>
        public Game.Interaction.Pickup TakeOneItem() => _slot != null ? _slot.TakeLast() : null;

        /// <summary>Where an NPC stands to reach the shelf: on the floor in front of it.</summary>
        public Vector3 FrontPoint => transform.position + transform.forward * (depth / 2f + 0.6f);

        // ---------- Unity ----------

        private void Start()
        {
            BuildFrame();

            // The shelf is built on Play, so the baked NavMesh doesn't know it: cut it out at runtime
            // so NPC shoppers walk around shelves, not through them.
            var obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, height / 2f, 0);
            obstacle.size = new Vector3(width, height, depth);
            obstacle.carving = true;

            BuildSlot();
            if (showLabel) _label = CreateLabel();
            SpawnItems();
            _slot.ItemPlaced += (_, __) => OnStockChanged();
            _slot.ItemRemoved += (_, item) =>
            {
                if (item != null) item.enabled = true; // off the shelf (dropped / knocked) → can be picked up again
                OnStockChanged();
            };
            UpdateLabel();
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
            if (bunkerRack) { BuildRack(root); return; }

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

        /// <summary>
        /// Bunker storage rack: 4 square posts, slatted boards on every level, X-braces on the back and sides,
        /// all in worn military paint. Same size and levels as the shop shelf, so the products sit the same.
        /// </summary>
        private void BuildRack(Transform root)
        {
            Material paint = MakeMaterial(frameColor);
            Material slats = MakeMaterial(boardColor);
            Material dark = MakeMaterial(frameColor * 0.55f);
            float halfW = width / 2f, halfD = depth / 2f, post = 0.05f;

            foreach (float x in new[] { -halfW + post / 2f, halfW - post / 2f })
            foreach (float z in new[] { -halfD + post / 2f, halfD - post / 2f })
                Part(root, "Post", new Vector3(x, height / 2f, z), new Vector3(post, height, post), paint);

            for (int i = 0; i <= levels; i++)
            {
                float y = (i == 0 ? BaseHeight : BoardTop(i)) - Thick / 2f;
                if (i == levels) y = height - Thick / 2f;
                // the board the items stand on (solid, so they don't fall through) + a lip at the front
                Part(root, i == levels ? "Top" : $"Board_{i}", new Vector3(0, y, 0), new Vector3(width - post, Thick * 0.6f, depth - post), slats);
                Part(root, "Lip", new Vector3(0, y + 0.02f, halfD - post / 2f), new Vector3(width - post, 0.06f, 0.02f), paint);
                // slat gaps: thin dark lines across the board
                for (int k = 1; k < 4; k++)
                    Part(root, "Gap", new Vector3(0, y + Thick * 0.31f, -halfD + depth * k / 4f), new Vector3(width - post, 0.004f, 0.015f), dark);
            }

            // X-braces (thin bars) on the back and both sides
            Brace(root, new Vector3(0, height / 2f, -halfD + post / 2f), width, height, Vector3.forward, dark);
            Brace(root, new Vector3(-halfW + post / 2f, height / 2f, 0), depth, height, Vector3.right, dark);
            Brace(root, new Vector3(halfW - post / 2f, height / 2f, 0), depth, height, Vector3.right, dark);
        }

        private static void Brace(Transform root, Vector3 centre, float w, float h, Vector3 normal, Material mat)
        {
            float len = Mathf.Sqrt(w * w + h * h), angle = Mathf.Atan2(h, w) * Mathf.Rad2Deg;
            Quaternion face = Quaternion.LookRotation(normal);
            foreach (float a in new[] { angle, -angle })
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Brace";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = centre;
                go.transform.localRotation = face * Quaternion.Euler(0, 0, a);
                go.transform.localScale = new Vector3(len, 0.025f, 0.012f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
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
            Shader ps1 = Shader.Find("Game/PS1 Lit"); // same wobbly PS1 look as the rest of the store
            if (ps1 != null) mat.shader = ps1;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            Destroy(temp);
            return mat;
        }

        // ---------- Items ----------

        /// <summary>
        /// One spot per product: itemsPerBoard in a row near the front edge of every board.
        /// Spot order = bottom board first, left to right (that is also the fill order).
        /// </summary>
        private void BuildSlot()
        {
            var spotsRoot = new GameObject("Spots").transform;
            spotsRoot.SetParent(transform, false);

            float usable = width - 4 * Thick;
            float frontZ = depth * 0.15f; // a bit in front of the middle: easy to see and reach
            var spots = new Transform[Capacity];

            for (int level = 0; level < levels; level++)
            for (int s = 0; s < itemsPerBoard; s++)
            {
                float x = itemsPerBoard > 1 ? -usable / 2f + usable * (s + 0.5f) / itemsPerBoard : 0f;
                var spot = new GameObject($"Spot_{level}_{s}").transform;
                spot.SetParent(spotsRoot, false);
                spot.localPosition = new Vector3(x, BoardTop(level), frontZ);
                spots[level * itemsPerBoard + s] = spot;
            }

            _slot = gameObject.AddComponent<ItemSlot>();
            string shelfName = item != null ? $"{item.displayName} shelf" : "Shelf";
            _slot.Setup(shelfName, "Put on shelf:", "shelf", ItemId, spots, lockItems: lockItemsOnShelf);
        }

        /// <summary>Puts `stock` copies of the product on the spots.</summary>
        private void SpawnItems()
        {
            if (item == null) return; // empty shelf: the player fills it
            if (item.prefab == null)
            {
                Debug.LogWarning($"[Shelf] '{name}': ItemData '{item.id}' has no prefab.", this);
                return;
            }
            if (item.prefab.GetComponent<Game.Interaction.Pickup>() == null)
            {
                Debug.LogWarning($"[Shelf] Prefab of '{item.id}' has no Game.Interaction.Pickup - " +
                                 "the player can't take it. Use Prefabs/Pickups/Pickup_*.prefab.", this);
                return;
            }

            int count = Mathf.Min(stock, Capacity);
            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(item.prefab);
                go.name = $"{item.id}_{i}";
                _slot.Place(go.GetComponent<Game.Interaction.Pickup>()); // snaps onto the next free spot, stays put
            }
        }

        private void OnStockChanged()
        {
            UpdateLabel();
            GameEvents.RaiseShelfStockChanged(gameObject, ItemId, Stock);
            if (IsEmpty) GameEvents.RaiseShelfEmptied(gameObject, ItemId);
        }

        // ---------- Label ----------

        const float SignHeight = 0.22f;

        /// <summary>
        /// A shop sign fixed to the top front edge of the shelf (like real supermarket shelf headers):
        /// dark board + red stripe at the bottom, product name on the left, stock on the right.
        /// It is part of the shelf — it does not float or turn to the camera, so signs never overlap.
        /// </summary>
        private TextMeshPro CreateLabel()
        {
            var sign = new GameObject("Sign").transform;
            sign.SetParent(transform, false);
            float halfD = depth / 2f;
            float y = height + SignHeight / 2f; // stands on the top board
            float frontZ = halfD + 0.01f;      // sticks out a little at the front

            Part(sign, "Board", new Vector3(0, y, frontZ), new Vector3(width, SignHeight, 0.02f), MakeMaterial(signColor));
            Part(sign, "Stripe", new Vector3(0, y - SignHeight / 2f + 0.02f, frontZ + 0.002f),
                new Vector3(width, 0.04f, 0.02f), MakeMaterial(signStripe));

            // Text sits just in front of the board. TextMeshPro is read looking along its +Z,
            // so it is turned around to face the aisle (the shelf's front).
            float textZ = frontZ + 0.012f;
            float textY = y + 0.015f;           // a bit above centre, clear of the stripe
            // Two separate boxes so name and count can never overlap:
            // name = left 66 % of the sign, count = right 30 %.
            // (The text is turned 180°, so the viewer's left is +X in shelf space.)
            float inner = width - 0.12f;
            float nameW = inner * 0.66f, countW = inner * 0.30f;
            _label = SignText(sign, "Name", new Vector3(inner / 2f - nameW / 2f, textY, textZ), nameW,
                TextAlignmentOptions.MidlineLeft);
            _count = SignText(sign, "Count", new Vector3(-(inner / 2f - countW / 2f), textY, textZ), countW,
                TextAlignmentOptions.MidlineRight);
            return _label;
        }

        private static TextMeshPro SignText(Transform parent, string objName, Vector3 localPos, float width,
            TextAlignmentOptions align)
        {
            var go = new GameObject(objName, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var text = go.GetComponent<TextMeshPro>();
            if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
            text.rectTransform.sizeDelta = new Vector2(width, SignHeight * 0.6f);
            text.enableAutoSizing = true;   // long names shrink to fit
            text.fontSizeMin = 0.4f;
            text.fontSizeMax = 1.1f;
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.characterSpacing = 4f;
            text.alignment = align; // e.g. MidlineLeft (combining Left | Midline with | breaks TMP alignment)
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = new Color(0.93f, 0.91f, 0.86f);
            return text;
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            if (item == null)
            {
                _label.text = "Empty shelf";
                _count.text = $"{Stock}/{Capacity}";
                return;
            }

            _label.text = item.displayName; // has its own box, no margin needed
            _count.text = IsEmpty ? "<color=#FF4444>SOLD OUT</color>" : $"<color=#FFC94A>x{Stock}</color>";
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
