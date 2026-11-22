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

        [Header("Aggression")]
        [Tooltip("How much the car goes for the player: 0 = ignores you, 1 = hunts you down.")]
        [Range(0f, 1f)] public float aggression = 0.6f;
        [Tooltip("The car notices the player this far in front of it (metres).")]
        public float noticeDistance = 30f;

        private Rigidbody _rb;
        private Transform _player;
        private CarAudio _audio;
        private float _huntUntil;
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
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;

            _audio = gameObject.AddComponent<CarAudio>();
            _audio.Init(this);
        }

        /// <summary>Current speed in m/s (engine sound).</summary>
        public float Speed => _speed;

        /// <summary>Player is in front of the car (in its driving direction), close enough, and not in a safe zone.</summary>
        private bool PlayerAhead()
        {
            if (_player == null) return false;
            if (Level2Manager.Instance != null && Level2Manager.Instance.InSafeZone) return false;
            float ahead = (_player.position.z - _rb.position.z) * _dir;
            return ahead > 3f && ahead < noticeDistance;
        }

        private bool Hunting => Time.time < _huntUntil;

        private void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            if (Time.time >= _nextEvent) ChaosEvent();

            // Hunting: keep steering at the player's lane while they're still in front
            if (Hunting)
            {
                if (PlayerAhead()) _laneX = Mathf.Clamp(_player.position.x, -halfWidth, halfWidth);
                else _huntUntil = 0f; // passed them / they hid: back to normal chaos
            }

            _speed = Mathf.MoveTowards(_speed, _targetSpeed, 12f * Time.fixedDeltaTime);

            Vector3 pos = _rb.position;
            float swerve = Hunting ? 0f : Mathf.Sin((Time.time + _phase) * (0.6f + chaos)) * 2.5f * chaos;
            float targetX = Mathf.Clamp(_laneX + swerve, -halfWidth, halfWidth);

            Vector3 next = pos;
            next.z += _dir * _speed * Time.fixedDeltaTime;
            float steer = (2f + 4f * chaos) * (Hunting ? 1f + aggression : 1f); // hunting cars steer harder
            next.x = Mathf.MoveTowards(pos.x, targetX, steer * Time.fixedDeltaTime);
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

            // Aggressive: go for the player — swerve into their lane and floor it
            if (PlayerAhead() && Random.value < aggression * 0.7f)
            {
                _laneX = Mathf.Clamp(_player.position.x, -halfWidth, halfWidth);
                _targetSpeed = maxSpeed * (1f + 0.35f * aggression);
                _huntUntil = Time.time + 2f + 2f * aggression;
                _audio.Horn();
                _nextEvent = Time.time + 2f + 2f * aggression; // no other event while hunting
                return;
            }

            float roll = Random.value;
            if (roll < 0.35f)
                _laneX = Random.Range(-halfWidth + 1f, halfWidth - 1f);              // swerve to a new lane
            else if (roll < 0.6f)
                _targetSpeed = Random.Range(minSpeed, maxSpeed) * (1f + 0.4f * chaos); // speed up / slow down
            else if (roll < 0.75f)
            {
                if (_speed > 4f) _audio.Skid();
                _targetSpeed = 0.5f;                                                 // hard brake
            }
            else if (roll < 0.75f + 0.2f * chaos)
            {
                _dir = -_dir;                                                        // turn around!
                _audio.Skid();
            }
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
            bool hit = Level2Manager.Instance.RegisterHit(push * Mathf.Lerp(6f, 14f, _speed / maxSpeed));
            if (hit && _audio != null) _audio.Crash();
        }
    }
}
