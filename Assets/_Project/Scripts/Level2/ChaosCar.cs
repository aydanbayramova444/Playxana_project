using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// A car driving like it's the end of the world: up and down the road, swerving between lanes,
    /// suddenly speeding up, braking, or turning around. Hitting the player counts as a hit
    /// (<see cref="Level2Manager.RegisterHit"/>). When it leaves the road at one end it comes back at the other.
    ///
    /// Needs: a kinematic Rigidbody on this object, solid colliders on the car parts,
    /// and one bigger trigger BoxCollider for the hit zone (all built by Tools → Level 2 → Build Level 2 Scene).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ChaosCar : MonoBehaviour
    {
        [Header("Road")]
        [Tooltip("Road runs along Z. The car loops between these Z values.")]
        public float minZ = -15f;
        public float maxZ = 215f;
        [Tooltip("How far left/right the car may go (road + a bit of the sidewalk: nowhere is safe).")]
        public float halfWidth = 6.5f;

        [Header("Driving")]
        public float minSpeed = 7f;
        public float maxSpeed = 16f;
        [Tooltip("0 = drives straight and calm, 1 = full apocalypse chaos.")]
        [Range(0f, 1f)] public float chaos = 0.6f;

        private Rigidbody _rb;
        private float _speed, _targetSpeed, _dir, _laneX, _phase, _nextEvent;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth movement (nobody holds a car)
        }

        private void Start()
        {
            _dir = transform.forward.z >= 0f ? 1f : -1f;
            _laneX = transform.position.x;
            _speed = _targetSpeed = Random.Range(minSpeed, maxSpeed);
            _phase = Random.value * 10f;
            _nextEvent = Time.time + Random.Range(1f, 4f);
        }

        private void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            if (Time.time >= _nextEvent) ChaosEvent();

            _speed = Mathf.MoveTowards(_speed, _targetSpeed, 12f * Time.fixedDeltaTime);

            Vector3 pos = _rb.position;
            float swerve = Mathf.Sin((Time.time + _phase) * (0.6f + chaos)) * 2.5f * chaos;
            float targetX = Mathf.Clamp(_laneX + swerve, -halfWidth, halfWidth);

            Vector3 next = pos;
            next.z += _dir * _speed * Time.fixedDeltaTime;
            next.x = Mathf.MoveTowards(pos.x, targetX, (2f + 4f * chaos) * Time.fixedDeltaTime);
            next.x = KeepOutOfSafeZones(next);

            // Off one end of the road → come back at the other end
            if (next.z > maxZ) next.z = minZ;
            else if (next.z < minZ) next.z = maxZ;

            Vector3 delta = next - pos;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f && Mathf.Abs(next.z - pos.z) < 5f) // not on a wrap jump
                _rb.MoveRotation(Quaternion.LookRotation(delta.normalized));
            _rb.MovePosition(next);
        }

        /// <summary>Next to a safe zone the car stays on its side of the zone's edge (never drives into it).</summary>
        private static float KeepOutOfSafeZones(Vector3 p)
        {
            const float carHalfWidth = 1.3f, carHalfLength = 2.6f;
            float x = p.x;
            foreach (SafeZone zone in SafeZone.All)
            {
                Bounds a = zone.Area;
                if (p.z < a.min.z - carHalfLength || p.z > a.max.z + carHalfLength) continue;
                if (a.center.x > 0f) x = Mathf.Min(x, a.min.x - carHalfWidth); // zone on the right side
                else x = Mathf.Max(x, a.max.x + carHalfWidth);                // zone on the left side
            }
            return x;
        }

        /// <summary>Something sudden: new lane, speed burst, hard brake, or a U-turn.</summary>
        private void ChaosEvent()
        {
            _nextEvent = Time.time + Random.Range(1.5f, 5f) * (1.3f - chaos);

            float roll = Random.value;
            if (roll < 0.35f)
                _laneX = Random.Range(-halfWidth + 1f, halfWidth - 1f);              // swerve to a new lane
            else if (roll < 0.6f)
                _targetSpeed = Random.Range(minSpeed, maxSpeed) * (1f + 0.4f * chaos); // speed up / slow down
            else if (roll < 0.75f)
                _targetSpeed = 0.5f;                                                 // hard brake
            else if (roll < 0.75f + 0.2f * chaos)
                _dir = -_dir;                                                        // turn around!
            else
                _targetSpeed = Random.Range(minSpeed, maxSpeed);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            if (Level2Manager.Instance == null) return;

            // Push the player away from the car, a bit forward in the driving direction
            Vector3 away = other.transform.position - transform.position;
            away.y = 0f;
            Vector3 push = (away.normalized + transform.forward * 0.6f).normalized;
            Level2Manager.Instance.RegisterHit(push * Mathf.Lerp(6f, 14f, _speed / maxSpeed));
        }
    }
}
