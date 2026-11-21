using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
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

        private VideoPlayer _video;
        private float _elapsed;
        private bool _done;
        private GUIStyle _style;

        private void Awake()
        {
            _video = GetComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.renderMode = VideoRenderMode.CameraNearPlane;
            if (_video.targetCamera == null) _video.targetCamera = Camera.main;
            _video.aspectRatio = VideoAspectRatio.FitInside; // black bars instead of stretching
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
            _video.Play();
        }

        private void Update()
        {
            if (_done) return;
            _elapsed += Time.unscaledDeltaTime;

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

        private void OnGUI()
        {
            if (_done || _elapsed < skipHintDelay) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = Mathf.Max(14, Screen.height / 40),
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) }
            };
            GUI.Label(new Rect(0, 0, Screen.width - 24, Screen.height - 18), skipHint, _style);
        }
    }
}
