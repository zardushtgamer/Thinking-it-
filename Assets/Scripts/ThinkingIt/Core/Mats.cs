using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThinkingIt
{
    // Material factory. Templates live in Resources/ThinkingIt (created by the editor setup) so the
    // shaders and their keyword variants are always included in builds; without them we fall back to Shader.Find.
    public static class Mats
    {
        static Material litT, litTransT, unlitT, unlitTransT, addT, particleT;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static TextureLibrary Lib;

        static Material Template(ref Material slot, string res, System.Func<Material> fallback)
        {
            if (slot) return slot;
            slot = Resources.Load<Material>("ThinkingIt/" + res);
            if (!slot) slot = fallback();
            return slot;
        }

        static Shader Find(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s) return s; }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        static Material LitTemplate => Template(ref litT, "Lit", () => new Material(Find("Universal Render Pipeline/Lit", "Standard")));
        static Material LitTransTemplate => Template(ref litTransT, "LitTransparent", () => { var m = new Material(Find("Universal Render Pipeline/Lit", "Standard")); MakeTransparent(m, false); return m; });
        static Material UnlitTemplate => Template(ref unlitT, "Unlit", () => new Material(Find("Universal Render Pipeline/Unlit", "Unlit/Color")));
        static Material UnlitTransTemplate => Template(ref unlitTransT, "UnlitTransparent", () => { var m = new Material(Find("Universal Render Pipeline/Unlit", "Unlit/Transparent")); MakeTransparent(m, false); return m; });
        static Material AddTemplate => Template(ref addT, "UnlitAdditive", () => { var m = new Material(Find("Universal Render Pipeline/Unlit", "Unlit/Transparent")); MakeTransparent(m, true); return m; });
        public static Material ParticleTemplate => Template(ref particleT, "ParticleAdditive", () => { var m = new Material(Find("Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit")); MakeTransparent(m, true); return m; });

        public static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void DoubleSided(Material m)
        {
            m.SetFloat("_Cull", 0); m.SetFloat("_RenderFace", 0);
            m.doubleSidedGI = true;
        }

        static void SetColor(Material m, Color c) { m.SetColor("_BaseColor", c); m.color = c; }
        static void SetMap(Material m, Texture t, Vector2 repeat)
        {
            if (!t) return;
            m.SetTexture("_BaseMap", t); m.mainTexture = t;
            m.SetTextureScale("_BaseMap", repeat);
        }

        // MeshStandardMaterial-alike: color, roughness, metalness, optional map/normal map/emission
        public static Material Std(Color c, float rough = 0.8f, float metal = 0f, Texture map = null, Vector2? repeat = null,
                                   Texture normal = null, float normalScale = 1f, Color? emission = null, float emissionK = 1f,
                                   bool doubleSided = false, bool cache = true)
        {
            string key = null;
            if (cache)
            {
                key = $"std{c}{rough:F2}{metal:F2}{(map ? map.GetHashCode() : 0)}{repeat}{(normal ? normal.GetHashCode() : 0)}{emission}{emissionK}{doubleSided}";
                if (Mats.cache.TryGetValue(key, out var hit) && hit) return hit;
            }
            var m = new Material(LitTemplate);
            SetColor(m, c);
            m.SetFloat("_Smoothness", Mathf.Clamp01(1 - rough) * 0.85f);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_EnvironmentReflections", 0.3f);
            SetMap(m, map, repeat ?? Vector2.one);
            if (normal)
            {
                m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BumpMap", repeat ?? Vector2.one);
            }
            if (emission.HasValue) SetEmission(m, emission.Value, emissionK);
            if (doubleSided) DoubleSided(m);
            if (key != null) Mats.cache[key] = m;
            return m;
        }

        public static void SetEmission(Material m, Color c, float k)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c * k);
        }

        public static Material Std(int hex, float rough = 0.8f, float metal = 0f) => Std(MathX.Hex(hex), rough, metal);

        public static Material LitTransparent(Color c, float rough = 0.1f, float metal = 0f)
        {
            var m = new Material(LitTransTemplate);
            SetColor(m, c);
            m.SetFloat("_Smoothness", 1 - rough); m.SetFloat("_Metallic", metal);
            return m;
        }

        public static Material Unlit(Color c, Texture map = null, bool doubleSided = false)
        {
            string key = $"unlit{c}{(map ? map.GetHashCode() : 0)}{doubleSided}";
            if (cache.TryGetValue(key, out var hit) && hit) return hit;
            var m = new Material(UnlitTemplate);
            SetColor(m, c); SetMap(m, map, Vector2.one);
            if (doubleSided) DoubleSided(m);
            cache[key] = m;
            return m;
        }
        public static Material Unlit(int hex) => Unlit(MathX.Hex(hex));

        // a fresh (uncached) transparent unlit material, for things whose opacity animates
        public static Material UnlitTransparent(Color c, bool doubleSided = false, Texture map = null)
        {
            var m = new Material(UnlitTransTemplate);
            SetColor(m, c); SetMap(m, map, Vector2.one);
            if (doubleSided) DoubleSided(m);
            return m;
        }

        public static Material Additive(Color c, Texture map = null, bool doubleSided = true)
        {
            var m = new Material(AddTemplate);
            SetColor(m, c); SetMap(m, map, Vector2.one);
            if (doubleSided) DoubleSided(m);
            return m;
        }

        public static void SetAlpha(Material m, float a)
        {
            var c = m.GetColor("_BaseColor"); c.a = a; m.SetColor("_BaseColor", c);
        }

        public static void ClearCache() { cache.Clear(); }
    }
}
