using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Makes an object carryable: look at it and press E to pick it up.
    /// Give each kind of item an id (e.g. "soda", "banana", "mop", "trash") —
    /// shelves, bins and tasks use the id to know what it is.
    /// Needs a Collider (on this object or a child) so the player can aim at it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class Pickup : MonoBehaviour
    {
        [Tooltip("What kind of item this is, e.g. soda, banana, mop, trash")]
        [SerializeField] private string itemId = "item";

        [Tooltip("Name shown to the player, e.g. \"Soda\"")]
        [SerializeField] private string displayName = "Item";

        public string ItemId => itemId;
        public string DisplayName => displayName;

        public bool IsHeld { get; private set; }

        /// <summary>The shelf / basket / fridge this item is on right now (or null).</summary>
        public ItemReceiver Receiver { get; private set; }

        private Rigidbody _rb;
        private Collider[] _colliders;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        // ---------- Called by PlayerInteractor / ItemReceiver ----------

        internal void Hold(Transform holdPoint)
        {
            if (Receiver != null)
            {
                Receiver.Release(this);
                Receiver = null;
            }

            IsHeld = true;
            _rb.isKinematic = true;
            SetColliders(false); // don't push the player or block the aim ray
            ItemPlacement.SnapTo(transform, holdPoint, bottomOnPoint: false);
        }

        internal void Drop(Vector3 velocity)
        {
            IsHeld = false;
            transform.SetParent(null, true);
            SetColliders(true);
            _rb.isKinematic = false;
            _rb.linearVelocity = velocity;
        }

        internal void PlaceOn(ItemReceiver receiver, Transform spot)
        {
            IsHeld = false;
            Receiver = receiver;
            _rb.isKinematic = true;
            SetColliders(true);
            ItemPlacement.SnapTo(transform, spot, bottomOnPoint: true);
        }

        private void SetColliders(bool on)
        {
            foreach (Collider c in _colliders)
                if (c != null) c.enabled = on;
        }
    }
}
