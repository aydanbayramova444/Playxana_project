using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Testing helper: writes every interaction event to the Console.
    /// Also a small example of how a task system can listen to the events.
    /// </summary>
    public class InteractionTestLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            GameEvents.ItemPickedUp += OnPickedUp;
            GameEvents.ItemDropped += OnDropped;
            GameEvents.ItemPlaced += OnPlaced;
            GameEvents.ObjectUsed += OnUsed;
        }

        private void OnDisable()
        {
            GameEvents.ItemPickedUp -= OnPickedUp;
            GameEvents.ItemDropped -= OnDropped;
            GameEvents.ItemPlaced -= OnPlaced;
            GameEvents.ObjectUsed -= OnUsed;
        }

        private void OnPickedUp(GameObject item, string id) => Debug.Log($"[Interaction] Picked up: {id}");
        private void OnDropped(GameObject item, string id) => Debug.Log($"[Interaction] Dropped: {id}");

        private void OnPlaced(GameObject item, string id, GameObject receiver)
        {
            var r = receiver.GetComponent<ItemReceiver>();
            string count = r != null && !r.IsFull && r.Capacity != int.MaxValue
                ? $" ({r.Count}/{r.Capacity})"
                : r != null && r.IsFull ? " (FULL)" : "";
            Debug.Log($"[Interaction] Placed {id} → {receiver.name}{count}");
        }

        private void OnUsed(GameObject obj, string id) => Debug.Log($"[Interaction] Used: {id}");
    }
}
