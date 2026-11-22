using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// All game sound effects, synthesised in code (no audio files needed) — retro, PS1-ish bleeps and thuds.
    ///
    ///   GameAudio.Play(Sfx.Pickup);                     // 2D (UI-like)
    ///   GameAudio.PlayAt(Sfx.Clatter, somePosition);     // 3D, in the world
    ///   GameAudio.Loop(Sfx.Hum, 0.15f);                  // background loop (until the scene changes)
    ///
    /// Also listens to the team's GameEvents, so picking up / dropping / placing items makes a sound in every scene.
    /// Created automatically before the first scene — nothing to put in scenes.
    /// </summary>
    public enum Sfx
    {
        Pickup, Drop, Register, Whoosh, Hit, KnockDown, Clatter, Crash, Tick, Alarm, Win, Lose, Hum, Drone, Steal,
        Siren, Explosion
    }

    public class GameAudio : MonoBehaviour
    {
        private const int Rate = 22050; // low sample rate = a bit crunchy, fits the PS1 look

        private static GameAudio _instance;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource _oneShots;
        private readonly List<AudioSource> _loops = new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[GameAudio]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameAudio>();
        }

        // ---------- Public API ----------

        public static void Play(Sfx sfx, float volume = 0.6f)
        {
            if (_instance == null) return;
            _instance._oneShots.PlayOneShot(_instance.Clip(sfx), volume);
        }

        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 0.8f)
        {
            if (_instance == null) return;
            var go = new GameObject($"Sfx_{sfx}");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = _instance.Clip(sfx);
            src.volume = volume;
            src.spatialBlend = 1f;          // 3D
            src.minDistance = 2f;
            src.maxDistance = 25f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.pitch = Random.Range(0.92f, 1.08f);
            src.Play();
            Destroy(go, src.clip.length / src.pitch + 0.1f);
        }

        /// <summary>A looping background sound. Stops by itself when the scene changes.</summary>
        public static void Loop(Sfx sfx, float volume)
        {
            if (_instance == null) return;
            var src = _instance.gameObject.AddComponent<AudioSource>();
            src.clip = _instance.Clip(sfx);
            src.loop = true;
            src.volume = volume;
            src.Play();
            _instance._loops.Add(src);
        }

        // ---------- Unity ----------

        private void Awake()
        {
            _oneShots = gameObject.AddComponent<AudioSource>();
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (_, __) => StopLoops();
        }

        private void OnEnable()
        {
            GameEvents.ItemPickedUp += OnPickedUp;
            GameEvents.ItemDropped += OnDropped;
            GameEvents.ItemPlaced += OnPlaced;
        }

        private void OnDisable()
        {
            GameEvents.ItemPickedUp -= OnPickedUp;
            GameEvents.ItemDropped -= OnDropped;
            GameEvents.ItemPlaced -= OnPlaced;
        }

        private void OnPickedUp(GameObject item, string id) => Play(Sfx.Pickup, 0.5f);
        private void OnDropped(GameObject item, string id) => PlayAt(Sfx.Drop, item != null ? item.transform.position : Vector3.zero, 0.6f);
        // Ka-ching only for the shopping cart (shelves also raise ItemPlaced, e.g. while filling up at the start).
        private void OnPlaced(GameObject item, string id, GameObject receiver)
        {
            if (receiver == null) return;
            var slot = receiver.GetComponent<Game.Interaction.ItemSlot>();
            bool cart = receiver.name.ToLowerInvariant().Contains("cart") || (slot != null && slot.SlotId == "cart");
            if (cart) Play(Sfx.Register, 0.55f);
            else if (Time.timeSinceLevelLoad > 1f) PlayAt(Sfx.Drop, receiver.transform.position, 0.4f); // put back on a shelf
        }

        private void StopLoops()
        {
            foreach (var l in _loops) if (l != null) Destroy(l);
            _loops.Clear();
        }

        // ---------- Synthesis ----------

        private AudioClip Clip(Sfx sfx)
        {
            if (_clips.TryGetValue(sfx, out var c)) return c;
            c = Make(sfx);
            _clips[sfx] = c;
            return c;
        }

        private static AudioClip Make(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Pickup:   return Tone("pickup", 0.09f, t => Sq(Lerp(520, 980, t / 0.09f), t) * Env(t, 0.09f) * 0.5f);
                case Sfx.Drop:     return Tone("drop", 0.18f, t => (Sin(Lerp(140, 60, t / 0.18f), t) + Noise() * 0.3f) * Env(t, 0.18f, 0.002f));
                case Sfx.Register: return Tone("register", 0.45f, t =>
                                       (t < 0.08f ? Sq(1320, t) : Sq(1760, t)) * Env(t, 0.45f) * 0.35f + Noise() * Env(t, 0.06f) * 0.25f);
                case Sfx.Whoosh:   return Tone("whoosh", 0.22f, t => LowNoise(t, 0.22f) * Mathf.Sin(Mathf.PI * t / 0.22f) * 0.7f);
                case Sfx.Hit:      return Tone("hit", 0.2f, t => (Sin(Lerp(110, 45, t / 0.2f), t) * 0.9f + Noise() * Env(t, 0.03f)) * Env(t, 0.2f, 0.001f));
                case Sfx.KnockDown:return Tone("down", 0.55f, t => (Sin(Lerp(80, 30, t / 0.55f), t) + Noise() * 0.25f * Env(t, 0.1f)) * Env(t, 0.55f, 0.002f));
                case Sfx.Clatter:  return Tone("clatter", 0.25f, t => Noise() * Env(t, 0.25f) * (0.5f + 0.5f * Mathf.Sin(t * 260f)) * 0.6f);
                case Sfx.Crash:    return Tone("crash", 0.5f, t => (Noise() * 0.7f + Sq(Lerp(300, 120, t / 0.5f), t) * 0.2f) * Env(t, 0.5f, 0.001f));
                case Sfx.Tick:     return Tone("tick", 0.05f, t => Sq(1800, t) * Env(t, 0.05f) * 0.4f);
                case Sfx.Alarm:    return Tone("alarm", 1.2f, t => Sq(Mathf.Repeat(t, 0.4f) < 0.2f ? 880 : 660, t) * Env(t, 1.2f, 0.01f) * 0.35f);
                case Sfx.Win:      return Tone("win", 0.9f, t => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.15f) * Env(t, 0.9f, 0.01f) * 0.4f);
                case Sfx.Lose:     return Tone("lose", 1.3f, t => Arp(t, new[] { 392f, 349f, 311f, 233f }, 0.28f) * Env(t, 1.3f, 0.01f) * 0.45f);
                case Sfx.Steal:    return Tone("steal", 0.6f, t => (Sin(Lerp(300, 120, t / 0.6f), t) + Sq(Lerp(300, 120, t / 0.6f) * 0.5f, t) * 0.3f) * Env(t, 0.6f, 0.01f) * 0.45f);
                case Sfx.Hum:      return Tone("hum", 2f, t => (Sin(60, t) * 0.5f + Sin(120, t) * 0.3f + Sin(180, t) * 0.12f + Noise() * 0.04f), loopable: true);
                case Sfx.Siren:    return Tone("siren", 8f, t => // air-raid wail: slowly rises and falls
                                       Sin(Lerp(380f, 760f, 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 8f)), t) * 0.5f
                                       + Sin(Lerp(380f, 760f, 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 8f)) * 2f, t) * 0.12f, loopable: true);
                case Sfx.Explosion:return Tone("explosion", 2.2f, t => (LowNoise(t, 2.2f) * 0.9f + Sin(Lerp(45f, 25f, t / 2.2f), t) * 0.8f)
                                       * Mathf.Clamp01(t / 0.03f) * Mathf.Pow(Mathf.Clamp01(1f - t / 2.2f), 1.5f));
                case Sfx.Drone:    return Tone("drone", 4f, t => (Sin(55, t) * 0.5f + Sin(55.5f, t) * 0.4f + Sin(82.5f, t) * 0.15f + LowNoise(t, 4f) * 0.08f), loopable: true);
            }
            return Tone("silence", 0.05f, t => 0f);
        }

        private delegate float Wave(float t);

        private static AudioClip Tone(string name, float seconds, Wave wave, bool loopable = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
            if (!loopable)
            {
                int fade = Mathf.Min(64, n); // no click at the end
                for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Sq(float hz, float t) => Mathf.Repeat(hz * t, 1f) < 0.5f ? 1f : -1f;
        private static float Lerp(float a, float b, float k) => Mathf.Lerp(a, b, k);
        private static float Noise() => Random.value * 2f - 1f;

        /// <summary>Quick attack, then fades to 0 at <paramref name="len"/>.</summary>
        private static float Env(float t, float len, float attack = 0.005f) =>
            Mathf.Clamp01(t / attack) * Mathf.Pow(Mathf.Clamp01(1f - t / len), 2f);

        // Smoothed noise (cheap low-pass) — wind / whoosh.
        private static float _lp;
        private static float LowNoise(float t, float len)
        {
            if (t == 0f) _lp = 0f;
            _lp = Mathf.Lerp(_lp, Noise(), 0.12f);
            return _lp * 3f;
        }

        private static float Arp(float t, float[] notes, float step)
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            return Sq(notes[i], t) * 0.6f + Sin(notes[i], t) * 0.4f;
        }
    }
}
