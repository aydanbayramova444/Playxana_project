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
    ///   - Every hit knocks supplies out of your bag (<see cref="RunInventory"/>, filled in the store).
    ///     They land on the road (<see cref="DroppedSupply"/>): run over them to pick them back up.
    /// Draws its own simple HUD (timer, hits, messages) and end screens: R = try again, M = main menu,
    /// and after surviving: Enter / Space = go inside (Level 3, <see cref="nextScene"/>).
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
        [Tooltip("Level 3, loaded with Enter / Space after you reach the shelter.")]
        [SerializeField] private string nextScene = "Shelter";
        [Tooltip("Nuclear siren on the shelter roof (0 = silent).")]
        [Range(0f, 1f)] [SerializeField] private float sirenVolume = 0.6f;
        [Tooltip("Engines, tyres, horns and crashes (0 = silent).")]
        [Range(0f, 1f)] [SerializeField] private float carVolume = 0.8f;

        [Header("Supplies (the bag from the store)")]
        [Tooltip("How many things one car hit knocks out of the bag (min, max).")]
        [SerializeField] private Vector2Int dropsPerHit = new Vector2Int(1, 2);
        [Tooltip("Editor only: when you press Play right in Level2 (empty bag), start with these.")]
        [SerializeField] private Game.Gameplay.ItemData[] editorTestSupplies;

        public float TimeLeft { get; private set; }
        public int Hits { get; private set; }
        public float CarVolume => carVolume;
        public bool InSafeZone => _safeZones > 0;
        public bool IsRunning => _end == End.None;

        private int _safeZones;   // how many zones the player is inside (they could overlap)
        private float _healTimer;

        private enum End { None, RunOver, TooLate, Survived }
        private End _end = End.None;
        private float _lastHitTime = -99f, _flash, _endTime;
        private CharacterController _player;
        private Vector3 _push;
        private GUIStyle _big, _mid, _small;
        private Game.UI.RoundHud _hud;
        private bool _everHit;
        private float _startZ, _goalZ, _furthest;
        private int _suppliesAtStart;
        private readonly System.Collections.Generic.List<RunInventory.Stack> _bagAtStart = new System.Collections.Generic.List<RunInventory.Stack>();
        private Game.Gameplay.ItemData _lastChanged;   // row that blinks in the bag list
        private float _lastChangedTime = -99f;
        private GUIStyle _tiny;
        private string _bagMessage = "";
        private Color _bagMessageColor;
        private float _bagMessageUntil;

        private const int RoundNumber = 2;
        private const int DistancePoints = 1000, ShelterBonus = 1000, PointsPerSecond = 25, PointsPerHealth = 150, NoHitBonus = 500;
        private const int PointsPerSupply = 50, AllSuppliesBonus = 300;

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
            NuclearSiren.Create(sirenVolume);

            _startZ = _furthest = pc != null ? pc.transform.position.z : 0f;
            var goal = FindAnyObjectByType<ShelterGoal>();
            _goalZ = goal != null ? goal.transform.position.z : _startZ + 1f;
            FillTestBag();
            _hud = Game.UI.RoundHud.Create(transform, RoundNumber, timeLimit, "RUN TO THE SHELTER",
                $"THE SIREN MEANS {Mathf.CeilToInt(timeLimit)} SECONDS UNTIL THE BOMB.\n" +
                "REACH THE SHELTER AT THE END OF THE STREET.\n" +
                $"DODGE THE CARS: {maxHits} HITS AND YOU ARE DEAD.\n" +
                "GREEN BUS STOPS ARE SAFE AND HEAL YOU.\n" +
                "A HIT KNOCKS SUPPLIES OUT OF YOUR BAG: RUN OVER THEM TO GET THEM BACK.",
                "WASD MOVE   MOUSE LOOK   SHIFT RUN   A HORN MEANS A CAR IS AFTER YOU");
            _suppliesAtStart = RunInventory.Count; // after the HUD: a retry has just given the bag back
            _bagAtStart.Clear();
            _bagAtStart.AddRange(RunInventory.Items);
        }

        /// <summary>Editor: Play pressed right in Level2 with an empty bag → fill it so drops can be tested.</summary>
        private void FillTestBag()
        {
#if UNITY_EDITOR
            if (RunInventory.Count > 0 || RunInventory.HasCheckpoint(RoundNumber)) return;
            if (editorTestSupplies != null && editorTestSupplies.Length > 0)
            {
                foreach (var item in editorTestSupplies) RunInventory.Add(item);
                return;
            }
            // Nothing set: take one of every item in the project
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData"))
            {
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Gameplay.ItemData>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.prefab != null) RunInventory.Add(item);
            }
            Debug.Log($"[Level2] Editor test: started with {RunInventory.Count} supplies in the bag.", this);
