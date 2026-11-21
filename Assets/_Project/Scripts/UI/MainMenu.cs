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
    /// Start screen of the game, built from code on Play:
    ///   soft sky-blue → mint → pink background, big title, Play / How to Play / Quit on the left,
    ///   and the shop-staff character on the right (gently floating, pointing at the buttons).
    ///
    /// Set up: Tools → Main Menu → Create Menu Scene
    ///   (creates Scenes/MainMenu.unity, assigns Art/UI/MenuHero.png, puts the scene FIRST in Build Profiles).
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

        [Header("Character (right side)")]
        [SerializeField] private Sprite heroImage;

        [Header("Colors")]
        [SerializeField] private Color skyColor = new Color(0.62f, 0.86f, 1.00f);   // light blue (top)
        [SerializeField] private Color mintColor = new Color(0.70f, 0.95f, 0.82f);  // light green (middle)
        [SerializeField] private Color pinkColor = new Color(1.00f, 0.80f, 0.88f);  // light pink (bottom)
        [SerializeField] private Color inkColor = new Color(0.13f, 0.17f, 0.34f);   // text
        [SerializeField] private Color buttonColor = new Color(1.00f, 0.42f, 0.62f); // main button / accents

        private Sprite _round;
        private CanvasGroup _mainGroup, _helpGroup;
        private RectTransform _title, _hero;
        private Vector2 _titlePos, _heroPos;
        private GameObject _playButton, _helpBack;
        private InputAction _cancel;
        private bool _leaving;

        // ---------- Unity ----------

        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu); // free cursor
            _cancel = InputSystem.actions?.FindAction("UI/Cancel");

            EnsureCameraAndEventSystem();
            _round = MakeRoundedSprite(64, 26);
            Build();

            Select(_playButton);
            StartCoroutine(Fade(_mainGroup, 0f, 1f, 0.6f));
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            // Gentle floating title and character.
            if (_title != null) _title.anchoredPosition = _titlePos + new Vector2(0, Mathf.Sin(t * 1.3f) * 6f);
            if (_hero != null)
            {
                _hero.anchoredPosition = _heroPos + new Vector2(0, Mathf.Sin(t * 1.1f) * 12f);
                _hero.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.8f) * 1.5f);
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

            // Background: pink (bottom) → mint → sky blue (top)
            var bg = Rect("Background", root);
            Stretch(bg);
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.texture = MakeGradient(pinkColor, mintColor, skyColor);
            raw.raycastTarget = false;

            // ----- Main group (everything that fades in/out together) -----
            var main = Rect("Main", root);
            Stretch(main);
            _mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            _mainGroup.alpha = 0f;

            BuildHero(main);

            var content = Rect("Content", main);
            Place(content, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 0), new Vector2(900, 820));

            var small = Text(content, "SmallTitle", smallTitle, 40, buttonColor, FontStyles.Bold);
            small.characterSpacing = 30;
            TopLeft(small.rectTransform, 0, new Vector2(900, 60));

            // Big title with a soft white "sticker" shadow
            _title = Rect("Title", content);
            TopLeft(_title, -45, new Vector2(900, 230));
            _titlePos = _title.anchoredPosition;
            var shadow = Text(_title, "Shadow", bigTitle, 210, new Color(1, 1, 1, 0.85f), FontStyles.Bold);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(7, -9);
            var big = Text(_title, "Text", bigTitle, 210, inkColor, FontStyles.Bold);
            Stretch(big.rectTransform);

            var bar = Rect("AccentBar", content);
            TopLeft(bar, -285, new Vector2(150, 12));
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = _round;
            barImg.type = Image.Type.Sliced;
            barImg.pixelsPerUnitMultiplier = 4f;
            barImg.color = buttonColor;

            var tag = Text(content, "Tagline", tagline, 34, WithAlpha(inkColor, 0.8f));
            tag.lineSpacing = 8;
            TopLeft(tag.rectTransform, -325, new Vector2(800, 120));

            _playButton = MakeButton(content, "PLAY", -490, primary: true, OnPlay);
            MakeButton(content, "HOW TO PLAY", -600, primary: false, () => ShowHelp(true));
            MakeButton(content, "QUIT", -710, primary: false, OnQuit);

            var foot = Text(main, "Footer", footer, 22, WithAlpha(inkColor, 0.5f));
            foot.characterSpacing = 8;
            Place(foot.rectTransform, Vector2.zero, Vector2.zero, new Vector2(160, 48), new Vector2(800, 40));

            BuildHelp(root);
        }

        /// <summary>The shop-staff character on the right, with a soft white glow behind her.</summary>
        private void BuildHero(Transform parent)
        {
            if (heroImage == null) return;

            var glow = Rect("HeroGlow", parent);
            Place(glow, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500, 20), new Vector2(950, 950));
            var glowImg = glow.gameObject.AddComponent<RawImage>();
            glowImg.texture = MakeRadialGlow(new Color(1, 1, 1, 0.75f));
            glowImg.raycastTarget = false;

            _hero = Rect("Hero", parent);
            // Stands on the bottom edge, right side; 92% of the screen height.
            _hero.anchorMin = _hero.anchorMax = new Vector2(1, 0);
            _hero.pivot = new Vector2(0.5f, 0);
            float h = 1080 * 0.92f;
            float w = h * heroImage.rect.width / heroImage.rect.height;
            _hero.sizeDelta = new Vector2(w, h);
            _hero.anchoredPosition = new Vector2(-500, 0);
            _heroPos = _hero.anchoredPosition;

            var img = _hero.gameObject.AddComponent<Image>();
            img.sprite = heroImage;
            img.preserveAspect = true;
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

            var dim = help.gameObject.AddComponent<Image>();
            dim.color = WithAlpha(inkColor, 0.45f);

            var card = Rect("Card", help);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 680));
            Panel(card, Color.white);

            var title = Text(card, "Title", "HOW TO PLAY", 56, buttonColor, FontStyles.Bold, TextAlignmentOptions.Center);
            title.characterSpacing = 14;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 80));

            string k = "#" + ColorUtility.ToHtmlStringRGB(buttonColor);
            string body =
                "Collect everything on your shopping list and put it in the cart before time runs out. " +
                "Shelves have limited stock — other shoppers want the same things!\n\n" +
                $"<color={k}><b>WASD</b></color><pos=26%>Move\n" +
                $"<color={k}><b>Shift</b></color><pos=26%>Run\n" +
                $"<color={k}><b>Space</b></color><pos=26%>Jump\n" +
                $"<color={k}><b>E</b></color><pos=26%>Pick up / put in the cart\n" +
                $"<color={k}><b>Esc</b></color><pos=26%>Pause";
            var text = Text(card, "Body", body, 32, inkColor);
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

            Color normal = primary ? buttonColor : new Color(1, 1, 1, 0.70f);
            Color hover = primary ? Color.Lerp(buttonColor, Color.white, 0.2f) : Color.white;
            img.color = normal;

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            var fx = rt.gameObject.AddComponent<ButtonFx>();
            fx.Bg = img;
            fx.Normal = normal;
            fx.Hover = hover;

            var text = Text(rt, "Label", label, 34, primary ? Color.white : inkColor, FontStyles.Bold,
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
                cam.backgroundColor = mintColor;
            }
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
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

        /// <summary>Vertical 3-colour gradient: bottom → middle → top.</summary>
        private static Texture2D MakeGradient(Color bottom, Color middle, Color top)
        {
            var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 256; y++)
            {
                float v = y / 255f;
                Color c = v < 0.5f
                    ? Color.Lerp(bottom, middle, Mathf.SmoothStep(0, 1, v * 2f))
                    : Color.Lerp(middle, top, Mathf.SmoothStep(0, 1, (v - 0.5f) * 2f));
                tex.SetPixel(0, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Round soft glow: `color` in the middle fading to transparent at the edge.</summary>
        private static Texture2D MakeRadialGlow(Color color)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
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
        private const string HeroImagePath = "Assets/_Project/Art/UI/MenuHero.png";

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
                camera.backgroundColor = new Color(0.70f, 0.95f, 0.82f);

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
