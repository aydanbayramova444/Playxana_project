using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Describes one kind of item in the shop (banana, soda, ...).
    /// Create: Project window → right click → Create → Game → Item.
    ///
    /// IMPORTANT: <see cref="id"/> must be exactly the same as Pickup.ItemId on the prefab,
    /// otherwise the shopping list will never count it.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item", fileName = "Item_New")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Must match Pickup.ItemId, e.g. \"banana\"")]
        public string id = "item";

        [Tooltip("Name shown in the UI, e.g. \"Banana\"")]
        public string displayName = "Item";

        [Tooltip("Optional icon for the shopping list HUD")]
        public Sprite icon;

        [Tooltip("Prefab of the item (used by shelves / spawners)")]
        public GameObject prefab;
    }
}
