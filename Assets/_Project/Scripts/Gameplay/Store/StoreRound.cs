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
        [Tooltip("Rigged low-poly human models; each shopper gets a random one (empty = simple shapes)")]
        [SerializeField] private GameObject[] characterModels;

        [Header("Timer")]
        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private string menuScene = "MainMenu";

        private static readonly Color Bone = new Color(0.92f, 0.90f, 0.84f);
        private static readonly Color Blood = new Color(0.85f, 0.15f, 0.12f);

        private readonly List<StoreShopper> _shoppers = new List<StoreShopper>();
        private float _timeLeft;
        private bool _running, _over, _listDone;
        private TextMeshProUGUI _messageText, _endTitle, _endSub;
        private Game.UI.RoundHud _hud;
        private ShoppingListTracker _list;
        private int _rescued;

        private const int RoundNumber = 1;
        private const int PointsPerItem = 100, ListBonus = 500, PointsPerSecond = 20, PointsPerRescue = 50, MaxRescues = 10;
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

        private void OnEnable()
        {
            GameEvents.ShoppingListCompleted += OnListCompleted;
            GameEvents.ShoppingListChanged += ScoreItems;
        }

        private void OnDisable()
        {
            GameEvents.ShoppingListCompleted -= OnListCompleted;
            GameEvents.ShoppingListChanged -= ScoreItems;
        }

        private IEnumerator Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.StartGame();
            EnsureEventSystem();
            BuildHud();
            _timeLeft = timeLimit;
            _list = FindAnyObjectByType<ShoppingListTracker>();
            _hud = Game.UI.RoundHud.Create(transform, RoundNumber, timeLimit, "GET SUPPLIES",
                $"THE BOMBS ARE COMING. YOU HAVE {Mathf.CeilToInt(timeLimit)} SECONDS.\n" +
                "FIND EVERYTHING ON YOUR LIST AND PUT IT IN THE CART.\n" +
                "OTHER SHOPPERS STEAL: TAKE THINGS BACK FROM THEM.\n" +
                "LIST DONE? RUN OUT THROUGH THE DOOR!",
                "WASD MOVE   MOUSE LOOK   SHIFT RUN   E TAKE / PUT / GRAB BACK");

            yield return null; // shelves build and fill themselves first
            yield return null;
            _running = true;
            GameAudio.Loop(Sfx.Hum, 0.08f); // fluorescent lights
            UpdateTimer();
            StartCoroutine(SpawnShoppers());
        }

        private void Update()
        {
            UpdateMessage();
            if (!_running || _over || _listDone) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return; // paused

            float before = _timeLeft;
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 10f && Mathf.CeilToInt(_timeLeft) < Mathf.CeilToInt(before)) GameAudio.Play(Sfx.Tick, 0.5f); // last 10 s
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
            if (characterModels != null && characterModels.Length > 0)
                shopper.BodyModel = characterModels[Random.Range(0, characterModels.Length)];
            _shoppers.Add(shopper);
        }

        public void OnItemsTaken(StoreShopper who, int count)
        {
            Message(count > 1 ? "You took their stuff back!" : "Got it back!", 1.5f);
            if (_over || _listDone) return;
            _rescued = Mathf.Min(MaxRescues, _rescued + count);
            GameScore.Set(RoundNumber, "TAKEN BACK", _rescued * PointsPerRescue);
        }

        private void ScoreItems()
        {
            if (_hud == null || _list == null || _over) return;
            int collected = 0;
            foreach (var entry in _list.Entries) collected += entry.Collected;
            GameScore.Set(RoundNumber, "ITEMS", collected * PointsPerItem);
        }

        /// <summary>A shopper took a product off a shelf (the shelf count goes down because of this).</summary>
        public void OnShopperGrabbed(StoreShopper who, Game.Interaction.Pickup item)
        {
            GameAudio.PlayAt(Sfx.Clatter, who.transform.position, 0.6f);
            Message($"A shopper took {Pretty(item.ItemId)}", 1.8f);
        }

        /// <summary>A shopper with full hands threw products off a shelf onto the floor.</summary>
        public void OnMess(StoreShopper who, string itemId)
        {
            GameAudio.PlayAt(Sfx.Crash, who.transform.position, 0.8f);
            Message($"A shopper knocked {Pretty(itemId)} off the shelf!", 1.8f, Blood);
        }

        private static string Pretty(string id) => string.IsNullOrEmpty(id) ? "something" : id.Replace('_', ' ').ToUpperInvariant();

        // ---------- Win / lose ----------

        private void OnListCompleted()
        {
            if (_over) return;
            _listDone = true; // timer stops — now get out through the exit (StoreExit → Level 2)
            GameAudio.Play(Sfx.Win, 0.7f);
            if (_hud == null) return;
            ScoreItems();
            GameScore.Set(RoundNumber, "LIST DONE", ListBonus);
            GameScore.Set(RoundNumber, "TIME", Mathf.FloorToInt(_timeLeft) * PointsPerSecond);
            GameScore.Finish(RoundNumber, cleared: true);
            _hud.SetTime(_timeLeft, done: true);
        }

        private void Lose()
        {
            if (_over) return;
            _over = true;
            GameAudio.Play(Sfx.Alarm, 0.5f);
            GameAudio.Play(Sfx.Lose, 0.7f);
            foreach (var s in _shoppers) if (s != null) s.Stop();
            if (GameManager.Instance != null) GameManager.Instance.GameOver(); // frees the cursor

            _endTitle.text = "TIME'S UP";
            _endSub.text = "The shelves are empty. So is your cart.";
            _hud.ShowResult(cleared: false);
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
            if (_hud != null) _hud.SetTime(_timeLeft, _listDone);
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

            var exit = GameObject.Find("NPC_Exit"); // Meryem's point on the sidewalk outside the door
            if (exit == null) Debug.LogWarning("[Store] No NPC_Exit in the scene — shoppers will spawn at the StoreRound object.");

            // Update the existing object, never recreate it: new file IDs turn into a second StoreRound
            // when the scene is merged with someone else's branch.
            var round = FindAnyObjectByType<StoreRound>();
            if (round == null) round = new GameObject("StoreRound").AddComponent<StoreRound>();
            if (round.GetComponent<StoreDecor>() == null) round.gameObject.AddComponent<StoreDecor>(); // post-nuclear look
            if (exit != null) round.transform.position = exit.transform.position;
            var so = new SerializedObject(round);
            so.FindProperty("spawnPoint").objectReferenceValue = exit != null ? exit.transform : null;
            var modelsProp = so.FindProperty("characterModels");
            var models = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("male0 t:Model", new[] { "Assets/Floreswa/Models" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.GetComponentInChildren<SkinnedMeshRenderer>() != null) models.Add(m);
            }
            modelsProp.arraySize = models.Count;
            for (int i = 0; i < models.Count; i++) modelsProp.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Store] StoreRound added to {scene.path}: {round.ShopperCount} shoppers, 60 s timer.");
        }
#endif
    }
}
