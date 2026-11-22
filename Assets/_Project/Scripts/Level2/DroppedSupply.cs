using Game.Core;
using Game.Gameplay;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// Level 2: a supply that a car knocked out of the player's bag. It flies off, lands on the road
    /// and glows; run over it (no button needed) to put it back into <see cref="RunInventory"/>.
    /// A car that drives over it crushes it: gone for good — so getting it back is a gamble.
    /// Created in code by <see cref="Spawn"/> — don't put it in the scene.
    /// </summary>
    public class DroppedSupply : MonoBehaviour
    {
        private const float PickupRadius = 1.5f, PickupDelay = 0.8f, FallOutY = -20f;
        // Cars can't crush it this soon after it fell out (the car that hit you is still right there).
        private const float CrushGrace = 1.2f;

        public ItemData Item { get; private set; }

        private Transform _player;
        private float _readyTime;
        private Light _glow;
        private float _spawnTime;
        private bool _crushed;

        public bool IsCrushed => _crushed;

        /// <summary>Throws <paramref name="item"/> out from <paramref name="from"/> with <paramref name="velocity"/>.</summary>
        public static DroppedSupply Spawn(ItemData item, Vector3 from, Vector3 velocity, Transform player)
        {
            GameObject go;
            if (item.prefab != null)
            {
                go = Instantiate(item.prefab, from, Random.rotation);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetPositionAndRotation(from, Random.rotation);
                go.transform.localScale = Vector3.one * 0.3f;
            }
            go.name = $"Dropped_{item.id}";

            // Not an E-pickup here: walking over it is enough (the player is running from cars).
            foreach (var it in go.GetComponentsInChildren<Game.Interaction.Interactable>()) it.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                c.enabled = true;
                if (c is MeshCollider mc) mc.convex = true;
            }

            var rb = go.GetComponentInChildren<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 8f;

            var drop = go.AddComponent<DroppedSupply>();
            drop.Item = item;
            drop._player = player;
            drop._readyTime = Time.time + PickupDelay;
            drop._spawnTime = Time.time;
            drop.AddGlow();
            return drop;
        }

        private void AddGlow()
        {
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.25f);
            light.range = 2.5f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;
            _glow = light;
        }

        private void Update()
        {
            if (transform.position.y < FallOutY) // fell off the world: gone for good
            {
                Destroy(gameObject);
                return;
            }

            // Pulse so it's easy to spot on the road
            if (_glow != null)
            {
                _glow.transform.position = transform.position + Vector3.up * 0.4f;
                _glow.intensity = 1.5f + Mathf.PingPong(Time.time * 3f, 1.5f);
            }

            if (_crushed || _player == null || Time.time < _readyTime) return;
            var manager = Level2Manager.Instance;
            if (manager == null || !manager.IsRunning) return;

            Vector3 d = transform.position - _player.position;
            if (Mathf.Abs(d.y) > 2.5f) return;
            d.y = 0f;
            if (d.sqrMagnitude > PickupRadius * PickupRadius) return;

            RunInventory.Add(Item);
            manager.OnSupplyRecovered(Item);
            Destroy(gameObject);
        }

        // The car's big hit-zone trigger (or its body) touches the item
        private void OnTriggerEnter(Collider other) => TryCrush(other);
        private void OnCollisionEnter(Collision collision) => TryCrush(collision.collider);

        private void TryCrush(Collider other)
        {
            if (_crushed || Time.time - _spawnTime < CrushGrace) return;
            var car = other.GetComponentInParent<ChaosCar>(true);
            if (car == null) return;
            Crush(car.transform.forward);
        }

        /// <summary>Flattened by a car: lost for good. Stays flat on the road for a moment, then vanishes.</summary>
        public void Crush(Vector3 carForward)
        {
            if (_crushed) return;
            _crushed = true;
            var rb = GetComponentInChildren<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(carForward, Vector3.up).normalized + Vector3.forward * 0.001f);
            Vector3 sc = transform.localScale;
            transform.localScale = new Vector3(sc.x * 1.4f, sc.y * 0.15f, sc.z * 1.4f);
            if (_glow != null) _glow.enabled = false;
            Game.Gameplay.GameAudio.PlayAt(Game.Gameplay.Sfx.Crash, transform.position, 0.5f);
            Level2Manager.Instance?.OnSupplyCrushed(Item);
            Destroy(gameObject, 1.5f);
        }
    }
}
