using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Interaction
{
    /// <summary>
    /// Anything the player can look at and use with E.
    ///
    /// Use it as-is for simple objects (cash register, door, light switch):
    ///   add the component, set the prompt, hook up "On Interact" in the inspector.
    /// Options:
    ///   - Hold Duration &gt; 0 → player must HOLD E (e.g. mopping a spill for 2 s).
    ///   - Required Held Item → only works while holding that item (e.g. "Mop", "Key").
    ///   - Single Use → can only be used once (e.g. "Check the register").
    ///
    /// Subclass it for special behaviour (see <see cref="Pickup"/> and <see cref="ItemSlot"/>).
    /// Needs a Collider on this object or one of its children so the player's ray can hit it.
    /// </summary>
    [AddComponentMenu("Interaction/Interactable (new)")]
    public class Interactable : MonoBehaviour
    {
        [Tooltip("Name shown in prompts. Empty = GameObject name.")]
        [SerializeField] protected string displayName = "";

        [Tooltip("Action text, e.g. \"Check register\". Shown as \"[E] Check register\".")]
        [SerializeField] protected string prompt = "Use";

        [Tooltip("0 = press E once. Above 0 = hold E for this many seconds.")]
        [Min(0f)]
        [SerializeField] protected float holdDuration = 0f;

        [Tooltip("ItemId of the Pickup the player must be holding (e.g. \"Mop\"). Empty = no item needed.")]
        [SerializeField] protected string requiredHeldItem = "";

        [Tooltip("Consume the held item (destroy it) when used, e.g. a key that stays in the door.")]
        [SerializeField] protected bool consumeHeldItem = false;

        [Tooltip("Can only be used once.")]
        [SerializeField] protected bool singleUse = false;

        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        /// <summary>Raised after a successful interaction. For code (task system, sounds...).</summary>
        public event Action<Interactable, PlayerInteractor> Interacted;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
        public virtual float HoldDuration => holdDuration;
        public bool HasBeenUsed { get; private set; }

        /// <summary>Can the player use this right now? If false, <see cref="GetBlockedReason"/> explains why.</summary>
        public virtual bool CanInteract(PlayerInteractor player)
        {
            if (singleUse && HasBeenUsed) return false;
            if (!string.IsNullOrEmpty(requiredHeldItem) && !player.IsHolding(requiredHeldItem)) return false;
            return true;
        }

        /// <summary>Action text when usable, e.g. "Check register".</summary>
        public virtual string GetPrompt(PlayerInteractor player) => prompt;

        /// <summary>Hint when NOT usable, e.g. "Need a Mop". Empty = show nothing.</summary>
        public virtual string GetBlockedReason(PlayerInteractor player)
        {
            if (singleUse && HasBeenUsed) return "";
            if (!string.IsNullOrEmpty(requiredHeldItem) && !player.IsHolding(requiredHeldItem))
                return $"{DisplayName}: need {requiredHeldItem}";
            return "";
        }

        /// <summary>Called by <see cref="PlayerInteractor"/>. Don't override — override <see cref="OnInteract"/>.</summary>
        public void Interact(PlayerInteractor player)
        {
            if (consumeHeldItem && !string.IsNullOrEmpty(requiredHeldItem))
            {
                var item = player.ReleaseHeld();
                if (item != null) Destroy(item.gameObject);
            }

            OnInteract(player);
            HasBeenUsed = true;
            onInteract.Invoke();
            Interacted?.Invoke(this, player);
        }

        protected virtual void OnInteract(PlayerInteractor player) { }
    }
}
