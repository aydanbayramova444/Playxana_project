using System.Collections;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Level 3 — The Shelter. Runs the whole round:
    ///   - counts your supplies (everything on the Shelf objects in the scene when the round starts);
    ///   - sends survivors (<see cref="ShelterIntruder"/>) in through the entrance, faster and faster;
    ///   - LOSE: every supply has been stolen.   WIN: you held out until the timer ran out.
    ///   - HUD (supplies + time left + messages) and the end screen (TRY AGAIN / MAIN MENU) are built in code.
    ///
    /// Put ONE in the scene (Tools → Shelter → Create Shelter Scene does it) and set Entrance / Exit.
    /// </summary>
    public class ShelterDirector : MonoBehaviour
    {
        [Header("Round")]
        [Tooltip("Hold out this long to win (seconds)")]
        [SerializeField] private float surviveSeconds = 60f;
        [SerializeField] private float firstIntruderDelay = 3f;
        [Tooltip("Seconds between groups at the start → at the end of the round")]
        [SerializeField] private Vector2 spawnInterval = new Vector2(7f, 4f);
        [Tooltip("How many come in together (min, max)")]
        [SerializeField] private Vector2Int groupSize = new Vector2Int(2, 3);
        [SerializeField] private int maxIntrudersAtOnce = 8;

        [Header("Places")]
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;

        [Header("Atmosphere")]
        [Tooltip("Ceiling lamps that stutter now and then")]
        [SerializeField] private Light[] flickerLights;

        [Header("Bat (scare thieves off)")]
        [Tooltip("Left click swings the bat; a thief within this distance in front of you runs off")]
        [SerializeField] private float batReach = 2.5f;

        [Header("After the round")]
        [SerializeField] private string menuScene = "MainMenu";

        [Header("Text")]
        [SerializeField] private string winTitle = "YOU SURVIVED";
        [SerializeField] private string loseTitle = "THEY TOOK EVERYTHING";

        private static readonly Color Bone = new Color(0.82f, 0.79f, 0.70f);
        private static readonly Color Blood = new Color(0.70f, 0.07f, 0.07f);

        private readonly List<ShelterIntruder> _intruders = new List<ShelterIntruder>();
        private int _total, _stolen, _scared;
        private float _timeLeft, _nextSpawn;
        private bool _running, _over;

        private TextMeshProUGUI _suppliesText, _timerText, _messageText;
        private float _messageLeft;
        private CanvasGroup _endGroup;
        private TextMeshProUGUI _endTitle, _endStats;
        private GameObject _endFirstOption;
        private float[] _lampIntensity;
        private float[] _lampFlicker;
        private Transform _bat, _cam;
        private Quaternion _batRest;
        private bool _swinging;

        public int SuppliesLeft => Mathf.Max(0, _total - _stolen);
        public int TotalSupplies => _total;
        public int Stolen => _stolen;
        public int ScaredOff => _scared;
        public bool IsRunning => _running;
        public bool IsOver => _over;
        public float TimeLeft => _timeLeft;
        public int IntrudersInside { get { _intruders.RemoveAll(i => i == null); return _intruders.Count; } }

        // ---------- Unity ----------

        private IEnumerator Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.StartGame(); // playing state, cursor locked
            EnsureEventSystem();
            BuildHud();
            BuildBat();

            _lampIntensity = new float[flickerLights != null ? flickerLights.Length : 0];
            _lampFlicker = new float[_lampIntensity.Length];
            for (int i = 0; i < _lampIntensity.Length; i++)
                if (flickerLights[i] != null) _lampIntensity[i] = flickerLights[i].intensity;

            yield return null; // shelves build and fill themselves in their Start
            yield return null;

            foreach (Shelf s in FindObjectsByType<Shelf>()) _total += s.Stock;
            if (_total == 0) Debug.LogWarning("[Shelter] No supplies on the shelves — nothing to protect.", this);

            _timeLeft = surviveSeconds;
            _nextSpawn = firstIntruderDelay;
            _running = true;
            Message("Protect your supplies.\nLeft click or E: hit a thief. 2-3 hits stop them.", 5f);
            UpdateHud();
        }

        private void Update()
        {
            Flicker();
            UpdateBat();
            UpdateMessage();
            if (!_running || _over) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                _timeLeft = 0f;
                EndRound(win: true);
                return;
            }

            _nextSpawn -= Time.deltaTime;
            _intruders.RemoveAll(i => i == null);
            if (_nextSpawn <= 0f && _intruders.Count < maxIntrudersAtOnce)
            {
                int count = Mathf.Min(Random.Range(groupSize.x, groupSize.y + 1), maxIntrudersAtOnce - _intruders.Count);
                StartCoroutine(SpawnGroup(count));
                float progress = 1f - _timeLeft / surviveSeconds; // 0 → 1 during the round
                _nextSpawn = Mathf.Lerp(spawnInterval.x, spawnInterval.y, progress) * Random.Range(0.8f, 1.2f);
            }

            UpdateHud();
        }

        // ---------- Called by intruders ----------

        public void OnIntruderGrabbed(ShelterIntruder who, Game.Interaction.Pickup item) =>
            Message($"Someone grabbed {Pretty(item.ItemId)}!", 2.5f, Blood);

        /// <summary>A hit that didn't finish them (they have 2–3 hits).</summary>
        public void OnIntruderHit(ShelterIntruder who, bool droppedItem)
        {
            Swing(); // pressing E on a thief also swings the bat
            Message(droppedItem ? "They dropped it! Grab it before someone else does." : "Hit!", 1.5f);
        }

        /// <summary>The last hit: knocked down or running away scared.</summary>
        public void OnIntruderStopped(ShelterIntruder who, bool knockedDown)
        {
            _scared++;
            Swing();
            Message(knockedDown ? "Down." : "Ran off scared.", 1.5f);
        }

        public void OnSupplyStolen(ShelterIntruder who, string itemId)
        {
            if (_over) return;
            _stolen++;
            UpdateHud();
            Message($"{Pretty(itemId)} is gone.", 3f, Blood);
            if (_total > 0 && _stolen >= _total) EndRound(win: false);
        }

        public void OnIntruderGone(ShelterIntruder who) => _intruders.Remove(who);

        // ---------- Round ----------

        /// <summary>A small group walks in one after another (0.7 s apart, side by side).</summary>
        private IEnumerator SpawnGroup(int count)
        {
            Message(count > 1 ? $"{count} of them are coming in..." : "Someone is coming in...", 2.5f);
            for (int i = 0; i < count && !_over; i++)
            {
                float side = count > 1 ? Mathf.Lerp(-0.5f, 0.5f, i / (float)(count - 1)) : 0f;
                SpawnIntruder(side);
                yield return new WaitForSeconds(0.7f);
            }
        }

        private void SpawnIntruder(float sideOffset)
        {
            if (entrance == null) return;
            Vector3 pos = entrance.position + entrance.right * sideOffset;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;

            var go = new GameObject("Intruder", typeof(NavMeshAgent), typeof(ShelterIntruder));
            go.transform.SetPositionAndRotation(pos, entrance.rotation);
            go.GetComponent<NavMeshAgent>().Warp(pos); // the agent was created at (0,0,0): move it onto the NavMesh here
            var intruder = go.GetComponent<ShelterIntruder>();
            intruder.Director = this;
            intruder.Exit = exit != null ? exit : entrance;
            _intruders.Add(intruder);
        }

        private void EndRound(bool win)
        {
            if (_over) return;
            _over = true;
            _running = false;
            foreach (var i in _intruders) if (i != null) i.Stop();

            if (GameManager.Instance != null)
            {
                if (win) GameManager.Instance.Win();
                else GameManager.Instance.GameOver(); // both free the cursor for the end screen
            }

            _endTitle.text = win ? winTitle : loseTitle;
            _endTitle.color = win ? Bone : Blood;
            _endStats.text = win
                ? $"Supplies kept: {SuppliesLeft}/{_total}\nIntruders stopped: {_scared}"
                : $"Held out for {Clock(surviveSeconds - _timeLeft)}\nIntruders stopped: {_scared}";
            StartCoroutine(FadeIn(_endGroup, 1.2f));
            _endGroup.interactable = true;
            _endGroup.blocksRaycasts = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_endFirstOption);
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
            else Debug.LogError($"[Shelter] Scene '{menuScene}' is not in Build Profiles.", this);
        }

        // ---------- HUD ----------

        private void UpdateHud()
        {
            if (_suppliesText == null) return;
            int left = SuppliesLeft;
            bool low = _total > 0 && left <= Mathf.CeilToInt(_total * 0.3f);
            _suppliesText.text = $"SUPPLIES  <color=#{ColorUtility.ToHtmlStringRGB(low ? Blood : Bone)}>{left}/{_total}</color>";
            _timerText.text = $"HOLD OUT  {Clock(_timeLeft)}";
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
            c.a = Mathf.Clamp01(_messageLeft / 0.5f); // fade out in the last half second
            _messageText.color = c;
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("ShelterHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            _suppliesText = Label(root, "Supplies", 40, TextAlignmentOptions.TopLeft);
            Place(_suppliesText.rectTransform, new Vector2(0, 1), new Vector2(60, -50), new Vector2(700, 60));
            _timerText = Label(root, "Timer", 40, TextAlignmentOptions.TopRight);
            Place(_timerText.rectTransform, new Vector2(1, 1), new Vector2(-60, -50), new Vector2(700, 60));
            _messageText = Label(root, "Message", 36, TextAlignmentOptions.Center);
            Place(_messageText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1400, 120));
            _messageText.fontStyle = FontStyles.Bold;

            // End screen
            var end = new GameObject("EndScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            end.transform.SetParent(root, false);
            Stretch((RectTransform)end.transform);
            end.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            _endGroup = end.GetComponent<CanvasGroup>();
            _endGroup.alpha = 0f;
            _endGroup.interactable = false;
            _endGroup.blocksRaycasts = false;

            _endTitle = Label(end.transform, "Title", 110, TextAlignmentOptions.Center);
            _endTitle.fontStyle = FontStyles.Bold;
            _endTitle.characterSpacing = 12;
            Place(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1800, 160));
            _endStats = Label(end.transform, "Stats", 40, TextAlignmentOptions.Center);
            _endStats.color = new Color(Bone.r, Bone.g, Bone.b, 0.75f);
            Place(_endStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 130));

            _endFirstOption = Option(end.transform, "TRY AGAIN", -120, TryAgain);
            Option(end.transform, "MAIN MENU", -210, ToMenu);
        }

        private GameObject Option(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Option_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(520, 76));
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible hit area
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            var text = Label(go.transform, "Label", 50, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 14;
            Stretch(text.rectTransform);
            var fx = go.AddComponent<OptionFx>();
            fx.Label = text;
            fx.Plain = label;
            text.text = label;
            text.color = new Color(Bone.r, Bone.g, Bone.b, 0.55f);
            return go;
        }

        // ---------- Bat ----------

        /// <summary>A wooden bat held in the lower right corner of the first-person view.</summary>
        private void BuildBat()
        {
            var player = FindAnyObjectByType<Game.Interaction.PlayerInteractor>();
            Camera cam = player != null && player.Camera != null ? player.Camera : Camera.main;
            if (cam == null) return;
            _cam = cam.transform;

            _bat = new GameObject("Bat").transform;
            _bat.SetParent(_cam, false);
            _bat.localPosition = new Vector3(0.34f, -0.36f, 0.62f);
            _batRest = Quaternion.Euler(-35f, -15f, 20f); // leaning forward, ready
            _bat.localRotation = _batRest;

            Color wood = new Color(0.45f, 0.30f, 0.17f);
            Color tape = new Color(0.12f, 0.11f, 0.10f);
            BatPart(PrimitiveType.Cylinder, new Vector3(0, 0.17f, 0), new Vector3(0.045f, 0.17f, 0.045f), tape);  // grip
            BatPart(PrimitiveType.Cylinder, new Vector3(0, 0.50f, 0), new Vector3(0.06f, 0.18f, 0.06f), wood);    // middle
            BatPart(PrimitiveType.Cylinder, new Vector3(0, 0.80f, 0), new Vector3(0.085f, 0.14f, 0.085f), wood);  // thick end
            BatPart(PrimitiveType.Sphere, new Vector3(0, 0.94f, 0), new Vector3(0.085f, 0.04f, 0.085f), wood);   // rounded top
            foreach (float y in new[] { 0.76f, 0.84f })                                                          // two nails
                BatPart(PrimitiveType.Cube, new Vector3(0.08f, y, 0), new Vector3(0.04f, 0.008f, 0.008f), new Color(0.5f, 0.5f, 0.5f));
        }

        private void BatPart(PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>()); // never blocks the E-ray or bumps into things
            go.transform.SetParent(_bat, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mat = new Material(r.sharedMaterial) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            r.sharedMaterial = mat;
        }

        private void UpdateBat()
        {
            if (_bat == null) return;

            // Gentle sway while idle.
            if (!_swinging)
            {
                float t = Time.time;
                _bat.localRotation = _batRest * Quaternion.Euler(Mathf.Sin(t * 1.7f) * 2f, Mathf.Sin(t * 1.1f) * 2f, 0f);
            }

            if (_over || !_running) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
            {
                Swing();
                HitThiefInFront();
            }
        }

        /// <summary>Left click: the first thief the bat reaches in front of you runs off.</summary>
        private void HitThiefInFront()
        {
            if (_cam == null) return;
            var player = FindAnyObjectByType<Game.Interaction.PlayerInteractor>();
            var hits = Physics.SphereCastAll(_cam.position, 0.35f, _cam.forward, batReach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var thief = hit.collider.GetComponentInParent<ShelterIntruder>();
                if (thief == null) continue;
                if (player != null && thief.CanInteract(player)) thief.Interact(player); // same as pressing E
                return;
            }
        }

        private void Swing()
        {
            if (_bat != null && !_swinging) StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            _swinging = true;
            Quaternion up = _batRest * Quaternion.Euler(-30f, 25f, 0f);      // wind up
            Quaternion down = _batRest * Quaternion.Euler(85f, -55f, -20f); // swing across
            yield return RotateBat(_bat.localRotation, up, 0.07f);
            yield return RotateBat(up, down, 0.10f);
            yield return RotateBat(down, _batRest, 0.22f);
            _swinging = false;
        }

        private IEnumerator RotateBat(Quaternion from, Quaternion to, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                if (_bat == null) yield break;
                _bat.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t / time));
                yield return null;
            }
            if (_bat != null) _bat.localRotation = to;
        }

        // ---------- Atmosphere ----------

        private void Flicker()
        {
            if (flickerLights == null) return;
            for (int i = 0; i < flickerLights.Length; i++)
            {
                Light l = flickerLights[i];
                if (l == null) continue;
                if (_lampFlicker[i] <= 0f && Random.value < Time.deltaTime * 0.3f) _lampFlicker[i] = Random.Range(0.1f, 0.6f);
                _lampFlicker[i] -= Time.deltaTime;
                l.intensity = _lampFlicker[i] > 0f ? _lampIntensity[i] * Random.Range(0f, 0.5f) : _lampIntensity[i];
            }
        }

        // ---------- Helpers ----------

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        private static string Pretty(string id) => string.IsNullOrEmpty(id) ? "Something" : id.Replace('_', ' ').ToUpperInvariant();

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

        private static TextMeshProUGUI Label(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.color = Bone;
            t.alignment = align;
            t.characterSpacing = 6;
            t.raycastTarget = false;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Same look as the main menu: selected option turns red with "&gt;" in front.</summary>
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
    }
}
