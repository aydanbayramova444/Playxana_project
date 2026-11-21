using System;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Put this on the Player (next to PlayerController).
    /// Looks where the camera aims and handles E ("Player/Interact"):
    ///   empty hands:  Pickup → pick up,  Interactable → use
    ///   holding item: ItemReceiver → put it there,  Interactable (e.g. spill + mop) → use,
    ///                 otherwise → drop it
    /// Hold-type interactables (HoldDuration > 0) need E held down.
    ///
    /// The hint text is sent through GameEvents.InteractionPromptChanged so the HUD can show it.
    /// Until a real HUD exists, "Show Simple Prompt" draws it at the bottom of the screen.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float reach = 2.5f;
        [SerializeField] private LayerMask aimMask = ~0;

        [Header("Holding")]
        [Tooltip("Where the held item floats, relative to the camera")]
        [SerializeField] private Vector3 holdOffset = new Vector3(0.35f, -0.3f, 0.7f);
        [SerializeField] private float dropForwardSpeed = 1.5f;

        [Header("Debug UI (turn off when the HUD shows the prompt)")]
        [SerializeField] private bool showSimplePrompt = true;

        private InputAction _interact;
        private Transform _holdPoint;
        private Pickup _held;

        private string _prompt = "";
        private Interactable _holdTarget;
        private float _holdProgress;

        public Pickup HeldItem => _held;
        public string HeldItemId => _held != null ? _held.ItemId : "";
        public bool IsHolding => _held != null;

        // Every scene with a player (Main, Dev scenes ...) gets interaction automatically,
        // without changing the shared Player prefab or the scenes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAddToPlayer()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            AddToPlayers();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s,
            UnityEngine.SceneManagement.LoadSceneMode m) => AddToPlayers();

        private static void AddToPlayers()
        {
            foreach (var p in FindObjectsByType<Game.Player.PlayerController>(FindObjectsInactive.Exclude))
                if (p.GetComponent<PlayerInteractor>() == null &&
                    p.GetComponent<Game.Interaction.PlayerInteractor>() == null) ;
        }

        private void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            _interact = InputSystem.actions.FindAction("Player/Interact", true);

            _holdPoint = new GameObject("HoldPoint").transform;
            _holdPoint.SetParent(cam.transform, false);
            _holdPoint.localPosition = holdOffset;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing)
            {
                SetPrompt("");
                return;
            }

            Look(out Pickup lookPickup, out ItemReceiver lookReceiver, out Interactable lookUse);

            string text = "";
            Action action = null;       // press-E action
            Interactable holdUse = null; // hold-E action

            if (_held != null)
            {
                if (lookReceiver != null && lookReceiver.CanReceive(_held))
                {
                    text = lookReceiver.GetPrompt(_held);
                    action = () => Place(lookReceiver);
                }
                else if (lookUse != null && lookUse.CanInteract(this))
                {
                    text = lookUse.GetPrompt(this);
                    if (lookUse.HoldDuration > 0f) holdUse = lookUse;
                    else action = () => Use(lookUse);
                }
                else
                {
                    text = "Drop " + _held.DisplayName;
                    action = Drop;
                }
            }
            else if (lookPickup != null)
            {
                text = "Pick up " + lookPickup.DisplayName;
                action = () => PickUp(lookPickup);
            }
            else if (lookUse != null)
            {
                text = lookUse.GetPrompt(this);
                if (lookUse.CanInteract(this))
                {
                    if (lookUse.HoldDuration > 0f) holdUse = lookUse;
                    else action = () => Use(lookUse);
                }
            }

            if (holdUse != null)
            {
                text = HandleHold(holdUse, text);
            }
            else
            {
                _holdTarget = null;
                _holdProgress = 0f;

                if (action != null)
                {
                    text = "[E] " + text;
                    if (_interact.WasPressedThisFrame()) action();
                }
            }

            SetPrompt(text);
        }

        // Hold E on a hold-type interactable (e.g. clean the spill)
        private string HandleHold(Interactable target, string text)
        {
            if (!_interact.IsPressed())
            {
                _holdTarget = null;
                _holdProgress = 0f;
                return "Hold [E] " + text;
            }

            if (_holdTarget != target)
            {
                _holdTarget = target;
                _holdProgress = 0f;
            }

            _holdProgress += Time.deltaTime;
            float pct = _holdProgress / target.HoldDuration;

            if (pct >= 1f)
            {
                _holdTarget = null;
                _holdProgress = 0f;
                Use(target);
                return "";
            }

            return $"{text}... {Mathf.RoundToInt(pct * 100f)}%";
        }

        // ---------- Aiming ----------

        private void Look(out Pickup pickup, out ItemReceiver receiver, out Interactable usable)
        {
            pickup = null;
            receiver = null;
            usable = null;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, reach, aimMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(transform)) continue; // ourselves and the held item

                Pickup p = hit.collider.GetComponentInParent<Pickup>();
                ItemReceiver r = hit.collider.GetComponentInParent<ItemReceiver>();
                Interactable u = hit.collider.GetComponentInParent<Interactable>();

                if (p == null && r == null && u == null)
                {
                    if (hit.collider.isTrigger) continue; // invisible zones don't block
                    return;                               // a wall / floor blocks the view
                }

                pickup = p;
                receiver = r;
                usable = u;
                return;
            }
        }

        // ---------- Actions ----------

        private void PickUp(Pickup item)
        {
            _held = item;
            item.Hold(_holdPoint);
            GameEvents.RaiseItemPickedUp(item.gameObject, item.ItemId);
        }

        private void Drop()
        {
            Pickup item = _held;
            _held = null;
            item.Drop(cam.transform.forward * dropForwardSpeed);
            GameEvents.RaiseItemDropped(item.gameObject, item.ItemId);
        }

        private void Place(ItemReceiver receiver)
        {
            Pickup item = _held;
            _held = null;
            receiver.Receive(item);
        }

        private void Use(Interactable target)
        {
            target.Interact(this);
        }

        private void SetPrompt(string text)
        {
            if (text == _prompt) return;
            _prompt = text;
            GameEvents.RaiseInteractionPromptChanged(text);
        }

        // ---------- Simple on-screen prompt (until the HUD is made) ----------

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!showSimplePrompt) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(16, Screen.height / 36),
                    normal = { textColor = Color.white }
                };
            }

            // Small dot in the middle so you know where you aim
            GUI.Label(new Rect(Screen.width / 2f - 10, Screen.height / 2f - 12, 20, 24), "•", _style);

            if (string.IsNullOrEmpty(_prompt)) return;

            var rect = new Rect(0, Screen.height * 0.78f, Screen.width, _style.fontSize * 2f);
            var shadow = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);

            Color old = _style.normal.textColor;
            _style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(shadow, _prompt, _style);
            _style.normal.textColor = old;
            GUI.Label(rect, _prompt, _style);
        }
    }
}
