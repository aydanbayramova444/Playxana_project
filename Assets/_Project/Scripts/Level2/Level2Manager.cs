using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Level2
{
    /// <summary>
    /// LEVEL 2 — run to the bomb shelter.
    ///   - 60 seconds on the clock.
    ///   - Chaotic cars (<see cref="ChaosCar"/>): every hit knocks you back; 4 hits = run over = game over.
    ///   - Safe zones (<see cref="SafeZone"/>) on the sidewalk: no hits inside, and health refills.
    ///   - Reach the shelter entrance (<see cref="ShelterGoal"/>) = you survive.
    /// Draws its own simple HUD (timer, hits, messages) and end screens: R = try again, M = main menu.
    /// </summary>
    public class Level2Manager : MonoBehaviour
    {
        public static Level2Manager Instance { get; private set; }

        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private int maxHits = 4;
        [Tooltip("Seconds you can't be hit again after a hit.")]
        [SerializeField] private float hitCooldown = 2f;
        [Tooltip("Inside a safe zone: one hit is healed every this many seconds.")]
        [SerializeField] private float healEvery = 2f;
        [SerializeField] private string menuScene = "MainMenu";

        public float TimeLeft { get; private set; }
        public int Hits { get; private set; }
        public bool InSafeZone => _safeZones > 0;

        private int _safeZones;   // how many zones the player is inside (they could overlap)
        private float _healTimer;

        private enum End { None, RunOver, TooLate, Survived }
        private End _end = End.None;
        private float _lastHitTime = -99f, _flash, _endTime;
        private CharacterController _player;
        private Vector3 _push;
        private GUIStyle _big, _mid, _small;

        private void Awake()
        {
            Instance = this;
            TimeLeft = timeLimit;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) _player = pc.GetComponent<CharacterController>();
            GameManager.Instance?.StartGame();
        }

        private void Update()
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 2f);

            if (_end != End.None)
            {
                HandleEndInput();
                return;
            }

            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return; // paused

            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                Finish(End.TooLate);
                return;
            }

            // Safe zone: slowly heal
            if (InSafeZone && Hits > 0)
            {
                _healTimer += Time.deltaTime;
                if (_healTimer >= healEvery)
                {
                    _healTimer = 0f;
                    Hits--;
                }
            }
            else _healTimer = 0f;

            // Knock-back from a car: slide the player, slowing down
            if (_player != null && _push.sqrMagnitude > 0.01f)
            {
                _player.Move((_push + Vector3.up * 0.5f) * Time.deltaTime);
                _push = Vector3.MoveTowards(_push, Vector3.zero, 25f * Time.deltaTime);
            }
        }

        /// <summary>A car hit the player. Called by <see cref="ChaosCar"/>.</summary>
        public void RegisterHit(Vector3 push)
        {
            if (_end != End.None || InSafeZone || Time.time - _lastHitTime < hitCooldown) return;

            _lastHitTime = Time.time;
            Hits++;
            _push = push;
            _flash = 1f;

            if (Hits >= maxHits) Finish(End.RunOver);
        }

        public void EnterSafeZone() => _safeZones++;
        public void ExitSafeZone() => _safeZones = Mathf.Max(0, _safeZones - 1);

        /// <summary>The player made it to the shelter. Called by <see cref="ShelterGoal"/>.</summary>
        public void ReachShelter()
        {
            if (_end == End.None) Finish(End.Survived);
        }

        private void Finish(End end)
        {
            _end = end;
            _endTime = Time.unscaledTime;
            if (end == End.Survived) GameManager.Instance?.Win();
            else GameManager.Instance?.GameOver();
            if (end == End.TooLate) _flash = 1f;
        }

        private void HandleEndInput()
        {
            if (Time.unscaledTime - _endTime < 1f) return; // don't skip by accident
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                GameManager.Instance?.StartGame();
                SceneLoader.ReloadCurrent();
            }
            else if (kb.mKey.wasPressedThisFrame)
            {
                SceneLoader.Load(menuScene);
            }
        }

        // ---------- HUD ----------

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            // Red flash on hit / white flash when time runs out (the bomb)
            if (_flash > 0f)
            {
                Color c = _end == End.TooLate ? new Color(1f, 1f, 1f, _flash) : new Color(0.8f, 0f, 0f, _flash * 0.45f);
                DrawRect(new Rect(0, 0, w, h), c);
            }

            if (_end == End.None)
            {
                // Timer (top centre), red in the last 10 seconds
                int secs = Mathf.CeilToInt(TimeLeft);
                _big.normal.textColor = secs <= 10 ? new Color(1f, 0.25f, 0.2f) : Color.white;
                Shadowed(new Rect(0, h * 0.02f, w, h * 0.1f), $"0:{secs:00}", _big);

                // Hits (top left)
                string marks = new string('■', Mathf.Max(0, maxHits - Hits)) + new string('□', Mathf.Min(Hits, maxHits));
                _small.alignment = TextAnchor.UpperLeft;
                Shadowed(new Rect(24, 20, w, 40), $"HEALTH  {marks}", _small);

                // Safe zone message
                if (InSafeZone)
                {
                    _mid.normal.textColor = new Color(0.45f, 1f, 0.5f);
                    Shadowed(new Rect(0, h * 0.7f, w, h * 0.08f),
                        Hits > 0 ? "SAFE ZONE — healing..." : "SAFE ZONE — full health, go!", _mid);
                    _mid.normal.textColor = Color.white;
                }

                // Opening message
                if (timeLimit - TimeLeft < 3f)
                    Shadowed(new Rect(0, h * 0.4f, w, h * 0.1f), "RUN TO THE SHELTER!", _mid);
                return;
            }

            // End screens
            DrawRect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, Mathf.Clamp01((Time.unscaledTime - _endTime) * 0.8f) * 0.7f));
            string title = _end switch
            {
                End.RunOver => "YOU GOT RUN OVER",
                End.TooLate => "TOO LATE...",
                _ => "YOU MADE IT TO THE SHELTER!",
            };
            string sub = _end switch
            {
                End.RunOver => "Watch the cars next time.",
                End.TooLate => "The sirens stopped. Then the flash.",
                _ => $"Time left: {Mathf.CeilToInt(TimeLeft)} s   ·   Hits: {Hits}/{maxHits}",
            };
            _big.normal.textColor = _end == End.Survived ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.3f, 0.25f);
            Shadowed(new Rect(0, h * 0.32f, w, h * 0.12f), title, _big);
            Shadowed(new Rect(0, h * 0.45f, w, h * 0.06f), sub, _mid);
            if (Time.unscaledTime - _endTime > 1f)
            {
                _small.alignment = TextAnchor.MiddleCenter;
                Shadowed(new Rect(0, h * 0.58f, w, h * 0.05f), "R — try again      M — main menu", _small);
            }
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            int fs = Mathf.Max(18, Screen.height / 12);
            _big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = fs, fontStyle = FontStyle.Bold };
            _mid = new GUIStyle(_big) { fontSize = fs / 2 };
            _mid.normal.textColor = Color.white;
            _small = new GUIStyle(_big) { fontSize = Mathf.Max(14, fs / 3), fontStyle = FontStyle.Normal };
            _small.normal.textColor = Color.white;
        }

        private static void Shadowed(Rect r, string text, GUIStyle style)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        private static void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
