using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Game.Gameplay
{
    /// <summary>
    /// Scene 1 (the store) round:
    ///   - sends <see cref="shopperCount"/> <see cref="StoreShopper"/>s in from outside the entrance door
    ///     (change the number in the Inspector);
    ///   - 60-second timer at the top of the screen. Time runs out before the shopping list is done → LOSE
    ///     (end screen: TRY AGAIN / MAIN MENU).
    ///   - Shopping list complete → the timer stops; the existing StoreExit then takes you to Level 2 (WIN).
    ///
    /// Set up: Tools → Store → Add NPC Shoppers + 60s Timer (Scene 1).
    /// </summary>
    public class StoreRound : MonoBehaviour
    {
        [Header("Shoppers")]
        [Tooltip("How many shoppers come in")]
        [Min(0)] [SerializeField] private int shopperCount = 3;
        [Tooltip("Outside the entrance door — they spawn here and walk in")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float firstShopperDelay = 1.5f;
        [SerializeField] private float spawnGap = 2.5f;

        [Header("Timer")]
        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private string menuScene = "MainMenu";

        private static readonly Color Bone = new Color(0.92f, 0.90f, 0.84f);
        private static readonly Color Blood = new Color(0.85f, 0.15f, 0.12f);

        private readonly List<StoreShopper> _shoppers = new List<StoreShopper>();
        private float _timeLeft;
        private bool _running, _over, _listDone;
        private TextMeshProUGUI _timerText, _messageText, _endTitle, _endSub;
        private float _messageLeft;
        private CanvasGroup _endGroup;
        private GameObject _endFirst;

        public int ShopperCount { get => shopperCount; set => shopperCount = Mathf.Max(0, value); }
        public IReadOnlyList<StoreShopper> Shoppers => _shoppers;
        public int Spawned => _shoppers.Count;
        public float TimeLeft => _timeLeft;
        public bool IsRunning => _running;
        public bool IsOver => _over;
        public bool ListDone => _listDone;
        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

        // ---------- Unity ----------

        private void OnEnable() => GameEvents.ShoppingListCompleted += OnListCompleted;
        private void OnDisable() => GameEvents.ShoppingListCompleted -= OnListCompleted;

        private IEnumerator Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
            EnsureEventSystem();
            BuildHud();
            _timeLeft = timeLimit;

            yield return null; // shelves build and fill themselves first
            yield return null;
            _running = true;
            UpdateTimer();
            StartCoroutine(SpawnShoppers());
        }

        private void Update()
        {
            UpdateMessage();
            if (!_running || _over || _listDone) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return; // paused

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                _timeLeft = 0f;
                Lose();
            }
            UpdateTimer();
        }

        // ---------- Shoppers ----------

        private IEnumerator SpawnShoppers()
        {
            yield return new WaitForSeconds(firstShopperDelay);
            for (int i = 0; i < shopperCount && !_over; i++)
            {
                Spawn(i);
                yield return new WaitForSeconds(spawnGap);
            }
        }

        private void Spawn(int index)
        {
            Vector3 pos = SpawnPosition + (spawnPoint != null ? spawnPoint.right : Vector3.right) * ((index % 3) - 1) * 0.7f;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;

            var go = new GameObject($"Shopper_{index + 1}", typeof(NavMeshAgent), typeof(StoreShopper));
            go.transform.position = pos;
            go.GetComponent<NavMeshAgent>().Warp(pos); // the agent was created at (0,0,0)
            var shopper = go.GetComponent<StoreShopper>();
            shopper.Round = this;
            _shoppers.Add(shopper);
        }

        public void OnItemsTaken(StoreShopper who, int count) =>
            Message(count > 1 ? "You took their stuff back!" : "Got it back!", 1.5f);

        public void OnMess(StoreShopper who)
        {
            if (Random.value < 0.35f) Message("They're wrecking the shelves!", 1.5f, Blood);
        }

        // ---------- Win / lose ----------

        private void OnListCompleted()
        {
            if (_over) return;
            _listDone = true; // timer stops — now get out through the exit (StoreExit → Level 2)
            _timerText.color = new Color(0.5f, 1f, 0.5f);
        }

        private void Lose()
        {
            if (_over) return;
            _over = true;
            foreach (var s in _shoppers) if (s != null) s.Stop();
            if (GameManager.Instance != null) GameManager.Instance.GameOver(); // frees the cursor

            _endTitle.text = "TIME'S UP";
            _endSub.text = "The shelves are empty. So is your cart.";
            StartCoroutine(FadeIn(_endGroup, 1f));
            _endGroup.interactable = true;
            _endGroup.blocksRaycasts = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_endFirst);
        }

        public void TryAgain()
        {
            if (GameManager.Instance != null) GameManager.Instance.Restart();
            else SceneLoader.ReloadCurrent();
        }

        public void ToMenu()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.MainMenu);
            if (Application.CanStreamedLevelBeLoaded(menuScene)) SceneLoader.Load(menuScene);
            else Debug.LogError($"[Store] Scene '{menuScene}' is not in Build Profiles.", this);
        }

        // ---------- HUD ----------

        private void UpdateTimer()
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
            _timerText.text = $"{s / 60}:{s % 60:00}";
            if (!_listDone) _timerText.color = s <= 10 ? Blood : Bone;
        }

        private void Message(string text, float seconds, Color? color = null)
        {
            if (_messageText == null) return;
            _messageText.text = text;
            _messageText.color = color ?? Bone;
            _messageLeft = seconds;
        }

        private void UpdateMessage()
        {
            if (_messageText == null) return;
            _messageLeft -= Time.unscaledDeltaTime;
            Color c = _messageText.color;
            c.a = Mathf.Clamp01(_messageLeft / 0.4f);
            _messageText.color = c;
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("StoreRoundHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            _timerText = Label(root, "Timer", 72, TextAlignmentOptions.Top);
            _timerText.fontStyle = FontStyles.Bold;
            Place(_timerText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(400, 100));
            _messageText = Label(root, "Message", 36, TextAlignmentOptions.Center);
            _messageText.fontStyle = FontStyles.Bold;
            Place(_messageText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1400, 80));

            var end = new GameObject("EndScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            end.transform.SetParent(root, false);
            var ert = (RectTransform)end.transform;
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = Vector2.one;
            ert.offsetMin = ert.offsetMax = Vector2.zero;
            end.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            _endGroup = end.GetComponent<CanvasGroup>();
            _endGroup.alpha = 0f;
            _endGroup.interactable = false;
            _endGroup.blocksRaycasts = false;

            _endTitle = Label(end.transform, "Title", 120, TextAlignmentOptions.Center);
            _endTitle.fontStyle = FontStyles.Bold;
            _endTitle.characterSpacing = 12;
            _endTitle.color = Blood;
            Place(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1800, 170));
            _endSub = Label(end.transform, "Sub", 40, TextAlignmentOptions.Center);
            Place(_endSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1400, 80));

            _endFirst = Option(end.transform, "TRY AGAIN", -100, TryAgain);
            Option(end.transform, "MAIN MENU", -190, ToMenu);
        }

        private GameObject Option(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Option_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(520, 76));
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            var text = Label(go.transform, "Label", 50, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 14;
            text.text = label;
            text.color = new Color(Bone.r, Bone.g, Bone.b, 0.55f);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fx = go.AddComponent<OptionFx>();
            fx.Label = text;
            fx.Plain = label;
            return go;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.color = Bone;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static IEnumerator FadeIn(CanvasGroup g, float time)
        {
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.SmoothStep(0, 1, t / time);
                yield return null;
            }
            g.alpha = 1f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        /// <summary>Selected option turns red with "&gt;" in front (same as the main menu).</summary>
        private class OptionFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
        {
            public TextMeshProUGUI Label;
            public string Plain;

            public void OnPointerEnter(PointerEventData e)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }

            public void OnSelect(BaseEventData e)
            {
                Label.text = "> " + Plain;
                Label.color = Blood;
            }

            public void OnDeselect(BaseEventData e)
            {
                Label.text = Plain;
                Label.color = new Color(Bone.r, Bone.g, Bone.b, 0.55f);
            }
        }

        // ---------- Editor: add to the store scene ----------
