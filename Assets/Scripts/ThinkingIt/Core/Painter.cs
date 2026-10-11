using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // A tiny 2D canvas (y grows downward, like the HTML canvas the web version drew its textures with).
    // Shapes are alpha-blended; text is added afterwards on the GPU with real fonts (see TexText).
    public class Painter
    {
        public readonly int w, h;
        readonly Color[] px;
        public readonly List<TexText.Item> texts = new List<TexText.Item>();

        public Painter(int w, int h, Color? bg = null)
        {
            this.w = w; this.h = h; px = new Color[w * h];
            var c = bg ?? Color.clear;
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        public Color Get(int x, int y) => px[y * w + x];

        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || c.a <= 0) return;
            int i = y * w + x;
            if (c.a >= 1) { px[i] = c; return; }
            Color d = px[i];
            float a = c.a + d.a * (1 - c.a);
            if (a <= 0) { px[i] = Color.clear; return; }
            px[i] = new Color((c.r * c.a + d.r * d.a * (1 - c.a)) / a, (c.g * c.a + d.g * d.a * (1 - c.a)) / a, (c.b * c.a + d.b * d.a * (1 - c.a)) / a, a);
        }

        public void Fill(Color c) => Rect(0, 0, w, h, c);

        public void Rect(float x, float y, float rw, float rh, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.RoundToInt(x)), y0 = Mathf.Max(0, Mathf.RoundToInt(y));
            int x1 = Mathf.Min(w, Mathf.RoundToInt(x + rw)), y1 = Mathf.Min(h, Mathf.RoundToInt(y + rh));
            if (rw > 0 && rw < 1 && x1 == x0) x1 = Mathf.Min(w, x0 + 1);
            if (rh > 0 && rh < 1 && y1 == y0) y1 = Mathf.Min(h, y0 + 1);
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Blend(xx, yy, c);
        }

        public void StrokeRect(float x, float y, float rw, float rh, float lw, Color c)
        {
            Rect(x - lw / 2, y - lw / 2, rw + lw, lw, c); Rect(x - lw / 2, y + rh - lw / 2, rw + lw, lw, c);
            Rect(x - lw / 2, y + lw / 2, lw, rh - lw, c); Rect(x + rw - lw / 2, y + lw / 2, lw, rh - lw, c);
        }

        // vertical gradient over a rect: stops are (t, color)
        public void VGradient(float x, float y, float rw, float rh, float gy0, float gy1, params (float t, Color c)[] stops)
        {
            int x0 = Mathf.Max(0, (int)x), y0 = Mathf.Max(0, (int)y), x1 = Mathf.Min(w, (int)(x + rw)), y1 = Mathf.Min(h, (int)(y + rh));
            for (int yy = y0; yy < y1; yy++)
            {
                float t = gy1 == gy0 ? 0 : (yy - gy0) / (gy1 - gy0);
                Color c = Stop(stops, t);
                for (int xx = x0; xx < x1; xx++) Blend(xx, yy, c);
            }
        }

        public void RadialGradient(float cx, float cy, float r0, float r1, params (float t, Color c)[] stops)
        {
            int x0 = Mathf.Max(0, (int)(cx - r1)), x1 = Mathf.Min(w, (int)(cx + r1) + 1), y0 = Mathf.Max(0, (int)(cy - r1)), y1 = Mathf.Min(h, (int)(cy + r1) + 1);
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    float d = Mathf.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy));
                    if (d > r1) continue;
                    Blend(xx, yy, Stop(stops, (d - r0) / Mathf.Max(0.001f, r1 - r0)));
                }
        }

        // full-canvas radial gradient (covers everything, clamps outside)
        public void RadialFill(float cx, float cy, float r0, float r1, params (float t, Color c)[] stops)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    float d = Mathf.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy));
                    Blend(xx, yy, Stop(stops, (d - r0) / Mathf.Max(0.001f, r1 - r0)));
                }
        }

        static Color Stop((float t, Color c)[] s, float t)
        {
            if (t <= s[0].t) return s[0].c;
            for (int i = 1; i < s.Length; i++)
                if (t <= s[i].t) return Color.Lerp(s[i - 1].c, s[i].c, (t - s[i - 1].t) / Mathf.Max(0.0001f, s[i].t - s[i - 1].t));
            return s[s.Length - 1].c;
        }

        public void Ellipse(float cx, float cy, float rx, float ry, float rot, Color c)
        {
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot), R = Mathf.Max(rx, ry) + 1;
            for (int yy = Mathf.Max(0, (int)(cy - R)); yy < Mathf.Min(h, (int)(cy + R) + 1); yy++)
                for (int xx = Mathf.Max(0, (int)(cx - R)); xx < Mathf.Min(w, (int)(cx + R) + 1); xx++)
                {
                    float dx = xx + 0.5f - cx, dy = yy + 0.5f - cy;
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    float k = (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry);
                    if (k <= 1) Blend(xx, yy, c);
                }
        }

        public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, 0, c);

        public void StrokeEllipse(float cx, float cy, float rx, float ry, float rot, float lw, Color c, float a0 = 0, float a1 = Mathf.PI * 2)
        {
            int n = Mathf.Max(24, (int)((rx + ry) * 1.5f));
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            Vector2 prev = default;
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + (a1 - a0) * i / n;
                float lx = Mathf.Cos(a) * rx, ly = Mathf.Sin(a) * ry;
                var p = new Vector2(cx + lx * cs - ly * sn, cy + lx * sn + ly * cs);
                if (i > 0) Line(prev.x, prev.y, p.x, p.y, lw, c);
                prev = p;
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float lw, Color c)
        {
            float r = Mathf.Max(0.5f, lw / 2);
            int minx = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - r - 1)), maxx = Mathf.Min(w - 1, (int)(Mathf.Max(x0, x1) + r + 1));
            int miny = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - r - 1)), maxy = Mathf.Min(h - 1, (int)(Mathf.Max(y0, y1) + r + 1));
            float dx = x1 - x0, dy = y1 - y0, len2 = dx * dx + dy * dy;
            for (int yy = miny; yy <= maxy; yy++)
                for (int xx = minx; xx <= maxx; xx++)
                {
                    float px0 = xx + 0.5f, py0 = yy + 0.5f;
                    float t = len2 > 0 ? Mathf.Clamp01(((px0 - x0) * dx + (py0 - y0) * dy) / len2) : 0;
                    float qx = x0 + dx * t - px0, qy = y0 + dy * t - py0;
                    float d = Mathf.Sqrt(qx * qx + qy * qy);
                    if (d <= r) Blend(xx, yy, c);
                    else if (d <= r + 1) Blend(xx, yy, new Color(c.r, c.g, c.b, c.a * (r + 1 - d)));
                }
        }

        public void Polyline(IList<Vector2> pts, float lw, Color c)
        {
            for (int i = 1; i < pts.Count; i++) Line(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, lw, c);
        }

        // even-odd scanline polygon fill
        public void Poly(IList<Vector2> pts, Color c)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            var xs = new List<float>();
            for (int yy = Mathf.Max(0, (int)minY); yy <= Mathf.Min(h - 1, (int)maxY); yy++)
            {
                float sy = yy + 0.5f; xs.Clear();
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy)) xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int xx = Mathf.Max(0, Mathf.RoundToInt(xs[k])); xx < Mathf.Min(w, Mathf.RoundToInt(xs[k + 1])); xx++) Blend(xx, yy, c);
            }
        }

        public static List<Vector2> Bezier(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, int steps = 12)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps, u = 1 - t;
                l.Add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p1);
            }
            return l;
        }

        // the web version's grain(): add the same random offset to r, g and b
        public void Grain(float amt)
        {
            for (int i = 0; i < px.Length; i++)
            {
                float n = (Random.value - 0.5f) * amt / 255f;
                var c = px[i]; px[i] = new Color(c.r + n, c.g + n, c.b + n, c.a);
            }
        }

        // canvas-style noise: fill 2x2 cells with base +/- random
        public void NoiseFill(Color baseC, float amt)
        {
            for (int y = 0; y < h; y += 2) for (int x = 0; x < w; x += 2)
                {
                    float n = (Random.value - 0.5f) * amt / 255f;
                    Rect(x, y, 2, 2, new Color(baseC.r + n, baseC.g + n, baseC.b + n, 1));
                }
        }

        public void Text(string s, float x, float y, int size, Color c, TexText.Face face = TexText.Face.Sans, bool bold = false, bool italic = false, int align = 0)
        {
            texts.Add(new TexText.Item { text = s, x = x, y = y, size = size, color = c, face = face, bold = bold, italic = italic, align = align });
        }

        public Texture2D ToTexture2D(bool repeat = true, bool mips = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips, false);
            var outPx = new Color[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, outPx, (h - 1 - y) * w, w);   // canvas rows go top-down
            tex.SetPixels(outPx);
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.Apply(mips, false);
            return tex;
        }

        // Final texture, with any text drawn in.
        public Texture Make(bool repeat = true)
        {
            var t = ToTexture2D(repeat, true);
            return texts.Count > 0 ? TexText.Bake(t, texts) : t;
        }
    }
}
