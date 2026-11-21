using System;
using Game.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Gameplay
{
    /// <summary>
    /// A place where the player can put a held item with E:
    /// a shelf (3 soda spots), a basket (banana), a fridge, a trash bin (items disappear).
    ///
    /// Tasks can listen to GameEvents.ItemPlaced, or to the UnityEvents below,
    /// or simply read Count / IsFull.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemReceiver : MonoBehaviour
    {
        [SerializeField] private string receiverId = "shelf";
        [SerializeField] private string displayName = "Shelf";

        [Tooltip("{item} and {target} are replaced, e.g. \"Put {item} on {target}\"")]
        [SerializeField] private string promptFormat = "Put {item} on {target}";

        [Tooltip("Item ids allowed here (e.g. soda). Empty = anything.")]
        [SerializeField] private string[] acceptedItemIds = Array.Empty<string>();

        [Tooltip("Where items go (empty children). Empty = one spot on top of this object.")]
        [SerializeField] private Transform[] spots = Array.Empty<Transform>();

        [Tooltip("Items disappear when put here (trash bin).")]
        [SerializeField] private bool consumeItems;

        public UnityEvent onItemReceived;
        public UnityEvent onFull;

        private Pickup[] _occupants;

        public string ReceiverId => receiverId;
        public string DisplayName => displayName;

        /// <summary>Items here now (for a bin: items thrown in so far).</summary>
        public int Count { get; private set; }
        public int Capacity => consumeItems ? int.MaxValue : spots.Length;
        public bool IsFull => !consumeItems && Count >= spots.Length;

        private void Awake()
        {
            if (!consumeItems && (spots == null || spots.Length == 0))
                spots = new[] { MakeTopSpot() };

            _occupants = new Pickup[spots?.Length ?? 0];
        }

        public bool Accepts(Pickup item) =>
            item != null &&
            (acceptedItemIds == null || acceptedItemIds.Length == 0 ||
             Array.IndexOf(acceptedItemIds, item.ItemId) >= 0);

        public bool CanReceive(Pickup item) =>
            Accepts(item) && (consumeItems || FreeIndex() >= 0);

        public string GetPrompt(Pickup item) =>
            promptFormat.Replace("{item}", item.DisplayName).Replace("{target}", displayName);

        public void Receive(Pickup item)
        {
            if (!CanReceive(item)) return;

            Count++;
            GameEvents.RaiseItemPlaced(item.gameObject, item.ItemId, gameObject);

            if (consumeItems)
            {
                Destroy(item.gameObject);
            }
            else
            {
                int i = FreeIndex();
                _occupants[i] = item;
                item.PlaceOn(this, spots[i]);
            }

            onItemReceived?.Invoke();
            if (IsFull) onFull?.Invoke();
        }

        /// <summary>Called when an item is taken back off.</summary>
        internal void Release(Pickup item)
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == item)
                {
                    _occupants[i] = null;
                    Count--;
                }
            }
        }

        private int FreeIndex()
        {
            for (int i = 0; i < _occupants.Length; i++)
                if (_occupants[i] == null && spots[i] != null) return i;
            return -1;
        }

        private Transform MakeTopSpot()
        {
            var spot = new GameObject("Spot").transform;
            spot.SetParent(transform, false);

            Collider col = GetComponentInChildren<Collider>();
            spot.position = col != null
                ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z)
                : transform.position;
            return spot;
        }

        private void OnDrawGizmosSelected()
        {
            if (spots == null) return;
            Gizmos.color = Color.cyan;
            foreach (Transform s in spots)
                if (s != null) Gizmos.DrawWireSphere(s.position, 0.08f);
        }
    }
}
