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

        public List<Entry> entries = new List<Entry>();
    }
}