#if UNITY_EDITOR
        [MenuItem("Tools/Store/Add NPC Shoppers + 60s Timer (Scene 1)", priority = 74)]
        public static void AddToOpenScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Store round", "Press Stop (■) first, then run this again.", "OK");
                return;
            }
            AddTo(EditorSceneManager.GetActiveScene().path);
        }

        /// <summary>Batch mode: -executeMethod Game.Gameplay.StoreRound.AddToStoreScene</summary>
        public static void AddToStoreScene() => AddTo("Assets/_Project/Scenes/Dev/user.unity");

        private static void AddTo(string scenePath)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath);

            var old = FindAnyObjectByType<StoreRound>();
            if (old != null) DestroyImmediate(old.gameObject);

            var exit = GameObject.Find("NPC_Exit"); // Meryem's point on the sidewalk outside the door
            if (exit == null) Debug.LogWarning("[Store] No NPC_Exit in the scene — shoppers will spawn at the StoreRound object.");

            var round = new GameObject("StoreRound").AddComponent<StoreRound>();
            if (exit != null) round.transform.position = exit.transform.position;
            var so = new SerializedObject(round);
            so.FindProperty("spawnPoint").objectReferenceValue = exit != null ? exit.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Store] StoreRound added to {scene.path}: {round.ShopperCount} shoppers, 60 s timer.");
        }
#endif
    }
}
