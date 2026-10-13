using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Fonts, and drawing real text into textures (signs, posters, the clock face, labels).
    // Text is drawn with the GPU into a RenderTexture on top of the painted background.
    public static class TexText
    {
        public enum Face { Serif, Sans, Mono }

        public struct Item
        {
            public string text; public float x, y; public int size; public Color color;
            public Face face; public bool bold, italic; public int align;   // 0 left, 1 center, 2 right
        }

        static Font serif, sans, mono;

        public static Font Serif => serif ? serif : (serif = OS(new[] { "Cormorant Garamond", "Georgia", "Palatino Linotype", "Book Antiqua", "Times New Roman", "Liberation Serif", "DejaVu Serif" }));
        public static Font Sans => sans ? sans : (sans = OS(new[] { "Arial", "Helvetica", "Segoe UI", "Liberation Sans", "DejaVu Sans" }));
        public static Font Mono => mono ? mono : (mono = OS(new[] { "IBM Plex Mono", "Consolas", "Courier New", "Lucida Console", "Liberation Mono", "DejaVu Sans Mono" }));
        static Font cutscene;
        // the pixel font for the chapter intro cards (Assets/Resources/ThinkingIt/CutsceneFont.ttf); falls back to the mono font
        public static Font Cutscene
        {
            get
            {
                if (cutscene) return cutscene;
                cutscene = Resources.Load<Font>("ThinkingIt/CutsceneFont");
                if (!cutscene) cutscene = Mono;
                return cutscene;
            }
        }
        public static Font Get(Face f) => f == Face.Serif ? Serif : f == Face.Mono ? Mono : Sans;

        static Font OS(string[] names)
        {
            Font f = null;
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                var use = new List<string>();
                foreach (var n in names) if (installed.Contains(n)) use.Add(n);
                if (use.Count > 0) f = Font.CreateDynamicFontFromOSFont(use.ToArray(), 32);
            }
            catch { f = null; }
            if (!f) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        public static float Measure(Font font, string s, int size, FontStyle style)
        {
            font.RequestCharactersInTexture(s, size, style);
            float wsum = 0;
            foreach (char ch in s) if (font.GetCharacterInfo(ch, out var ci, size, style)) wsum += ci.advance;
            return wsum;
        }

        public static Texture Bake(Texture2D bg, List<Item> items)
        {
            if (items == null || items.Count == 0) return bg;
            RenderTexture rt;
            try
            {
                rt = new RenderTexture(bg.width, bg.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    useMipMap = true, autoGenerateMips = false, wrapMode = bg.wrapMode, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "textTex"
                };
                rt.Create();
            }
            catch { return bg; }

            var prev = RenderTexture.active;
            Graphics.Blit(bg, rt);
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, bg.width, bg.height, 0);   // y grows downward, like the canvas
            foreach (var it in items)
            {
                var font = Get(it.face);
                if (!font || string.IsNullOrEmpty(it.text)) continue;
                var style = it.bold && it.italic ? FontStyle.BoldAndItalic : it.bold ? FontStyle.Bold : it.italic ? FontStyle.Italic : FontStyle.Normal;
                int size = Mathf.Max(4, it.size);
                font.RequestCharactersInTexture(it.text, size, style);
                float width = 0;
                foreach (char ch in it.text) if (font.GetCharacterInfo(ch, out var ci0, size, style)) width += ci0.advance;
                float pen = it.align == 1 ? it.x - width / 2 : it.align == 2 ? it.x - width : it.x;
                float baseY = it.y;
                font.material.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Color(it.color);
                foreach (char ch in it.text)
                {
                    if (!font.GetCharacterInfo(ch, out var ci, size, style)) continue;
                    float x0 = pen + ci.minX, x1 = pen + ci.maxX, yTop = baseY - ci.maxY, yBot = baseY - ci.minY;
                    GL.TexCoord(ci.uvTopLeft); GL.Vertex3(x0, yTop, 0);
                    GL.TexCoord(ci.uvTopRight); GL.Vertex3(x1, yTop, 0);
                    GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, yBot, 0);
                    GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(x0, yBot, 0);
                    pen += ci.advance;
                }
                GL.End();
            }
            GL.PopMatrix();
            RenderTexture.active = prev;
            rt.GenerateMips();
            return rt;
        }
    }
}
