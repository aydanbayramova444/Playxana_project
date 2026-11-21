using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Interaction
{
    /// <summary>
    /// Lives on the Player. Every frame it casts a ray from the camera, finds the
    /// <see cref="Interactable"/> being looked at and handles E:
    ///   - press E  → interact (pick up, place, use)
    ///   - hold E   → for interactables with Hold Duration (shows progress)
    ///   - press E while holding something and not looking at a valid target → drop it
    /// Input: project-wide action "Player/Interact".
    /// The text to show is in <see cref="Prompt"/> (drawn by <see cref="InteractionPromptUI"/>).
    /// </summary>
    [AddComponentMenu("Interaction/Player Interactor (new)")]
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Camera to aim from. Empty = the child camera with FirstPersonLook.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Where held items sit. Empty = created in front of the camera.")]
        [SerializeField] private Transform holdPoint;

        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactMask = ~0;

        [Tooltip("Forward speed given to an item when dropped.")]
        [SerializeField] private float dropSpeed = 1.5f;

        // ----- State other scripts can read -----

        public Interactable CurrentTarget { get; private set; }
        public Pickup HeldItem { get; private set; }
        public bool IsHoldingAnything => HeldItem != null;

        /// <summary>0..1 while holding E on a hold-interactable, otherwise 0.</summary>
        public float HoldProgress { get; private set; }

        /// <summary>Text to show on screen, e.g. "[E] Pick up Soda". Empty = nothing.</summary>
        public string Prompt { get; private set; } = "";

        public Camera Camera => playerCamera;

        public event Action<Pickup> ItemPickedUp;
        public event Action<Pickup> ItemDropped;
        public event Action<Interactable> Interacted;

        // ----- Internals -----

        private InputAction _interact;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<Interactable> _found = new List<Interactable>();
        private Interactable _holdTarget;
        private float _holdTimer;
        private bool _waitForRelease;

        public bool IsHolding(string itemId) =>
            HeldItem != null && string.Equals(HeldItem.ItemId, itemId, StringComparison.OrdinalIgnoreCase);

        private void Awake()
        {
            _interact = InputSystem.actions.FindAction("Player/Interact", true);

            if (playerCamera == null)
            {
                var look = GetComponentInChildren<FirstPersonLook>();
                playerCamera = look != null ? look.GetComponent<Camera>() : Camera.main;
            }

            if (holdPoint == null && playerCamera != null)
            {
                holdPoint = new GameObject("HoldPoint").transform;
                holdPoint.SetParent(playerCamera.transform, false);
                holdPoint.localPosition = new Vector3(0.35f, -0.3f, 0.7f);
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;
            if (playerCamera == null) return;

            CurrentTarget = FindTarget();
            HandleInput();
            Prompt = BuildPrompt();
        }

        // ---------- Public API (used by Pickup / ItemSlot / Interactable) ----------

        public void PickUp(Pickup item)
        {
            if (item == null || IsHoldingAnything) return;

            item.OnPickedUp();
            HeldItem = item;
            PlacementHelper.SnapTo(item.gameObject, holdPoint, sitOnTop: false);
            GameEvents.RaiseItemPickedUp(item.gameObject, item.ItemId); // shelves lower their stock
            ItemPickedUp?.Invoke(item);
        }

        public void Drop()
        {
            if (!IsHoldingAnything) return;

            var item = HeldItem;
            HeldItem = null;

            // Don't push the item through a wall we're facing
            Transform cam = playerCamera.transform;
            float reach = Vector3.Distance(cam.position, holdPoint.position);
            if (Physics.Raycast(cam.position, cam.forward, out var hit, reach, interactMask,
                    QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                item.transform.position = hit.point - cam.forward * 0.2f;
            }

            item.OnDropped(cam.forward * dropSpeed);
            GameEvents.RaiseItemDropped(item.gameObject, item.ItemId);
            ItemDropped?.Invoke(item);
        }

        /// <summary>Takes the held item out of the player's hands without dropping it (slots use this).</summary>
        public Pickup ReleaseHeld()
        {
            var item = HeldItem;
            HeldItem = null;
            return item;
        }

        // ---------- Internals ----------

        private Interactable FindTarget()
        {
            Transform cam = playerCamera.transform;
            int count = Physics.RaycastNonAlloc(cam.position, cam.forward, _hits, interactDistance,
                interactMask, QueryTriggerInteraction.Collide);

            Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider col = _hits[i].collider;

                // Skip the player itself (and anything held, which lives under the camera)
                if (col.transform.IsChildOf(transform)) continue;

                // First enabled interactable on this object or its parents
                // (e.g. a locked bottle on a shelf → the shelf)
                col.GetComponentsInParent(false, _found);
                foreach (var it in _found)
                    if (it.isActiveAndEnabled) return it;

                // A solid object (wall, shelf without script) blocks the view
                if (!col.isTrigger) return null;
            }

            return null;
        }

        private void HandleInput()
        {
            if (_waitForRelease)
            {
                if (_interact.IsPressed()) return;
                _waitForRelease = false;
            }

            Interactable target = CurrentTarget;
            bool usable = target != null && target.CanInteract(this);

            // Hold-E interactions (mop a spill, etc.)
            if (usable && target.HoldDuration > 0f)
            {
                if (_interact.IsPressed())
                {
                    if (_holdTarget != target)
                    {
                        _holdTarget = target;
                        _holdTimer = 0f;
                    }

                    _holdTimer += Time.deltaTime;
                    HoldProgress = Mathf.Clamp01(_holdTimer / target.HoldDuration);

                    if (HoldProgress >= 1f)
                    {
                        ResetHold();
                        _waitForRelease = true;
                        DoInteract(target);
                    }
                }
                else
                {
                    ResetHold();
                }

                return;
            }

            ResetHold();

            if (!_interact.WasPressedThisFrame()) return;

            if (usable) DoInteract(target);
            else if (IsHoldingAnything) Drop();
        }

        private void DoInteract(Interactable target)
        {
            target.Interact(this);
            Interacted?.Invoke(target);
        }

        private void ResetHold()
        {
            _holdTarget = null;
            _holdTimer = 0f;
            HoldProgress = 0f;
        }

        private string BuildPrompt()
        {
            Interactable target = CurrentTarget;

            if (target != null && target.CanInteract(this))
            {
                string action = target.GetPrompt(this);
                if (target.HoldDuration <= 0f) return $"[E] {action}";
                if (HoldProgress > 0f) return $"{action}... {Mathf.RoundToInt(HoldProgress * 100f)}%";
                return $"[Hold E] {action}";
            }

            string reason = target != null ? target.GetBlockedReason(this) : "";
            string drop = IsHoldingAnything ? $"[E] Drop {HeldItem.DisplayName}" : "";

            if (reason.Length > 0 && drop.Length > 0) return reason + "\n" + drop;
            return reason.Length > 0 ? reason : drop;
        }

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
