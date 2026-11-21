using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// A shelter on the sidewalk: while the player stands inside, cars can't hit them and their health
    /// refills (see <see cref="Level2Manager"/>). Cars also steer around it (<see cref="ChaosCar"/>).
    /// Needs a trigger BoxCollider (the zone) — built by Tools → Level 2 → Add Safe Zones To Open Scene.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SafeZone : MonoBehaviour
    {
        /// <summary>All zones in the scene (cars read this to keep away).</summary>
        public static readonly List<SafeZone> All = new List<SafeZone>();

        private BoxCollider _box;

        /// <summary>The zone's area on the ground (world space, XZ).</summary>
        public Bounds Area => _box.bounds;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                Level2Manager.Instance?.EnterSafeZone();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                Level2Manager.Instance?.ExitSafeZone();
        }
    }
}
