namespace Game.Core
{
    /// <summary>
    /// Test switches for the Unity Editor, set in Window → Game → Scene Launcher.
    /// Stored per computer (EditorPrefs), never in the project, and ALWAYS off in a build.
    ///
    ///   if (DevSettings.Invincible) return false;
    /// </summary>
    public static class DevSettings
    {
        public const string SkipBriefingsKey = "Game.Dev.SkipBriefings";
        public const string InvincibleKey = "Game.Dev.Invincible";

        /// <summary>Rounds start at once, without the "press ENTER" briefing screen.</summary>
        public static bool SkipBriefings => Get(SkipBriefingsKey);

        /// <summary>Level 2: cars still push you, but hits don't count.</summary>
        public static bool Invincible => Get(InvincibleKey);

        private static bool Get(string key)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(key, false);
#else
            return false;
#endif
        }
    }
}
