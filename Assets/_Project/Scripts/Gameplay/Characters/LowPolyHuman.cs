using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay
{
    /// <summary>
    /// A low-poly human body for NPCs (uses the rigged Floreswa male models), with a simple
    /// walk cycle done in code: legs and arms swing with the NavMeshAgent's speed, the body bobs.
    ///
    ///   LowPolyHuman.Build(npcRoot, model, outfit, grime) → adds the body under the NPC.
    ///   outfit: colour for the T-shirt (null = keep the model's own).
    ///   grime 0..1: 0 = clean shopper, 1 = filthy survivor (darker, dirtier, less colour).
    ///   CarryPose = true → both arms forward (holding something).
    /// </summary>
    public class LowPolyHuman : MonoBehaviour
    {
        [Tooltip("Degrees the legs swing at full walking speed")]
        [SerializeField] private float legSwing = 22f;
        [SerializeField] private float armSwing = 18f;
        [Tooltip("The models stand in an A-pose; this lowers the arms to the sides (degrees)")]
        [SerializeField] private float armsDown = 48f;
        [Tooltip("Steps per metre walked")]
        [SerializeField] private float stride = 0.9f;

        /// <summary>Both arms reach forward (carrying something).</summary>
        public bool CarryPose { get; set; }

        private NavMeshAgent _agent;
        private Transform _thighL, _thighR, _shinL, _shinR, _armL, _armR;
        private Quaternion _thighLRest, _thighRRest, _shinLRest, _shinRRest, _armLRest, _armRRest;
        private Vector3 _restPos;
        private float _phase, _amount;

        /// <summary>Builds the body as a child "Body" of <paramref name="npc"/>, feet at its origin.</summary>
        public static LowPolyHuman Build(Transform npc, GameObject model, float height = 1.75f,
            Color? outfit = null, float grime = 0f)
        {
            var body = Instantiate(model, npc);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            foreach (var c in body.GetComponentsInChildren<Collider>()) Kill(c);
            foreach (var a in body.GetComponentsInChildren<Animator>()) a.enabled = false; // we pose the bones ourselves

            // Scale to a real height and stand the feet on the NPC's origin.
            Bounds b = BodyBounds(body);
            if (b.size.y > 0.01f) body.transform.localScale *= height / b.size.y;
            b = BodyBounds(body);
            body.transform.position += Vector3.up * (npc.position.y - b.min.y);

            Recolor(body, outfit, grime);

            var human = body.AddComponent<LowPolyHuman>();
            human._restPos = body.transform.localPosition;
            human.FindBones();
            return human;
        }

        private void Awake() => _restPos = transform.localPosition;

        private void Start()
        {
            _agent = GetComponentInParent<NavMeshAgent>();
            _restPos = transform.localPosition;
        }

        private void LateUpdate()
        {
            float speed = _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;
            float target = Mathf.Clamp01(speed / 2.5f);
            _amount = Mathf.MoveTowards(_amount, target, Time.deltaTime * 4f); // ease in/out of walking
            _phase += speed * stride * Mathf.PI * Time.deltaTime;

            ApplyPose(_phase, _amount);
        }

        /// <summary>Poses the bones: phase = where in the step cycle, amount 0 (standing) .. 1 (full walk).</summary>
        public void ApplyPose(float phase, float amount)
        {
            float s = Mathf.Sin(phase) * amount;
            float leg = legSwing, arm = armSwing;

            Swing(_thighL, _thighLRest, s * leg);
            Swing(_thighR, _thighRRest, -s * leg);
            Swing(_shinL, _shinLRest, -Mathf.Max(0f, -s) * leg * 1.2f);  // knee bends (lower leg back) as the leg swings back
            Swing(_shinR, _shinRRest, -Mathf.Max(0f, s) * leg * 1.2f);

            if (CarryPose)
            {
                Swing(_armL, _armLRest, 65f); // both arms forward, holding something
                Swing(_armR, _armRRest, 65f);
            }
            else
            {
                Swing(_armL, _armLRest, -s * arm);  // arms opposite to the legs
                Swing(_armR, _armRRest, s * arm);
            }

            // Small bob: up twice per step cycle.
            transform.localPosition = _restPos + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * 0.035f * amount);
        }

        // ---------- Setup ----------

        private void FindBones()
        {
            _thighL = Find("thigh.L"); _thighR = Find("thigh.R");
            _shinL = Find("shin.L"); _shinR = Find("shin.R");
            _armL = Find("upper_arm.L"); _armR = Find("upper_arm.R");
            _thighLRest = Rest(_thighL); _thighRRest = Rest(_thighR);
            _shinLRest = Rest(_shinL); _shinRRest = Rest(_shinR);
            // Rig axes (checked with test renders): upper arm local Y lowers the arm (L +, R -),
            // local X swings arms and legs forward (+) / back (-).
            _armLRest = Rest(_armL) * Quaternion.Euler(0f, armsDown, 0f);
            _armRRest = Rest(_armR) * Quaternion.Euler(0f, -armsDown, 0f);
        }

        private Transform Find(string bone)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == bone) return t;
            return null;
        }

        private static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        /// <summary>Rotates a bone around its own X axis (the Blender rig's bend axis), relative to its rest pose.</summary>
        private static void Swing(Transform bone, Quaternion rest, float degrees)
        {
            if (bone != null) bone.localRotation = rest * Quaternion.Euler(degrees, 0f, 0f);
        }

        private static Bounds BodyBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Own material copies: optional T-shirt colour, plus grime (darker, browner, greyer).</summary>
        private static void Recolor(GameObject body, Color? outfit, float grime)
        {
            Color dirt = new Color(0.22f, 0.19f, 0.15f);
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials; // instances: other NPCs keep their own colours
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    Color c = GetColor(m);
                    if (outfit.HasValue && n.Contains("tshirt")) c = outfit.Value;
                    if (grime > 0f && !n.Contains("eyes"))
                    {
                        float grey = c.grayscale;
                        c = Color.Lerp(c, new Color(grey, grey, grey), grime * 0.5f); // washed-out colours
                        c = Color.Lerp(c, dirt, grime * 0.35f) * Mathf.Lerp(1f, 0.6f, grime);
                        c.a = 1f;
                    }
                    SetColor(m, c);
                }
                r.materials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static Color GetColor(Material m) =>
            m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.white;

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.color = c;
        }
    }
}
