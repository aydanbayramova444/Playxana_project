namespace Game.Core
{
    /// <summary>High-level state of the game. Owned by <see cref="GameManager"/>.</summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory,
        /// <summary>Round briefing on screen: the world stands still until the player presses Enter.</summary>
        Briefing
    }
}
