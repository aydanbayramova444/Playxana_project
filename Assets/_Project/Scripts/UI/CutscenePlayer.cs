using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.UI
{
    /// <summary>
    /// Plays a cutscene video full screen, then loads the game scene.
    /// Flow: MainMenu → Play → Cutscene scene (this) → Next Scene (the game).
    /// Skip: Space, Enter, Escape or left click (a hint appears after a few seconds).
    /// If the video can't play, it goes straight to the game.
    /// Set up with Tools → Main Menu → Create Cutscene Scene.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class CutscenePlayer : MonoBehaviour
    {
        [Tooltip("Scene to load when the video ends or is skipped. Must be in Build Profiles.")]
        [SerializeField] private string nextScene = "user";

        [Tooltip("Seconds before the 'skip' hint shows.")]
        [SerializeField] private float skipHintDelay = 2f;

        [SerializeField] private string skipHint = "SPACE — skip";

        [Header("Old PlayStation look")]
        [SerializeField] private bool oldPlayStationLook = true;
        [Tooltip("Lines of the video picture. Lower = blurrier.")]
        [Range(96, 480)] [SerializeField] private int videoLines = 216;
        [Tooltip("0 = original colours, 1 = very blue.")]
        [Range(0f, 1f)] [SerializeField] private float blueTint = 0.35f;
        [Tooltip("Blocky 90s video compression.")]
        [Range(0f, 1f)] [SerializeField] private float compression = 0.5f;

        private VideoPlayer _video;
        private OldVideoScreen _screen;
        private PixelLabel _hint;
        private float _elapsed;
        private bool _done;

        private void Awake()
        {
            _video = GetComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.renderMode = oldPlayStationLook ? VideoRenderMode.RenderTexture : VideoRenderMode.CameraNearPlane;
            if (_video.targetCamera == null) _video.targetCamera = Camera.main;
            _video.aspectRatio = oldPlayStationLook
                ? VideoAspectRatio.Stretch         // into our own texture of the same shape
                : VideoAspectRatio.FitInside;      // black bars instead of stretching

            var go = new GameObject("CutsceneCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (oldPlayStationLook) _screen = OldVideoScreen.Create(go.transform, _video, videoLines, blueTint, compression);
            _hint = PixelLabel.Create(go.transform, "SkipHint", PixelFont.Small, 3, new Vector2(1, 0), new Vector2(-30, 24),
                new Color(0.12f, 1f, 0.22f, 0.8f), 1f);
        }

        private void Start()
        {
            if (_video.clip == null && string.IsNullOrEmpty(_video.url))
            {
                Debug.LogWarning("[Cutscene] No video set — going straight to the game.", this);
                Finish();
                return;
            }

            _video.loopPointReached += _ => Finish();
            _video.errorReceived += (_, message) =>
            {
                Debug.LogWarning($"[Cutscene] Video error: {message} — going to the game.", this);
                Finish();
            };
            if (_screen == null)
            {
                _video.Play();
                return;
            }
            _video.prepareCompleted += _ =>
            {
                if (_done) return;
                _screen.Begin();
                _video.Play();
            };
            _video.Prepare();
        }

        private void Update()
        {
            if (_done) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= skipHintDelay) _hint.Set(skipHint.Replace("\u2014", "-"));

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool skip = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                                        kb.escapeKey.wasPressedThisFrame)) ||
                        (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (skip && _elapsed > 0.3f) Finish(); // tiny delay so the menu click doesn't skip it at once
        }

        private void Finish()
        {
            if (_done) return;
            _done = true;
            if (_video != null) _video.Stop();

            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError($"[Cutscene] Scene '{nextScene}' is not in Build Profiles.", this);
                return;
            }
            SceneLoader.Load(nextScene);
        }
    }
}
