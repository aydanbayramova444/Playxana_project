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
    /// The player hits them (left-click bat swing or E). Every intruder takes 2–3 hits:
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

        [SerializeField] private float walkSpeed = 2.6f;
        [SerializeField] private float escapeSpeed = 3.4f;    // player walks 5, sprints 8: you can catch them
        [SerializeField] private float fleeSpeed = 6f;
        [SerializeField] private float grabTime = 1.2f;       // at a shelf: the window to stop them
        [SerializeField] private float floorGrabTime = 0.6f;  // snatching something off the floor is quicker
        [SerializeField] private float reachDistance = 1.3f;
        [Tooltip("Hits it takes to stop this intruder (random, min..max)")]
        [SerializeField] private Vector2Int hitsToStop = new Vector2Int(2, 3);
        [Tooltip("Chance that the last hit knocks them down; otherwise they run away scared")]
        [Range(0f, 1f)] [SerializeField] private float knockDownChance = 0.5f;

        public ShelterDirector Director { get; set; }
        public Transform Exit { get; set; }
        public bool IsCarrying => _carried != null;
        public int Health => _health;
        public int MaxHealth => _maxHealth;
        public bool IsDown => _state == State.Down;
        public bool IsFleeing => _state == State.Fleeing;
        /// <summary>Out of the fight: knocked down, running away scared, or gone.</summary>
        public bool IsStopped => _done || _state == State.Down || _state == State.Fleeing;

        private NavMeshAgent _agent;
        private Transform _hand, _body;
        private State _state;
        private Shelf _shelf;
        private Game.Interaction.Pickup _floorItem, _carried;
        private float _timer, _repathAt, _giveUpAt;
        private int _health, _maxHealth;
        private bool _done;

        // ---------- Interaction: the player hits them (E or bat) ----------

        public override bool CanInteract(Game.Interaction.PlayerInteractor player) => !IsStopped;

        public override string GetPrompt(Game.Interaction.PlayerInteractor player) => "Hit";

        public override string GetBlockedReason(Game.Interaction.PlayerInteractor player) => "";

        protected override void OnInteract(Game.Interaction.PlayerInteractor player) => TakeHit(player.transform.position);

        /// <summary>One hit from the player standing at <paramref name="from"/>.</summary>
        public void TakeHit(Vector3 from)
        {
            if (IsStopped) return;
            _health--;

            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

            bool dropped = false;
            if (_carried != null) // they let go of it — it's on the floor now, anyone can grab it
            {
                _carried.KnockByNpc(away * 1.5f + Vector3.up * 2.5f);
                _carried = null;
                dropped = true;
            }
            if (_agent.isOnNavMesh) _agent.Move(away * 0.6f); // knocked back a step

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
            _timer = 0.7f;
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
            _agent.acceleration = 14f;

            _maxHealth = _health = Random.Range(hitsToStop.x, hitsToStop.y + 1);

            if (GetComponent<Collider>() == null) // the player's ray / bat needs something to hit
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);
            }
            if (transform.Find("Body") == null) BuildBody();
            _body = transform.Find("Body");

            _hand = new GameObject("Hand").transform;
            _hand.SetParent(transform, false);
            _hand.localPosition = new Vector3(0.25f, 1.0f, 0.35f);
        }

        private void Start()
        {
            if (!_agent.isOnNavMesh)
            {
                Debug.LogError("[Shelter] Intruder is not on the NavMesh — run Tools → Shelter → Create Shelter Scene.", this);
                enabled = false;
                return;
            }
            PickTarget();
        }

        private void Update()
        {
            if (_done || _state == State.Down || !_agent.isOnNavMesh) return;

            switch (_state)
            {
                case State.GoingToTarget:
                    if (Time.time > _giveUpAt) { PickTarget(); return; } // can't reach it: try something else
                    if (_floorItem != null)
                    {
                        if (!IsLoose(_floorItem)) { PickTarget(); return; } // someone (the player!) took it
                        Vector3 p = _floorItem.transform.position;
                        if (Flat(transform.position - p) < reachDistance * 0.9f) StartGrab(p, floorGrabTime);
                        else if (Time.time > _repathAt) { GoTo(p); _repathAt = Time.time + 0.5f; } // it may still be rolling
                    }
                    else if (_shelf != null)
                    {
                        if (_shelf.IsEmpty) { PickTarget(); return; }
                        if (Flat(transform.position - _shelf.FrontPoint) < reachDistance)
                            StartGrab(_shelf.transform.position, grabTime);
                    }
                    else PickTarget();
                    break;

                case State.Grabbing:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    Game.Interaction.Pickup item = null;
                    if (_floorItem != null && IsLoose(_floorItem)) item = _floorItem;
                    else if (_shelf != null) item = _shelf.TakeOneItem();
                    if (item == null) { PickTarget(); return; }
                    item.TakeByNpc(_hand);
                    _carried = item;
                    _floorItem = null;
                    if (Director != null) Director.OnIntruderGrabbed(this, item);
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
                if (!IsLoose(p)) continue;
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
                if (NavMesh.SamplePosition(stand, out NavMeshHit hit, 2f, NavMesh.AllAreas)) stand = hit.position;
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
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
            _agent.SetDestination(p);
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
