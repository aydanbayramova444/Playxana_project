using Game.Core;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// The nuclear-attack siren of Level 2: a mast with horns and a flashing red light on the shelter roof.
    ///   - Sound: Resources/Audio/Level2_NuclearSiren (real recording, ~85 s: wind-up, then rising and falling).
    ///   - Comes from the shelter in 3D: far away = quieter and muffled, close = loud and clear.
    ///   - Louder in the last 10 seconds, pauses with the game, dies away when the level ends.
    /// Created by <see cref="Level2Manager"/> on Play, so rebuilding the scene doesn't lose it.
    /// Volume: Level2Manager → Siren Volume.
    /// </summary>
    public class NuclearSiren : MonoBehaviour
    {
        private const string ClipPath = "Audio/Level2_NuclearSiren";
        private const float HeardUpTo = 420f;  // metres (the street is ~210 m long)
        private const float MuffledFrom = 200f; // metres: from here on it's fully muffled
        private const float Urgent = 10f;       // seconds left

        private AudioSource _source;
        private AudioLowPassFilter _muffle;
        private Transform _listener;
        private Light _beacon;
        private Renderer _lamp;
        private Material _metal, _lampOn, _lampOff;
        private float _volume;
        private bool _paused;

        /// <summary>Builds the siren on the bunker roof, next to the entrance.</summary>
        public static void Create(float volume)
        {
            var goal = FindAnyObjectByType<ShelterGoal>();
            if (goal == null) return;

            var go = new GameObject("NuclearSiren");
            go.transform.position = goal.transform.position + new Vector3(-4.5f, 2.6f, 3f); // on the roof
            var siren = go.AddComponent<NuclearSiren>();
            siren._volume = volume;
            siren.BuildMast();
            siren.BuildSound();
        }

        private void BuildMast()
        {
            Shader shader = Shader.Find("Game/PS1 Lit");
            if (shader == null) return; // sound still works without the model

            _metal = new Material(shader) { color = new Color(0.95f, 0.78f, 0.25f) };
            _lampOff = new Material(shader) { color = new Color(0.3f, 0.03f, 0.03f) };
            _lampOn = new Material(shader) { color = new Color(1f, 0.12f, 0.08f) };
            _lampOn.SetFloat("_AmbientBoost", 2f);

            Part(PrimitiveType.Cylinder, new Vector3(0, 1.75f, 0), new Vector3(0.16f, 1.75f, 0.16f), Quaternion.identity);
            for (int i = 0; i < 4; i++) // four horns, one to each side: narrow neck, wide mouth
            {
                Quaternion turn = Quaternion.Euler(0, i * 90f, 0);
                Quaternion lying = turn * Quaternion.Euler(90, 0, 0);
                Part(PrimitiveType.Cylinder, turn * new Vector3(0, 0, 0.4f) + Vector3.up * 3.2f, new Vector3(0.16f, 0.4f, 0.16f), lying);
                Part(PrimitiveType.Cylinder, turn * new Vector3(0, 0, 0.9f) + Vector3.up * 3.2f, new Vector3(0.5f, 0.14f, 0.5f), lying);
            }
            _lamp = Part(PrimitiveType.Cube, new Vector3(0, 3.65f, 0), Vector3.one * 0.35f, Quaternion.identity);

            _beacon = new GameObject("Beacon").AddComponent<Light>();
            _beacon.transform.SetParent(transform, false);
            _beacon.transform.localPosition = new Vector3(0, 3.9f, -0.7f);
            _beacon.type = LightType.Point;
            _beacon.color = new Color(1f, 0.1f, 0.05f);
            _beacon.range = 18f;
            _beacon.intensity = 6f;
            _beacon.shadows = LightShadows.None;
        }

        private Renderer Part(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _metal;
            return r;
        }

        private void BuildSound()
        {
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[NuclearSiren] Sound not found: Resources/{ClipPath}", this);
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.volume = _volume;
            _source.spatialBlend = 0.8f;   // mostly from the shelter, a little from everywhere (echoing city)
            _source.spread = 60f;
            _source.dopplerLevel = 0f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 25f;
            _source.maxDistance = HeardUpTo;
            _source.priority = 32;
            _muffle = gameObject.AddComponent<AudioLowPassFilter>();

            var ears = FindAnyObjectByType<AudioListener>();
            if (ears != null) _listener = ears.transform;
            UpdateMuffle();
            _source.Play();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            GameState state = gm != null ? gm.State : GameState.Playing;

            if (_beacon != null && state != GameState.Paused)
            {
                bool on = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
                _beacon.enabled = on;
                _lamp.sharedMaterial = on ? _lampOn : _lampOff;
            }

            if (_source == null) return;

            bool paused = state == GameState.Paused;
            if (paused != _paused)
            {
                _paused = paused;
                if (paused) _source.Pause();
                else _source.UnPause();
            }
            if (paused) return;

            float target = 0f, speed = 0.8f; // level over → the siren dies away
            if (state == GameState.Playing)
            {
                var level = Level2Manager.Instance;
                bool urgent = level != null && level.TimeLeft <= Urgent;
                target = Mathf.Clamp01(urgent ? _volume * 1.35f : _volume);
                speed = 0.2f;
            }
            _source.volume = Mathf.MoveTowards(_source.volume, target, speed * Time.unscaledDeltaTime);
            UpdateMuffle();
        }

        private void UpdateMuffle()
        {
            if (_listener == null) return;
            float far = Mathf.Clamp01(Vector3.Distance(_listener.position, transform.position) / MuffledFrom);
            _muffle.cutoffFrequency = Mathf.Lerp(11000f, 2200f, far); // distance eats the high notes
        }

        private void OnDestroy()
        {
            Destroy(_metal);
            Destroy(_lampOn);
            Destroy(_lampOff);
        }
    }
}
