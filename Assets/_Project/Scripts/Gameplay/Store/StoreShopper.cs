using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// Scene 1 (the store): a panicking shopper.
    ///
    ///   Spawned outside the entrance by <see cref="StoreRound"/> → walks in → takes products off the shelves,
    ///   one per hand (max 2) → with full hands it makes a mess: knocks products off the shelves onto the
    ///   floor and kicks loose ones around.
    ///
    /// The player walks up and presses E: the shopper's products are taken — one goes into the player's
    /// hand (if free), the rest drop at the player's feet. Then the shopper starts collecting again.
    /// Uses the team's interaction system (it is a <see cref="Game.Interaction.Interactable"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class StoreShopper : Game.Interaction.Interactable
    {
        private enum State { Walking, Grabbing, Knocking, Stunned }

        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float grabTime = 0.9f;      // time at a shelf before taking a product
        [SerializeField] private float knockTime = 0.6f;
        [SerializeField] private float reachDistance = 1.3f;
        [Tooltip("Loose products this close get kicked around while the shopper makes a mess")]
        [SerializeField] private float kickRadius = 1.4f;
        [SerializeField] private float kickForce = 2.5f;

        public const int HandsCapacity = 2;

        public StoreRound Round { get; set; }
        /// <summary>Rigged character model for the body (set by StoreRound before Start). Null = simple shapes.</summary>
        public GameObject BodyModel { get; set; }
        public int HeldCount => (_left != null ? 1 : 0) + (_right != null ? 1 : 0);
        public bool HandsFull => HeldCount >= HandsCapacity;
        /// <summary>How many products this shopper has thrown on the floor so far.</summary>
        public int MessMade { get; private set; }

        private NavMeshAgent _agent;
        private Transform _handL, _handR;
        private LowPolyHuman _human;
        private Game.Interaction.Pickup _left, _right;
        private State _state;
        private Shelf _shelf, _lastShelf;
        private float _timer, _nextKick;
        private bool _stopped;

        // ---------- Interaction (player presses E) ----------

        public override bool CanInteract(Game.Interaction.PlayerInteractor player) => !_stopped && HeldCount > 0;

        public override string GetPrompt(Game.Interaction.PlayerInteractor player) =>
            HeldCount > 1 ? "Take their items (2)" : "Take their item";

        public override string GetBlockedReason(Game.Interaction.PlayerInteractor player) => "";

        protected override void OnInteract(Game.Interaction.PlayerInteractor player)
        {
            var items = new List<Game.Interaction.Pickup>();
            if (_left != null) items.Add(_left);
            if (_right != null) items.Add(_right);
            _left = _right = null;

            Vector3 feet = player.transform.position + player.transform.forward * 0.6f + Vector3.up * 0.5f;
            foreach (var item in items)
            {
                if (!player.IsHoldingAnything) { player.PickUp(item); continue; } // straight into your hand
                item.transform.position = feet;                                    // the rest: at your feet
                item.KnockByNpc(Random.insideUnitSphere * 0.5f + Vector3.up);
            }

            if (Round != null) Round.OnItemsTaken(this, items.Count);
            _state = State.Stunned;  // a moment of surprise, then back to grabbing
            _timer = 1.5f;
            _agent.isStopped = true;
        }

        // ---------- Unity ----------

        private void Awake()
        {
            displayName = "Shopper";
            _agent = GetComponent<NavMeshAgent>();
            _agent.radius = 0.3f;
            _agent.height = 1.8f;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 12f;

            if (GetComponent<Collider>() == null) // the player's E-ray needs something to hit
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);
            }
            _handL = Hand("Hand_L", -0.17f);
            _handR = Hand("Hand_R", 0.17f);
        }

        private void Start()
        {
            if (transform.Find("Body") == null)
            {
                Color[] shirts =
                {
                    new Color(0.55f, 0.15f, 0.15f), new Color(0.18f, 0.3f, 0.55f), new Color(0.25f, 0.45f, 0.25f),
                    new Color(0.6f, 0.5f, 0.18f), new Color(0.4f, 0.4f, 0.42f), new Color(0.45f, 0.25f, 0.45f),
                };
                if (BodyModel != null)
                    _human = LowPolyHuman.Build(transform, BodyModel, 1.75f, shirts[Random.Range(0, shirts.Length)], grime: 0.15f);
                else BuildBody();
            }

            if (!_agent.isOnNavMesh)
            {
                Debug.LogError("[Store] Shopper is not on the NavMesh — bake one (Tools → Store → Add Shopper Bots + NavMesh).", this);
                enabled = false;
                return;
            }
            NextTarget();
        }

        private void Update()
        {
            if (_human != null) _human.CarryPose = HeldCount > 0; // arms forward while holding something
            if (_stopped || !_agent.isOnNavMesh) return;

            switch (_state)
            {
                case State.Walking:
                    if (HandsFull && Time.time >= _nextKick) { KickLooseItems(); _nextKick = Time.time + 1.2f; }
                    if (_shelf == null) { if (Arrived()) NextTarget(); return; }        // wandering
                    if (_shelf.IsEmpty && !HandsFull) { NextTarget(); return; }        // someone was faster
                    if (Flat(transform.position - _shelf.FrontPoint) < reachDistance)
                    {
                        _agent.isStopped = true;
                        Face(_shelf.transform.position);
                        _state = HandsFull ? State.Knocking : State.Grabbing;
                        _timer = HandsFull ? knockTime : grabTime;
                    }
                    break;

                case State.Grabbing:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    var item = _shelf != null ? _shelf.TakeOneItem() : null;
                    if (item != null)
                    {
                        Transform hand = _left == null ? _handL : _handR;
                        item.TakeByNpc(hand);
                        if (_left == null) _left = item; else _right = item;
                        if (Round != null) Round.OnShopperGrabbed(this, item);
                    }
                    NextTarget();
                    break;

                case State.Knocking:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    MakeMessAt(_shelf);
                    NextTarget();
                    break;

                case State.Stunned:
                    if ((_timer -= Time.deltaTime) > 0f) return;
                    NextTarget();
                    break;
            }
        }

        // ---------- Behaviour ----------

        /// <summary>Hands not full → a shelf with stock. Hands full → any shelf to wreck, or wander and kick things.</summary>
        private void NextTarget()
        {
            _state = State.Walking;
            _agent.isStopped = false;
            _agent.speed = HandsFull ? walkSpeed * 1.3f : walkSpeed;

            var shelves = new List<Shelf>();
            foreach (Shelf s in FindObjectsByType<Shelf>())
                if (!s.IsEmpty && (s != _lastShelf || HandsFull)) shelves.Add(s);
            if (shelves.Count == 0)
                foreach (Shelf s in FindObjectsByType<Shelf>()) if (!s.IsEmpty) shelves.Add(s);

            if (shelves.Count > 0)
            {
                _shelf = shelves[Random.Range(0, shelves.Count)];
                _lastShelf = _shelf;
                Vector3 stand = _shelf.FrontPoint;
                if (NavMesh.SamplePosition(stand, out NavMeshHit hit, 2f, NavMesh.AllAreas)) stand = hit.position;
                _agent.SetDestination(stand);
                return;
            }

            // Nothing left on the shelves: wander around the store kicking things.
            _shelf = null;
            Vector3 p = transform.position + Random.insideUnitSphere * 6f;
            if (NavMesh.SamplePosition(p, out NavMeshHit h, 6f, NavMesh.AllAreas)) _agent.SetDestination(h.position);
        }

        /// <summary>Throws 1–2 products off this shelf onto the floor and kicks nearby loose ones.</summary>
        private void MakeMessAt(Shelf shelf)
        {
            int count = Random.Range(1, 3);
            string knocked = null;
            for (int i = 0; i < count && shelf != null; i++)
            {
                var product = shelf.TakeOneItem();
                if (product == null) break;
                Vector3 dir = shelf.transform.forward + shelf.transform.right * Random.Range(-1f, 1f);
                product.KnockByNpc(dir.normalized * Random.Range(2f, 3.5f) + Vector3.up * Random.Range(1f, 2.5f));
                MessMade++;
                knocked = product.ItemId;
            }
            KickLooseItems();
            if (Round != null && knocked != null) Round.OnMess(this, knocked);
        }

        private void KickLooseItems()
        {
            foreach (Collider c in Physics.OverlapSphere(transform.position, kickRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var p = c.GetComponentInParent<Game.Interaction.Pickup>();
                Rigidbody rb = c.attachedRigidbody;
                if (p == null || rb == null || rb.isKinematic || p.IsHeld || p.CurrentSlot != null) continue;
                Vector3 away = p.transform.position - transform.position;
                away.y = 0f;
                rb.AddForce((away.normalized + Random.insideUnitSphere * 0.4f) * kickForce + Vector3.up * 1.5f,
                    ForceMode.VelocityChange);
            }
        }

        /// <summary>Freeze in place (the round is over).</summary>
        public void Stop()
        {
            _stopped = true;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private bool Arrived() => !_agent.pathPending && _agent.remainingDistance < 0.8f;

        private void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private Transform Hand(string handName, float side)
        {
            var h = new GameObject(handName).transform;
            h.SetParent(transform, false);
            h.localPosition = new Vector3(side, 1.18f, 0.5f); // where the hands are with both arms forward
            return h;
        }

        /// <summary>A simple shopper: coloured jacket, head, two arms reaching forward.</summary>
        private void BuildBody()
        {
            var body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            Color[] jackets =
            {
                new Color(0.45f, 0.12f, 0.12f), new Color(0.15f, 0.25f, 0.45f), new Color(0.2f, 0.35f, 0.2f),
                new Color(0.45f, 0.38f, 0.15f), new Color(0.3f, 0.3f, 0.32f),
            };
            Color jacket = jackets[Random.Range(0, jackets.Length)];
            Color skin = new Color(0.85f, 0.68f, 0.55f);
            Shape(PrimitiveType.Capsule, body, new Vector3(0, 0.85f, 0), new Vector3(0.55f, 0.85f, 0.45f), jacket);
            Shape(PrimitiveType.Sphere, body, new Vector3(0, 1.62f, 0), new Vector3(0.32f, 0.36f, 0.32f), skin);
            Shape(PrimitiveType.Cube, body, new Vector3(-0.32f, 1.1f, 0.15f), new Vector3(0.12f, 0.12f, 0.4f), jacket); // arms forward
            Shape(PrimitiveType.Cube, body, new Vector3(0.32f, 1.1f, 0.15f), new Vector3(0.12f, 0.12f, 0.4f), jacket);
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
