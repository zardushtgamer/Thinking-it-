using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThinkingIt.EditorTools
{
    // One-time project setup for THINKING IT. Runs by itself the first time these scripts load,
    // and can be re-run from Tools > Thinking It > Setup Project.
    //  - creates the material templates in Resources/ThinkingIt (so shaders and their variants are in builds)
    //  - links the photo textures in Assets/grafics/textures into a TextureLibrary (and marks normal maps)
    //  - puts the scenes in the build list in the right order
    [InitializeOnLoad]
    public static class ThinkingItSetup
    {
        const string Res = "Assets/Resources/ThinkingIt";
        static readonly string[] Scenes = {
            "Assets/Scenes/LoadingScene.unity", "Assets/Scenes/Title.unity",
            "Assets/Scenes/Chapter1.unity", "Assets/Scenes/Chapter2.unity", "Assets/Scenes/Chapter3.unity", "Assets/Scenes/Chapter4.unity", "Assets/Scenes/Chapter5.unity", "Assets/Scenes/Chapter6.unity" };

        static ThinkingItSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(Res + "/TextureLibrary.asset") || !File.Exists(Res + "/Lit.mat")) Setup(false);
                else if (EditorBuildSettings.scenes.Length < Scenes.Length) { BuildSettings(); AssetDatabase.SaveAssets(); }
            };
        }

        [MenuItem("Tools/Thinking It/Setup Project")]
        public static void SetupMenu() => Setup(true);

        [MenuItem("Tools/Thinking It/Play From Start")]
        public static void PlayFromStart()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scenes[0]);
                EditorApplication.isPlaying = true;
            }
        }

        static void Setup(bool verbose)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Res)) AssetDatabase.CreateFolder("Assets/Resources", "ThinkingIt");

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit && unlit)
            {
                Mat("Lit", lit, m => { });
                Mat("LitEmissive", lit, m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
                Mat("LitNormal", lit, m => { m.EnableKeyword("_NORMALMAP"); });
                Mat("LitTransparent", lit, m => Mats.MakeTransparent(m, false));
                Mat("Unlit", unlit, m => { });
                Mat("UnlitTransparent", unlit, m => Mats.MakeTransparent(m, false));
                Mat("UnlitAdditive", unlit, m => Mats.MakeTransparent(m, true));
                Mat("ParticleAdditive", particles ? particles : unlit, m => Mats.MakeTransparent(m, true));
            }
            else Debug.LogWarning("[Thinking It] URP shaders not found. Is the Universal Render Pipeline active?");

            BuildTextureLibrary();
            BuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (verbose) EditorUtility.DisplayDialog("Thinking It", "Setup complete.\n\nOpen Assets/Scenes/LoadingScene and press Play,\nor use Tools > Thinking It > Play From Start.", "OK");
            Debug.Log("[Thinking It] Project setup complete.");
        }

        static void Mat(string name, Shader shader, System.Action<Material> configure)
        {
            string path = $"{Res}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = !m;
            if (isNew) m = new Material(shader) { name = name };
            else m.shader = shader;
            configure(m);
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        }

        static void BuildTextureLibrary()
        {
            string path = Res + "/TextureLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<TextureLibrary>(path);
            if (!lib) { lib = ScriptableObject.CreateInstance<TextureLibrary>(); AssetDatabase.CreateAsset(lib, path); }

            var all = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/grafics" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            TextureLibrary.Set Find(string stem)
            {
                var s = new TextureLibrary.Set();
                foreach (var p in all)
                {
                    var file = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    if (!file.StartsWith(stem)) continue;
                    if (file.Contains("_diff")) s.diff = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    else if (file.Contains("_nor")) { MarkNormal(p); s.nor = AssetDatabase.LoadAssetAtPath<Texture2D>(p); }
                    else if (file.Contains("_rough")) s.rough = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
                return s;
            }
            lib.barkWillow = Find("bark_willow");
            lib.beigeWall = Find("beige_wall_001");
            lib.brownLeather = Find("brown_leather");
            lib.brownPlanks = Find("brown_planks_05");
            lib.brushedConcrete = Find("brushed_concrete");
            lib.ceilingInterior = Find("ceiling_interior");
            lib.coastSand = Find("coast_sand_01");
            lib.darkWoodenPlanks = Find("dark_wooden_planks");
            lib.decrepitWallpaper = Find("decrepit_wallpaper");
            lib.greyRoofTiles = Find("grey_roof_tiles");
            lib.laminateFloor = Find("laminate_floor_02");
            lib.leafyGrass = Find("leafy_grass");
            lib.metalPlate = Find("metal_plate");
            lib.oldLinoleum = Find("old_linoleum_flooring_01");
            lib.rustyMetal = Find("rusty_metal_02");
            EditorUtility.SetDirty(lib);
        }

        static void MarkNormal(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || imp.textureType == TextureImporterType.NormalMap) return;
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }

        static void BuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var s in Scenes) if (File.Exists(s)) list.Add(new EditorBuildSettingsScene(s, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (!Scenes.Contains(s.path) && File.Exists(s.path)) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
