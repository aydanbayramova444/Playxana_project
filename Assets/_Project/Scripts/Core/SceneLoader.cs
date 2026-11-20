using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>Thin wrapper over SceneManager. Scenes must be listed in Build Profiles.</summary>
    public static class SceneLoader
    {
        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public static void ReloadCurrent()
        {
            Load(SceneManager.GetActiveScene().name);
        }
    }
}
