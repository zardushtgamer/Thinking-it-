using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThinkingIt
{
    // Moving between scenes always goes through the loading screen.
    public static class SceneFlow
    {
        public static string Next;
        public const string Loading = "LoadingScene", TitleScene = "Title";

        public static void Go(string scene)
        {
            Next = scene;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 1;
            if (Application.CanStreamedLevelBeLoaded(Loading)) SceneManager.LoadScene(Loading);
            else SceneManager.LoadScene(scene);
        }
        public static void Chapter(int n) => Go("Chapter" + n);
        public static void Title() => Go(TitleScene);
    }

    // Post-processing that follows dread: colour drains, contrast rises, the edges close in, the image smears.
    public class PostFX
    {
        Volume vol;
        Bloom bloom; Vignette vig; ColorAdjustments color; FilmGrain grain; ChromaticAberration chroma; LensDistortion lens; Tonemapping tone;

        public static PostFX Create(Camera cam)
        {
            var fx = new PostFX();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var go = new GameObject("PostFX");
            fx.vol = go.AddComponent<Volume>(); fx.vol.isGlobal = true; fx.vol.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            fx.vol.profile = p;
            fx.bloom = p.Add<Bloom>(true); fx.bloom.intensity.Override(0.55f); fx.bloom.threshold.Override(0.85f); fx.bloom.scatter.Override(0.6f);
            fx.vig = p.Add<Vignette>(true); fx.vig.smoothness.Override(0.55f); fx.vig.color.Override(Color.black);
            fx.color = p.Add<ColorAdjustments>(true);
            fx.grain = p.Add<FilmGrain>(true); fx.grain.type.Override(FilmGrainLookup.Medium3); fx.grain.response.Override(0.6f);
            fx.chroma = p.Add<ChromaticAberration>(true);
            fx.lens = p.Add<LensDistortion>(true);
            fx.tone = p.Add<Tonemapping>(true); fx.tone.mode.Override(TonemappingMode.Neutral);
            return fx;
        }

        public void Apply(float dread, float time, float exposure = 0)
        {
            float pulse = dread > 0.35f ? Mathf.Max(0, Mathf.Sin(time * (3 + dread * 4))) * dread * 0.15f : 0;
            vig.intensity.Override(Mathf.Clamp01(0.26f + dread * 0.3f + pulse));
            color.saturation.Override(-dread * 80);
            color.contrast.Override(dread * 35);
            color.postExposure.Override(exposure - dread * 0.35f);
            grain.intensity.Override(Mathf.Clamp01(0.22f + dread * 0.55f));
            chroma.intensity.Override(dread > 0.3f ? Mathf.Clamp01((dread - 0.3f) * 1.4f * (1 + Mathf.Sin(time * 3) * 0.3f)) : 0);
            lens.intensity.Override(-dread * 0.18f);
        }

        public void Title(float time)
        {
            vig.intensity.Override(0.5f); vig.center.Override(new Vector2(0.6f, 0.5f));
            color.saturation.Override(-10); color.contrast.Override(5); color.postExposure.Override(0);
            grain.intensity.Override(0.35f); chroma.intensity.Override(0); lens.intensity.Override(0);
        }
    }
}
