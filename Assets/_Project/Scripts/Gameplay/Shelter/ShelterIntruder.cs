using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// Level 3 (The Shelter): another survivor who sneaks in to steal your supplies.
    ///
    ///   Enter → walk to a shelf with stock → grab one product (takes a moment) → run back to the exit.
    ///   Reaches the exit with the product → it's gone for good (ShelterDirector counts it as stolen).
    ///
    /// The player looks at the intruder and presses E:
    ///   carrying something → they drop it (it lands on the floor, the player can put it back on its shelf)
    ///   and flee; not carrying yet → they just flee.
    ///
    /// Spawned by <see cref="ShelterDirector"/>; builds its own simple body if it has none.
    /// Uses the team's interaction system (it is an <see cref="Game.Interaction.Interactable"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ShelterIntruder : Game.Interaction.Interactable
    {
        private enum State { GoingToShelf, Grabbing, Escaping, Fleeing }

        [SerializeField] private float walkSpeed = 2.6f;
        [SerializeField] private float escapeSpeed = 3.4f;  // player walks 5, sprints 8: you can catch them
        [SerializeField] private float fleeSpeed = 6f;
        [SerializeField] private float grabTime = 1.2f;      // window to stop them at the shelf
        [SerializeField] private float reachDistance = 1.3f;

        public ShelterDirector Director { get; set; }
        public Transform Exit { get; set; }
        public bool IsCarrying => _carried != null;

        private NavMeshAgent _agent;
        private Transform _hand;
        private State _state;
        private Shelf _shelf;
        private Game.Interaction.Pickup _carried;
        private float _grabLeft;
        private bool _done;

        // ---------- Interaction (player presses E) ----------

        public override bool CanInteract(Game.Interaction.PlayerInteractor player) => !_done && _state != State.Fleeing;

        public override string GetPrompt(Game.Interaction.PlayerInteractor player) =>
            _carried != null ? "Take it back" : "Scare away";

        public override string GetBlockedReason(Game.Interaction.PlayerInteractor player) => "";

        protected override void OnInteract(Game.Interaction.PlayerInteractor player)
        {
            // Throw the stolen product away from the player so it lands in front of them.
            if (_carried != null)
            {
                Vector3 away = (transform.position - player.transform.position);
                away.y = 0f;
                _carried.KnockByNpc(away.normalized * 1.5f + Vector3.up * 2.5f);
                _carried = null;
            }

            if (Director != null) Director.OnIntruderScared(this);
            Flee();
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

            if (GetComponent<Collider>() == null) // the player's E-ray needs something to hit
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);
            }
            if (transform.Find("Body") == null) BuildBody();

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
            PickShelf();
        }

        private void Update()
        {
            if (_done || !_agent.isOnNavMesh) return;

            switch (_state)
            {
                case State.GoingToShelf:
                    if (_shelf == null || _shelf.IsEmpty) { PickShelf(); return; } // the player moved it / it ran out
                    if (Flat(transform.position - _shelf.FrontPoint) < reachDistance)
                    {
                        _state = State.Grabbing;
                        _grabLeft = grabTime;
                        _agent.isStopped = true;
                        Face(_shelf.transform.position);
                    }
                    break;

                case State.Grabbing:
                    _grabLeft -= Time.deltaTime;
                    if (_grabLeft > 0f) return;
                    _carried = _shelf != null ? _shelf.TakeOneItem() : null;
                    if (_carried == null) { PickShelf(); return; } // empty by now
                    _carried.TakeByNpc(_hand);
                    if (Director != null) Director.OnIntruderGrabbed(this, _carried);
                    GoToExit(escapeSpeed, State.Escaping);
                    break;

                case State.Escaping:
                case State.Fleeing:
                    if (!_agent.pathPending && _agent.remainingDistance < 0.8f) LeaveShelter();
                    break;
            }
        }

        // ---------- Behaviour ----------

        private void PickShelf()
        {
            var options = new List<Shelf>();
            foreach (Shelf s in FindObjectsByType<Shelf>())
                if (!s.IsEmpty) options.Add(s);

            if (options.Count == 0) { Flee(); return; } // nothing left to take

            _shelf = options[Random.Range(0, options.Count)];
            Vector3 stand = _shelf.FrontPoint;
            if (NavMesh.SamplePosition(stand, out NavMeshHit hit, 2f, NavMesh.AllAreas)) stand = hit.position;

            _state = State.GoingToShelf;
            _agent.isStopped = false;
            _agent.speed = walkSpeed;
            _agent.SetDestination(stand);
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

        /// <summary>Freeze in place (the round is over).</summary>
        public void Stop()
        {
            _done = true;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
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
