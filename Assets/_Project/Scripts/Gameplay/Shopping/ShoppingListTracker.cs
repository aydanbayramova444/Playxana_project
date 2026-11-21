using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Tracks progress of the player's shopping list.
    /// Put ONE of these in the scene and assign a ShoppingListData asset.
    ///
    /// How an item gets counted:
    ///   the player puts an item into an ItemReceiver whose ReceiverId == cartReceiverId ("cart")
    ///   → ItemReceiver raises GameEvents.ItemPlaced → we check the id against the list.
    ///
    /// Raises GameEvents.ShoppingListChanged on every change and
    /// GameEvents.ShoppingListCompleted once when everything is collected.
    /// Winning the game is NOT done here — the timer / win-lose system listens to ShoppingListCompleted.
    /// </summary>
    public class ShoppingListTracker : MonoBehaviour
    {
        /// <summary>Runtime progress of one line on the list (read-only from outside).</summary>
        public class Progress
        {
            public ItemData Item { get; }
            public int Required { get; }
            public int Collected { get; internal set; }
            public bool IsDone => Collected >= Required;

            internal Progress(ItemData item, int required)
            {
                Item = item;
                Required = required;
            }
        }

        [SerializeField] private ShoppingListData shoppingList;

        [Tooltip("Only items placed into an ItemReceiver with this ReceiverId are counted")]
        [SerializeField] private string cartReceiverId = "cart";

        private readonly List<Progress> _entries = new List<Progress>();
        private bool _built;
        private bool _completedRaised;

        // ---------- Public API ----------

        /// <summary>All lines of the list with their progress.</summary>
        public IReadOnlyList<Progress> Entries
        {
            get
            {
                EnsureBuilt();
                return _entries;
            }
        }

        /// <summary>True when every line is done.</summary>
        public bool IsComplete
        {
            get
            {
                EnsureBuilt();
                if (_entries.Count == 0) return false;
                foreach (Progress p in _entries)
                    if (!p.IsDone) return false;
                return true;
            }
        }

        // ---------- Unity ----------

        private void Awake() => EnsureBuilt();

        private void OnEnable() => GameEvents.ItemPlaced += OnItemPlaced;

        private void OnDisable() => GameEvents.ItemPlaced -= OnItemPlaced;

        private void Start()
        {
            // Let the HUD draw the initial "0/2" state.
            GameEvents.RaiseShoppingListChanged();
        }

        // ---------- Internals ----------

        /// <summary>Builds runtime progress from the asset (only once).</summary>
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _entries.Clear();

            if (shoppingList == null)
            {
                Debug.LogWarning("[ShoppingList] No ShoppingListData assigned.", this);
                return;
            }

            foreach (ShoppingListData.Entry e in shoppingList.entries)
            {
                if (e == null || e.item == null) continue; // skip empty rows in the asset
                _entries.Add(new Progress(e.item, Mathf.Max(1, e.amount)));
            }
        }

        private void OnItemPlaced(GameObject item, string itemId, GameObject receiver)
        {
            // Only count items put into the cart, not on shelves / in bins.
            if (receiver == null || !receiver.TryGetComponent(out ItemReceiver r)) return;
            if (r.ReceiverId != cartReceiverId) return;

            TryCollect(itemId);
        }

        /// <summary>Counts one item with this id. Returns false if it is not needed.</summary>
        private bool TryCollect(string itemId)
        {
            EnsureBuilt();

            Progress target = null;
            bool onList = false;
            foreach (Progress p in _entries)
            {
                if (p.Item.id != itemId) continue;
                onList = true;
                if (!p.IsDone)
                {
                    target = p;
                    break;
                }
            }

            if (target == null)
            {
                Debug.Log(onList
                    ? $"[ShoppingList] '{itemId}' is already complete — ignored."
                    : $"[ShoppingList] '{itemId}' is not on the list — ignored. " +
                      "(Check that ItemData.id matches Pickup.ItemId.)", this);
                return false;
            }

            target.Collected++;
            GameEvents.RaiseShoppingListChanged();

            if (!_completedRaised && IsComplete)
            {
                _completedRaised = true;
                Debug.Log("[ShoppingList] Everything collected!", this);
                GameEvents.RaiseShoppingListCompleted();
            }

            return true;
        }

#if UNITY_EDITOR || DEBUG
        // Right click the component header in the Inspector (Play mode) to test the UI
        // without the interaction system.
        [ContextMenu("Debug: collect random item")]
        private void DebugCollectRandom()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ShoppingList] Enter Play mode first.", this);
                return;
            }

            var needed = new List<Progress>();
            foreach (Progress p in Entries)
                if (!p.IsDone) needed.Add(p);

            if (needed.Count == 0)
            {
                Debug.Log("[ShoppingList] Nothing left to collect.", this);
                return;
            }

            TryCollect(needed[Random.Range(0, needed.Count)].Item.id);
        }
#endif
    }
}
