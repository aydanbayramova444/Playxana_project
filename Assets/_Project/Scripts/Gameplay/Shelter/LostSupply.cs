using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Level 3: a supply a thief dropped when you hit them. While it lies on the floor it:
    ///   - glows (easy to spot in the dark bunker);
    ///   - is protected for a moment: the thief who dropped it can't grab it for <see cref="OwnThiefDelay"/> s,
    ///     the others for <see cref="OtherThievesDelay"/> s — time for you to get there;
    ///   - goes straight back onto its shelf the moment you pick it up (E).
    /// Added by <see cref="ShelterIntruder"/> in code; removed when someone takes it.
    /// </summary>
    [DisallowMultipleComponent]
    public class LostSupply : MonoBehaviour
    {
        public const float OwnThiefDelay = 3f, OtherThievesDelay = 1.5f;

        /// <summary>The shelf it was taken from (null = unknown: any shelf that takes it).</summary>
        public Shelf Home { get; private set; }
        public ShelterIntruder DroppedBy { get; private set; }
        public float DroppedAt { get; private set; }

        private Game.Interaction.Pickup _pickup;
        private Light _glow;

        /// <summary>Marks <paramref name="item"/> as just dropped by <paramref name="thief"/>.</summary>
        public static LostSupply Mark(Game.Interaction.Pickup item, Shelf home, ShelterIntruder thief)
        {
            if (item == null) return null;
            var lost = item.GetComponent<LostSupply>();
            if (lost == null) lost = item.gameObject.AddComponent<LostSupply>();
            lost._pickup = item;
            if (home != null) lost.Home = home;
            lost.DroppedBy = thief;
            lost.DroppedAt = Time.time;
            return lost;
        }

        /// <summary>Can this thief grab it yet?</summary>
        public bool CanBeTakenBy(ShelterIntruder thief)
        {
            float wait = thief == DroppedBy ? OwnThiefDelay : OtherThievesDelay;
            return Time.time - DroppedAt >= wait;
        }

        /// <summary>A thief picked it up off the floor: no glow any more, it's in their hands.</summary>
        public void Clear()
        {
            if (_glow != null) Destroy(_glow.gameObject);
            Destroy(this);
        }

        private void Start()
        {
            _glow = new GameObject("LostGlow").AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = new Color(1f, 0.8f, 0.25f);
            _glow.range = 2.2f;
            _glow.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (_glow != null) Destroy(_glow.gameObject);
        }

        private void Update()
        {
            if (_pickup == null)
            {
                Destroy(this);
                return;
            }

            // The light follows the item but isn't its child (the item gets re-parented to hands / shelves)
            if (_glow != null)
            {
                _glow.transform.position = transform.position + Vector3.up * 0.4f;
                _glow.intensity = 1.5f + Mathf.PingPong(Time.time * 3f, 1.5f);
            }

            // Picked up while glowing: thieves Clear() this before they take it, so in hands = the player's
            if (_pickup.IsHeld) ReturnToShelf();
        }

        private void ReturnToShelf()
        {
            Game.Interaction.PlayerInteractor player = null;
            foreach (var p in FindObjectsByType<Game.Interaction.PlayerInteractor>())
                if (p.HeldItem == _pickup) player = p;
            if (player == null) return; // not in the player's hands after all

            Shelf shelf = FindShelf();
            if (shelf == null) return; // every shelf is full: keep holding it, put it down anywhere

            player.ReleaseHeld();
            if (!shelf.PutBack(_pickup))
            {
                player.PickUp(_pickup); // shouldn't happen (FindShelf checked) — give it back
                return;
            }
            var director = FindAnyObjectByType<ShelterDirector>();
            if (director != null) director.OnSupplyReturned(_pickup);
            Clear();
        }

        /// <summary>Its own shelf if there's room, otherwise any shelf that takes it.</summary>
        private Shelf FindShelf()
        {
            if (Home != null && Home.CanTake(_pickup)) return Home;
            foreach (var s in FindObjectsByType<Shelf>())
                if (s.CanTake(_pickup)) return s;
            return null;
        }
    }
}
