using System;
using Game.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Interaction
{
    /// <summary>
    /// A place where the player puts the item they are holding:
    ///   - Shelf (restock): Accepts Item Id = "Soda", 3 child points → holds 3 bottles, "On Full" when done.
    ///   - Fridge / basket: Accepts Item Id = "Banana", 1 point.
    ///   - Trash can: Consume Items = on → the item is destroyed when thrown in.
    ///
    /// Needs a Collider (e.g. the shelf's) so the player's ray can hit it.
    /// </summary>
    [AddComponentMenu("Interaction/Item Slot")]
    public class ItemSlot : Interactable
    {
        [Tooltip("What this slot is, e.g. \"cart\", \"fridge\", \"trash\". The shopping list counts items put in the \"cart\".")]
        [SerializeField] private string slotId = "";

        [Tooltip("Only Pickups with this Item Id fit. Empty = anything.")]
        [SerializeField] private string acceptsItemId = "";

        [Tooltip("Where placed items go, one item per point. Empty = this object's position, 1 item.")]
        [SerializeField] private Transform[] points = Array.Empty<Transform>();

        [Tooltip("Destroy items when placed (trash can). Points are ignored.")]
        [SerializeField] private bool consumeItems = false;

        [Tooltip("Only used with Consume Items. 0 = unlimited.")]
        [Min(0)]
        [SerializeField] private int consumeCapacity = 0;

        [Tooltip("Placed items stay put and can't be picked up again.")]
        [SerializeField] private bool lockPlacedItems = true;

        [SerializeField] private UnityEvent onItemPlaced = new UnityEvent();
        [SerializeField] private UnityEvent onFull = new UnityEvent();

        /// <summary>(slot, item). For code: task system, sounds...</summary>
        public event Action<ItemSlot, Pickup> ItemPlaced;
        public event Action<ItemSlot, Pickup> ItemRemoved;
        public event Action<ItemSlot> Filled;

        private Pickup[] _occupants;
        private int _consumedCount;

        public string AcceptsItemId => acceptsItemId;
        public string SlotId => slotId;

        public int Count
        {
            get
            {
                if (consumeItems) return _consumedCount;
                int n = 0;
                foreach (var o in Occupants) if (o != null) n++;
                return n;
            }
        }

        /// <summary>Max items, or int.MaxValue when unlimited.</summary>
        public int Capacity =>
            consumeItems
                ? (consumeCapacity > 0 ? consumeCapacity : int.MaxValue)
                : Occupants.Length;

        public bool IsFull => Count >= Capacity;

        private Pickup[] Occupants =>
            _occupants ??= new Pickup[Mathf.Max(1, points != null ? points.Length : 0)];

        private void Reset() => prompt = "Place";

        public bool Accepts(Pickup item) =>
            item != null && (string.IsNullOrEmpty(acceptsItemId) ||
                             string.Equals(item.ItemId, acceptsItemId, StringComparison.OrdinalIgnoreCase));

        public override bool CanInteract(PlayerInteractor player) =>
            player.IsHoldingAnything && Accepts(player.HeldItem) && !IsFull;

        public override string GetPrompt(PlayerInteractor player) =>
            $"{prompt} {player.HeldItem.DisplayName}{CountSuffix()}";

        public override string GetBlockedReason(PlayerInteractor player)
        {
            if (IsFull) return $"{DisplayName} is full";

            if (player.IsHoldingAnything && !Accepts(player.HeldItem))
                return $"Only {acceptsItemId} goes in {DisplayName}";

            if (!player.IsHoldingAnything && !string.IsNullOrEmpty(acceptsItemId))
                return $"{DisplayName}: needs {acceptsItemId}{CountSuffix()}";

            return "";
        }

        protected override void OnInteract(PlayerInteractor player)
        {
            var item = player.ReleaseHeld();
            if (item != null) Place(item);
        }

        /// <summary>Put an item in this slot directly (also usable from code, e.g. to pre-fill shelves).</summary>
        public bool Place(Pickup item)
        {
            if (!Accepts(item) || IsFull) return false;

            // Before a consumed item is destroyed: the shopping list / tasks listen to this.
            GameEvents.RaiseItemPlaced(item.gameObject, item.ItemId, gameObject);

            if (consumeItems)
            {
                _consumedCount++;
                Destroy(item.gameObject);
            }
            else
            {
                int index = Array.IndexOf(Occupants, null);
                Transform point = points != null && index < points.Length && points[index] != null
                    ? points[index]
                    : transform;

                Occupants[index] = item;
                PlacementHelper.SnapTo(item.gameObject, point, sitOnTop: true);
                item.OnPlaced(this);
                if (lockPlacedItems) item.enabled = false;
            }

            onItemPlaced.Invoke();
            ItemPlaced?.Invoke(this, item);

            if (IsFull)
            {
                onFull.Invoke();
                Filled?.Invoke(this);
            }

            return true;
        }

        internal void Remove(Pickup item)
        {
            int index = Array.IndexOf(Occupants, item);
            if (index < 0) return;

            Occupants[index] = null;
            ItemRemoved?.Invoke(this, item);
        }

        private string CountSuffix() =>
            Capacity == int.MaxValue || Capacity <= 1 ? "" : $" ({Count}/{Capacity})";
    }
}
