namespace Game.Core
{
    /// <summary>High-level state of the game. Owned by <see cref="GameManager"/>.</summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }
}
