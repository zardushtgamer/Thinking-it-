using UnityEngine;
using UnityEngine.UI;

namespace ThinkingIt
{
    // Makes a UI panel look like an old green monitor: scanlines, a slow rolling bright band, a vignette and a faint flicker.
    public class CrtFx : MonoBehaviour
    {
        Image bg, roll; Color bgBase; float t;
        static Texture2D linesTex, vignetteTex;

        public static CrtFx Attach(RectTransform panel, Image background)
        {
            var fx = panel.gameObject.AddComponent<CrtFx>();
            fx.bg = background; fx.bgBase = background ? background.color : Color.clear;

            if (!linesTex)
            {
                linesTex = new Texture2D(1, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                linesTex.SetPixels(new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.3f) });
                linesTex.Apply();
            }
            if (!vignetteTex)
            {
                const int N = 64;
                vignetteTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.95f);
                    px[y * N + x] = new Color(0, 0, 0, d * d * 0.85f);
                }
                vignetteTex.SetPixels(px); vignetteTex.Apply();
            }

            var lines = UIKit.Raw(panel, "scanlines", linesTex, Color.white);
            lines.uvRect = new Rect(0, 0, 1, 360);      // a 3 px pattern over the 1080 px reference height
            UIKit.Raw(panel, "vignette", vignetteTex, Color.white);

            fx.roll = UIKit.Img(panel, "roll", new Color(0.3f, 1f, 0.5f, 0.045f), UIKit.Solid);
            var r = fx.roll.rectTransform;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = new Vector2(0, 200);
            return fx;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (roll) roll.rectTransform.anchoredPosition = new Vector2(0, (Mathf.Repeat(t * 0.12f, 1.3f) - 0.2f) * 1080f);
            if (bg)
            {
                float n = 0.93f + 0.07f * Mathf.PerlinNoise(t * 9f, 3.1f) + (Random.value < 0.015f ? 0.08f : 0f);
                bg.color = new Color(bgBase.r * n, bgBase.g * n, bgBase.b * n, bgBase.a);
            }
        }
    }
}