#endif
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
            if (_hud != null) _hud.SetTime(TimeLeft);
            ScoreDistance();

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

        /// <summary>A car hit the player. Called by <see cref="ChaosCar"/>. False = didn't count (safe zone, cooldown...).</summary>
        public bool RegisterHit(Vector3 push)
        {
            if (_end != End.None || InSafeZone || Time.time - _lastHitTime < hitCooldown) return false;

            _lastHitTime = Time.time;
            _push = push;
            _flash = 1f;
            if (DevSettings.Invincible) return true; // test mode: knocked back, but no damage

            Hits++;
            _everHit = true;

            if (Hits >= maxHits) Finish(End.RunOver);
            else DropSupplies(push);
            return true;
        }

        /// <summary>A car hit knocks a few things out of the bag; they fly off and land on the road.</summary>
        private void DropSupplies(Vector3 push)
        {
            if (_player == null) return;
            int count = Random.Range(dropsPerHit.x, Mathf.Max(dropsPerHit.x, dropsPerHit.y) + 1);
            var names = new System.Collections.Generic.List<string>();
            Vector3 from = _player.transform.position + Vector3.up * 1.2f;
            Vector3 dir = push.sqrMagnitude > 0.01f ? push.normalized : _player.transform.forward;

            for (int i = 0; i < count; i++)
            {
                var item = RunInventory.TakeRandom();
                if (item == null) break;
                Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-2.5f, 2.5f);
                Vector3 velocity = dir * Random.Range(3f, 6f) + side + Vector3.up * Random.Range(3f, 5f);
                DroppedSupply.Spawn(item, from + Random.insideUnitSphere * 0.3f, velocity, _player.transform);
                names.Add(item.displayName.ToUpperInvariant());
                MarkChanged(item);
            }
            if (names.Count > 0)
                SayBag($"DROPPED: {string.Join(", ", names)}!", new Color(1f, 0.35f, 0.25f));
        }

        /// <summary>The player ran over a dropped supply. Called by <see cref="DroppedSupply"/>.</summary>
        public void OnSupplyRecovered(Game.Gameplay.ItemData item)
        {
            SayBag($"GOT {item.displayName.ToUpperInvariant()} BACK", new Color(0.45f, 1f, 0.5f));
            MarkChanged(item);
        }

        /// <summary>A car drove over a dropped supply. Called by <see cref="DroppedSupply"/>.</summary>
        public void OnSupplyCrushed(Game.Gameplay.ItemData item)
        {
            if (_end != End.None) return;
            SayBag($"A CAR CRUSHED THE {item.displayName.ToUpperInvariant()}!", new Color(1f, 0.35f, 0.25f));
            MarkChanged(item);
        }

        private void MarkChanged(Game.Gameplay.ItemData item)
        {
            _lastChanged = item;
            _lastChangedTime = Time.time;
        }

        private static int InBag(Game.Gameplay.ItemData item)
        {
            foreach (var st in RunInventory.Items) if (st.Item == item) return st.Count;
            return 0;
        }

        /// <summary>The bag, top left under HEALTH: one row per product, "have / had at the start".</summary>
        private void DrawBag(float x, float y)
        {
            if (_suppliesAtStart <= 0) return;
            float line = _small.fontSize * 1.4f;
            Shadowed(new Rect(x, y, Screen.width, line), $"BAG  {RunInventory.Count}/{_suppliesAtStart}", _small);
            y += line * 1.05f;

            float row = _tiny.fontSize * 1.35f;
            foreach (var st in _bagAtStart)
            {
                int have = InBag(st.Item);
                bool blink = st.Item == _lastChanged && Time.time - _lastChangedTime < 1.2f
                             && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
                _tiny.normal.textColor = blink ? new Color(1f, 0.9f, 0.3f)
                                       : have == 0 ? new Color(1f, 0.3f, 0.25f, 0.8f)
                                       : have < st.Count ? new Color(1f, 0.65f, 0.3f)
                                       : new Color(0.85f, 0.85f, 0.85f);
                string name = st.Item.displayName.ToUpperInvariant();
                Shadowed(new Rect(x + 12, y, Screen.width, row), have == 0 ? $"- {name}  LOST" : $"- {name}  {have}/{st.Count}", _tiny);
                y += row;
            }
        }

        private void SayBag(string text, Color color)
        {
            _bagMessage = text;
            _bagMessageColor = color;
            _bagMessageUntil = Time.time + 2f;
        }

        public void EnterSafeZone() => _safeZones++;
        public void ExitSafeZone() => _safeZones = Mathf.Max(0, _safeZones - 1);

        /// <summary>The player made it to the shelter. Called by <see cref="ShelterGoal"/>.</summary>
        public void ReachShelter()
        {
            if (_end == End.None) Finish(End.Survived);
        }

        /// <summary>After surviving: go inside the shelter (Level 3). Enter / Space on the end screen.</summary>
        public void EnterShelter()
        {
            if (_end != End.Survived) return;
            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError($"[Level2] Scene '{nextScene}' is not in Build Profiles.", this);
                return;
            }
            GameManager.Instance?.StartGame();
            SceneLoader.Load(nextScene);
        }

        private void Finish(End end)
        {
            _end = end;
            _endTime = Time.unscaledTime;
            if (end == End.Survived) GameManager.Instance?.Win();
            else GameManager.Instance?.GameOver();
            if (end == End.TooLate) _flash = 1f;
            if (_hud == null) return;

            bool survived = end == End.Survived;
            if (survived)
            {
                GameScore.Set(RoundNumber, "DISTANCE", DistancePoints);
                GameScore.Set(RoundNumber, "SHELTER", ShelterBonus);
                GameScore.Set(RoundNumber, "TIME", Mathf.FloorToInt(TimeLeft) * PointsPerSecond);
                GameScore.Set(RoundNumber, "HEALTH", Mathf.Max(0, maxHits - Hits) * PointsPerHealth);
                if (!_everHit) GameScore.Set(RoundNumber, "NOT A SCRATCH", NoHitBonus);
                int kept = RunInventory.Count;
                if (kept > 0) GameScore.Set(RoundNumber, "SUPPLIES KEPT", kept * PointsPerSupply);
                if (_suppliesAtStart > 0 && kept >= _suppliesAtStart) GameScore.Set(RoundNumber, "NOTHING LOST", AllSuppliesBonus);
            }
            _hud.ShowResult(survived, dim: true);
        }

        /// <summary>Points for the furthest point reached (running back and forth gives nothing).</summary>
        private void ScoreDistance()
        {
            if (_player == null || _hud == null) return;
            _furthest = Mathf.Max(_furthest, _player.transform.position.z);
            float done = Mathf.InverseLerp(_startZ, _goalZ, _furthest);
            GameScore.Set(RoundNumber, "DISTANCE", Mathf.FloorToInt(done * 100f) * (DistancePoints / 100));
        }

        private void HandleEndInput()
        {
            if (Time.unscaledTime - _endTime < 1f) return; // don't skip by accident
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                EnterShelter();
            }
            else if (kb.rKey.wasPressedThisFrame)
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
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Briefing) return;
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
                // Hits (top left)
                string marks = new string('■', Mathf.Max(0, maxHits - Hits)) + new string('□', Mathf.Min(Hits, maxHits));
                _small.alignment = TextAnchor.UpperLeft;
                Shadowed(new Rect(24, 20, w, 40), $"HEALTH  {marks}", _small);
                DrawBag(24, 20 + _small.fontSize * 1.6f);

                // Bag message: what fell out / what you picked back up
                if (Time.time < _bagMessageUntil)
                {
                    _mid.normal.textColor = _bagMessageColor;
                    Shadowed(new Rect(0, h * 0.22f, w, h * 0.08f), _bagMessage, _mid);
                    _mid.normal.textColor = Color.white;
                }

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
                _ => $"Time left: {Mathf.CeilToInt(TimeLeft)} s   ·   Hits: {Hits}/{maxHits}" +
                     (_suppliesAtStart > 0 ? $"   ·   Supplies: {RunInventory.Count}/{_suppliesAtStart}" : ""),
            };
            _big.normal.textColor = _end == End.Survived ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.3f, 0.25f);
            Shadowed(new Rect(0, h * 0.32f, w, h * 0.12f), title, _big);
            Shadowed(new Rect(0, h * 0.45f, w, h * 0.06f), sub, _mid);
            if (Time.unscaledTime - _endTime > 1f)
            {
                _small.alignment = TextAnchor.MiddleCenter;
                if (_end == End.Survived)
                {
                    _mid.normal.textColor = new Color(0.5f, 1f, 0.5f);
                    Shadowed(new Rect(0, h * 0.56f, w, h * 0.06f), "ENTER — enter the shelter", _mid);
                    _mid.normal.textColor = Color.white;
                }
                Shadowed(new Rect(0, h * (_end == End.Survived ? 0.64f : 0.58f), w, h * 0.05f),
                    "R — try again      M — main menu", _small);
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
            _tiny = new GUIStyle(_small) { fontSize = Mathf.Max(12, fs / 4), alignment = TextAnchor.UpperLeft };
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
