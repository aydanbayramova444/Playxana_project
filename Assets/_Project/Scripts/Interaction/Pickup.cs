using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// An object the player can pick up with E, carry, and drop with E (soda bottle, banana, mop, key...).
    /// Just add it to any model (e.g. from the food pack): at start it adds a Rigidbody if missing,
    /// makes mesh colliders convex, and adds a BoxCollider if the object has no collider at all.
    /// Set Item Id so slots and tasks know what it is ("Soda", "Banana", "Mop").
    /// </summary>
    [AddComponentMenu("Interaction/Pickup (new)")]
    public class Pickup : Interactable
    {
        [Tooltip("What this item is, used by ItemSlot / Required Held Item / tasks. Empty = Display Name.")]
        [SerializeField] private string itemId = "";

        public string ItemId => string.IsNullOrEmpty(itemId) ? DisplayName : itemId;

        /// <summary>The slot this item currently sits in (shelf, fridge...), or null.</summary>
        public ItemSlot CurrentSlot { get; internal set; }

        public bool IsHeld { get; private set; }

        private Rigidbody _rigidbody;
        private Collider[] _colliders;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            if (_colliders.Length == 0)
            {
                AddFittingBoxCollider();
                _colliders = GetComponentsInChildren<Collider>(true);
            }

            // A moving Rigidbody only works with convex mesh colliders
            foreach (var c in _colliders)
                if (c is MeshCollider mc && !mc.convex) mc.convex = true;

            _rigidbody = GetComponentInChildren<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Interpolation makes a held item lag at its old spot (looks like it vanished),
            // so it's off for every pickup, also ones that came with their own Rigidbody.
            _rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        private void AddFittingBoxCollider()
        {
            var box = gameObject.AddComponent<BoxCollider>();
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (var r in renderers)
            {
                Bounds lb = r.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3(x, y, z));
                    Vector3 local = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }

            box.center = (min + max) * 0.5f;
            box.size = max - min;
        }

        public override bool CanInteract(PlayerInteractor player) => !player.IsHoldingAnything && !IsHeld;

        public override string GetPrompt(PlayerInteractor player) => $"Pick up {DisplayName}";

        public override string GetBlockedReason(PlayerInteractor player) => "";

        protected override void OnInteract(PlayerInteractor player) => player.PickUp(this);

        // ----- Called by NPC shoppers -----

        /// <summary>An NPC takes this item: it sits in their hand and the player can't grab it.</summary>
        public void TakeByNpc(Transform hand)
        {
            OnPickedUp(); // leaves its shelf slot, colliders off, kinematic, IsHeld = true
            PlacementHelper.SnapTo(gameObject, hand, sitOnTop: false);
        }

        /// <summary>An NPC knocks this item off: it flies off and lands on the floor, free for anyone.</summary>
        public void KnockByNpc(Vector3 velocity)
        {
            OnPickedUp();
            OnDropped(velocity); // unparents, colliders on, physics on, IsHeld = false
        }

        // ----- Called by PlayerInteractor / ItemSlot -----

        internal void OnPickedUp()
        {
            if (CurrentSlot != null)
            {
                CurrentSlot.Remove(this);
                CurrentSlot = null;
            }

            IsHeld = true;
            SetColliders(false);
            if (_rigidbody != null) _rigidbody.isKinematic = true;
        }

        internal void OnDropped(Vector3 velocity)
        {
            IsHeld = false;
            transform.SetParent(null, true);
            SetColliders(true);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = velocity;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        internal void OnPlaced(ItemSlot slot)
        {
            IsHeld = false;
            CurrentSlot = slot;
            SetColliders(true);
            if (_rigidbody != null) _rigidbody.isKinematic = true;
        }

        private void SetColliders(bool on)
        {
            if (_colliders == null) return;
            foreach (var c in _colliders)
                if (c != null) c.enabled = on;
        }
    }
}
