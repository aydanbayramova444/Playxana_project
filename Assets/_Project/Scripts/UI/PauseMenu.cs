using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Pause screen for every round. Created automatically (like GameManager) — don't put it in scenes.
    ///   ESC or P  — pause / resume (GameManager.TogglePause; only while a round is being played)
    ///   R         — restart the round        M — main menu
    /// While paused: time stops, all sound is paused, the mouse is free to click the buttons.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private const string MenuScene = "MainMenu";

        private static PauseMenu _instance;
        private GUIStyle _title, _button, _hint;
        private bool _wasPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[PauseMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PauseMenu>();
        }

        private static bool Paused => GameManager.Instance != null && GameManager.Instance.State == GameState.Paused;

        private void Update()
        {
            bool paused = Paused;
            if (paused != _wasPaused)
            {
                AudioListener.pause = paused; // sirens, engines, hum... all stop with the game
                _wasPaused = paused;
            }
            if (!paused) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) Restart();
            else if (kb.mKey.wasPressedThisFrame) ToMenu();
        }

        private static void Resume() => GameManager.Instance?.TogglePause();

        private static void Restart()
        {
            AudioListener.pause = false;
            GameManager.Instance?.Restart(); // retrying gives back the round's start (score, bag)
        }

        private static void ToMenu()
        {
            AudioListener.pause = false;
            if (!Application.CanStreamedLevelBeLoaded(MenuScene))
            {
                Debug.LogError($"[PauseMenu] Scene '{MenuScene}' is not in Build Profiles.");
                return;
            }
            GameManager.Instance?.SetState(GameState.MainMenu);
            SceneLoader.Load(MenuScene);
        }

        private void OnGUI()
        {
            if (!Paused) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            GUI.depth = -1000; // on top of the round HUDs
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(0, h * 0.22f, w, h * 0.14f), "PAUSED", _title);

            float bw = Mathf.Max(260f, w * 0.24f), bh = Mathf.Max(44f, h * 0.075f), gap = bh * 0.3f;
            float x = (w - bw) / 2f, y = h * 0.42f;
            if (GUI.Button(new Rect(x, y, bw, bh), "RESUME", _button)) Resume();
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "RESTART ROUND", _button)) Restart();
            y += bh + gap;
            if (GUI.Button(new Rect(x, y, bw, bh), "MAIN MENU", _button)) ToMenu();

            GUI.Label(new Rect(0, h * 0.86f, w, h * 0.05f), "ESC / P  RESUME      R  RESTART      M  MENU", _hint);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            int fs = Mathf.Max(18, Screen.height / 10);
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = fs, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(14, fs / 3), fontStyle = FontStyle.Bold };
            _button.normal.background = Solid(new Color(0.12f, 0.12f, 0.12f, 0.95f));
            _button.hover.background = Solid(new Color(0.35f, 0.08f, 0.06f, 0.95f));
            _button.active.background = Solid(new Color(0.55f, 0.1f, 0.08f, 1f));
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = Color.white;

            _hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, fs / 4) };
            _hint.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
