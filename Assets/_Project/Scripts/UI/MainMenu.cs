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
    /// Start screen in an old-school (PS1-era) survival-horror style, built from code on Play:
    ///   - the menu is rendered at a LOW resolution (pixelHeight, e.g. 360p) and scaled up with
    ///     point filtering → chunky pixels;
    ///   - dark background, flickering fluorescent light, scanlines, film grain and vignette;
    ///   - text-only options: the selected one turns blood red with a "&gt;" in front;
    ///   - the stressed shop worker on the right, trembling.
    ///
    /// Set up: Tools → Main Menu → Create Menu Scene. Picture: Assets/_Project/Resources/MenuHero.png.
    /// Play → loads <see cref="gameScene"/> and switches GameManager to Playing.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private string smallTitle = "SUPERMARKET";
        [SerializeField] private string bigTitle = "RUSH";
        [SerializeField] private string tagline = "The shelves are emptying.\nDon't come back empty-handed.";
        [SerializeField] private string footer = "PlayXana Hackathon";

        [Header("Scenes")]
        [Tooltip("Scene loaded by the Play button (must be in Build Profiles)")]
        [SerializeField] private string gameScene = "Main";

        [Header("Character (right side)")]
        [SerializeField] private Sprite heroImage;

        [Header("Look")]
        [Tooltip("Height of the low-resolution menu image. Smaller = bigger pixels.")]
        [Range(180, 720)] [SerializeField] private int pixelHeight = 360;
        [SerializeField] private Color darkColor = new Color(0.020f, 0.020f, 0.024f);   // background
        [SerializeField] private Color sickColor = new Color(0.10f, 0.13f, 0.10f);      // fluorescent glow
        [SerializeField] private Color bloodColor = new Color(0.62f, 0.05f, 0.05f);     // accents / selected
        [SerializeField] private Color boneColor = new Color(0.80f, 0.77f, 0.68f);      // text

        private const int UiLayer = 5; // Unity's built-in "UI" layer

        private RenderTexture _rt;
        private CanvasGroup _mainGroup, _helpGroup;
        private RectTransform _title, _hero;
        private TextMeshProUGUI _bigTitle;
        private Vector2 _titlePos, _heroPos;
        private Image _flicker;
        private RawImage _grain;
        private GameObject _playButton, _helpBack;
        private InputAction _cancel;
        private bool _leaving;
        private float _flickerLeft, _glitchLeft, _nextJitter;

        // ---------- Unity ----------

        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu); // free cursor
            GameScore.NewRun();
            _cancel = InputSystem.actions?.FindAction("UI/Cancel");
            if (heroImage == null) heroImage = Resources.Load<Sprite>("MenuHero");

            EnsureEventSystem();
            Camera pixelCam = SetUpPixelCamera();
            Build(pixelCam);
            BuildScreenEffects();

            Select(_playButton);
            StartCoroutine(Fade(_mainGroup, 0f, 1f, 1.2f));
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // Fluorescent light: mostly steady, sometimes it stutters for a moment.
            if (_flickerLeft <= 0f && Random.value < dt * 0.45f) _flickerLeft = Random.Range(0.06f, 0.35f);
            _flickerLeft -= dt;
            if (_flicker != null) _flicker.color = new Color(0, 0, 0, _flickerLeft > 0f ? Random.Range(0.25f, 0.7f) : 0f);

            // Film grain: jump the noise texture around ~20 times a second.
            if (_grain != null && Time.frameCount % 3 == 0)
            {
                UnityEngine.Rect r = _grain.uvRect;
                r.position = new Vector2(Random.value, Random.value);
                _grain.uvRect = r;
            }

            // Title: slow drift, rare red glitch.
            if (_title != null)
            {
                if (_glitchLeft <= 0f && Random.value < dt * 0.25f) _glitchLeft = Random.Range(0.08f, 0.2f);
                _glitchLeft -= dt;
                bool glitch = _glitchLeft > 0f;
                _title.anchoredPosition = _titlePos + new Vector2(glitch ? Random.Range(-10f, 10f) : 0f,
                    Mathf.Sin(t * 0.7f) * 4f);
                _bigTitle.color = glitch ? bloodColor : boneColor;
            }

            // The worker trembles (small random jumps) and breathes fast.
            if (_hero != null)
            {
                if (t >= _nextJitter)
                {
                    _nextJitter = t + Random.Range(0.05f, 0.12f);
                    _hero.anchoredPosition = _heroPos + new Vector2(Random.Range(-3f, 3f), Random.Range(-2f, 2f));
                }
                _hero.localScale = Vector3.one * (1f + Mathf.Sin(t * 4.5f) * 0.006f);
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
            yield return Fade(_mainGroup, 1f, 0f, 0.6f);
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
            SceneLoader.Load(gameScene);
        }

        private void ShowHelp(bool show)
        {
            _mainGroup.interactable = !show;
            _helpGroup.interactable = show;
            _helpGroup.blocksRaycasts = show;
            StartCoroutine(Fade(_helpGroup, _helpGroup.alpha, show ? 1f : 0f, 0.25f));
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

        // ---------- Low-resolution rendering ----------

        /// <summary>
        /// A camera that draws ONLY the menu canvas into a small RenderTexture.
        /// That texture is then shown full screen with point filtering (see BuildScreenEffects).
        /// </summary>
        private Camera SetUpPixelCamera()
        {
            int h = pixelHeight;
            int w = Mathf.Max(1, Mathf.RoundToInt(h * Screen.width / (float)Mathf.Max(1, Screen.height)));
            _rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point, name = "MenuPixels" };

            // The scene camera shows nothing but black behind everything.
            Camera main = Camera.main;
            if (main != null)
            {
                main.clearFlags = CameraClearFlags.SolidColor;
                main.backgroundColor = Color.black;
                main.cullingMask &= ~(1 << UiLayer);
            }

            var cam = new GameObject("PixelCamera", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = darkColor;
            cam.cullingMask = 1 << UiLayer;
            cam.orthographic = true;
            cam.depth = -10;
            cam.targetTexture = _rt;
            return cam;
        }

        // ---------- Building the menu ----------

        private void Build(Camera pixelCam)
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = pixelCam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            // Clicks arrive in full-screen pixels; this raycaster maps them onto the small texture.
            canvasGo.AddComponent<PixelRaycaster>().Target = _rt;
            Transform root = canvasGo.transform;

            // Background: black at the bottom, a sick greenish light from above.
            var bg = Rect("Background", root);
            Stretch(bg);
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.texture = MakeGradient(darkColor, sickColor);
            raw.raycastTarget = false;

            // ----- Main group (fades in/out together) -----
            var main = Rect("Main", root);
            Stretch(main);
            _mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            _mainGroup.alpha = 0f;

            BuildHero(main);

            var content = Rect("Content", main);
            Place(content, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 10), new Vector2(950, 860));

            var small = Text(content, "SmallTitle", smallTitle, 46, bloodColor, FontStyles.Bold);
            small.characterSpacing = 40;
            TopLeft(small.rectTransform, 0, new Vector2(950, 70));

            // Big title with a dark-red shadow
            _title = Rect("Title", content);
            TopLeft(_title, -50, new Vector2(950, 250));
            _titlePos = _title.anchoredPosition;
            var shadow = Text(_title, "Shadow", bigTitle, 240, new Color(0.25f, 0.01f, 0.01f, 0.9f), FontStyles.Bold);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(9, -9);
            _bigTitle = Text(_title, "Text", bigTitle, 240, boneColor, FontStyles.Bold);
            Stretch(_bigTitle.rectTransform);

            var line = Rect("Line", content);
            TopLeft(line, -305, new Vector2(520, 6));
            line.gameObject.AddComponent<Image>().color = bloodColor;

            var tag = Text(content, "Tagline", tagline, 40, WithAlpha(boneColor, 0.7f), FontStyles.Italic);
            tag.lineSpacing = 6;
            TopLeft(tag.rectTransform, -335, new Vector2(900, 130));

            _playButton = MakeOption(content, "PLAY", -510, OnPlay);
            MakeOption(content, "HOW TO PLAY", -600, () => ShowHelp(true));
            MakeOption(content, "QUIT", -690, OnQuit);

            var foot = Text(main, "Footer", footer, 30, WithAlpha(boneColor, 0.35f));
            foot.characterSpacing = 10;
            Place(foot.rectTransform, Vector2.zero, Vector2.zero, new Vector2(150, 45), new Vector2(900, 50));
            if (GameScore.BestRun > 0)
            {
                var best = Text(main, "BestScore", $"BEST SCORE  {GameScore.BestRun}   RANK {GameScore.Rank(GameScore.BestRun)}", 30,
                    new Color(0.12f, 1f, 0.22f), FontStyles.Bold);
                Place(best.rectTransform, Vector2.zero, Vector2.zero, new Vector2(150, 95), new Vector2(900, 50));
            }

            BuildHelp(root);
            SetLayer(canvasGo.transform, UiLayer);
        }

        /// <summary>The stressed shop worker on the right, slightly darkened, with a dim red glow behind.</summary>
        private void BuildHero(Transform parent)
        {
            if (heroImage == null) return;

            var glow = Rect("HeroGlow", parent);
            Place(glow, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480, 40), new Vector2(1000, 1000));
            var glowImg = glow.gameObject.AddComponent<RawImage>();
            glowImg.texture = MakeRadialGlow(new Color(bloodColor.r, bloodColor.g, bloodColor.b, 0.35f));
            glowImg.raycastTarget = false;

            _hero = Rect("Hero", parent);
            _hero.anchorMin = _hero.anchorMax = new Vector2(1, 0);
            _hero.pivot = new Vector2(0.5f, 0);
            float h = 1080 * 0.95f;
            float w = h * heroImage.rect.width / heroImage.rect.height;
            _hero.sizeDelta = new Vector2(w, h);
            _hero.anchoredPosition = new Vector2(-480, 0);
            _heroPos = _hero.anchoredPosition;

            var img = _hero.gameObject.AddComponent<Image>();
            img.sprite = heroImage;
            img.preserveAspect = true;
            img.color = new Color(0.78f, 0.72f, 0.70f); // a bit dark and drained
            img.raycastTarget = false;
        }

        private void BuildHelp(Transform root)
        {
            var help = Rect("HowToPlay", root);
            Stretch(help);
            _helpGroup = help.gameObject.AddComponent<CanvasGroup>();
            _helpGroup.alpha = 0f;
            _helpGroup.interactable = false;
            _helpGroup.blocksRaycasts = false;
            help.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

            var border = Rect("Border", help);
            Place(border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 760));
            border.gameObject.AddComponent<Image>().color = bloodColor;
            var card = Rect("Card", border);
            Stretch(card);
            card.offsetMin = new Vector2(6, 6);
            card.offsetMax = new Vector2(-6, -6);
            card.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.035f);

            var title = Text(card, "Title", "HOW TO PLAY", 64, bloodColor, FontStyles.Bold, TextAlignmentOptions.Center);
            title.characterSpacing = 20;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(1000, 90));

            string k = "#" + ColorUtility.ToHtmlStringRGB(bloodColor);
            string body =
                "Find everything on your list and put it in the cart before time runs out. " +
                "Stock is limited. The other shoppers want it too.\n\n" +
                $"<color={k}><b>WASD</b></color><pos=26%>Move\n" +
                $"<color={k}><b>SHIFT</b></color><pos=26%>Run\n" +
                $"<color={k}><b>SPACE</b></color><pos=26%>Jump\n" +
                $"<color={k}><b>E</b></color><pos=26%>Pick up / put in the cart\n" +
                $"<color={k}><b>ESC</b></color><pos=26%>Pause";
            var text = Text(card, "Body", body, 38, boneColor);
            text.lineSpacing = 10;
            text.richText = true;
            Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(940, 440));

            var back = Rect("BackRow", card);
            Place(back, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(400, 80));
            _helpBack = MakeOption(back, "BACK", 0, () => ShowHelp(false), centered: true);
        }

        /// <summary>A text-only menu option. Selected (mouse hover or arrows): red, "&gt;" in front, slight flicker.</summary>
        private GameObject MakeOption(Transform parent, string label, float top,
            UnityEngine.Events.UnityAction onClick, bool centered = false)
        {
            var rt = Rect($"Option_{label}", parent);
            Vector2 size = new Vector2(600, 80);
            if (centered) Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            else Place(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, top), size);

            // Invisible hit area (alpha 0 still receives clicks).
            rt.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            var text = Text(rt, "Label", label, 52, WithAlpha(boneColor, 0.55f), FontStyles.Bold,
                centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            text.characterSpacing = 14;
            Stretch(text.rectTransform);

            var fx = rt.gameObject.AddComponent<OptionFx>();
            fx.Label = text;
            fx.Plain = label;
            fx.Normal = WithAlpha(boneColor, 0.55f);
            fx.Selected = bloodColor;
            return rt.gameObject;
        }

        // ---------- Full-screen effects (drawn on top at full resolution) ----------

        private void BuildScreenEffects()
        {
            var canvasGo = new GameObject("ScreenCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Transform root = canvasGo.transform;

            // 1. The pixelated menu, scaled up.
            var view = Rect("PixelView", root);
            Stretch(view);
            var viewImg = view.gameObject.AddComponent<RawImage>();
            viewImg.texture = _rt;
            viewImg.raycastTarget = false;

            // 2. Scanlines: one dark line every 3 screen pixels.
            var scan = Rect("Scanlines", root);
            Stretch(scan);
            var scanImg = scan.gameObject.AddComponent<RawImage>();
            scanImg.texture = MakeScanlines();
            scanImg.uvRect = new UnityEngine.Rect(0, 0, 1, Screen.height / 3f);
            scanImg.raycastTarget = false;

            // 3. Film grain.
            var grain = Rect("Grain", root);
            Stretch(grain);
            _grain = grain.gameObject.AddComponent<RawImage>();
            _grain.texture = MakeNoise(128);
            _grain.uvRect = new UnityEngine.Rect(0, 0, Screen.width / 256f, Screen.height / 256f);
            _grain.raycastTarget = false;

            // 4. Dark corners.
            var vig = Rect("Vignette", root);
            Stretch(vig);
            var vigImg = vig.gameObject.AddComponent<RawImage>();
            vigImg.texture = MakeVignette();
            vigImg.raycastTarget = false;

            // 5. Flicker: a black layer that blinks when the light stutters.
            var fl = Rect("Flicker", root);
            Stretch(fl);
            _flicker = fl.gameObject.AddComponent<Image>();
            _flicker.color = new Color(0, 0, 0, 0);
            _flicker.raycastTarget = false;
        }

        // ---------- Helpers ----------

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
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

        // ---------- Generated textures ----------

        private static Texture2D NewTex(int w, int h, TextureWrapMode wrap, FilterMode filter) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = filter };

        private static Texture2D MakeGradient(Color bottom, Color top)
        {
            var tex = NewTex(1, 64, TextureWrapMode.Clamp, FilterMode.Bilinear);
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, Mathf.Pow(y / 63f, 1.6f)));
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeRadialGlow(Color color)
        {
            const int size = 64;
            var tex = NewTex(size, size, TextureWrapMode.Clamp, FilterMode.Bilinear);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                Color c = color;
                c.a *= Mathf.SmoothStep(1f, 0f, d);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeVignette()
        {
            const int size = 64;
            var tex = NewTex(size, size, TextureWrapMode.Clamp, FilterMode.Bilinear);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                tex.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0f, 0.9f, (d - 0.45f) / 0.75f)));
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeScanlines()
        {
            var tex = NewTex(1, 3, TextureWrapMode.Repeat, FilterMode.Point);
            tex.SetPixel(0, 0, new Color(0, 0, 0, 0.35f));
            tex.SetPixel(0, 1, new Color(0, 0, 0, 0f));
            tex.SetPixel(0, 2, new Color(0, 0, 0, 0f));
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeNoise(int size)
        {
            var tex = NewTex(size, size, TextureWrapMode.Repeat, FilterMode.Point);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = Random.value;
                tex.SetPixel(x, y, new Color(v, v, v, 0.07f));
            }
            tex.Apply();
            return tex;
        }

        // ---------- Small helper components ----------

        /// <summary>Maps full-screen mouse positions onto the low-resolution menu texture before raycasting.</summary>
        private class PixelRaycaster : GraphicRaycaster
        {
            public RenderTexture Target;

            public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
            {
                if (Target == null)
                {
                    base.Raycast(eventData, resultAppendList);
                    return;
                }
                Vector2 screen = eventData.position;
                eventData.position = new Vector2(screen.x * Target.width / Screen.width,
                                                 screen.y * Target.height / Screen.height);
                base.Raycast(eventData, resultAppendList);
                eventData.position = screen;
            }
        }

        /// <summary>Text option look: selected = red with "&gt; " and a slight flicker; otherwise dim.</summary>
        private class OptionFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
        {
            public TextMeshProUGUI Label;
            public string Plain;
            public Color Normal, Selected;
            private bool _selected;

            private void Update()
            {
                if (!_selected) return;
                // Tiny unstable flicker on the selected option.
                Color c = Selected;
                c.a = Random.value < 0.06f ? 0.55f : 1f;
                Label.color = c;
            }

            // Hovering with the mouse selects the option, so mouse and keyboard highlight the same one.
            public void OnPointerEnter(PointerEventData e)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }

            public void OnSelect(BaseEventData e)
            {
                _selected = true;
                Label.text = "> " + Plain;
            }

            public void OnDeselect(BaseEventData e)
            {
                _selected = false;
                Label.text = Plain;
                Label.color = Normal;
            }
        }

        // ---------- Editor: create the scene ----------
#if UNITY_EDITOR
        private const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string HeroImagePath = "Assets/_Project/Resources/MenuHero.png";

        [MenuItem("Tools/Main Menu/Create Menu Scene", priority = 50)]
        private static void CreateMenuScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Main Menu", "Press Stop (■) first, then run this again.", "OK");
                return;
            }
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
                camera.backgroundColor = Color.black;

                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

                new GameObject("MainMenu").AddComponent<MainMenu>();
                EditorSceneManager.SaveScene(scene, MenuScenePath);
            }

            // The character picture must be imported as a Sprite.
            if (AssetImporter.GetAtPath(HeroImagePath) is TextureImporter importer &&
                importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var menu = FindAnyObjectByType<MainMenu>();
            if (menu != null)
            {
                var so = new SerializedObject(menu);
                so.FindProperty("heroImage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HeroImagePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
                EditorSceneManager.SaveScene(menu.gameObject.scene);
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
