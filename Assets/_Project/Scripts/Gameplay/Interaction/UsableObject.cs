using Game.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Gameplay
{
    /// <summary>
    /// Ready-made "use with E" object, set up in the Inspector:
    ///   Register:  id "register",  action "Check"
    ///   Door lock: id "door_lock", action "Lock"
    ///   Spill:     id "spill",     action "Clean", hold 2 s, needs item "mop", destroy after use
    /// Raises GameEvents.ObjectUsed(object, id) and onUsed when used.
    /// </summary>
    public class UsableObject : Interactable
    {
        [SerializeField] private string objectId = "register";
        [SerializeField] private string displayName = "Register";
        [SerializeField] private string actionText = "Check";

        [Tooltip("0 = press E. More = hold E for this many seconds.")]
        [SerializeField, Min(0f)] private float holdSeconds;

        [Tooltip("Player must hold this item to use it (e.g. mop). Empty = no item needed.")]
        [SerializeField] private string requiredItemId = "";
        [SerializeField] private string requiredItemName = "";

        [SerializeField] private bool oneTimeOnly = true;
        [SerializeField] private bool destroyAfterUse;

        public UnityEvent onUsed;

        public string ObjectId => objectId;
        public bool IsDone { get; private set; }

        public override float HoldDuration => holdSeconds;

        public override bool CanInteract(PlayerInteractor player)
        {
            if (oneTimeOnly && IsDone) return false;
            if (!string.IsNullOrEmpty(requiredItemId))
                return player.HeldItemId == requiredItemId;
            return true;
        }

        public override string GetPrompt(PlayerInteractor player)
        {
            if (oneTimeOnly && IsDone)
                return displayName + " - done";

            if (!string.IsNullOrEmpty(requiredItemId) && player.HeldItemId != requiredItemId)
            {
                string need = string.IsNullOrEmpty(requiredItemName) ? requiredItemId : requiredItemName;
                return $"Need {need} to {actionText.ToLower()} {displayName}";
            }

            return $"{actionText} {displayName}";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return;

            IsDone = true;
            GameEvents.RaiseObjectUsed(gameObject, objectId);
            onUsed?.Invoke();

            if (destroyAfterUse) Destroy(gameObject);
        }
    }
}
