using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>One piece of pixel text on a Canvas (see <see cref="PixelFont"/>). Make it with <see cref="Create"/>.</summary>
    [RequireComponent(typeof(RawImage))]
    public class PixelLabel : MonoBehaviour
    {
        private RawImage _image;
        private Texture2D _tex;
        private PixelFont _font;
        private int _scale;
        private float _align;

        public string Text { get; private set; }

        /// <param name="anchor">Where on the screen (0..1). Also the pivot, so text grows away from that edge.</param>
        /// <param name="scale">Screen pixels per font pixel (at 1920x1080).</param>
        /// <param name="align">0 = left, 0.5 = centre, 1 = right (lines of multi-line text).</param>
        public static PixelLabel Create(Transform parent, string name, PixelFont font, int scale,
            Vector2 anchor, Vector2 position, Color color, float align = 0.5f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(PixelLabel));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;

            var label = go.GetComponent<PixelLabel>();
            label._image = go.GetComponent<RawImage>();
            label._image.raycastTarget = false;
            label._image.color = color;
            label._image.enabled = false; // until there is text
            label._font = font;
            label._scale = scale;
            label._align = align;
            return label;
        }

        public void Set(string text)
        {
            if (text == Text) return;
            Text = text;
            if (string.IsNullOrEmpty(text))
            {
                _image.enabled = false;
                return;
            }
            _font.Draw(text, _align, ref _tex);
            _image.texture = _tex;
            _image.enabled = true;
            _image.rectTransform.sizeDelta = new Vector2(_tex.width, _tex.height) * _scale;
        }

        public void SetColor(Color color) => _image.color = color;

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
