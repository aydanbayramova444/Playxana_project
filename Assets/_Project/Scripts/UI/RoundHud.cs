using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The old-console HUD shared by all 3 rounds (each round creates one in its Start):
    ///   - phosphor-green pixel timer  1'00"00  (red and blinking in the last 10 seconds),
    ///   - score + best run, top right,
    ///   - a short briefing before the round (the game stands still until ENTER / SPACE),
    ///   - the score breakdown when the round is over.
    /// </summary>
    public class RoundHud : MonoBehaviour
    {
        public static readonly Color Green = new Color(0.12f, 1f, 0.22f);
        private static readonly Color DarkGreen = new Color(0.1f, 0.62f, 0.2f);
        private static readonly Color Red = new Color(1f, 0.16f, 0.1f);
        private static readonly Color Bone = new Color(0.85f, 0.85f, 0.78f);
        private static readonly Color Amber = new Color(1f, 0.8f, 0.2f);

        private const float InputDelay = 0.6f; // so the key that skipped the cutscene doesn't skip this too
        private const float Urgent = 10f;

        private static RoundHud _current;

        private int _round;
        private GameObject _play, _briefing, _result;
        private PixelLabel _timer, _score, _best, _press;
        private float _time = -1f, _briefingShown;
        private bool _timerDone, _finished;

        public bool BriefingOpen => _briefing != null;
        public string TimerText => _timer.Text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;

        /// <summary>Starts the round's score and HUD, and shows its briefing (not again on a retry).</summary>
        /// <param name="goal">A few short lines: what to do in this round.</param>
        /// <param name="controls">The keys used in this round.</param>
        public static RoundHud Create(Transform owner, int round, float seconds, string title, string goal, string controls)
        {
            GameScore.StartRound(round);

            var go = new GameObject("RoundHUD", typeof(Canvas), typeof(CanvasScaler), typeof(RoundHud));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var hud = go.GetComponent<RoundHud>();
            hud._round = round;
            hud.BuildPlayHud();
            hud.SetTime(seconds);
            _current = hud;

            if (GameScore.TakeBriefing(round)) hud.BuildBriefing(title, goal, controls);
            return hud;
        }

        /// <summary>Closes the open briefing at once (used by the play tests).</summary>
        public static void SkipBriefing()
        {
            if (_current != null) _current.CloseBriefing();
        }

        /// <summary>1'00"00 = minutes ' seconds " hundredths.</summary>
        public static string FormatTime(float seconds)
        {
            int cs = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f + 0.001f));
            return $"{cs / 6000}'{cs / 100 % 60:00}\"{cs % 100:00}";
        }

        public void SetTime(float seconds, bool done = false)
        {
            _time = seconds;
            _timerDone = done;
            _timer.Set(FormatTime(seconds));
        }

        // ---------- Unity ----------

        private void Update()
        {
            if (_briefing != null)
            {
                float shown = Time.unscaledTime - _briefingShown;
                _press.gameObject.SetActive(shown > InputDelay && Mathf.Repeat(shown, 0.9f) < 0.6f);
                var kb = Keyboard.current;
                if (shown > InputDelay && kb != null &&
                    (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    CloseBriefing();
                return;
            }
            if (_finished) return;

            Color color = Green;
            if (_timerDone) color.a = Mathf.Repeat(Time.unscaledTime, 1f) < 0.7f ? 1f : 0.3f;   // stopped: slow blink
            else if (_time >= 0f && _time <= Urgent)
            {
                color = Red;
                color.a = Mathf.Repeat(_time, 0.5f) > 0.2f ? 1f : 0.45f;                         // last seconds: fast blink
            }
            _timer.SetColor(color);

            int total = GameScore.Total;
            _score.Set($"SCORE {total:000000}");
            _best.Set($"BEST {Mathf.Max(total, GameScore.BestRun):000000}");
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        // ---------- Play HUD ----------

        private void BuildPlayHud()
        {
            _play = Panel("Play", Color.clear);
            _timer = PixelLabel.Create(_play.transform, "Timer", PixelFont.Timer, 6, new Vector2(0.5f, 1f), new Vector2(0, -22), Green);
            var backing = new GameObject("ScoreBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_play.transform, false);
            var rt = (RectTransform)backing.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
            rt.anchoredPosition = new Vector2(-18, -18);
            rt.sizeDelta = new Vector2(324, 88);
            var image = backing.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);
            image.raycastTarget = false;
            _score = PixelLabel.Create(_play.transform, "Score", PixelFont.Small, 4, Vector2.one, new Vector2(-30, -28), Green, 1f);
            _best = PixelLabel.Create(_play.transform, "Best", PixelFont.Small, 3, Vector2.one, new Vector2(-30, -70), DarkGreen, 1f);
        }

        // ---------- Briefing ----------

        private void BuildBriefing(string title, string goal, string controls)
        {
            _play.SetActive(false);
            _briefing = Panel("Briefing", new Color(0.005f, 0.02f, 0.01f, 1f));
            Transform p = _briefing.transform;

            if (_round > 1 && GameScore.IsCleared(_round - 1))
            {
                Centred(p, 3, 462, Amber, $"ROUND {_round - 1} CLEAR   +{GameScore.RoundTotal(_round - 1)}");
                Centred(p, 3, 408, Bone, Breakdown(_round - 1));
            }
            Centred(p, 5, 300, DarkGreen, $"ROUND {_round} / {GameScore.Rounds}");
            Centred(p, 10, 208, Green, title);
            Centred(p, 5, 20, Bone, goal);
            Centred(p, 3, -205, DarkGreen, controls);

            int best = GameScore.BestRound(_round);
            if (best > 0) Centred(p, 4, -310, Amber, $"YOUR RECORD IN THIS ROUND: {best}");
            _press = Centred(p, 5, -420, Green, "PRESS ENTER TO START");
            _press.gameObject.SetActive(false);

            _briefingShown = Time.unscaledTime;
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Briefing);
        }

        private void CloseBriefing()
        {
            if (_briefing == null) return;
            Destroy(_briefing);
            _briefing = null;
            _play.SetActive(true);
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
        }

        // ---------- Round over ----------

        /// <summary>Saves the score and shows it under the round's own end screen.</summary>
        /// <param name="dim">Darken the game behind it (for a round without its own dark end screen).</param>
        public void ShowResult(bool cleared, bool dim = false)
        {
            if (_finished) return;
            _finished = true;
            GameScore.Finish(_round, cleared);
            _play.SetActive(false);

            _result = Panel("Result", dim ? new Color(0, 0, 0, 0.72f) : Color.clear);
            Transform p = _result.transform;
            int total = GameScore.Total;
            bool gameComplete = cleared && _round == GameScore.Rounds;

            string head = gameComplete
                ? $"FINAL SCORE {total}   RANK {GameScore.Rank(total)}"
                : $"SCORE {total}   ROUND {_round} +{GameScore.RoundTotal(_round)}";
            Centred(p, 5, -290, Green, head);
            Centred(p, 3, -370, Bone, Breakdown(_round));

            string record = GameScore.NewRunRecord ? "NEW RECORD!"
                : GameScore.NewRoundRecord ? $"NEW ROUND {_round} RECORD!"
                : $"BEST {GameScore.BestRun}   {Mathf.Max(0, GameScore.BestRun - total)} POINTS TO BEAT IT";
            Centred(p, 4, -455, Amber, record);
        }

        // ---------- Helpers ----------

        private static string Breakdown(int round)
        {
            var sb = new StringBuilder();
            int shown = 0;
            foreach (GameScore.Line line in GameScore.LinesOf(round))
            {
                if (line.Points == 0) continue;
                if (shown > 0) sb.Append(shown % 3 == 0 ? "\n" : "   /   ");
                sb.Append(line.Label).Append(" +").Append(line.Points);
                shown++;
            }
            return sb.ToString();
        }

        private static PixelLabel Centred(Transform parent, int scale, float y, Color color, string text)
        {
            var label = PixelLabel.Create(parent, "Text", PixelFont.Small, scale, new Vector2(0.5f, 0.5f), new Vector2(0, y), color);
            label.Set(text);
            return label;
        }

        private GameObject Panel(string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
                image.raycastTarget = false; // never blocks the end screens' buttons
            }
            return go;
        }
    }
}
