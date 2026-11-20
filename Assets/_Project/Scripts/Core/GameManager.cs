using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// Owns the global game state (playing / paused / game over ...), time scale and cursor.
    ///
    /// Created automatically before the first scene loads, so it does NOT need to be placed
    /// in any scene — every scene (including personal Dev scenes) just works.
    ///
    /// Usage from anywhere:
    ///   GameManager.Instance.GameOver();
    ///   if (GameManager.Instance.State == GameState.Playing) { ... }
    /// Or react to changes via GameEvents.GameStateChanged.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Playing;

        private InputAction _pauseAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[GameManager]").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Project-wide actions: Assets/InputSystem_Actions.inputactions
            _pauseAction = InputSystem.actions?.FindAction("UI/Cancel");
        }

        private void Start()
        {
            ApplyState(State);
        }

        private void Update()
        {
            if (_pauseAction != null && _pauseAction.WasPressedThisFrame())
                TogglePause();
        }

        // ---------- Public API ----------

        public void StartGame() => SetState(GameState.Playing);

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void GameOver() => SetState(GameState.GameOver);

        public void Win() => SetState(GameState.Victory);

        public void Restart()
        {
            SetState(GameState.Playing);
            SceneLoader.ReloadCurrent();
        }

        public void SetState(GameState newState)
        {
            if (newState == State) return;

            var previous = State;
            State = newState;
            ApplyState(newState);
            GameEvents.RaiseGameStateChanged(previous, newState);
        }

        // ---------- Internals ----------

        private static void ApplyState(GameState state)
        {
            bool playing = state == GameState.Playing;

            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }
    }
}
