using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// Level 3 (The Shelter): another survivor who sneaks in to steal your supplies.
    ///
    ///   Enter → go for a product: one lying on the FLOOR (dropped by someone else) or one on a shelf →
    ///   grab it → run back to the exit. Out of the door with it → it's gone for good (stolen).
    ///
    /// The player hits them (left-click bat swing or E). Every intruder takes 3–4 hits (and is untouchable for a moment after each one):
    ///   each hit staggers them, and if they carry something they DROP it (others may grab it!);
    ///   the last hit either knocks them DOWN or makes them RUN AWAY scared.
    ///
    /// Spawned by <see cref="ShelterDirector"/>; builds its own simple body if it has none.
    /// Uses the team's interaction system (it is an <see cref="Game.Interaction.Interactable"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ShelterIntruder : Game.Interaction.Interactable
    {
        private enum State { GoingToTarget, Grabbing, Escaping, Fleeing, Staggered, Down }

        [SerializeField] private float walkSpeed = 3.6f;
        [SerializeField] private float escapeSpeed = 4.3f;    // player walks 5, sprints 8: you can catch them, barely
        [SerializeField] private float fleeSpeed = 6f;
        [SerializeField] private float grabTime = 1.2f;       // at a shelf: the window to stop them
        [SerializeField] private float floorGrabTime = 0.6f;  // snatching something off the floor is quicker
        [SerializeField] private float reachDistance = 1.3f;
        [Tooltip("Hits it takes to stop this intruder (random, min..max)")]
        [SerializeField] private Vector2Int hitsToStop = new Vector2Int(3, 4);
        [Tooltip("After a hit they can't be hit again for this long (no spam-clicking them down)")]
        [SerializeField] private float hitRecovery = 0.35f;
        [Tooltip("How long a hit stuns them before they go back to stealing")]
        [SerializeField] private float staggerTime = 0.4f;
        [Tooltip("After a hit they jump sideways up to this far (harder to hit twice in a row)")]
        [SerializeField] private float dodgeDistance = 0.85f;
        [Tooltip("Chance that the last hit knocks them down; otherwise they run away scared")]
        [Range(0f, 1f)] [SerializeField] private float knockDownChance = 0.5f;

        public ShelterDirector Director { get; set; }
        /// <summary>Rigged character model for the body (set by the director before Start). Null = simple shapes.</summary>
        public GameObject BodyModel { get; set; }
        public Transform Exit { get; set; }
        public bool IsCarrying => _carried != null;
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public bool IsDown => _state == State.Down;
        public bool IsFleeing => _state == State.Fleeing;
        /// <summary>Out of the fight: knocked down, running away scared, or gone.</summary>
        public bool IsStopped => _done || _state == State.Down || _state == State.Fleeing;
        /// <summary>Reaching into a shelf right now (the window to stop them).</summary>
        public bool IsGrabbing => _state == State.Grabbing;
        /// <summary>The shelf this intruder last took something from, and the frame it happened (for checks / tests).</summary>
        public Shelf LastTakenFrom { get; private set; }
        public int LastTakeFrame { get; private set; } = -1;
        public float ReachDistance => reachDistance;

        // The baked NavMesh also covers the ROOFS of the bunker, corridor and tunnel. An intruder up there walks
        // unseen above the room — they must always stay on the floor (anything higher than this is a roof).
        public const float FloorMaxY = 1.0f;

        private NavMeshAgent _agent;
        private Transform _hand, _body;
        private Light _glow;
        private State _state;
        private Shelf _shelf;
        private Game.Interaction.Pickup _floorItem, _carried;
        private Shelf _carriedHome; // the shelf what we carry came from: a dropped supply goes back there
        private float _timer, _repathAt, _giveUpAt;
        private int _health, _maxHealth;
        private bool _done;
        private float _hittableAt;

        // ---------- Interaction: the player hits them (E or bat) ----------

        public override bool CanInteract(Game.Interaction.PlayerInteractor player) => !IsStopped && CanBeHit;

        /// <summary>False for a moment right after a hit (they're recovering — the next swing misses).</summary>
        public bool CanBeHit => Time.time >= _hittableAt;

        public override string GetPrompt(Game.Interaction.PlayerInteractor player) => "Hit";

        public override string GetBlockedReason(Game.Interaction.PlayerInteractor player) => "";

        protected override void OnInteract(Game.Interaction.PlayerInteractor player) => TakeHit(player.transform.position);

        /// <summary>One hit from the player standing at <paramref name="from"/>.</summary>
        public void TakeHit(Vector3 from)
        {
            if (IsStopped || !CanBeHit) return;
            _health--;
            _hittableAt = Time.time + hitRecovery;

            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

            bool dropped = false;
            if (_carried != null) // they let go of it — it's on the floor now, glowing: pick it up to put it back
            {
                _carried.KnockByNpc(away * 1.5f + Vector3.up * 2.5f);
                LostSupply.Mark(_carried, _carriedHome, this);
                _carried = null;
                _carriedHome = null;
                dropped = true;
            }
            if (_agent.isOnNavMesh) // knocked back a step and dodging sideways: the next hit isn't free
            {
                Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -1f : 1f);
                _agent.Move(away * 0.6f + side * Random.Range(dodgeDistance * 0.5f, dodgeDistance));
            }

            if (_health <= 0)
            {
                bool down = Random.value < knockDownChance;
                if (Director != null) Director.OnIntruderStopped(this, down);
                if (down) StartCoroutine(Collapse(away));
                else Flee();
                return;
            }

            if (Director != null) Director.OnIntruderHit(this, dropped);
            _state = State.Staggered;
            _timer = staggerTime;
            _agent.isStopped = true;
            StartCoroutine(Flinch(away));
        }

        // ---------- Unity ----------

        private void Awake()
        {
            displayName = "Survivor";
            _agent = GetComponent<NavMeshAgent>();
            _agent.radius = 0.3f;
            _agent.height = 1.8f;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 20f;
            // No avoidance between intruders: in the narrow corridor / tunnel they used to stop and wait
            // for each other (one coming in, one running out). Now they slip past each other.
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            _agent.autoBraking = false; // don't slow down near every corner / target

            _maxHealth = _health = Random.Range(hitsToStop.x, hitsToStop.y + 1);

            if (GetComponent<Collider>() == null) // the player's ray / bat needs something to hit
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);
            }
            // Stolen goods ride on the shoulder: visible from behind as they run for the exit.
            _hand = new GameObject("Hand").transform;
            _hand.SetParent(transform, false);
            _hand.localPosition = new Vector3(0.24f, 1.62f, -0.05f);

            // Red glow on anyone carrying your supplies — easy to spot in the dark shelter.
            _glow = new GameObject("CarryGlow").AddComponent<Light>();
            _glow.transform.SetParent(transform, false);
            _glow.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            _glow.type = LightType.Point;
            _glow.color = new Color(1f, 0.15f, 0.1f);
            _glow.range = 2.5f;
            _glow.intensity = 1.6f;
            _glow.shadows = LightShadows.None;
            _glow.enabled = false;
        }

        private void Start()
        {
            if (transform.Find("Body") == null)
            {
                if (BodyModel != null) LowPolyHuman.Build(transform, BodyModel, 1.75f, null, grime: 1f); // filthy survivor
                else BuildBody();
            }
            _body = transform.Find("Body");

            if (!_agent.isOnNavMesh)
            {
                Debug.LogError("[Shelter] Intruder is not on the NavMesh — run Tools → Shelter → Create Shelter Scene.", this);
                enabled = false;
                return;
            }
            KeepOnFloor();
            PickTarget();
        }

        private void Update()
        {
            // Red glow while reaching into a shelf and while carrying your supplies: thefts are easy to see
            if (_glow != null) _glow.enabled = (_carried != null || _state == State.Grabbing) && _state != State.Down;
            if (_done || _state == State.Down || !_agent.isOnNavMesh) return;
            if (KeepOnFloor()) { PickTarget(); return; }

            switch (_state)
            {
                case State.GoingToTarget:
                    if (Time.time > _giveUpAt) { PickTarget(); return; } // can't reach it: try something else
                    if (_floorItem != null)
                    {
                        if (!CanTake(_floorItem)) { PickTarget(); return; } // someone (the player!) took it
                        Vector3 p = _floorItem.transform.position;
                        if (Flat(transform.position - p) < reachDistance * 0.9f) StartGrab(p, floorGrabTime);
                        else if (Time.time > _repathAt) { GoTo(p); _repathAt = Time.time + 0.5f; } // it may still be rolling
                    }
                    else if (_shelf != null)
                    {
                        if (_shelf.IsEmpty) { PickTarget(); return; }
                        if (CanReach(_shelf))
                            StartGrab(_shelf.transform.position, grabTime);
                    }
                    else PickTarget();
                    break;

                case State.Grabbing:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    Game.Interaction.Pickup item = null;
                    bool fromFloor = _floorItem != null && CanTake(_floorItem);
                    Shelf home = _shelf;
                    if (fromFloor)
                    {
                        item = _floorItem;
                        var lost = item.GetComponent<LostSupply>();
                        if (lost != null)
                        {
                            home = lost.Home;
                            lost.Clear(); // no glow on a thief's shoulder (they glow red themselves)
                        }
                    }
                    else if (_shelf != null)
                    {
                        LastTakenFrom = _shelf;
                        LastTakeFrame = Time.frameCount;
                        item = _shelf.TakeOneItem();
                    }
                    if (item == null) { PickTarget(); return; }
                    _carriedHome = home;
                    item.TakeByNpc(_hand);
                    _carried = item;
                    _floorItem = null;
                    if (Director != null) Director.OnIntruderGrabbed(this, item, fromFloor);
                    GoToExit(escapeSpeed, State.Escaping);
                    break;

                case State.Staggered:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    PickTarget(); // back to stealing
                    break;

                case State.Escaping:
                case State.Fleeing:
                    if (!_agent.pathPending && _agent.remainingDistance < 0.8f) LeaveShelter();
                    break;
            }
        }

        // ---------- Behaviour ----------

        /// <summary>Something lying on the floor nearby first (quick to grab), otherwise a shelf with stock.</summary>
        private void PickTarget()
        {
            _shelf = null;
            _floorItem = null;

            Game.Interaction.Pickup nearest = null;
            float best = float.MaxValue;
            foreach (var p in FindObjectsByType<Game.Interaction.Pickup>())
            {
                if (!CanTake(p)) continue;
                float d = Flat(p.transform.position - transform.position);
                if (d < best) { best = d; nearest = p; }
            }

            var shelves = new List<Shelf>();
            foreach (Shelf s in FindObjectsByType<Shelf>())
                if (!s.IsEmpty) shelves.Add(s);

            if (nearest != null && (shelves.Count == 0 || best < 7f || Random.value < 0.5f))
            {
                _floorItem = nearest;
                GoTo(nearest.transform.position);
            }
            else if (shelves.Count > 0)
            {
                _shelf = shelves[Random.Range(0, shelves.Count)];
                Vector3 stand = _shelf.FrontPoint;
                FloorPoint(stand, 2f, out stand);
                GoTo(stand);
            }
            else
            {
                Flee(); // nothing left to take
                return;
            }

            _state = State.GoingToTarget;
            _agent.isStopped = false;
            _agent.speed = walkSpeed;
            _giveUpAt = Time.time + 9f;
        }

        private void GoTo(Vector3 p)
        {
            if (FloorPoint(p, 1.5f, out Vector3 floor)) p = floor;
            _agent.SetDestination(p);
        }

        /// <summary>Standing right in front of the shelf, on the same floor (not on the roof above it).</summary>
        private bool CanReach(Shelf shelf) =>
            Flat(transform.position - shelf.FrontPoint) < reachDistance &&
            Mathf.Abs(transform.position.y - shelf.transform.position.y) < FloorMaxY;

        /// <summary>The nearest NavMesh point to <paramref name="p"/> that is on the floor (never on a roof).</summary>
        public static bool FloorPoint(Vector3 p, float radius, out Vector3 floor)
        {
            floor = p;
            Vector3 low = new Vector3(p.x, Mathf.Min(p.y, 0.05f), p.z);
            for (float r = 0.25f; r <= radius + 0.001f; r *= 2f)
                if (NavMesh.SamplePosition(low, out NavMeshHit hit, r, NavMesh.AllAreas) && hit.position.y < FloorMaxY)
                {
                    floor = hit.position;
                    return true;
                }
            return false;
        }

        /// <summary>Somehow up on a roof (spawned there / pushed there)? Back down onto the floor. True if moved.</summary>
        private bool KeepOnFloor()
        {
            if (transform.position.y < FloorMaxY) return false;
            Vector3 from = transform.position;
            Vector3 to;
            if (!FloorPoint(from, 3f, out to) && !(Exit != null && FloorPoint(Exit.position, 3f, out to))) return false;
            Debug.LogWarning($"[Shelter] {name} was up on a roof at {from} — moved down to the floor at {to}.", this);
            _agent.Warp(to);
            return true;
        }

        private void StartGrab(Vector3 lookAt, float time)
        {
            _state = State.Grabbing;
            _timer = time;
            _agent.isStopped = true;
            Face(lookAt);
        }

        /// <summary>A product lying around: not in anyone's hands, not on a shelf.</summary>
        private static bool IsLoose(Game.Interaction.Pickup p) => p != null && !p.IsHeld && p.CurrentSlot == null;

        /// <summary>Loose, and not protected right after a thief dropped it (<see cref="LostSupply"/>).</summary>
        private bool CanTake(Game.Interaction.Pickup p)
        {
            if (!IsLoose(p)) return false;
            var lost = p.GetComponent<LostSupply>();
            return lost == null || lost.CanBeTakenBy(this);
        }

        private void Flee() => GoToExit(fleeSpeed, State.Fleeing);

        private void GoToExit(float speed, State state)
        {
            _state = state;
            _agent.isStopped = false;
            _agent.speed = speed;
            if (Exit != null) _agent.SetDestination(Exit.position);
            else LeaveShelter();
        }

        private void LeaveShelter()
        {
            if (_done) return;
            _done = true;
            if (_carried != null)
            {
                if (Director != null) Director.OnSupplyStolen(this, _carried.ItemId);
                Destroy(_carried.gameObject);
            }
            if (Director != null) Director.OnIntruderGone(this);
            Destroy(gameObject);
        }

        /// <summary>Knocked out: falls backwards, lies there a moment, then is gone.</summary>
        private IEnumerator Collapse(Vector3 away)
        {
            _state = State.Down;
            _agent.isStopped = true;
            foreach (var c in GetComponents<Collider>()) c.enabled = false; // no more hits, doesn't block the aisle
            _agent.enabled = false;

            Quaternion from = transform.rotation;
            Quaternion to = Quaternion.AngleAxis(85f, Vector3.Cross(Vector3.up, away)) * from; // tip over away from you
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(from, to, t / 0.45f);
                yield return null;
            }
            transform.rotation = to;
            yield return new WaitForSeconds(3f);

            _done = true;
            if (Director != null) Director.OnIntruderGone(this);
            Destroy(gameObject);
        }

        /// <summary>Quick recoil of the body when hit.</summary>
        private IEnumerator Flinch(Vector3 away)
        {
            if (_body == null) yield break;
            Quaternion rest = _body.localRotation;
            Vector3 axis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away));
            Quaternion hit = Quaternion.AngleAxis(25f, axis) * rest;
            for (float t = 0; t < 0.1f; t += Time.deltaTime) { _body.localRotation = Quaternion.Slerp(rest, hit, t / 0.1f); yield return null; }
            for (float t = 0; t < 0.3f; t += Time.deltaTime) { _body.localRotation = Quaternion.Slerp(hit, rest, t / 0.3f); yield return null; }
            _body.localRotation = rest;
        }

        /// <summary>Freeze in place (the round is over).</summary>
        public void Stop()
        {
            _done = true;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>A hunched, ragged figure: dark coat, hood, two faint eyes.</summary>
        private void BuildBody()
        {
            var body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            Color coat = new Color(Random.Range(0.10f, 0.18f), Random.Range(0.10f, 0.15f), Random.Range(0.09f, 0.13f));
            Shape(PrimitiveType.Capsule, body, new Vector3(0, 0.8f, 0), new Vector3(0.6f, 0.8f, 0.5f), coat);
            Shape(PrimitiveType.Sphere, body, new Vector3(0, 1.55f, 0.06f), new Vector3(0.38f, 0.4f, 0.38f), coat * 0.8f); // hood
            Color eye = new Color(0.9f, 0.85f, 0.6f);
            Shape(PrimitiveType.Cube, body, new Vector3(-0.07f, 1.57f, 0.24f), new Vector3(0.05f, 0.03f, 0.02f), eye);
            Shape(PrimitiveType.Cube, body, new Vector3(0.07f, 1.57f, 0.24f), new Vector3(0.05f, 0.03f, 0.02f), eye);
            body.localRotation = Quaternion.Euler(8f, 0, 0); // hunched forward
        }

        private static void Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            var mat = new Material(r.sharedMaterial) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            r.sharedMaterial = mat;
        }
    }
}
