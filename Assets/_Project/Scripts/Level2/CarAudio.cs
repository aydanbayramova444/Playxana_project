using Game.Core;
using UnityEngine;

namespace Game.Level2
{
    /// <summary>
    /// Old-game (PS1 style) sounds for one <see cref="ChaosCar"/>, the way 90s racing games did it:
    ///   - a short engine loop, pitched up and down through 3 fake gears,
    ///   - exaggerated doppler "nyyeeOOWWW" when a car passes you,
    ///   - tyre screech on hard brakes and U-turns, horn when it comes for you (police car: siren yelp),
    ///   - crunch + glass when it hits you.
    /// Samples: Resources/Audio/Cars (11 kHz, PS1 4-bit ADPCM). Added by the car itself on Play.
    /// Volume: Level2Manager → Car Volume.
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        private const string Folder = "Audio/Cars/";
        private const float TopSpeed = 22f;        // fastest a car gets (hunting)
        private const int Gears = 3;
        private const float SpeedOfSound = 343f;
        private const float DopplerAmount = 2.5f;  // old games overdid it on purpose
        private const float HornCooldown = 3f, SkidCooldown = 1f;

        private ChaosCar _car;
        private AudioSource _engine, _effects, _impact;
        private AudioClip _skid, _horn, _crash;
        private Transform _listener;
        private Vector3 _lastListenerPos;
        private float _basePitch, _hornPitch, _hornReady, _skidReady, _pitch = 1f;
        private bool _muscle, _paused;

        private static float Volume => Level2Manager.Instance != null ? Level2Manager.Instance.CarVolume : 0.8f;

        public void Init(ChaosCar car)
        {
            _car = car;
            string model = ModelName();
            _muscle = model.Contains("Muscle");
            bool police = model.Contains("Police");

            _skid = Load("TyreSkid");
            _horn = Load(police ? "PoliceYelp" : "Horn");
            _crash = Load("Crash");

            _engine = NewSource(3f, 45f, 128);
            _engine.clip = Load(_muscle ? "EngineMuscle" : "EngineCompact");
            _engine.loop = true;
            _effects = NewSource(6f, 70f, 80);
            _impact = NewSource(6f, 70f, 16);
            _impact.spatialBlend = 0.2f; // you got hit: it's in your face

            // Every car sounds a little different
            _basePitch = Random.Range(0.9f, 1.1f);
            _hornPitch = police ? 1f : Random.Range(0.85f, 1.2f);

            var ears = FindAnyObjectByType<AudioListener>();
            if (ears != null)
            {
                _listener = ears.transform;
                _lastListenerPos = _listener.position;
            }

            if (_engine.clip == null) return;
            _engine.volume = 0f; // fades in
            _engine.timeSamples = Random.Range(0, _engine.clip.samples); // so 12 engines don't pulse together
            _engine.Play();
        }

        /// <summary>Hard brake or U-turn.</summary>
        public void Skid()
        {
            if (_skid == null || Time.time < _skidReady) return;
            _skidReady = Time.time + SkidCooldown;
            _effects.PlayOneShot(_skid, 0.7f * Volume);
        }

        /// <summary>The car is coming for the player.</summary>
        public void Horn()
        {
            if (_horn == null || Time.time < _hornReady) return;
            _hornReady = Time.time + HornCooldown;
            _effects.pitch = _hornPitch;
            _effects.PlayOneShot(_horn, 0.8f * Volume);
        }

        /// <summary>The car hit the player.</summary>
        public void Crash()
        {
            if (_crash == null) return;
            _impact.pitch = Random.Range(0.9f, 1.05f);
            _impact.PlayOneShot(_crash, Volume);
        }

        private void Update()
        {
            if (_car == null) return;

            var gm = GameManager.Instance;
            GameState state = gm != null ? gm.State : GameState.Playing;

            bool paused = state == GameState.Paused;
            if (paused != _paused)
            {
                _paused = paused;
                foreach (var s in new[] { _engine, _effects, _impact })
                {
                    if (paused) s.Pause();
                    else s.UnPause();
                }
            }
            if (paused) return;

            bool driving = state == GameState.Playing;
            float target = driving ? (_muscle ? 0.95f : 0.75f) * Volume : 0f; // level over → engines die away
            _engine.volume = Mathf.MoveTowards(_engine.volume, target, 1.5f * Time.unscaledDeltaTime);

            float dt = Time.deltaTime;
            if (!driving || dt <= 0f) return;

            _pitch = Mathf.Lerp(_pitch, EnginePitch(_car.Speed), 1f - Mathf.Exp(-12f * dt));
            _engine.pitch = _pitch * _basePitch * Doppler(dt);
        }

        /// <summary>Revs climb inside each gear and drop when it shifts up.</summary>
        private static float EnginePitch(float speed)
        {
            float k = Mathf.Clamp01(speed / TopSpeed) * Gears;
            int gear = Mathf.Min(Gears - 1, (int)k);
            return 0.65f + gear * 0.12f + (k - gear) * 0.5f;
        }

        private float Doppler(float dt)
        {
            if (_listener == null) return 1f;

            Vector3 ears = _listener.position;
            Vector3 listenerVelocity = Vector3.ClampMagnitude((ears - _lastListenerPos) / dt, 15f);
            _lastListenerPos = ears;

            // From the car's own speed, not its position: cars jump from one end of the road to the other
            Vector3 toEars = (ears - transform.position).normalized;
            float closing = Vector3.Dot(transform.forward * _car.Speed - listenerVelocity, toEars);
            return Mathf.Clamp(1f + closing * DopplerAmount / SpeedOfSound, 0.75f, 1.3f);
        }

        private string ModelName()
        {
            foreach (Transform child in transform)
                if (child.name.StartsWith("RetroCar_")) return child.name;
            return "";
        }

        private AudioSource NewSource(float min, float max, int priority)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0f; // we do our own
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = min;
            s.maxDistance = max;
            s.priority = priority;
            return s;
        }

        private AudioClip Load(string clipName)
        {
            var clip = Resources.Load<AudioClip>(Folder + clipName);
            if (clip == null) Debug.LogWarning($"[CarAudio] Sound not found: Resources/{Folder}{clipName}", this);
            return clip;
        }
    }
}
