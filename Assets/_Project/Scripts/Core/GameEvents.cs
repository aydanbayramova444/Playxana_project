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

        // Clears stale subscribers when "Enter Play Mode Options" skips domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameStateChanged = null;
            ScoreChanged = null;
            PlayerDied = null;
        }
    }
}
