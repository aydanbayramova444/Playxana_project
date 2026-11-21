using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Game.UI
{
    /// <summary>
    /// Start screen of the game. The whole UI is built from code on Play — no sprites or prefabs needed:
    ///   gradient background with slowly floating bubbles, big title, Play / How to Play / Quit,
    ///   and a tilted "shopping list" card on the right.
    ///
    /// Set up: Tools → Main Menu → Create Menu Scene
    ///   (creates Scenes/MainMenu.unity and puts it FIRST in Build Profiles, before Main).
    ///
    /// Play → loads <see cref="gameScene"/> and switches GameManager to Playing.
    /// Keyboard / gamepad work too (arrows + Enter, Esc closes "How to Play").
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private string smallTitle = "SUPERMARKET";
        [SerializeField] private string bigTitle = "RUSH";
        [SerializeField] private string tagline = "Grab everything on your list\nbefore the shelves run empty.";
        [SerializeField] private string footer = "PlayXana Hackathon";

        [Header("Scenes")]
        [Tooltip("Scene loaded by the Play button (must be in Build Profiles)")]
        [SerializeField] private string gameScene = "Main";

        [Header("Colors")]
        [SerializeField] private Color topColor = new Color(0.09f, 0.33f, 0.38f);
        [SerializeField] private Color bottomColor = new Color(0.04f, 0.06f, 0.13f);
        [SerializeField] private Color accent = new Color(1f, 0.73f, 0.22f);

        private Sprite _round, _circle;
        private CanvasGroup _mainGroup, _helpGroup;
        private RectTransform _title, _card;
        private Vector2 _titlePos, _cardPos;
        private GameObject _playButton, _helpBack;
        private InputAction _cancel;
        private bool _leaving;

        private struct Bubble
        {
            public RectTransform Rt;
            public float Speed, Sway, Phase, X;
        }
        private readonly List<Bubble> _bubbles = new List<Bubble>();

        // ---------- Unity ----------

        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu); // free cursor
            _cancel = InputSystem.actions?.FindAction("UI/Cancel");

            EnsureCameraAndEventSystem();
            _round = MakeRoundedSprite(64, 26);
            _circle = MakeRoundedSprite(64, 32);
            Build();

            Select(_playButton);
            StartCoroutine(Fade(_mainGroup, 0f, 1f, 0.6f));
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // Gentle floating title and card.
            if (_title != null) _title.anchoredPosition = _titlePos + new Vector2(0, Mathf.Sin(t * 1.3f) * 6f);
            if (_card != null) _card.anchoredPosition = _cardPos + new Vector2(0, Mathf.Sin(t * 0.9f + 1f) * 10f);

            // Bubbles drift up and wrap around.
            for (int i = 0; i < _bubbles.Count; i++)
            {
                Bubble b = _bubbles[i];
                Vector2 p = b.Rt.anchoredPosition;
                p.y += b.Speed * dt;
                if (p.y > 1080 + b.Rt.sizeDelta.y) p.y = -b.Rt.sizeDelta.y;
                p.x = b.X + Mathf.Sin(t * 0.5f + b.Phase) * b.Sway;
                b.Rt.anchoredPosition = p;
            }

            if (_cancel != null && _cancel.WasPressedThisFrame() && _helpGroup.blocksRaycasts)
                ShowHelp(false);
        }

        // ---------- Buttons ----------

        private void OnPlay()
        {
            if (_leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError($"[MainMenu] Scene '{gameScene}' is not in Build Profiles.", this);
                return;
            }
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            _leaving = true;
            _mainGroup.interactable = false;
            yield return Fade(_mainGroup, 1f, 0f, 0.35f);
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
            SceneLoader.Load(gameScene);
        }

        private void ShowHelp(bool show)
        {
            _mainGroup.interactable = !show;
            _helpGroup.interactable = show;
            _helpGroup.blocksRaycasts = show;
            StartCoroutine(Fade(_helpGroup, _helpGroup.alpha, show ? 1f : 0f, 0.2f));
            Select(show ? _helpBack : _playButton);
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Building the UI ----------

        private void Build()
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            // Background gradient
            var bg = Rect("Background", root);
            Stretch(bg);
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.texture = MakeGradient(bottomColor, topColor);
            raw.raycastTarget = false;

            // Bubbles (behind everything else)
            var bubbles = Rect("Bubbles", root);
            Stretch(bubbles);
            for (int i = 0; i < 16; i++)
            {
                float size = Random.Range(40f, 230f);
                var rt = Rect("Bubble", bubbles);
                Place(rt, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0, Random.Range(0f, 1080f)), Vector2.one * size);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = _circle;
                img.raycastTarget = false;
                Color c = i % 3 == 0 ? accent : Color.white;
                c.a = Random.Range(0.04f, 0.11f);
                img.color = c;
                _bubbles.Add(new Bubble
                {
                    Rt = rt, X = Random.Range(0f, 1920f), Speed = Random.Range(12f, 38f),
                    Sway = Random.Range(10f, 40f), Phase = Random.Range(0f, 6.28f)
                });
            }

            // ----- Main group (everything that fades in/out together) -----
            var main = Rect("Main", root);
            Stretch(main);
            _mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            _mainGroup.alpha = 0f;

            var content = Rect("Content", main);
            Place(content, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 0), new Vector2(900, 820));

            var small = Text(content, "SmallTitle", smallTitle, 40, accent, FontStyles.Bold);
            small.characterSpacing = 30;
            TopLeft(small.rectTransform, 0, new Vector2(900, 60));

            // Big title with a soft shadow behind it
            _title = Rect("Title", content);
            TopLeft(_title, -45, new Vector2(900, 230));
            _titlePos = _title.anchoredPosition;
            var shadow = Text(_title, "Shadow", bigTitle, 210, new Color(0, 0, 0, 0.35f), FontStyles.Bold);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(8, -10);
            var big = Text(_title, "Text", bigTitle, 210, Color.white, FontStyles.Bold);
            Stretch(big.rectTransform);

            var bar = Rect("AccentBar", content);
            TopLeft(bar, -285, new Vector2(150, 12));
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = _round;
            barImg.type = Image.Type.Sliced;
            barImg.pixelsPerUnitMultiplier = 4f;
            barImg.color = accent;

            var tag = Text(content, "Tagline", tagline, 34, new Color(1, 1, 1, 0.78f));
            tag.lineSpacing = 8;
            TopLeft(tag.rectTransform, -325, new Vector2(800, 120));

            _playButton = MakeButton(content, "PLAY", -490, primary: true, OnPlay);
            MakeButton(content, "HOW TO PLAY", -600, primary: false, () => ShowHelp(true));
            MakeButton(content, "QUIT", -710, primary: false, OnQuit);

            BuildCard(main);

            var foot = Text(main, "Footer", footer, 22, new Color(1, 1, 1, 0.4f));
            foot.characterSpacing = 8;
            Place(foot.rectTransform, Vector2.zero, Vector2.zero, new Vector2(160, 48), new Vector2(800, 40));

            var hint = Text(main, "Hint", "Mouse  or  Arrows + Enter", 22, new Color(1, 1, 1, 0.4f), FontStyles.Normal,
                TextAlignmentOptions.BottomRight);
            Place(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 48), new Vector2(600, 40));

            BuildHelp(root);
        }

        /// <summary>The tilted "shopping list" card on the right — shows what the game is about.</summary>
        private void BuildCard(Transform parent)
        {
            _card = Rect("ListCard", parent);
            Place(_card, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-430, 10), new Vector2(560, 650));
            _card.localRotation = Quaternion.Euler(0, 0, -6f);
            _cardPos = _card.anchoredPosition;

            var cardShadow = Rect("Shadow", _card);
            Stretch(cardShadow);
            cardShadow.anchoredPosition = new Vector2(14, -18);
            Panel(cardShadow, new Color(0, 0, 0, 0.3f));

            var face = Rect("Face", _card);
            Stretch(face);
            Panel(face, new Color(0.98f, 0.96f, 0.91f)); // paper

            var header = Rect("Header", face);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 120);
            header.anchoredPosition = Vector2.zero;
            Panel(header, accent);

            var title = Text(header, "Title", "SHOPPING LIST", 44, bottomColor, FontStyles.Bold, TextAlignmentOptions.Center);
            title.characterSpacing = 12;
            Stretch(title.rectTransform);

            string done = "#2E8B57", todo = "#2A2F3A", faded = "#9AA0A6";
            string list =
                $"<color={faded}><s>Banana</s></color><pos=78%><color={done}><b>2/2</b></color>\n" +
                $"<color={faded}><s>Soda</s></color><pos=78%><color={done}><b>1/1</b></color>\n" +
                $"<color={todo}>Apple</color><pos=78%><color={todo}><b>0/1</b></color>\n" +
                $"<color={todo}>Bread</color><pos=78%><color={todo}><b>0/1</b></color>\n" +
                $"<color={todo}>Chocolate</color><pos=78%><color={todo}><b>0/2</b></color>";
            var items = Text(face, "Items", list, 40, Color.black);
            items.lineSpacing = 38;
            items.richText = true;
            Place(items.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(460, 400));

            var timer = Text(face, "Timer", "<b>01:24</b>  left", 34, new Color(0.85f, 0.25f, 0.25f), FontStyles.Normal,
                TextAlignmentOptions.Center);
            Place(timer.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(460, 60));
        }

        private void BuildHelp(Transform root)
        {
            var help = Rect("HowToPlay", root);
            Stretch(help);
            _helpGroup = help.gameObject.AddComponent<CanvasGroup>();
            _helpGroup.alpha = 0f;
            _helpGroup.interactable = false;
            _helpGroup.blocksRaycasts = false;

            var dim = help.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.65f);

            var card = Rect("Card", help);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 680));
            Panel(card, Color.Lerp(bottomColor, topColor, 0.35f));

            var title = Text(card, "Title", "HOW TO PLAY", 56, accent, FontStyles.Bold, TextAlignmentOptions.Center);
            title.characterSpacing = 14;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 80));

            string a = "#FFD27A";
            string body =
                "Collect everything on your shopping list and put it in the cart before time runs out. " +
                "Shelves have limited stock — other shoppers want the same things!\n\n" +
                $"<color={a}><b>WASD</b></color><pos=26%>Move\n" +
                $"<color={a}><b>Shift</b></color><pos=26%>Run\n" +
                $"<color={a}><b>Space</b></color><pos=26%>Jump\n" +
                $"<color={a}><b>E</b></color><pos=26%>Pick up / put in the cart\n" +
                $"<color={a}><b>Esc</b></color><pos=26%>Pause";
            var text = Text(card, "Body", body, 32, new Color(1, 1, 1, 0.9f));
            text.lineSpacing = 12;
            text.richText = true;
            Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(820, 380));

            var back = Rect("BackRow", card);
            Place(back, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(440, 92));
            _helpBack = MakeButton(back, "BACK", 0, primary: true, () => ShowHelp(false), centered: true);
        }

        /// <summary>A pill-shaped button with smooth hover / press animation.</summary>
        private GameObject MakeButton(Transform parent, string label, float top, bool primary,
            UnityEngine.Events.UnityAction onClick, bool centered = false)
        {
            var rt = Rect($"Button_{label}", parent);
            Vector2 size = new Vector2(440, 92);
            if (centered) Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            else Place(rt, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(size.x / 2f, top - size.y / 2f), size);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.55f; // round ends

            Color normal = primary ? accent : new Color(1, 1, 1, 0.10f);
            Color hover = primary ? Color.Lerp(accent, Color.white, 0.25f) : new Color(1, 1, 1, 0.22f);
            img.color = normal;

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            var fx = rt.gameObject.AddComponent<ButtonFx>();
            fx.Bg = img;
            fx.Normal = normal;
            fx.Hover = hover;

            var text = Text(rt, "Label", label, 34, primary ? bottomColor : Color.white, FontStyles.Bold,
                TextAlignmentOptions.Center);
            text.characterSpacing = 10;
            Stretch(text.rectTransform);
            return rt.gameObject;
        }

        // ---------- Helpers ----------

        private void EnsureCameraAndEventSystem()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = bottomColor;
            }
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private static void Select(GameObject go)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float time)
        {
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / time));
                yield return null;
            }
            group.alpha = to;
        }

        private void Panel(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _round;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Anchors to the top-left of the parent, `top` pixels down.</summary>
        private static void TopLeft(RectTransform rt, float top, Vector2 size) =>
            Place(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, top), size);

        private static Texture2D MakeGradient(Color bottom, Color top)
        {
            var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 256; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, Mathf.SmoothStep(0, 1, y / 255f)));
            tex.Apply();
            return tex;
        }

        /// <summary>White square with rounded corners (radius in pixels), ready for 9-slicing.</summary>
        private static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance from the nearest corner circle centre (0 inside the straight parts).
                float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f); // 1px anti-aliased edge
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            int border = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Smooth grow + lighten when hovered or selected (keyboard), squash when pressed.</summary>
        private class ButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler,
            IPointerDownHandler, IPointerUpHandler
        {
            public Image Bg;
            public Color Normal, Hover;
            private bool _selected, _down;

            private void Update()
            {
                float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
                float scale = _down ? 0.96f : _selected ? 1.06f : 1f;
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * scale, k);
                Bg.color = Color.Lerp(Bg.color, _selected ? Hover : Normal, k);
            }

            // Hovering with the mouse selects the button, so mouse and keyboard highlight the same one.
            public void OnPointerEnter(PointerEventData e)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }

            public void OnSelect(BaseEventData e) => _selected = true;
            public void OnDeselect(BaseEventData e) => _selected = false;
            public void OnPointerDown(PointerEventData e) => _down = true;
            public void OnPointerUp(PointerEventData e) => _down = false;
        }

        // ---------- Editor: create the scene ----------
#if UNITY_EDITOR
        private const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("Tools/Main Menu/Create Menu Scene", priority = 50)]
        private static void CreateMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null)
            {
                EditorSceneManager.OpenScene(MenuScenePath);
            }
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cam.tag = "MainCamera";
                var camera = cam.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.06f, 0.13f);

                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

                new GameObject("MainMenu").AddComponent<MainMenu>();
                EditorSceneManager.SaveScene(scene, MenuScenePath);
            }

            // The menu must be the FIRST scene in the build; keep every other scene after it.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[MainMenu] {MenuScenePath} is ready and first in Build Profiles. Press Play.");
        }
#endif
    }
}
