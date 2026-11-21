using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Base for things you USE with E (not carry): register, door lock, spill, alarm...
    /// Make your own by inheriting from this, or use the ready-made <see cref="UsableObject"/>.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>0 = press E once. More than 0 = hold E this many seconds.</summary>
        public virtual float HoldDuration => 0f;

        /// <summary>Can the player use it right now? (e.g. holding the mop for a spill)</summary>
        public abstract bool CanInteract(PlayerInteractor player);

        /// <summary>Text without the key, e.g. "Check Register" or "Need Mop to clean Spill".</summary>
        public abstract string GetPrompt(PlayerInteractor player);

        public abstract void Interact(PlayerInteractor player);
    }
}
