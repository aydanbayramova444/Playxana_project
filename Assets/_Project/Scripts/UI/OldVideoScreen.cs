using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.UI
{
    /// <summary>
    /// Shows a VideoPlayer like a PlayStation 1 FMV: low resolution, soft, blocky, 15 frames a second, a bit blue.
    /// Built by <see cref="CutscenePlayer"/>; tune it there (Inspector → Old PlayStation look).
    /// </summary>
    public class OldVideoScreen : MonoBehaviour
    {
        private const float Fps = 15f;

        private VideoPlayer _video;
        private RawImage _picture;
        private AspectRatioFitter _fitter;
        private Material _material;
        private RenderTexture _full, _small;
        private float _nextFrame, _blueTint, _blocks;
        private int _height;

        public static OldVideoScreen Create(Transform canvas, VideoPlayer video, int height, float blueTint, float blocks)
        {
            var bg = new GameObject("VideoBackground", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = Color.black; // the bars around the picture

            var go = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(OldVideoScreen));
            go.transform.SetParent(bg.transform, false);
            Stretch((RectTransform)go.transform);

            var screen = go.GetComponent<OldVideoScreen>();
            screen._picture = go.GetComponent<RawImage>();
            screen._picture.enabled = false;
            screen._fitter = go.GetComponent<AspectRatioFitter>();
            screen._fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            screen._video = video;
            screen._height = Mathf.Max(64, height);
            screen._blueTint = blueTint;
            screen._blocks = blocks;

            var shader = Resources.Load<Shader>("Shaders/OldFmv");
            if (shader != null && shader.isSupported) screen._material = new Material(shader);
            else Debug.LogWarning("[Cutscene] OldFmv shader missing — playing the video only low-res.");
            return screen;
        }

        /// <summary>Call when the video is prepared (its size is known then), before Play.</summary>
        public void Begin()
        {
            int w = Mathf.Max(2, (int)_video.width), h = Mathf.Max(2, (int)_video.height);
            float aspect = w / (float)h;
            _fitter.aspectRatio = aspect;

            _full = new RenderTexture(w, h, 0) { name = "CutsceneVideo" };
            int smallWidth = Mathf.Max(2, Mathf.RoundToInt(_height * aspect));
            // Bilinear: stretched back up it turns soft and blurry, like the console's video on a tube TV
            _small = new RenderTexture(smallWidth, _height, 0) { name = "CutsceneOldLook", filterMode = FilterMode.Bilinear };
            Clear(_full);
            Clear(_small);

            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _full;
            _picture.texture = _small;
        }

        private void Update()
        {
            if (_full == null || _video == null || !_video.isPlaying || _video.frame < 0) return;
            if (Time.unscaledTime < _nextFrame) return;
            _nextFrame = Time.unscaledTime + 1f / Fps;

            if (_material != null)
            {
                _material.SetVector("_Out", new Vector4(_small.width, _small.height, 0, 0));
                _material.SetFloat("_BlueTint", _blueTint);
                _material.SetFloat("_Blocks", _blocks);
                Graphics.Blit(_full, _small, _material);
            }
            else Graphics.Blit(_full, _small);
            _picture.enabled = true;
        }

        private void OnDestroy()
        {
            if (_video != null && _video.targetTexture == _full) _video.targetTexture = null;
            Release(_full);
            Release(_small);
            if (_material != null) Destroy(_material);
        }

        private static void Release(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Destroy(rt);
        }

        private static void Clear(RenderTexture rt)
        {
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = old;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
