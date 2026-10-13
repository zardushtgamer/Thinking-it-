using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 2: The Waiting Room.
    // Participant 07's practice room. The same small apartment, over and over. Each time through,
    // one thing has changed. Find it and stare at it until you're sure, and the door lets you go on.
    // The title screen drifts through this same apartment.
    // ======================================================================
    public class Chapter2 : Chapter
    {
        public override int Number => 2;
        public override AudioReverbPreset Space => AudioReverbPreset.Livingroom;
        public override string HallZone => null;
        public override float FlashBase => 0.9f;
        public override bool Wood(float x, float z) => true;
        public override float FogDensity => 0.06f;
        public override Color FogColor => new Color(0.02f, 0.016f, 0.012f);
        public override Color Ambient => new Color(0.42f, 0.45f, 0.5f) * 0.08f;
        public override bool CustomItActive => loop == LAST_LOOP && !solved;

        static readonly string[] LOOP_CHANGE = { null, null, "clock", "painting", "photo", "mirror", "bathroom", "armchair", "tv", "radio", "piano", "shoes", "it" };
        public const int LAST_LOOP = 12;
        public int loop = 1;
        public bool solved = true;
        string change;
        float eyesT, transT, tickT;
        int wrong;
        bool findHint, bathHint;

        // the things that change
        public Transform hourHand, minuteHand, secondHand;
        public Vector3 clockPos, radioPos, bathPos;
        bool clockStopped, radioOn, tvOn, pianoOn;
        GameObject armchair, stoolGo, sandalGo, mirrorFigure; Material tvScreen; Vector3 tvPos, pianoPos, stoolHome; float pianoT, tvT;
        GameObject painting; Transform bathHinge;
        Material photoMat, radioLed; Texture photoTex, photoItTex;
        GameObject page; Interactable pageIt;
        public DustCloud dust;

        // ---------------------------------------------------------------- textures
        static Texture Wallpaper() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x8a7656));
            for (int x = 0; x < W; x += 32) g.Rect(x, 0, 14, H, new Color(1, 0.94f, 0.82f, 0.05f));
            var fl = Rgba(70, 50, 30, 0.32f);
            for (int y = 0; y < H; y += 64) for (int x = 0; x < W; x += 64)
                {
                    float ox = x + ((y / 64) % 2) * 32 + 16, oy = y + 32;
                    var pts = Painter.Bezier(new Vector2(ox, oy - 20), new Vector2(ox + 16, oy - 10), new Vector2(ox + 16, oy + 10), new Vector2(ox, oy + 20));
                    pts.AddRange(Painter.Bezier(new Vector2(ox, oy + 20), new Vector2(ox - 16, oy + 10), new Vector2(ox - 16, oy - 10), new Vector2(ox, oy - 20)));
                    g.Poly(pts, fl);
                    g.Circle(ox, oy, 4, Rgba(200, 170, 120, 0.25f));
                }
            g.VGradient(0, 0, W, H, 0, H, (0, new Color(0, 0, 0, 0.05f)), (1, Rgba(20, 10, 0, 0.35f)));
            for (int i = 0; i < 3; i++) { float sx = Random.value * W; g.VGradient(sx - 6, 0, 10 + Random.value * 14, H * 0.7f, 0, H * 0.7f, (0, Rgba(60, 40, 15, 0.3f)), (1, Rgba(60, 40, 15, 0))); }
            g.Grain(14);
        });

        static Texture WoodFloor() => Paint(512, 512, (g, W, H) =>
        {
            int rows = 8; float ph = H / (float)rows;
            for (int r = 0; r < rows; r++)
            {
                float x = -Random.value * 200;
                while (x < W)
                {
                    float len = 180 + Random.value * 220, b = 70 + Random.value * 30;
                    g.Rect(x, r * ph, len, ph, Rgba(b + 30, b * 0.62f, b * 0.38f, 1));
                    for (int k = 0; k < 7; k++)
                    {
                        var pts = new List<Vector2>(); float yy = r * ph + 6 + Random.value * (ph - 12);
                        for (float xx = x; xx < x + len; xx += 8) pts.Add(new Vector2(xx, yy + Mathf.Sin(xx * 0.03f + k) * 2.5f));
                        g.Polyline(pts, 1 + Random.value * 1.5f, Rgba(40, 20, 8, 0.12f + Random.value * 0.12f));
                    }
                    g.Rect(x, r * ph, 2, ph, Rgba(20, 10, 4, 0.6f));
                    x += len;
                }
                g.Rect(0, r * ph, W, 2, Rgba(15, 8, 3, 0.7f));
            }
            g.Grain(10);
        });

        static Texture Plaster() => Paint(256, 256, (g, W, H) =>
        {
            g.Fill(Col(0xc9c0ae)); g.Grain(16);
            var pts = new List<Vector2>(); float x = 40, y = 0; pts.Add(new Vector2(x, y));
            while (y < H) { x += MathX.Jit(20); y += 12; pts.Add(new Vector2(x, y)); }
            g.Polyline(pts, 1, Rgba(80, 70, 55, 0.25f));
        });

        public static Texture DoorSlab() => Paint(256, 512, (g, W, H) =>
        {
            g.Fill(Col(0x5c3a22));
            for (int i = 0; i < 40; i++) { float xx = Random.value * W; g.Line(xx, 0, xx + MathX.Jit(20), H, 1, Rgba(30, 15, 5, 0.08f + Random.value * 0.1f)); }
            System.Action<float, float, float, float> panel = (x, y, pw, ph) =>
            {
                g.Rect(x, y, pw, ph, new Color(0, 0, 0, 0.25f));
                g.Rect(x + 6, y + 6, pw - 12, ph - 12, new Color(1, 0.86f, 0.7f, 0.08f));
                g.StrokeRect(x + 6, y + 6, pw - 12, ph - 12, 3, new Color(0, 0, 0, 0.5f));
            };
            panel(30, 30, 196, 190); panel(30, 260, 196, 220);
            g.Grain(10);
        });

        static Texture Curtain() => Paint(128, 256, (g, W, H) =>
        {
            for (int x = 0; x < W; x++) { float k = 0.6f + Mathf.Sin(x / (float)W * PI * 6) * 0.25f; g.Rect(x, 0, 1, H, Rgba(110 * k, 28 * k, 30 * k, 1)); }
            g.Grain(8);
        });

        static Texture Rug() => Paint(256, 384, (g, W, H) =>
        {
            g.Fill(Col(0x6a1c1c));
            g.StrokeRect(14, 14, W - 28, H - 28, 6, Col(0xc8a050));
            g.StrokeRect(30, 30, W - 60, H - 60, 10, Col(0x1e2a4a));
            for (int y = 70; y < H - 60; y += 50) for (int x = 70; x < W - 60; x += 50)
                    g.Poly(new List<Vector2> { new Vector2(x, y - 14), new Vector2(x + 14, y), new Vector2(x, y + 14), new Vector2(x - 14, y) }, (x + y) % 100 != 0 ? Col(0xc8a050) : Col(0x1e2a4a));
            g.Grain(18);
        });

        // a lake at dusk, painted
        public static Texture LakePainting() => Paint(512, 384, (g, W, H) =>
        {
            g.VGradient(0, 0, W, H * 0.55f, 0, H * 0.55f, (0, Col(0x2a3a5a)), (0.6f, Col(0xc8805a)), (1, Col(0xe8b880)));
            var hill = new List<Vector2> { new Vector2(0, H * 0.55f) };
            for (int x = 0; x <= W; x += 32) hill.Add(new Vector2(x, H * 0.55f - 30 - Mathf.Sin(x * 0.012f) * 40 - Random.value * 10));
            hill.Add(new Vector2(W, H * 0.55f)); g.Poly(hill, Col(0x2a3428));
            g.VGradient(0, H * 0.55f, W, H * 0.45f, H * 0.55f, H, (0, Col(0xa0705a)), (0.3f, Col(0x3a5068)), (1, Col(0x14202c)));
            for (int i = 0; i < 40; i++) g.Rect(Random.value * W, H * 0.57f + Random.value * H * 0.3f, 20 + Random.value * 40, 2, new Color(1, 0.86f, 0.7f, 0.12f));
            g.Rect(W * 0.55f, H * 0.72f, W * 0.45f, 10, Col(0x3a2618));
            for (float x = W * 0.57f; x < W; x += 26) g.Rect(x, H * 0.72f, 5, 50, Col(0x3a2618));
            for (int i = 0; i < 30; i++) { float x = Random.value * W * 0.4f; g.Line(x, H, x + MathX.Jit(10), H - 30 - Random.value * 50, 2, Col(0x1e2a18)); }
            for (int i = 0; i < 4000; i++) g.Rect(Random.value * W, Random.value * H, 3, 2, new Color(Random.value, Random.value * 0.8f, Random.value * 0.6f, 0.04f));
        });

        // an old sepia photograph: a girl and a little boy on a dock. Later, someone tall is standing in the water behind them.
        public static Texture Photo(bool withIt) => Paint(256, 200, (g, W, H) =>
        {
            g.Fill(Col(0xefe6d2));
            float ix = 12, iy = 12, iw = W - 24, ih = H - 44;
            g.VGradient(ix, iy, iw, ih * 0.5f, iy, iy + ih * 0.5f, (0, Col(0xd8c8a4)), (1, Col(0xc4b088)));
            g.VGradient(ix, iy + ih * 0.5f, iw, ih * 0.5f, iy + ih * 0.5f, iy + ih, (0, Col(0x8a7a5a)), (1, Col(0x4a3e2c)));
            g.Rect(ix, iy + ih * 0.5f - 10, iw, 10, Col(0x5a4a32));
            g.Rect(ix + 40, iy + ih * 0.66f, iw - 40, 8, Col(0x4a3a24));   // dock
            System.Action<float, float> kid = (x, ht) =>
            {
                g.Rect(x, iy + ih * 0.66f - ht, ht * 0.32f, ht, Col(0x3a2c20));
                g.Circle(x + ht * 0.16f, iy + ih * 0.66f - ht - 7, 7 * ht / 50, Col(0x3a2c20));
            };
            kid(110, 52); kid(150, 32);
            if (withIt)
            {
                var k = Col(0x0a0806); float fx = iw - 30, b = iy + ih * 0.78f;
                g.Rect(fx, b - 110, 8, 110, k); g.Ellipse(fx + 5, b - 118, 7, 9, 0.4f, k);
                g.Rect(fx - 5, b - 98, 2, 60, k); g.Rect(fx + 11, b - 98, 2, 60, k);
            }
            g.RadialGradient(W / 2, H / 2 - 14, 30, W * 0.6f, (0, new Color(0, 0, 0, 0)), (1, Rgba(40, 25, 10, 0.5f)));
            g.Text("summer '98", 18, H - 14, 14, Col(0x5a4a3a), TexText.Face.Serif, false, true);
            g.Grain(22);
        });

        static Texture ClockFace() => Paint(256, 256, (g, W, H) =>
        {
            float r = W / 2f;
            g.Fill(new Color(0, 0, 0, 0));
            g.RadialGradient(r, r, 10, r, (0, Col(0xf2ead6)), (1, Col(0xcbbf9e)));
            string[] roman = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
            for (int i = 0; i < 12; i++) { float a = i / 12f * PI * 2; g.Text(roman[i], r + Mathf.Sin(a) * 96, r - Mathf.Cos(a) * 96 + 9, 26, Col(0x2a2018), TexText.Face.Serif, true, false, 1); }
            for (int i = 0; i < 60; i++) { float a = i / 60f * PI * 2, l = i % 5 != 0 ? 6 : 12; g.Rect(r + Mathf.Sin(a) * (122 - l) - 1, r - Mathf.Cos(a) * (122 - l) - 1, 2, 2, Col(0x2a2018)); }
            g.Text("Halvard", r, r + 54, 14, Col(0x2a2018), TexText.Face.Serif, false, true, 1);
            g.Grain(8);
        });

        public static Texture DiaryPaper() => Paint(160, 200, (g, W, H) =>
        {
            g.Fill(Col(0xe8e0cc));
            for (int y = 24; y < H; y += 12) g.Line(10, y, W - 10, y, 1, Rgba(80, 90, 140, 0.35f));
            for (int y = 22; y < H - 20; y += 12) { float x = 14; while (x < W - 20) { float ww = 6 + Random.value * 18; g.Rect(x, y - 4, ww, 2, Rgba(40, 30, 25, 0.75f)); x += ww + 4; } }
            g.Grain(10);
        });

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(12, 10);
            w.Carve(1, 1, 10, 2);    // entry hallway
            w.Carve(9, 3, 10, 4);    // the turn
            w.Carve(1, 5, 10, 8);    // living room
            w[0, 1] = 'A'; w[0, 7] = 'B'; w[5, 3] = 'R';

            var lib = TextureLibrary.Find(l => l.decrepitWallpaper);
            var wallM = lib != null ? Mats.Std(MathX.Hex(0xd8c8a8), 0.9f, 0, lib.diff, Vector2.one, lib.nor, 0.8f) : Mats.Std(Color.white, 0.9f, 0, Wallpaper());
            w.CellBlocks("#ABR", wallM, "wall");
            Pl(1.2f, 2.37f, Mats.Unlit(Color.black)).At(cx(5), 1.185f, 3 * C - 0.01f).RotY(PI);   // the dark bathroom behind its door (the room itself is just wall)
            var wood = TextureLibrary.Find(l => l.darkWoodenPlanks);
            var floorM = wood != null ? Mats.Std(Color.white, 0.55f, 0, wood.diff, new Vector2(w.W, w.H), wood.nor, 0.8f) : Mats.Std(Color.white, 0.55f, 0, WoodFloor(), new Vector2(w.W / 2f, w.H / 2f));
            var plaster = TextureLibrary.Find(l => l.beigeWall);
            var ceilM = plaster != null ? Mats.Std(MathX.Hex(0xd8d0c0), 1, 0, plaster.diff, new Vector2(w.W, w.H), plaster.nor, 0.6f) : Mats.Std(Color.white, 1, 0, Plaster(), new Vector2(w.W / 2f, w.H / 2f));
            w.FloorAndCeiling(floorM, ceilM);

            var trim = Std(0xe8dcc4, 0.6f); var dark = Std(0x3a2414, 0.5f);
            w.Skirting(trim, "#AB", 0.16f, 0.04f, trim);

            // Doors: panelled slabs with frames, in front of the west wall
            var slabM = Mats.Std(Color.white, 0.55f, 0, DoorSlab());
            System.Func<int, string, GameObject> doorObj = (zCell, tagName) =>
            {
                var gd = Grp(tagName);
                Bx(0.06f, 2.37f, 1.05f, slabM, gd).At(0, 1.185f, 0);
                foreach (var z in new[] { -0.58f, 0.58f }) Bx(0.08f, 2.45f, 0.1f, trim, gd).At(0.01f, 1.22f, z);
                Bx(0.08f, 0.1f, 1.26f, trim, gd).At(0.01f, 2.42f, 0);
                Sp(0.045f, 12, 10, Mats.Std(MathX.Hex(0xc8a060), 0.3f, 0.8f), gd).At(0.07f, 1.05f, 0.38f);
                gd.At(C + 0.03f, 0, cx(zCell));
                World.Tagged(gd, tagName);
                return gd;
            };
            doorObj(1, "doorA"); doorObj(7, "doorB");
            AddInteract("doorA", "doorA", null, new Vector3(cx(0) + 0.8f, 1.2f, cx(1)), null, "open the door");
            AddInteract("doorB", "doorB", null, new Vector3(cx(0) + 0.8f, 1.2f, cx(7)), null, "open the door");

            // ---- Lights: warm lamps, a cold window. Two of them cast real shadows. ----
            var glowMat = Mats.Unlit(MathX.Hex(0xffd9a0), null, true);
            System.Action<float, float> pendant = (x, z) =>
            {
                Cy(0.01f, 0.01f, 0.7f, 6, dark).At(x, WALL_H - 0.35f, z);
                Cy(0, 0.32f, 0.28f, 20, Mats.Std(MathX.Hex(0x3a2a1c), 0.6f, 0, null, null, null, 1, null, 1, true), null, true).At(x, WALL_H - 0.82f, z);
                Sp(0.07f, 12, 10, glowMat).At(x, WALL_H - 0.9f, z).NoShadow();
            };
            pendant(cx(3), cx(1) + 1); w.PointLight(new Vector3(cx(3), WALL_H - 1.0f, cx(1) + 1), MathX.Hex(0xffb070), 2.2f, 10, true);
            pendant(cx(8), cx(1) + 1); w.PointLight(new Vector3(cx(8), WALL_H - 1.0f, cx(1) + 1), MathX.Hex(0xffb070), 1.2f, 8);
            // the floor lamp by the sofa
            var fl = Grp("floor lamp"); var lampMetal = Mats.Std(MathX.Hex(0x2a2018), 0.4f, 0.6f);
            Cy(0.025f, 0.025f, 1.6f, 8, lampMetal, fl).At(0, 0.8f, 0);
            Cy(0.2f, 0.22f, 0.04f, 16, lampMetal, fl).At(0, 0.02f, 0);
            Cy(0.22f, 0.32f, 0.36f, 20, Mats.Std(MathX.Hex(0xf0d8b0), 1, 0, null, null, null, 1, MathX.Hex(0xffb060), 0.9f, true), fl, true).At(0, 1.68f, 0).NoShadow();
            fl.At(cx(2) + 0.4f, 0, cx(5) - 0.3f); Prop(fl);
            w.PointLight(new Vector3(cx(2) + 0.4f, 1.6f, cx(5) - 0.3f), MathX.Hex(0xffa860), 2.4f, 11, true);
            // a dim sconce at the turn
            Sp(0.09f, 12, 8, Mats.Unlit(MathX.Hex(0xffc890)), null, 0, PI * 2, 0, PI / 2).At(cx(10) + C / 2 - 0.03f, 2.1f, cx(3) + 1).Rot(0, 0, PI / 2);
            w.PointLight(new Vector3(cx(10) + 0.6f, 2.0f, cx(3) + 1), MathX.Hex(0xffb070), 0.8f, 6);
            // moonlight through the window
            w.SpotLight(new Vector3(cx(6) + 1, 2.4f, 9 * C - 0.2f), new Vector3(cx(5), 0, cx(6)), MathX.Hex(0x8fa4d0), 1.6f, 14, 0.6f, 0.8f, true);

            // ---- Window and curtains on the living room's south wall ----
            var win = Grp("window");
            Pl(1.3f, 1.5f, Mats.Unlit(MathX.Hex(0x5a6e98)), win).RotY(PI);
            foreach (var b in new[] { (1.45f, 0.08f, 0f, 0.75f), (1.45f, 0.08f, 0f, -0.75f), (0.08f, 1.58f, 0.7f, 0f), (0.08f, 1.58f, -0.7f, 0f), (0.05f, 1.5f, 0f, 0f), (1.3f, 0.05f, 0f, 0f) })
                Bx(b.Item1, b.Item2, 0.08f, trim, win).At(b.Item3, b.Item4, -0.02f);
            win.At(cx(6) + 1, 1.75f, 9 * C - 0.03f);
            var curM = Mats.Std(Color.white, 0.95f, 0, Curtain(), null, null, 1, null, 1, true);
            foreach (var sx in new[] { -0.95f, 0.95f })
            {
                var cur = Curtains(0.6f, 2.4f, curM);
                cur.At(cx(6) + 1 + sx, 1.55f, 9 * C - 0.1f).RotY(PI);
            }
            Cy(0.02f, 0.02f, 2.6f, 8, Mats.Std(MathX.Hex(0x3a2a1a), 0.4f, 0.5f)).At(cx(6) + 1, 2.78f, 9 * C - 0.1f).Rot(0, 0, PI / 2);

            Furniture(trim, dark);
            ThingsThatChange(trim);

            dust = new DustCloud(w.root, DustPositions(), MathX.Hex(0xffe0b0), 0.035f, 0.55f);
            dust.driftZ = 0.25f;
        }

        List<Vector3> DustPositions()
        {
            var l = new List<Vector3>();
            for (int i = 0; i < 260; i++)
            {
                bool living = Random.value < 0.6f;
                l.Add(new Vector3(2.2f + Random.value * 19.5f, 0.3f + Random.value * 2.7f, living ? 10.2f + Random.value * 7.6f : 2.2f + Random.value * 3.6f));
            }
            return l;
        }

        GameObject Curtains(float cw, float ch, Material m)
        {
            var mesh = Object.Instantiate(MeshGen.Plane(cw, ch, 8, 1));
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i].z = Mathf.Sin(v[i].x * 18) * 0.05f;
            mesh.vertices = v; mesh.RecalculateNormals();
            return w.MeshObj(mesh, m, null, "curtain");
        }

        void Furniture(Material trim, Material dark)
        {
            var shelfWood = Std(0x4a2e1a, 0.7f);
            // rug
            w.FloorPlane(4.2f, 2.8f, Mats.Std(Color.white, 1, 0, Rug()), new Vector3(cx(5), 0.006f, cx(6) + 1), PI / 2).NoShadow();
            // sofa with cushions
            var leather = TextureLibrary.Find(l => l.brownLeather);
            var velvet = Std(0x5a2428, 0.95f);
            var sofa = Grp("sofa");
            Bx(3.4f, 0.42f, 0.95f, velvet, sofa).At(0, 0.32f, 0);
            Bx(3.4f, 0.75f, 0.25f, velvet, sofa).At(0, 0.8f, -0.38f);
            foreach (var sx in new[] { -1.75f, 1.75f }) Bx(0.2f, 0.6f, 0.95f, velvet, sofa).At(sx, 0.5f, 0);
            foreach (var sx in new[] { -1.05f, 0f, 1.05f }) Bx(1.0f, 0.16f, 0.8f, Std(0x6a2c30, 0.95f), sofa).At(sx, 0.6f, 0.05f).Rot(-0.03f, 0, 0);
            foreach (var sx in new[] { -1.4f, 1.4f }) Cy(0.04f, 0.03f, 0.12f, 8, dark, sofa).At(sx, 0.06f, 0.35f);
            sofa.At(cx(3) + C / 2, 0, cx(5) - 0.32f); Prop(sofa, 3, 5);
            // armchair
            var chairM = leather != null ? Mats.Std(MathX.Hex(0x7a8a7a), 0.9f, 0, leather.diff, Vector2.one, leather.nor, 0.7f) : Std(0x3a4a3a, 0.95f);
            var chair = Grp("armchair");
            Bx(1.0f, 0.42f, 0.9f, chairM, chair).At(0, 0.3f, 0);
            Bx(1.0f, 0.8f, 0.2f, chairM, chair).At(0, 0.8f, -0.38f);
            foreach (var sx in new[] { -0.5f, 0.5f }) Bx(0.16f, 0.55f, 0.9f, chairM, chair).At(sx, 0.48f, 0);
            chair.At(cx(7), 0, cx(8)).RotY(PI + 0.5f); Prop(chair, 7, 8); armchair = chair; AddSuspect("armchair", chair);
            // bookshelf in the corner
            var shelf = Grp("bookshelf");
            Bx(1.4f, 2.2f, 0.03f, shelfWood, shelf).At(0, 1.1f, -0.185f); foreach (var sx in new[] { -0.685f, 0.685f }) Bx(0.03f, 2.2f, 0.4f, shelfWood, shelf).At(sx, 1.1f, 0); Bx(1.4f, 0.03f, 0.4f, shelfWood, shelf).At(0, 2.185f, 0); Bx(1.4f, 0.1f, 0.4f, shelfWood, shelf).At(0, 0.05f, 0);
            int[] bookCols = { 0x6a1f1f, 0x1f3a5a, 0x2a4a2a, 0x7a6a3a, 0x3a2a4a, 0x8a7a6a };
            for (int r = 0; r < 4; r++)
            {
                Bx(1.36f, 0.03f, 0.36f, shelfWood, shelf).At(0, 0.25f + r * 0.5f, 0.03f);
                float x = -0.62f;
                while (x < 0.6f)
                {
                    float bw = 0.04f + Random.value * 0.05f, bh = 0.3f + Random.value * 0.12f;
                    Bx(bw, bh, 0.26f, Std(MathX.Pick(bookCols), 0.8f), shelf).At(x + bw / 2, 0.27f + r * 0.5f + bh / 2, 0.08f).Rot(0, 0, Random.value < 0.1f ? 0.2f : 0);
                    x += bw + 0.005f;
                }
            }
            shelf.At(cx(10), 0, cx(8) + 0.75f).RotY(PI); Prop(shelf, 10, 8);
            // side table with a plant
            var side = Grp("side table");
            Cy(0.35f, 0.35f, 0.04f, 20, shelfWood, side).At(0, 0.62f, 0);
            Cy(0.04f, 0.06f, 0.6f, 10, shelfWood, side).At(0, 0.3f, 0);
            Cy(0.13f, 0.1f, 0.22f, 14, Std(0x8a4a2a, 0.9f), side).At(0, 0.75f, 0);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * PI * 2;
                Sp(0.1f, 8, 6, Std(0x2a4a24, 0.8f), side).At(Mathf.Cos(a) * 0.1f, 0.98f + Random.value * 0.1f, Mathf.Sin(a) * 0.1f).Rot(Mathf.Sin(a) * 0.6f, a, Mathf.Cos(a) * 0.6f).Scl(0.5f, 1.6f, 0.25f);
            }
            side.At(cx(8), 0, cx(5)); Prop(side, 8, 5);
            // table with the photo frame and the day's diary page
            var table = Grp("table");
            Bx(1.4f, 0.06f, 0.9f, shelfWood, table).At(0, 0.72f, 0);
            foreach (var l in new[] { (-0.6f, -0.38f), (0.6f, -0.38f), (-0.6f, 0.38f), (0.6f, 0.38f) }) Cy(0.035f, 0.025f, 0.7f, 8, shelfWood, table).At(l.Item1, 0.35f, l.Item2);
            table.At(cx(5), 0, cx(7)); Prop(table, 5, 7);
            // coat rack by the door
            var rack = Grp("coat rack");
            Cy(0.035f, 0.045f, 1.9f, 10, shelfWood, rack).At(0, 0.95f, 0);
            Cy(0.16f, 0.3f, 1.0f, 12, Mats.Std(MathX.Hex(0x2a2a34), 0.95f, 0, null, null, null, 1, null, 1, true), rack, true).At(0.1f, 1.25f, 0);
            rack.At(cx(1) - 0.4f, 0, cx(5) - 0.4f); Prop(rack);

            // ---- More furniture: the apartment should feel lived in ----
            var brass = Mats.Std(MathX.Hex(0xb08a3a), 0.35f, 0.7f);
            var console1 = Grp("console");
            Bx(1.5f, 0.05f, 0.42f, shelfWood, console1).At(0, 0.85f, 0);
            foreach (var sx in new[] { -0.68f, 0.68f }) foreach (var sz in new[] { -0.16f, 0.16f }) Bx(0.05f, 0.85f, 0.05f, shelfWood, console1).At(sx, 0.42f, sz);
            Bx(1.4f, 0.03f, 0.36f, shelfWood, console1).At(0, 0.25f, 0);
            Sp(0.13f, 16, 8, Mats.Std(MathX.Hex(0x2a4a5a), 0.3f, 0, null, null, null, 1, null, 1, true), console1, 0, PI * 2, PI / 2, PI / 2).At(-0.4f, 1.005f, 0);
            for (int i = 0; i < 3; i++) Bx(0.06f, 0.01f, 0.02f, brass, console1).At(-0.4f + MathX.Jit(0.03f), 0.9f, MathX.Jit(0.03f)).RotY(Random.value * 3);
            for (int i = 0; i < 4; i++) Bx(0.24f, 0.006f, 0.12f, Std(0xe8e0d0, 0.9f), console1).At(0.3f + MathX.Jit(0.08f), 0.88f + i * 0.007f, MathX.Jit(0.06f)).RotY(MathX.Jit(0.4f));
            console1.At(cx(7), 0, C + 0.25f); Prop(console1, 7, 1);
            var mirror = Grp("mirror");
            Pl(0.88f, 1.06f, Mats.Std(MathX.Hex(0x1c242a), 0.08f, 0.95f), mirror);
            foreach (var b in new[] { (0.92f, 0.06f, 0f, 0.55f), (0.92f, 0.06f, 0f, -0.55f), (0.06f, 1.16f, 0.46f, 0f), (0.06f, 1.16f, -0.46f, 0f) }) Bx(b.Item1, b.Item2, 0.04f, brass, mirror).At(b.Item3, b.Item4, 0.01f);
            mirrorFigure = Pl(0.22f, 0.8f, Mats.UnlitTransparent(new Color(0, 0, 0, 0.85f)), mirror).At(0.22f, -0.08f, 0.012f);
            w.Disc(0.07f, 12, Mats.Unlit(Color.black), mirrorFigure.transform).At(0.03f, 0.45f, 0.001f);
            mirrorFigure.SetActive(false);
            mirror.At(cx(7), 1.75f, C + 0.03f);
            AddSuspect("mirror", mirror);
            var shoes = Grp("shoe rack");
            Bx(1.0f, 0.04f, 0.32f, shelfWood, shoes).At(0, 0.2f, 0);
            Bx(1.0f, 0.04f, 0.32f, shelfWood, shoes).At(0, 0.45f, 0);
            foreach (var sx in new[] { -0.48f, 0.48f }) Bx(0.03f, 0.5f, 0.32f, shelfWood, shoes).At(sx, 0.25f, 0);
            int[] shoeCols = { 0x1a1a1a, 0x5a3a22, 0x8a2a2a };
            for (int i = 0; i < 4; i++) Bx(0.11f, 0.08f, 0.26f, Std(MathX.Pick(shoeCols), 0.5f), shoes).At(-0.32f + i * 0.21f, 0.26f + (i % 2) * 0.25f, 0);
            sandalGo = Grp("small red sandal", shoes);
            Bx(0.06f, 0.02f, 0.14f, Std(0xc81e1e, 0.5f), sandalGo).At(0, 0.01f, 0);
            Bx(0.06f, 0.025f, 0.015f, Std(0xc81e1e, 0.5f), sandalGo).At(0, 0.03f, 0.03f);
            sandalGo.At(0.1f, 0.47f, 0.02f).RotY(0.3f); sandalGo.SetActive(false);
            shoes.At(cx(2), 0, 3 * C - 0.2f); Prop(shoes, 2, 2); AddSuspect("shoes", shoes);
            var ustand = Grp("umbrella stand");
            Cy(0.13f, 0.11f, 0.55f, 14, Mats.Std(MathX.Hex(0x2a3a2a), 0.5f, 0, null, null, null, 1, null, 1, true), ustand, true).At(0, 0.28f, 0);
            for (int i = 0; i < 2; i++) Cy(0.015f, 0.05f, 0.9f, 6, Std(0x1a1a2a, 0.6f), ustand).At((i - 0.5f) * 0.06f, 0.55f, 0).Rot(0, 0, (i - 0.5f) * 0.15f);
            ustand.At(C + 0.35f, 0, C + 0.35f);
            var phoneW = Grp("phone");
            Bx(0.2f, 0.28f, 0.08f, Std(0x1a1a1a, 0.3f), phoneW);
            Cy(0.06f, 0.06f, 0.015f, 16, Std(0xd8d0c0, 0.4f), phoneW).At(0, -0.03f, 0.05f).Rot(PI / 2, 0, 0);
            Bx(0.05f, 0.3f, 0.06f, Std(0x1a1a1a, 0.3f), phoneW).At(-0.13f, 0.02f, 0.05f);
            phoneW.At(cx(1) + 0.6f, 1.45f, C + 0.05f);
            System.Action<float, float, float, float, float, float> framed = (x, y, z, fw, fh, hue) =>
            {
                var fg = Grp("frame");
                var tex = Paint(96, 96, (p, W, H) =>
                {
                    p.VGradient(0, 0, 96, 96, 0, 96, (0, MathX.HSL(hue, 0.18f, 0.62f)), (1, MathX.HSL(hue + 20, 0.14f, 0.36f)));
                    p.Ellipse(48, 58, 22 + Random.value * 10, 30, 0, MathX.HSL(hue + 180, 0.1f, 0.2f, 0.5f));
                    p.Grain(20);
                });
                Pl(fw - 0.08f, fh - 0.08f, Mats.Std(Color.white, 0.8f, 0, tex), fg).At(0, 0, 0.012f);
                Bx(fw, fh, 0.02f, Std(0x1a120c, 0.5f), fg);
                fg.At(x, y, z);
            };
            framed(cx(2) + 0.3f, 1.85f, C + 0.03f, 0.45f, 0.55f, 30);
            framed(cx(9), 1.7f, C + 0.03f, 0.35f, 0.45f, 200);
            framed(cx(3) + 0.4f, 1.95f, 4 * C + C + 0.03f, 0.55f, 0.42f, 90);
            framed(cx(3) + 1.6f, 1.95f, 4 * C + C + 0.03f, 0.3f, 0.42f, 340);
            var boxes = Grp("moving boxes");
            for (int i = 0; i < 3; i++) Bx(0.65f, 0.48f, 0.55f, Std(0xa07a4a, 0.9f), boxes).At((i % 2) * 0.12f, 0.24f + i * 0.48f, (i % 2) * 0.1f).RotY(i * 0.25f);
            boxes.At(cx(10) + 0.35f, 0, C + 0.5f); Prop(boxes, 10, 1);
            var piano = Grp("piano"); var pianoWood = Std(0x1e120a, 0.35f);
            Bx(0.62f, 1.25f, 1.5f, pianoWood, piano).At(0, 0.62f, 0);
            Bx(0.3f, 0.06f, 1.42f, pianoWood, piano).At(-0.42f, 0.74f, 0);
            Bx(0.16f, 0.025f, 1.36f, Std(0xece6d6, 0.4f), piano).At(-0.46f, 0.78f, 0);
            for (int i = 0; i < 24; i++) { if (new[] { 1, 4, 8, 11, 15, 18, 22 }.Contains(i)) continue; Bx(0.09f, 0.03f, 0.025f, Std(0x0a0a0a, 0.3f), piano).At(-0.41f, 0.8f, -0.66f + i * 0.057f); }
            Bx(0.02f, 0.28f, 0.4f, Std(0xe8e0cc, 0.9f), piano).At(-0.35f, 1.0f, 0.1f).Rot(0, 0, -0.2f);
            piano.At(cx(10) + 0.6f, 0, cx(6)); Prop(piano, 10, 6); AddSuspect("piano", piano); pianoPos = piano.transform.position + Vector3.up;
            var stool = Grp("stool");
            Bx(0.4f, 0.06f, 0.6f, Std(0x1e120a, 0.4f), stool).At(0, 0.48f, 0);
            foreach (var sx in new[] { -0.15f, 0.15f }) foreach (var sz in new[] { -0.25f, 0.25f }) Bx(0.04f, 0.46f, 0.04f, Std(0x1e120a, 0.4f), stool).At(sx, 0.23f, sz);
            stool.At(cx(10) - 0.35f, 0, cx(6)); stoolGo = stool; stoolHome = stool.transform.localPosition;
            var tv = Grp("tv");
            Bx(1.0f, 0.5f, 0.5f, shelfWood, tv).At(0, 0.25f, 0);
            Bx(0.7f, 0.55f, 0.55f, Std(0x2a2620, 0.5f), tv).At(0, 0.78f, 0.02f);
            tvScreen = Mats.Std(MathX.Hex(0x10141a), 0.08f, 0.4f, null, null, null, 1, MathX.Hex(0xc8d0e0), 0, false, false);
            Pl(0.52f, 0.4f, tvScreen, tv).At(-0.04f, 0.8f, -0.28f).RotY(PI);
            foreach (var s in new[] { -1, 1 }) Cy(0.006f, 0.006f, 0.5f, 4, brass, tv).At(s * 0.12f, 1.25f, 0.05f).Rot(0, 0, -s * 0.45f);
            tv.At(cx(4), 0, 9 * C - 0.4f); Prop(tv, 4, 8); AddSuspect("tv", tv); tvPos = tv.transform.position + Vector3.up * 0.8f;
            System.Action<float, float, float> chairAt = (x, z, rot) =>
            {
                var cg = Grp("dining chair");
                Bx(0.45f, 0.05f, 0.45f, shelfWood, cg).At(0, 0.46f, 0);
                Bx(0.45f, 0.5f, 0.04f, shelfWood, cg).At(0, 0.74f, -0.21f);
                foreach (var sx in new[] { -0.19f, 0.19f }) foreach (var sz in new[] { -0.19f, 0.19f }) Bx(0.035f, 0.46f, 0.035f, shelfWood, cg).At(sx, 0.23f, sz);
                cg.At(x, 0, z).RotY(rot);
            };
            chairAt(cx(5) - 0.15f, cx(7) - 0.75f, 0.15f); chairAt(cx(5) + 0.2f, cx(7) + 0.78f, PI - 0.3f);
            Bx(0.55f, 0.32f, 0.45f, chairM).At(cx(7) - 0.9f, 0.16f, cx(8) - 0.9f);
            var wshelf = Grp("wall shelf");
            Bx(0.22f, 0.04f, 1.2f, shelfWood, wshelf);
            var vases = new[] { (0x5a7a8a, 0.07f, 0.22f), (0xc8b090, 0.05f, 0.3f), (0x8a3a2a, 0.08f, 0.18f) };
            for (int i = 0; i < 3; i++) Cy(vases[i].Item2 * 0.6f, vases[i].Item2, vases[i].Item3, 14, Std(vases[i].Item1, 0.3f), wshelf).At(0, vases[i].Item3 / 2 + 0.02f, -0.4f + i * 0.4f);
            wshelf.At(C + 0.12f, 1.55f, cx(5) + 0.4f);
            var rad = Grp("radiator");
            for (int i = 0; i < 10; i++) Bx(0.06f, 0.6f, 0.1f, Std(0xd8d0c0, 0.5f), rad).At(-0.55f + i * 0.12f, 0.42f, 0);
            rad.At(cx(6) + 1, 0, 9 * C - 0.1f);
            Bx(0.5f, 0.06f, 0.95f, Std(0x8a7a5a, 1)).At(cx(3) + C / 2 - 1.75f, 0.82f, cx(5) - 0.25f).Rot(0, 0, 0.5f);
            int[] magCols = { 0xc84a3a, 0xe8e0d0, 0x3a6a8a, 0xd8c070 };
            for (int i = 0; i < 6; i++) Bx(0.3f, 0.012f, 0.4f, Std(MathX.Pick(magCols), 0.6f)).At(cx(6) - 0.3f, 0.006f + i * 0.013f, cx(5) + 0.2f).RotY(MathX.Jit(0.5f));
            var chand = Grp("chandelier");
            Cy(0.01f, 0.01f, 0.5f, 6, brass, chand).At(0, -0.25f, 0);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * PI * 2;
                Cy(0.008f, 0.008f, 0.35f, 5, brass, chand).At(Mathf.Cos(a) * 0.17f, -0.5f, Mathf.Sin(a) * 0.17f).Rot(0, -a, PI / 2);
                Cy(0.03f, 0.02f, 0.05f, 8, Std(0xe8e0cc, 0.4f), chand).At(Mathf.Cos(a) * 0.34f, -0.47f, Mathf.Sin(a) * 0.34f);
            }
            chand.At(cx(5) + 1, WALL_H, cx(6) + 1);
        }

        void ThingsThatChange(Material trim)
        {
            var shelfWood = Std(0x4a2e1a, 0.7f);
            // photo frame on the table
            photoTex = Photo(false); photoItTex = Photo(true);
            var photo = Grp("photo");
            Bx(0.34f, 0.28f, 0.03f, Std(0x2a1a10, 0.5f), photo);
            photoMat = Mats.Std(Color.white, 0.6f, 0, photoTex, null, null, 1, null, 1, false, false);
            Pl(0.3f, 0.235f, photoMat, photo).At(0, 0, 0.017f);
            Bx(0.03f, 0.2f, 0.15f, Std(0x2a1a10, 0.5f), photo).At(0, -0.06f, -0.08f).Rot(0.4f, 0, 0);
            photo.At(cx(5) + 0.3f, 0.9f, cx(7) - 0.2f).Rot(0.12f, PI + 0.5f, 0);
            AddSuspect("photo", photo);
            // wall clock: wooden case, face, glass, real hands
            var clock = Grp("clock");
            Cy(0.47f, 0.47f, 0.08f, 40, Std(0x4a2a14, 0.45f), clock).Rot(PI / 2, 0, 0);
            w.Disc(0.42f, 40, Mats.Std(Color.white, 0.7f, 0, ClockFace()), clock.transform).At(0, 0, 0.045f);
            var handMat = Mats.Std(MathX.Hex(0x1a1410), 0.4f, 0.5f);
            System.Func<float, float, float, Material, Transform> hand = (len, wd, z, col) =>
            {
                var piv = Grp("hand", clock);
                Bx(wd, len, 0.008f, col, piv).At(0, len / 2 - 0.04f, 0);
                piv.At(0, 0, z);
                return piv.transform;
            };
            hourHand = hand(0.24f, 0.035f, 0.055f, handMat); minuteHand = hand(0.34f, 0.022f, 0.06f, handMat); secondHand = hand(0.37f, 0.008f, 0.065f, Std(0x9a1a1a, 0.4f));
            Cy(0.02f, 0.02f, 0.03f, 10, Mats.Std(MathX.Hex(0xc8a060), 0.3f, 0.8f), clock).At(0, 0, 0.07f).Rot(PI / 2, 0, 0);
            w.Disc(0.43f, 40, Mats.LitTransparent(new Color(1, 1, 1, 0.12f), 0.05f, 0.3f), clock.transform).At(0, 0, 0.08f);
            clock.At(cx(6), 2.15f, 4 * C + C + 0.02f);
            AddSuspect("clock", clock);
            clockPos = clock.transform.position;
            // a painted lake in a gilt frame, in the hallway
            painting = Grp("painting");
            var gold = Mats.Std(MathX.Hex(0xb08a3a), 0.35f, 0.7f);
            foreach (var b in new[] { (1.62f, 0.1f, 0f, 0.6f), (1.62f, 0.1f, 0f, -0.6f), (0.1f, 1.3f, 0.81f, 0f), (0.1f, 1.3f, -0.81f, 0f) }) Bx(b.Item1, b.Item2, 0.07f, gold, painting).At(b.Item3, b.Item4, 0);
            Pl(1.52f, 1.12f, Mats.Std(Color.white, 0.85f, 0, LakePainting()), painting).At(0, 0, 0.01f);
            painting.At(cx(5), 1.8f, C + 0.05f);
            AddSuspect("painting", painting);
            // bathroom door: hinged, panelled. Closed... at first.
            var bath = Grp("bathroom door");
            var hinge = Grp("hinge", bath); hinge.At(cx(5) - 0.63f, 0, 3 * C - 0.03f);
            Bx(1.26f, 2.37f, 0.05f, Mats.Std(Color.white, 0.55f, 0, DoorSlab()), hinge).At(0.63f, 1.185f, 0);
            Sp(0.04f, 12, 10, Mats.Std(MathX.Hex(0xc8a060), 0.3f, 0.8f), hinge).At(1.05f, 1.05f, -0.05f);
            foreach (var zx in new[] { -0.68f, 0.68f }) Bx(0.1f, 2.45f, 0.08f, trim, bath).At(cx(5) + zx, 1.22f, 3 * C - 0.02f);
            Bx(1.46f, 0.1f, 0.08f, trim, bath).At(cx(5), 2.42f, 3 * C - 0.02f);
            AddSuspect("bathroom", bath);
            bathHinge = hinge.transform; bathPos = new Vector3(cx(5), 1.2f, 3 * C + 0.5f);
            // vintage radio on a cabinet in the corner
            var cab = Grp("cabinet");
            Bx(1.0f, 0.8f, 0.55f, shelfWood, cab).At(0, 0.4f, 0);
            foreach (var sx in new[] { -0.24f, 0.24f }) Bx(0.44f, 0.6f, 0.02f, Std(0x5a3a22, 0.6f), cab).At(sx, 0.4f, 0.285f);
            cab.At(cx(2), 0, 9 * C - 0.33f).RotY(PI); Prop(cab, 2, 8);
            var radio = Grp("radio");
            Bx(0.52f, 0.32f, 0.24f, Std(0x6a4426, 0.45f), radio);
            Cy(0.16f, 0.16f, 0.52f, 20, Std(0x6a4426, 0.45f), radio, false, 0, PI).At(0, 0.16f, 0).Rot(0, 0, PI / 2);
            Bx(0.24f, 0.2f, 0.01f, Std(0x2a2014, 1), radio).At(-0.1f, 0, 0.125f);
            radioLed = new Material(Mats.Unlit(MathX.Hex(0x2a1600)));
            Pl(0.14f, 0.06f, radioLed, radio).At(0.14f, 0.04f, 0.122f);
            foreach (var kx in new[] { 0.09f, 0.19f }) Cy(0.025f, 0.025f, 0.03f, 12, Std(0x1a1410, 0.4f), radio).At(kx, -0.08f, 0.13f).Rot(PI / 2, 0, 0);
            radio.At(cx(2), 0.96f, 9 * C - 0.4f).RotY(PI);
            AddSuspect("radio", radio);
            radioPos = radio.transform.position;

            // the day's diary page lives on the table
            page = Pl(0.26f, 0.33f, Mats.Std(Color.white, 0.9f, 0, DiaryPaper()));
            page.At(cx(5) - 0.3f, 0.756f, cx(7) + 0.1f).Rot(-PI / 2, 0, 0.2f);
            World.Tagged(page, "doc", "diary1");
            pageIt = AddInteract("doc", "doc", "diary1", page.transform.position, page, "read");
        }

        public void SetClock(float h, float m, float? sec)
        {
            if (!hourHand) return;
            // clockwise as seen from the front (the opposite sign of the web version: Unity is left-handed)
            hourHand.localRotation = Quaternion.Euler(0, 0, ((h % 12) + m / 60) / 12 * 360);
            minuteHand.localRotation = Quaternion.Euler(0, 0, m / 60 * 360);
            secondHand.gameObject.SetActive(sec.HasValue);
            if (sec.HasValue) secondHand.localRotation = Quaternion.Euler(0, 0, sec.Value / 60 * 360);
        }

        // ---------------------------------------------------------------- loops
        void ApplyLoop()
        {
            int L = loop;
            change = LOOP_CHANGE[L];
            solved = change == null;   // the first loop has nothing to find
            eyesT = 0;
            foreach (var f in focusables) { f.done = false; f.progress = 0; }
            clockStopped = L >= 2;
            painting.transform.localRotation = Quaternion.Euler(0, 0, L >= 3 ? 180 : 0);
            photoMat.SetTexture("_BaseMap", L >= 4 ? photoItTex : photoTex);
            mirrorFigure.SetActive(L >= 5);
            bathHinge.localRotation = MathX.Euler3(0, L >= 6 ? 1.5f : L >= 3 ? 0.08f * (L - 2) : 0, 0);
            armchair.transform.localRotation = MathX.Euler3(0, L >= 7 ? -0.75f : PI + 0.5f, 0);
            tvOn = L >= 8;
            if (!tvOn) Mats.SetEmission(tvScreen, MathX.Hex(0xc8d0e0), 0);
            radioOn = L >= 9;
            pianoOn = L >= 10;
            stoolGo.transform.localPosition = L >= 10 ? new Vector3(cx(7), 0, cx(6) + 0.8f) : stoolHome;
            stoolGo.transform.localRotation = MathX.Euler3(0, L >= 10 ? 0.9f : 0, 0);
            sandalGo.SetActive(L >= 11);
            radioLed.SetColor("_BaseColor", radioOn ? MathX.Hex(0xff8a20) : MathX.Hex(0x331a00));
            // a new diary page each time
            string id = "diary" + L;
            if (GameData.Docs.ContainsKey(id)) { page.SetActive(true); page.GetComponent<Tag>().data = id; pageIt.id = id; pageIt.used = false; }
            else { page.SetActive(false); pageIt.used = true; }
            g.HideIt(5 + Random.value * 6);
            if (L == LAST_LOOP)
            {
                // this time, the thing that changed is it
                g.dread = Mathf.Max(g.dread, 0.3f);
                g.ShowIt(cx(1) + 0.3f, cx(8) - 0.2f, "final");
                g.timers.After(2.5f, () => g.Think("don't look at it", 2));
                g.timers.After(9, () => { if (loop == LAST_LOOP && !solved) g.Hint("You can't find this one by looking.", 6); });
            }
        }

        public override void OnFocusDone(Focusable f)
        {
            if (solved) { f.progress = 0; g.Hint("It looks the same as before.", 3); return; }
            if (f.key == change)
            {
                f.done = true; solved = true;
                g.au.Materialize();
                g.au.Noise(0.15f, 1400, 0.5f, Synth.F.Bandpass, new Vector3(cx(0) + 1, 1, cx(7)));   // the door unlocks
                g.Hint(MathX.Pick(new[] { "That's it. That's what changed.", "Yes. That wasn't like that before.", "There. You're sure now." }), 4);
                g.dread = MathX.Clamp01(g.dread + 0.05f);
            }
            else
            {
                f.progress = 0; wrong++;
                g.dread = MathX.Clamp01(g.dread + 0.15f);
                g.au.Sting(); g.IT.cool = Mathf.Min(g.IT.cool, 1);
                g.Hint(MathX.Pick(new[] { "It's the same. It's always been the same.", "No. That was always like that.", "Are you sure that's how it was?" }), 4);
            }
        }

        void TryDoorB()
        {
            if (transT > 0) return;
            if (!solved) { g.Hint("It won't open. Something in here is different.", 3.5f); g.dread = MathX.Clamp01(g.dread + 0.03f); g.au.Bump(); return; }
            g.au.DoorCreak();
            transT = 1.6f;
        }

        public override void OnInteract(Interactable it)
        {
            if (it.kind == "doorB") TryDoorB();
            else if (it.kind == "doorA") { g.Hint("It won't open. It never opens from this side.", 3); g.au.Bump(); }
        }

        // ---------------------------------------------------------------- play
        public override void Begin()
        {
            loop = 1;
            g.px = cx(1); g.pz = cx(1) + 1; g.yaw = -PI / 2; g.pitch = 0;
            ApplyLoop();
            g.Hint("Same room. Every time. Remember it.", 6);
        }

        public override string Zone(float wx, float wz)
        {
            int y = Mathf.FloorToInt(wz / C);
            return y <= 2 ? "entry" : y <= 4 ? "turn" : "living";
        }

        public void Ambience(float time, float dt, Sfx au, bool ticking)
        {
            dust.Update(time);
            tickT -= dt;
            if (tickT <= 0)
            {
                tickT = 1;
                if (clockStopped) SetClock(3, 33, null);
                else { int sec = (int)time % 60; SetClock(2, 14 + (int)(time / 60), sec); if (ticking) au.Tick(clockPos, sec % 2 == 1); }
            }
        }

        public override void Tick(float dt)
        {
            var au = g.au; float time = g.time;
            if (loop == 2 && !findHint) { findHint = true; g.Hint("Something in here is different. Find it. Look at it until you're sure.", 7); }

            // through the door... and you're back at the start of the hallway
            if (transT > 0)
            {
                float before = transT;
                transT -= dt;
                if (before > 0.8f && transT <= 0.8f)
                {
                    if (loop >= LAST_LOOP) { g.hud.SetTransition(0, null, 0); g.EndChapter(); return; }
                    loop++;
                    g.px = cx(1); g.pz = cx(1) + 1; g.yaw = -PI / 2; g.pitch = 0;
                    g.checkpoint = new Vector3(g.px, g.pz, g.yaw);
                    ApplyLoop();
                }
            }
            float a = transT > 0 ? (transT > 0.8f ? 1 - (transT - 0.8f) / 0.8f : transT / 0.8f) : 0;
            g.hud.SetTransition(a * 1.3f, transT > 0 && transT < 0.8f && loop > 1 && loop != LAST_LOOP ? loop.ToString() : null, a);

            Ambience(time, dt, au, true);
            // the bathroom: water running, dripping, a girl crying
            if (loop >= 6 && Random.value < dt * 1.2f) au.Drip(bathPos);
            if (loop >= 6 && Random.value < dt * 0.15f) au.Whisper(bathPos, 0.25f);
            // the television: snow, and something standing in it
            if (tvOn)
            {
                Mats.SetEmission(tvScreen, MathX.Hex(0xc8d0e0), 0.4f + Random.value * 0.6f);
                tvT -= dt; if (tvT <= 0) { tvT = 0.25f; au.Noise(0.3f, 3000, 0.04f, Synth.F.Bandpass, tvPos, 0.5f); }
            }
            // one note on the piano, over and over
            if (pianoOn) { pianoT -= dt; if (pianoT <= 0) { pianoT = 4 + Random.value * 3; au.Play(Synth.Piano(233.1f, 0.35f), pianoPos); } }
            if (loop >= 6 && !bathHint && MathX.Hypot(g.px - bathPos.x, g.pz - bathPos.z) < 5) { bathHint = true; g.Hint("Somewhere inside, a girl is crying.", 4); }
            // the radio
            au.SetPos(au.radio, radioPos);
            au.Set(au.radio, radioOn ? 0.18f + Random.value * 0.06f : 0);
            if (radioOn && !g.HasSubtitles && Random.value < dt * 0.08f && MathX.Hypot(g.px - radioPos.x, g.pz - radioPos.z) < 9)
                g.Say(new[] { new Line("RADIO", MathX.Pick(new[] { "...you were supposed to be watching him...", "...the water was so cold, Mara...", "...don't look away...", "...she looked away..." }), 4) });
            // final loop: close your eyes and let it go
            if (loop == LAST_LOOP && !solved)
            {
                if (!g.eyes) eyesT += dt; else eyesT = Mathf.Max(0, eyesT - dt * 0.5f);
                if (eyesT >= 4)
                {
                    solved = true; g.HideIt(999);
                    au.Noise(0.15f, 1400, 0.5f, Synth.F.Bandpass, new Vector3(cx(0) + 1, 1, cx(7)));
                    g.Think("let it go", 2.5f);
                    g.timers.After(1.5f, () => g.Hint("The door is unlocked.", 4));
                }
            }
        }

        // In the final loop, "it" is the change. Looking at it only makes it stronger, and it creeps closer while your eyes are open.
        public override bool CustomIt(float dt)
        {
            var IT = g.IT;
            if (IT.state != "final") return false;
            bool seen = g.InView(IT.x, IT.z, 0.86f);
            if (seen)
            {
                g.dread = MathX.Clamp01(g.dread + 0.3f * dt);
                if (!IT.wasSeen) { IT.wasSeen = true; g.timesSeen++; g.au.Sting(); }
            }
            else IT.wasSeen = false;
            IT.stepT -= dt;
            if (g.eyes && !seen && IT.stepT <= 0)
            {
                IT.stepT = 2.2f - g.dread * 1.4f;
                float vx = g.px - IT.x, vz = g.pz - IT.z, vd = Mathf.Max(0.001f, MathX.Hypot(vx, vz)), j = Mathf.Min(vd - 0.6f, 0.9f);
                float nx = IT.x + vx / vd * j, nz = IT.z + vz / vd * j;
                if (!w.Solid(nx, nz)) { IT.x = nx; IT.z = nz; g.au.Thud(new Vector3(IT.x, 1, IT.z)); }
            }
            if (MathX.Hypot(IT.x - g.px, IT.z - g.pz) < 1.25f) { g.Caught(); return true; }
            g.itModel.SetVisible(true);
            g.itModel.Place(IT.x, IT.z, g.px, g.pz, 0.15f);
            g.itModel.Flicker(true);
            g.au.SetPos(g.au.breath, new Vector3(IT.x, 2.4f, IT.z));
            g.au.Set(g.au.breath, (0.3f + Mathf.Sin(g.time * 1.7f) * 0.15f) * (g.eyes ? 1 : 2));
            return true;
        }

        public override void OnRespawn()
        {
            if (loop == LAST_LOOP && !solved) { g.dread = 0.3f; eyesT = 0; g.ShowIt(cx(1) + 0.3f, cx(8) - 0.2f, "final"); }
        }

        public override string Objective()
        {
            if (loop == 1) return "Walk through the room. Remember everything in it.";
            if (loop == LAST_LOOP) return solved ? "Go through the door." : "Something new is in the room. Don't look for it.";
            return solved ? "Go through the door." : "Something has changed. Find it, and look at it until you're sure.";
        }

        public override EndInfo End()
        {
            return new EndInfo
            {
                title = "END OF CHAPTER TWO", sub = "The Waiting Room", hasNext = true, next = "Chapter Three: The Archive",
                stats = $"loops survived: {LAST_LOOP}\nwrong guesses: {wrong}\ndiary pages read: {g.forbiddenRead} forbidden\ntimes you looked at it: {g.timesSeen}\ntimes it reached you: {g.timesCaught}"
            };
        }
    }
}
