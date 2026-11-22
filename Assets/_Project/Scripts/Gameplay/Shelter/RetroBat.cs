using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// The shelter bat, PS1 style: six flat sides, a 48x64 pixel texture (dithered wood, scratches, stains,
    /// wrapped tape) and a few bent rusty nails. All drawn in code; no colliders.
    /// </summary>
    public class RetroBat : MonoBehaviour
    {
        private const int Sides = 6;
        private const int TexWidth = 48, TexHeight = 64;
        private const float Length = 0.96f;
        private const float TapeTop = 0.34f; // 0..1 along the bat

        // (height, radius) from the knob to the rounded tip
        private static readonly Vector2[] Profile =
        {
            new Vector2(0.00f, 0.036f), new Vector2(0.035f, 0.038f), new Vector2(0.05f, 0.023f),
            new Vector2(0.32f, 0.025f), new Vector2(0.56f, 0.040f), new Vector2(0.74f, 0.052f),
            new Vector2(0.92f, 0.052f), new Vector2(Length, 0.036f),
        };

        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        private readonly List<Object> _made = new List<Object>();

        /// <summary>Builds the model under <paramref name="bat"/> (pivot = bottom of the handle).</summary>
        public static void Build(Transform bat) => bat.gameObject.AddComponent<RetroBat>().BuildModel();

        private void BuildModel()
        {
            Texture2D wood = WoodTexture();
            AddPart("Model", BodyMesh(), NewMaterial(wood, Color.white));

            Material rust = NewMaterial(Texture2D.whiteTexture, new Color(0.58f, 0.43f, 0.32f));
            Mesh nail = NailMesh();
            // height, angle around the bat, bend up / down
            foreach (Vector3 n in new[] { new Vector3(0.71f, 100, 14), new Vector3(0.78f, 250, -10), new Vector3(0.84f, 170, 20), new Vector3(0.89f, 310, -6) })
            {
                Transform t = AddPart("Nail", nail, rust);
                Quaternion around = Quaternion.Euler(0, n.y, 0);
                t.localPosition = new Vector3(0, n.x, 0) + around * Vector3.forward * 0.03f;
                t.localRotation = around * Quaternion.Euler(n.z, 0, 0);
            }
        }

        private Transform AddPart(string partName, Mesh mesh, Material material)
        {
            var go = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private Material NewMaterial(Texture texture, Color color)
        {
            Shader shader = Shader.Find("Game/PS1 Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader);
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_AmbientBoost", 2f);      // the bunker is dark: keep the pixels readable
            m.SetFloat("_AffineAmount", 0.6f);    // a little texture warp
            m.SetFloat("_SnapResolution", 120f);  // and wobble
            _made.Add(m);
            return m;
        }

        // ---------- Meshes: every face has its own corners, so it is flat shaded ----------

        private Mesh BodyMesh()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < Sides; i++)
            {
                float u0 = i / (float)Sides, u1 = (i + 1) / (float)Sides;
                for (int p = 0; p < Profile.Length - 1; p++)
                {
                    Vector2 low = Profile[p], high = Profile[p + 1];
                    Vector3 a = Ring(low, u0), b = Ring(low, u1), c = Ring(high, u0), d = Ring(high, u1);
                    Vector2 ta = new Vector2(u0, low.x / Length), tb = new Vector2(u1, low.x / Length);
                    Vector2 tc = new Vector2(u0, high.x / Length), td = new Vector2(u1, high.x / Length);
                    Tri(v, uv, tris, a, c, b, ta, tc, tb);
                    Tri(v, uv, tris, b, c, d, tb, tc, td);
                }

                // caps
                Vector2 bottom = Profile[0], top = Profile[Profile.Length - 1];
                var bottomUv = new Vector2(u0, 0.02f);
                var topUv = new Vector2(u0, 0.97f);
                Tri(v, uv, tris, new Vector3(0, bottom.x, 0), Ring(bottom, u0), Ring(bottom, u1), bottomUv, bottomUv, bottomUv);
                Tri(v, uv, tris, new Vector3(0, top.x, 0), Ring(top, u1), Ring(top, u0), topUv, topUv, topUv);
            }
            return NewMesh("RetroBat", v, uv, tris);
        }

        /// <summary>Four-sided spike along +Z with a flat head.</summary>
        private Mesh NailMesh()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            var tip = new Vector3(0, 0, 0.105f);
            Vector3[] foot =
            {
                new Vector3(0.01f, 0, 0), new Vector3(0, 0.01f, 0), new Vector3(-0.01f, 0, 0), new Vector3(0, -0.01f, 0),
            };
            for (int i = 0; i < foot.Length; i++)
                Tri(v, uv, tris, foot[i], foot[(i + 1) % foot.Length], tip, Vector2.zero, Vector2.zero, Vector2.zero);
            return NewMesh("RetroNail", v, uv, tris);
        }

        private Mesh NewMesh(string meshName, List<Vector3> v, List<Vector2> uv, List<int> tris)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _made.Add(mesh);
            return mesh;
        }

        private static Vector3 Ring(Vector2 profile, float u)
        {
            float a = u * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * profile.y, profile.x, Mathf.Sin(a) * profile.y);
        }

        private static void Tri(List<Vector3> v, List<Vector2> uv, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            uv.Add(ta); uv.Add(tb); uv.Add(tc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // ---------- Pixel texture: few colours + ordered dither ----------

        private Texture2D WoodTexture()
        {
            var random = new System.Random(1998); // the same bat every time
            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "RetroBatPixels",
            };
            Color32[] wood =
            {
                new Color32(74, 42, 24, 255), new Color32(108, 64, 34, 255),
                new Color32(142, 90, 46, 255), new Color32(178, 122, 66, 255),
            };
            Color32[] tape =
            {
                new Color32(26, 24, 24, 255), new Color32(54, 50, 48, 255), new Color32(96, 90, 80, 255),
            };
            var scratch = new Color32(206, 158, 100, 255);
            var stain = new Color32(88, 22, 18, 255);

            var grain = new float[TexWidth];
            for (int x = 0; x < TexWidth; x++) grain[x] = (float)random.NextDouble();

            var px = new Color32[TexWidth * TexHeight];
            int tapeRows = Mathf.RoundToInt(TexHeight * TapeTop);
            for (int y = 0; y < TexHeight; y++)
            for (int x = 0; x < TexWidth; x++)
            {
                float threshold = (Bayer[y % 4 * 4 + x % 4] + 0.5f) / 16f;
                if (y < tapeRows)
                {
                    int wrap = (y + x / 4) % 6; // spiral
                    px[y * TexWidth + x] = wrap == 0 ? tape[0] : wrap == 5 && threshold > 0.4f ? tape[2] : tape[1];
                }
                else
                {
                    int bend = Mathf.RoundToInt(Mathf.Sin(y * 0.22f) * 1.5f);
                    float shade = 0.4f + grain[(x + bend + TexWidth) % TexWidth] * 2.1f + threshold;
                    px[y * TexWidth + x] = wood[Mathf.Clamp((int)shade, 0, wood.Length - 1)];
                }
            }

            for (int i = 0; i < 14; i++) // scratches
            {
                int x = random.Next(TexWidth), y = random.Next(tapeRows, TexHeight - 5);
                for (int k = 0, n = random.Next(2, 6); k < n; k++)
                    px[(y + k) * TexWidth + (x + k / 2) % TexWidth] = scratch;
            }
            for (int i = 0; i < 6; i++) // old dark stains near the nails
            {
                int cx = random.Next(TexWidth), cy = random.Next(TexHeight - 18, TexHeight - 4), r = random.Next(2, 5);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = (cx + dx + TexWidth) % TexWidth, y = Mathf.Clamp(cy + dy, 0, TexHeight - 1);
                    if (dx * dx + dy * dy <= r * r && Bayer[y % 4 * 4 + x % 4] < 11) px[y * TexWidth + x] = stain;
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);
            _made.Add(tex);
            return tex;
        }

        private void OnDestroy()
        {
            foreach (Object o in _made) Destroy(o);
        }
    }
}
