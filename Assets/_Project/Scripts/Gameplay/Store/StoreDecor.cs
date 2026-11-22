using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Scene 1 look: the minutes after a nuclear strike — panic buying in a half-wrecked store. Built in code on Play:
    ///   - red rotating emergency beacons; some of the store's own lamps flicker or die
    ///   - emergency-broadcast TVs on the walls (blinking alert text)
    ///   - hand-written cardboard signs, radiation warnings, hazard tape across the door
    ///   - fallen ceiling tiles, scattered papers, dust in the air, fire glow outside the door
    ///   - every 15–25 s a distant blast: rumble, camera shake, lights dip, dust falls from the ceiling
    ///   - air-raid siren in the background
    ///
    /// It MEASURES the room (walls / ceiling / door via raycasts from the middle of the shelves), so it still fits
    /// if the store is rebuilt. Decoration has no colliders: it never blocks the player, the NPCs or the E-ray.
    /// </summary>
    public class StoreDecor : MonoBehaviour
    {
        [SerializeField] private bool blasts = true;
        [SerializeField] private Vector2 blastEvery = new Vector2(15f, 25f);
        [SerializeField] private float sirenVolume = 0.12f;

        private static readonly Color AlertRed = new Color(1f, 0.12f, 0.08f);
        private static readonly Color Cardboard = new Color(0.62f, 0.47f, 0.3f);
        private static readonly Color Marker = new Color(0.08f, 0.07f, 0.07f);
        private static readonly Color HazardYellow = new Color(0.95f, 0.78f, 0.1f);
        private static readonly Color Fire = new Color(1f, 0.45f, 0.1f);

        private Transform _root;
        private Vector3 _center;
        private float _floorY, _ceilY;
        private readonly List<Light> _storeLights = new List<Light>();
        private readonly List<float> _storeLightBase = new List<float>();
        private readonly List<ParticleSystem> _dust = new List<ParticleSystem>();
        private static Material _dustMat;

        private void Start()
        {
            if (!MeasureRoom()) return;
            _root = new GameObject("StoreDecor_Built").transform;

            RenderSettings.fog = true; // smoke and dust in the air
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.32f, 0.27f, 0.22f);
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 26f;

            DimAndBreakLights();
            Beacons();
            AlertScreens();
            CardboardSigns();
            HazardTape();
            Debris();
            AirDust();
            FireOutside();
            GameAudio.Loop(Sfx.Siren, sirenVolume);
            if (blasts) StartCoroutine(Blasts());
        }

        // ---------- Measuring the store ----------

        private bool MeasureRoom()
        {
            var shelves = FindObjectsByType<Shelf>();
            if (shelves.Length == 0) return false;
            Vector3 sum = Vector3.zero;
            foreach (var s in shelves) sum += s.transform.position;
            _center = sum / shelves.Length;
            _floorY = shelves[0].transform.position.y;
            Vector3 eye = new Vector3(_center.x, _floorY + 1.6f, _center.z);
            _ceilY = Physics.Raycast(eye, Vector3.up, out RaycastHit up, 10f, ~0, QueryTriggerInteraction.Ignore)
                ? up.point.y : _floorY + 3.2f;
            return true;
        }

        /// <summary>The nearest wall (not a shelf / product / person) from the middle of the store.</summary>
        private bool Wall(Vector3 from, Vector3 dir, out RaycastHit hit)
        {
            hit = default;
            foreach (var r in Physics.RaycastAll(from, dir, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (r.collider.GetComponentInParent<Shelf>() != null) continue;
                if (r.collider.GetComponentInParent<Game.Interaction.Interactable>() != null) continue;
                if (r.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                if (hit.collider == null || r.distance < hit.distance) hit = r;
            }
            return hit.collider != null;
        }

        private Vector3 At(float dx, float h, float dz) => new Vector3(_center.x + dx, _floorY + h, _center.z + dz);

        // ---------- Pieces ----------

        /// <summary>Half the store's own lamps stutter, a couple die completely.</summary>
        private void DimAndBreakLights()
        {
            RenderSettings.ambientLight *= 0.55f;
            RenderSettings.ambientIntensity *= 0.6f;
            int i = 0;
            foreach (var l in FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional) continue;
                if (Vector3.Distance(l.transform.position, _center) > 15f) continue;
                l.intensity *= 0.6f;                                      // power is failing
                l.color = Color.Lerp(l.color, new Color(1f, 0.75f, 0.5f), 0.4f); // dirty, warmer light
                _storeLights.Add(l);
                _storeLightBase.Add(l.intensity);
                if (i % 5 == 3) l.enabled = false;                      // dead lamp
                else if (i % 2 == 0) l.gameObject.AddComponent<Stutter>(); // flickering lamp
                i++;
            }
        }

        private void Beacons()
        {
            foreach (var p in new[] { At(-4.5f, 0f, 0f), At(4.5f, 0f, 2f) })
            {
                Vector3 pos = new Vector3(p.x, _ceilY - 0.15f, p.z);
                Shape(PrimitiveType.Cylinder, pos, new Vector3(0.22f, 0.08f, 0.22f), Quaternion.identity, Mat(AlertRed, 1.5f), "Beacon");
                var spot = new GameObject("BeaconLight").AddComponent<Light>();
                spot.transform.SetParent(_root, false);
                spot.transform.position = pos + Vector3.down * 0.12f;
                spot.type = LightType.Spot;
                spot.color = AlertRed;
                spot.range = 12f;
                spot.spotAngle = 55f;
                spot.intensity = 6f;
                spot.shadows = LightShadows.None;
                spot.gameObject.AddComponent<Spin>();
            }
        }

        private void AlertScreens()
        {
            var screens = new (Vector3 dir, float dz)[] { (Vector3.forward, 0f), (Vector3.left, 1.5f), (Vector3.right, -1f) };
            foreach (var sc in screens)
            {
                if (!Wall(At(0f, 2.3f, sc.dz), sc.dir, out RaycastHit hit)) continue;
                Quaternion face = Quaternion.LookRotation(-hit.normal);
                Shape(PrimitiveType.Cube, hit.point + hit.normal * 0.12f, new Vector3(1.4f, 0.85f, 0.2f), face, Mat(new Color(0.06f, 0.06f, 0.06f), 0f), "TV");
                Shape(PrimitiveType.Cube, hit.point + hit.normal * 0.225f, new Vector3(1.25f, 0.72f, 0.01f), face, Mat(new Color(0.25f, 0.02f, 0.02f), 0.6f), "TV_Screen");
                var t = Text(hit.point + hit.normal * 0.235f, face, "!! EMERGENCY ALERT !!\n<size=70%>NUCLEAR STRIKE CONFIRMED\nSEEK SHELTER NOW</size>",
                    Color.white, 1.15f, 0.62f, 2.2f);
                t.gameObject.AddComponent<Blink>();
            }
        }

        private void CardboardSigns()
        {
            var signs = new (Vector3 dir, float dz, float h, string text)[]
            {
                (Vector3.left, -2.8f, 2.35f, "LIMIT 2\nPER PERSON"),
                (Vector3.left, 4.2f, 2.4f, "NO WATER\nLEFT"),
                (Vector3.right, 2.4f, 2.35f, "CASH\nONLY"),
                (Vector3.right, -3.6f, 2.4f, "GOD\nHELP US"),
                (Vector3.forward, -3.6f, 2.4f, "TAKE WHAT\nYOU NEED"),
                (Vector3.forward, 3.6f, 2.45f, "!! RADIATION !!\nSTAY INSIDE"),
            };
            foreach (var s in signs)
            {
                Vector3 from = At(s.dir == Vector3.forward ? s.dz : 0f, s.h, s.dir == Vector3.forward ? 0f : s.dz);
                if (!Wall(from, s.dir, out RaycastHit hit)) continue;
                bool radiation = s.text.Contains("RADIATION");
                Quaternion face = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(-9f, 9f));
                Shape(PrimitiveType.Cube, hit.point + hit.normal * 0.015f, new Vector3(0.95f, 0.65f, 0.01f), face,
                    Mat(radiation ? HazardYellow : Cardboard, 0f), radiation ? "RadiationSign" : "Cardboard");
                Text(hit.point + hit.normal * 0.025f, face, s.text, Marker, 0.85f, 0.55f, 2.4f);
            }
        }

        /// <summary>Yellow/black tape stretched across the top of the doorway and across a wall corner.</summary>
        private void HazardTape()
        {
            if (!Wall(At(0f, 2.9f, 0f), Vector3.back, out RaycastHit door)) return; // wall above the door
            Vector3 c = new Vector3(door.point.x, _floorY + 1.3f, door.point.z) + door.normal * 0.12f;
            foreach (float tilt in new[] { 12f, -9f })
                Tape(c + Vector3.up * Random.Range(-0.15f, 0.1f), door.normal, 2.8f, tilt);
        }

        /// <summary>A strip of hazard tape lying flat against a wall (normal = the wall's facing), slightly tilted.</summary>
        private void Tape(Vector3 centre, Vector3 wallNormal, float length, float tilt)
        {
            int n = Mathf.RoundToInt(length / 0.16f);
            Quaternion rot = Quaternion.LookRotation(-wallNormal) * Quaternion.Euler(0, 0, tilt);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = centre + rot * Vector3.right * ((i - n / 2f) * 0.16f);
                Shape(PrimitiveType.Cube, p, new Vector3(0.16f, 0.07f, 0.005f), rot, Mat(i % 2 == 0 ? HazardYellow : Marker, 0f), "Tape");
            }
        }

        /// <summary>Ceiling tiles on the floor, one hanging from the ceiling, papers everywhere.</summary>
        private void Debris()
        {
            Material tile = Mat(new Color(0.55f, 0.52f, 0.47f), 0f);
            Material paper = Mat(new Color(0.85f, 0.83f, 0.78f), 0f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = At(Random.Range(-6f, 6f), 0.02f, Random.Range(-4f, 5f));
                Shape(PrimitiveType.Cube, p, new Vector3(0.6f, 0.025f, 0.6f), Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f)), tile, "FallenTile");
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = At(Random.Range(-5f, 5f), 0f, Random.Range(-3f, 4f));
                p.y = _ceilY - 0.35f;
                Shape(PrimitiveType.Cube, p, new Vector3(0.6f, 0.025f, 0.6f), Quaternion.Euler(55f + Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f), tile, "HangingTile");
            }
            for (int i = 0; i < 40; i++)
            {
                Vector3 p = At(Random.Range(-6.5f, 6.5f), 0.01f, Random.Range(-4.5f, 5.5f));
                Shape(PrimitiveType.Cube, p, new Vector3(0.21f, 0.002f, 0.29f), Quaternion.Euler(0, Random.Range(0f, 360f), 0), paper, "Paper");
            }
        }

        private void AirDust()
        {
            var ps = MakeDust("AirDust", At(0f, 1.6f, 0.5f), new Vector3(13f, 3f, 10f));
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.05f;
            main.startSize = 0.03f;
            main.maxParticles = 300;
            main.gravityModifier = 0.002f;
            main.startColor = new Color(0.8f, 0.75f, 0.65f, 0.35f);
            var em = ps.emission;
            em.rateOverTime = 30f;
            ps.Play();
        }

        private void FireOutside()
        {
            if (!Wall(At(0f, 1.2f, 0f), Vector3.back, out RaycastHit door)) return;
            var l = new GameObject("FireGlow").AddComponent<Light>();
            l.transform.SetParent(_root, false);
            l.transform.position = door.point - door.normal * 2.5f + Vector3.up * 0.8f; // outside, beyond the door
            l.type = LightType.Point;
            l.color = Fire;
            l.range = 9f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            l.gameObject.AddComponent<FireFlicker>();
        }

        // ---------- Distant blasts ----------

        private IEnumerator Blasts()
        {
            yield return new WaitForSeconds(Random.Range(6f, 10f));
            while (true)
            {
                GameAudio.Play(Sfx.Explosion, 0.9f);
                StartCoroutine(Shake(0.9f, 0.06f));
                StartCoroutine(LightsDip());
                DustFall();
                yield return new WaitForSeconds(Random.Range(blastEvery.x, blastEvery.y));
            }
        }

        private IEnumerator Shake(float time, float strength)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            Transform t = cam.transform;
            Vector3 rest = t.localPosition;
            for (float e = 0; e < time; e += Time.deltaTime)
            {
                float k = strength * (1f - e / time);
                t.localPosition = rest + new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
                yield return null;
            }
            t.localPosition = rest;
        }

        private IEnumerator LightsDip()
        {
            for (int i = 0; i < _storeLights.Count; i++) if (_storeLights[i] != null) _storeLights[i].intensity = _storeLightBase[i] * 0.15f;
            yield return new WaitForSeconds(0.35f);
            for (int i = 0; i < _storeLights.Count; i++) if (_storeLights[i] != null) _storeLights[i].intensity = _storeLightBase[i];
        }

        private void DustFall()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = At(Random.Range(-5f, 5f), 0f, Random.Range(-3f, 4f));
                p.y = _ceilY - 0.1f;
                var ps = MakeDust("DustFall", p, new Vector3(1.2f, 0.05f, 1.2f));
                var main = ps.main;
                main.loop = false;
                main.duration = 1.5f;
                main.startLifetime = 2.2f;
                main.startSpeed = 0f;
                main.startSize = 0.05f;
                main.gravityModifier = 0.25f;
                main.startColor = new Color(0.75f, 0.7f, 0.6f, 0.6f);
                main.stopAction = ParticleSystemStopAction.Destroy;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });
                ps.Play();
            }
        }

        private ParticleSystem MakeDust(string objName, Vector3 pos, Vector3 box)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            if (_dustMat == null)
            {
                Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _dustMat = sh != null ? new Material(sh) : null;
            }
            if (_dustMat != null) pr.sharedMaterial = _dustMat;
            else pr.enabled = false; // better no dust than magenta squares
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            _dust.Add(ps);
            return ps;
        }

        // ---------- Helpers ----------

        private GameObject Shape(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, string objName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = objName;
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>()); // decoration never blocks anyone
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private TextMeshPro Text(Vector3 pos, Quaternion rot, string text, Color colour, float width, float height, float size)
        {
            var go = new GameObject("Text", typeof(TextMeshPro));
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.GetComponent<TextMeshPro>();
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(width, height);
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.4f;
            t.fontSizeMax = size;
            t.color = colour;
            return t;
        }

        private readonly Dictionary<(Color, float), Material> _mats = new Dictionary<(Color, float), Material>();

        private Material Mat(Color c, float emission)
        {
            if (_mats.TryGetValue((c, emission), out var m)) return m;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m = new Material(temp.GetComponent<Renderer>().sharedMaterial) { color = c };
            Destroy(temp);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            _mats[(c, emission)] = m;
            return m;
        }

        // ---------- Little behaviours ----------

        /// <summary>Rotating emergency beacon.</summary>
        private class Spin : MonoBehaviour
        {
            private void Update() => transform.rotation = Quaternion.Euler(60f, Time.time * 220f, 0f);
        }

        /// <summary>Broken lamp: mostly on, often stutters.</summary>
        private class Stutter : MonoBehaviour
        {
            private Light _l;
            private float _base, _left;
            private void Awake() { _l = GetComponent<Light>(); _base = _l.intensity; }
            private void Update()
            {
                if (_left <= 0f && Random.value < Time.deltaTime * 0.8f) _left = Random.Range(0.05f, 0.5f);
                _left -= Time.deltaTime;
                _l.intensity = _left > 0f ? _base * Random.Range(0f, 0.3f) : _base;
            }
        }

        /// <summary>Alert text blinks on and off.</summary>
        private class Blink : MonoBehaviour
        {
            private TextMeshPro _t;
            private void Awake() => _t = GetComponent<TextMeshPro>();
            private void Update() => _t.color = Mathf.Repeat(Time.time, 1.1f) < 0.6f ? Color.white : new Color(1f, 0.25f, 0.2f);
        }

        /// <summary>Fire light outside: restless orange.</summary>
        private class FireFlicker : MonoBehaviour
        {
            private Light _l;
            private float _base;
            private void Awake() { _l = GetComponent<Light>(); _base = _l.intensity; }
            private void Update() => _l.intensity = _base * (0.75f + Mathf.PerlinNoise(Time.time * 4f, 0f) * 0.5f);
        }
    }
}
