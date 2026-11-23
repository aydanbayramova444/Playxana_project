using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// PS1 / Resident Evil 1-2 look for 3D models (the low-res screen + dithering already
    /// comes from the Player camera and Pixel_Renderer).
    ///
    /// Tools → PS1 Look → Apply To All Materials (+ Open Scene)
    ///   Switches every URP Lit / Simple Lit material in the project to "Game/PS1 Lit"
    ///   (wobbly vertices, warping textures, crisp texels). Texture and color are kept.
    ///   Objects in the open scene that use Unity's built-in default material get a PS1 copy.
    /// Tools → PS1 Look → Add Dark Fog To Open Scene    (RE-style: things fade into darkness)
    /// Tools → PS1 Look → Revert All Materials To URP Lit
    /// </summary>
    public static class PS1LookTool
    {
        const string ShaderName = "Game/PS1 Lit";
        const string DefaultsFolder = "Assets/_Project/Art/Materials/PS1";

        static readonly string[] ConvertShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Baked Lit",
        };

        // Third-party / engine folders we never touch
        static readonly string[] SkipFolders = { "Assets/TextMesh Pro", "Assets/Settings", "Assets/Plugins" };

        [MenuItem("Tools/PS1 Look/Apply To All Materials (+ Open Scene)", priority = 60)]
        public static void ApplyAll()
        {
            Shader ps1 = Shader.Find(ShaderName);
            if (ps1 == null)
            {
                EditorUtility.DisplayDialog("PS1 Look", $"Shader '{ShaderName}' not found (Art/Shaders/PS1Lit.shader).\n" +
                                                        "Check the Console for shader errors.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("PS1 Look",
                    "Switch all Lit materials in the project to the PS1 shader?\n" +
                    "This changes shared materials (food pack etc.) for the whole team.\n" +
                    "Undo any time: Tools → PS1 Look → Revert All Materials To URP Lit.",
                    "Apply", "Cancel"))
                return;

            Apply(ps1);
        }

        /// <summary>
        /// Command line (Unity closed):
        /// Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.PS1LookTool.ApplyFromCommandLine
        ///       -ps1Scene Assets/_Project/Scenes/Dev/user.unity
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-ps1Scene");
            if (i >= 0 && i + 1 < args.Length) EditorSceneManager.OpenScene(args[i + 1]);

            Shader ps1 = Shader.Find(ShaderName);
            if (ps1 == null)
            {
                Debug.LogError($"[PS1 Look] Shader '{ShaderName}' not found or has errors.");
                EditorApplication.Exit(1);
                return;
            }
            Apply(ps1);
        }

        static void Apply(Shader ps1)
        {
            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsSkipped(path)) continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || !ShouldConvert(mat.shader)) continue;

                ToPS1(mat, ps1);
                converted++;
            }

            int sceneFixed = FixOpenSceneDefaults(ps1);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PS1 Look] {converted} materials switched to {ShaderName}, " +
                      $"{sceneFixed} default-material objects fixed in the open scene. " +
                      "Tune wobble per material: 'Vertex Snap' (lower = more wobble).");
        }

        [MenuItem("Tools/PS1 Look/Add Dark Fog To Open Scene", priority = 61)]
        public static void AddFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 28f;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PS1 Look] Dark linear fog 6–28 m. Change it in Window → Rendering → Lighting → Environment.");
        }

        [MenuItem("Tools/PS1 Look/Revert All Materials To URP Lit", priority = 62)]
        public static void RevertAll()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null || mat.shader.name != ShaderName) continue;

                Texture tex = mat.GetTexture("_BaseMap");
                Color color = mat.GetColor("_BaseColor");
                mat.shader = lit;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                EditorUtility.SetDirty(mat);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PS1 Look] {n} materials back on URP Lit.");
        }

        // ------------------------------------------------------------------

        static bool IsSkipped(string path)
        {
            foreach (string f in SkipFolders)
                if (path.StartsWith(f)) return true;
            return false;
        }

        static bool ShouldConvert(Shader shader) =>
            shader != null && System.Array.IndexOf(ConvertShaders, shader.name) >= 0;

        /// <summary>Switch shader, keep texture + color, make the texture crisp.</summary>
        static void ToPS1(Material mat, Shader ps1)
        {
            Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            Vector2 scale = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : Vector2.one;
            Vector2 offset = mat.HasProperty("_BaseMap") ? mat.GetTextureOffset("_BaseMap") : Vector2.zero;
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            float cull = mat.HasProperty("_Cull") ? mat.GetFloat("_Cull") : (float)CullMode.Back;

            mat.shader = ps1;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cull", cull);
            EditorUtility.SetDirty(mat);

            MakeCrisp(tex);
        }

        static void MakeCrisp(Texture tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || IsSkipped(path)) return;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            if (ti.filterMode == FilterMode.Point && !ti.mipmapEnabled) return;

            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }

        /// <summary>Cubes/planes made in the editor use Unity's built-in material: give them a PS1 copy.</summary>
        static int FixOpenSceneDefaults(Shader ps1)
        {
            var copies = new Dictionary<Material, Material>();
            int fixedCount = 0;

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;

                Material[] mats = r.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !ShouldConvert(m.shader)) continue;
                    string path = AssetDatabase.GetAssetPath(m);
                    if (path.StartsWith("Assets/")) continue; // project materials were converted above

                    if (!copies.TryGetValue(m, out Material copy))
                    {
                        EnsureFolder(DefaultsFolder);
                        string name = $"M_PS1_{Sanitize(m.name)}_{ColorUtility.ToHtmlStringRGB(m.color)}";
                        string copyPath = $"{DefaultsFolder}/{name}.mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
                        if (copy == null)
                        {
                            copy = new Material(m);
                            AssetDatabase.CreateAsset(copy, copyPath);
                        }
                        ToPS1(copy, ps1);
                        copies[m] = copy;
                    }

                    mats[i] = copy;
                    changed = true;
                }

                if (!changed) continue;
                Undo.RecordObject(r, "PS1 materials");
                r.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return fixedCount;
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
