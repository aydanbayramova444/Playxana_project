using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Global event bus. Modules talk to each other through these events
    /// instead of holding direct references to each other's classes.
    ///
    /// Subscribe in OnEnable, unsubscribe in OnDisable:
    ///   void OnEnable()  => GameEvents.ScoreChanged += OnScoreChanged;
    ///   void OnDisable() => GameEvents.ScoreChanged -= OnScoreChanged;
    ///
    /// Raise:
    ///   GameEvents.RaiseScoreChanged(42);
    ///
    /// Need a new event? Add it here (event + Raise method + reset line).
    /// </summary>
    public static class GameEvents
    {
        /// <summary>(previous, current)</summary>
        public static event Action<GameState, GameState> GameStateChanged;
        public static event Action<int> ScoreChanged;
        public static event Action PlayerDied;

        public static void RaiseGameStateChanged(GameState previous, GameState current) =>
            GameStateChanged?.Invoke(previous, current);

        public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);

        public static void RaisePlayerDied() => PlayerDied?.Invoke();

        // ---------- Interaction (Game.Gameplay) ----------
        // Items are passed as GameObject + item id (e.g. "soda", "banana"),
        // so listeners don't need to reference gameplay classes.

        /// <summary>Text to show on screen, e.g. "[E] Pick up Banana". Empty = hide.</summary>
        public static event Action<string> InteractionPromptChanged;
        /// <summary>(item, itemId)</summary>
        public static event Action<GameObject, string> ItemPickedUp;
        /// <summary>(item, itemId)</summary>
        public static event Action<GameObject, string> ItemDropped;
        /// <summary>(item, itemId, receiver) — item put on a shelf / in a bin / fridge ...</summary>
        public static event Action<GameObject, string, GameObject> ItemPlaced;
        /// <summary>(usable object, its id) — register checked, door locked, spill cleaned ...</summary>
        public static event Action<GameObject, string> ObjectUsed;

        public static void RaiseInteractionPromptChanged(string text) => InteractionPromptChanged?.Invoke(text);
        public static void RaiseItemPickedUp(GameObject item, string id) => ItemPickedUp?.Invoke(item, id);
        public static void RaiseItemDropped(GameObject item, string id) => ItemDropped?.Invoke(item, id);
        public static void RaiseItemPlaced(GameObject item, string id, GameObject receiver) =>
            ItemPlaced?.Invoke(item, id, receiver);
        public static void RaiseObjectUsed(GameObject obj, string id) => ObjectUsed?.Invoke(obj, id);

        // ---------- Shopping list (Game.Gameplay) ----------
        // No data is passed: listeners read ShoppingListTracker.Entries themselves.

        /// <summary>Progress on the shopping list changed (an item was counted).</summary>
        public static event Action ShoppingListChanged;
        /// <summary>Raised ONCE when every item on the list is collected.</summary>
        public static event Action ShoppingListCompleted;

        public static void RaiseShoppingListChanged() => ShoppingListChanged?.Invoke();
        public static void RaiseShoppingListCompleted() => ShoppingListCompleted?.Invoke();

        // ---------- Shelves (Game.Gameplay) ----------

        /// <summary>(shelf, itemId, stockLeft) — someone took an item from a shelf.</summary>
        public static event Action<GameObject, string, int> ShelfStockChanged;
        /// <summary>(shelf, itemId) — the shelf just ran out.</summary>
        public static event Action<GameObject, string> ShelfEmptied;

        public static void RaiseShelfStockChanged(GameObject shelf, string itemId, int stock) =>
            ShelfStockChanged?.Invoke(shelf, itemId, stock);
        public static void RaiseShelfEmptied(GameObject shelf, string itemId) =>
            ShelfEmptied?.Invoke(shelf, itemId);

        // Clears stale subscribers when "Enter Play Mode Options" skips domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameStateChanged = null;
            ScoreChanged = null;
            PlayerDied = null;
            InteractionPromptChanged = null;
            ItemPickedUp = null;
            ItemDropped = null;
            ItemPlaced = null;
            ObjectUsed = null;
            ShoppingListChanged = null;
            ShoppingListCompleted = null;
            ShelfStockChanged = null;
            ShelfEmptied = null;
        }
    }
}
