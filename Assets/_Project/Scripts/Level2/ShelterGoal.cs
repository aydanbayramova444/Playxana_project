using Game.Player;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>Trigger at the bomb shelter entrance: the player walks in = level won.</summary>
    [RequireComponent(typeof(Collider))]
    public class ShelterGoal : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                Level2Manager.Instance?.ReachShelter();
        }
    }
}
