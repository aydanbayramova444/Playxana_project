using Game.Core;
using Game.Gameplay;
using Game.Player;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// Trigger at the store door (Level 1). When the shopping list is complete, walking out loads Level 2
    /// (run to the bomb shelter). Before that it only shows a reminder.
    /// Also shows "Everything collected — get out!" the moment the list is done.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StoreExit : MonoBehaviour
    {
        [SerializeField] private string nextScene = "Level2";

        private ShoppingListTracker _tracker;
        private string _message = "";
        private float _messageUntil;
        private bool _loading;
        private GUIStyle _style;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Awake() => _tracker = FindAnyObjectByType<ShoppingListTracker>();

        private void OnEnable() => GameEvents.ShoppingListCompleted += OnListDone;
        private void OnDisable() => GameEvents.ShoppingListCompleted -= OnListDone;

        private void OnListDone() => Say("EVERYTHING COLLECTED — RUN TO THE EXIT!", 5f);

        private void OnTriggerEnter(Collider other)
        {
            if (_loading || other.GetComponentInParent<PlayerController>() == null) return;

            if (_tracker != null && !_tracker.IsComplete)
            {
                Say("You can't leave yet — finish the shopping list!", 2.5f);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError($"[StoreExit] Scene '{nextScene}' is not in Build Profiles.", this);
                return;
            }
            _loading = true;
            SceneLoader.Load(nextScene);
        }

        private void Say(string text, float seconds)
        {
            _message = text;
            _messageUntil = Time.time + seconds;
        }

        private void OnGUI()
        {
            if (Time.time > _messageUntil || string.IsNullOrEmpty(_message)) return;
            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(16, Screen.height / 26),
                fontStyle = FontStyle.Bold,
            };
            var r = new Rect(0, Screen.height * 0.3f, Screen.width, Screen.height * 0.1f);
            _style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), _message, _style);
            _style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(r, _message, _style);
        }
    }
}
