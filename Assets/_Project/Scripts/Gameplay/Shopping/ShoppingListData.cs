using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A shopping list: which items and how many of each.
    /// Create: Project window → right click → Create → Game → Shopping List.
    /// This asset is read-only data — progress is tracked by ShoppingListTracker at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Shopping List", fileName = "ShoppingList_New")]
    public class ShoppingListData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemData item;
            [Min(1)] public int amount = 1;
        }

        [Tooltip("Always on the list (e.g. Water, First Aid Kit).")]
        public List<Entry> entries = new List<Entry>();

        [Header("Random items (added on top of the list above, new every game)")]
        [Tooltip("How many items to pick at random from the pool. 0 = no random items.")]
        [Min(0)] public int randomCount = 0;

        [Tooltip("Items the random picks come from. Items already in the list above are skipped.")]
        public List<ItemData> randomPool = new List<ItemData>();

        [Tooltip("Each random item needs between Min and Max of it.")]
        [Min(1)] public int randomMinAmount = 1;
        [Min(1)] public int randomMaxAmount = 2;
    }
}
