using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Interaction
{
    /// <summary>
    /// Shows the crosshair and the interaction prompt ("[E] Pick up Soda").
    ///
    /// Works with zero setup: if no UI is assigned it draws a simple overlay itself.
    /// When the real HUD exists, assign a TMP text (and optionally an Image with
    /// Image Type = Filled for the hold-E progress) and the fallback turns off.
    /// </summary>
    [AddComponentMenu("Interaction/Interaction Prompt UI")]
    public class InteractionPromptUI : MonoBehaviour
    {
        [Tooltip("Empty = found on this object.")]
        [SerializeField] private PlayerInteractor interactor;

        [Header("Optional real UI")]
        [SerializeField] private TMP_Text label;
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] private Image progressFill;

        [Header("Fallback overlay (used when Label is empty)")]
        [SerializeField] private bool drawCrosshair = true;
        [SerializeField] private int fontSize = 22;

        private GUIStyle _style;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        }

        private void LateUpdate()
        {
            if (interactor == null) return;

            if (label != null) label.text = interactor.Prompt;

            if (progressFill != null)
            {
                progressFill.fillAmount = interactor.HoldProgress;
                progressFill.enabled = interactor.HoldProgress > 0f;
            }
        }

        private void OnGUI()
        {
            if (label != null || interactor == null || !interactor.isActiveAndEnabled) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (drawCrosshair)
            {
                Color c = interactor.CurrentTarget != null ? Color.yellow : new Color(1f, 1f, 1f, 0.6f);
                DrawRect(new Rect(cx - 2f, cy - 2f, 4f, 4f), c);
            }

            if (interactor.HoldProgress > 0f)
            {
                var bar = new Rect(cx - 60f, cy + 20f, 120f, 8f);
                DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
                DrawRect(new Rect(bar.x, bar.y, bar.width * interactor.HoldProgress, bar.height), Color.yellow);
            }

            string text = interactor.Prompt;
            if (string.IsNullOrEmpty(text)) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
            };
            _style.fontSize = fontSize;

            var rect = new Rect(cx - 300f, cy + 40f, 600f, 80f);

            // Simple shadow so it's readable on any background
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _style);
            _style.normal.textColor = Color.white;
            GUI.Label(rect, text, _style);
        }

        private static void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
