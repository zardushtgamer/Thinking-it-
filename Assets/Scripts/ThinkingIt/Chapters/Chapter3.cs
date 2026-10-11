using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 3: The Archive.
    // Every session was recorded. Three tapes explain Project White Bear; Teo left the last one himself.
    // The restricted vault is behind a door you have to look into existence. The far stacks are dark,
    // and something is searching them: you cross with your eyes closed, following a tape machine left running.
    // ======================================================================
    public class Chapter3 : Chapter
    {
        public override int Number => 3;
        public override AudioReverbPreset Space => AudioReverbPreset.StoneCorridor;
        public override string HallHint => "Something is moving between the shelves. Close your eyes. Follow the tape machine.";
        public override float FogDensity => 0.065f;
        public override Color FogColor => new Color(0.012f, 0.008f, 0.008f);
        public override Color Ambient => new Color(0.42f, 0.45f, 0.5f) * 0.05f;

        readonly HashSet<string> tapes = new HashSet<string>();
        float tapeHissT;
        bool stackHint, vaultHint, campHint, listenHint, deepHint;
        Focusable vaultDoor;
        DustCloud dust;
        class Bulb { public Light l; public Material m; public float baseI; public bool flicker; }
        readonly List<Bulb> bulbs = new List<Bulb>();
        Light redLight; Material redMat;

        // ---------------------------------------------------------------- textures
        static Texture Wall() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0xb8ab92)); g.Grain(10);
            var mortar = Rgba(60, 50, 35, 0.35f);
            for (int y = 0; y < H; y += 34) { g.Rect(0, y, W, 3, mortar); for (int x = ((y / 34) % 2) * 64; x < W; x += 128) g.Rect(x, y, 3, 34, mortar); }
            g.Rect(0, 300, W, H - 300, Col(0x5a4630)); g.Rect(0, 296, W, 5, Col(0x3a2c1e));
            for (int i = 0; i < 4; i++) { float sx = Random.value * W; g.VGradient(sx, 0, 4 + Random.value * 8, H, 0, H * 0.8f, (0, Rgba(60, 45, 20, 0.3f)), (1, Rgba(60, 45, 20, 0))); }
            g.VGradient(0, 0, W, H, H * 0.7f, H, (0, new Color(0, 0, 0, 0)), (1, Rgba(15, 10, 5, 0.5f)));
        });

        static Texture Floor() => Paint(512, 512, (g, W, H) =>
        {
            g.Fill(Col(0x6e6a62)); g.Grain(18);
            for (int i = 0; i < 25; i++) g.RadialGradient(Random.value * W, Random.value * H, 0, 60, (0, Random.value < 0.5f ? Rgba(30, 24, 15, 0.18f) : Rgba(150, 145, 130, 0.18f)), (1, new Color(0, 0, 0, 0)));
            g.Rect(0, 255, W, 2, Rgba(20, 18, 14, 0.5f)); g.Rect(255, 0, 2, H, Rgba(20, 18, 14, 0.5f));
            for (int i = 0; i < 3; i++)
            {
                var pts = new List<Vector2>(); float x = Random.value * W, y = Random.value * H; pts.Add(new Vector2(x, y));
                for (int k = 0; k < 14; k++) { x += MathX.Jit(30); y += (Random.value - 0.3f) * 20; pts.Add(new Vector2(x, y)); }
                g.Polyline(pts, 1.2f, Rgba(25, 22, 18, 0.4f));
            }
        });

        static Texture Ceil() => Paint(256, 256, (g, W, H) => { g.Fill(Col(0x5a5852)); g.Grain(16); g.Rect(0, 120, W, 8, new Color(0, 0, 0, 0.25f)); });

        static Texture DoorTex() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x5a6266)); g.Grain(10);
            g.Rect(28, 40, 200, 370, Col(0x3a4246));
            g.Rect(58, 90, 140, 54, Col(0xd8d4c8));
            g.Text("WING C", 128, 116, 22, Col(0x1a1a1a), TexText.Face.Sans, true, false, 1);
            g.Text("RESIDENTIAL", 128, 136, 13, Col(0x1a1a1a), TexText.Face.Sans, false, false, 1);
            g.Rect(40, 250, 176, 10, Col(0x8a8e90));
        });

        public static Texture Sign(string text, string sub) => Paint(256, 96, (g, W, H) =>
        {
            g.Fill(Col(0x1e2a3a)); g.StrokeRect(6, 6, W - 12, H - 12, 4, Col(0xc8c4b4));
            g.Text(text, W / 2, sub != null ? 50 : 62, 34, Col(0xe8e4d4), TexText.Face.Sans, true, false, 1);
            if (sub != null) g.Text(sub, W / 2, 76, 16, Col(0xe8e4d4), TexText.Face.Sans, false, false, 1);
        });

        static Texture Catalog() => Paint(256, 256, (g, W, H) =>
        {
            g.Fill(Col(0x6a4424));
            for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++)
                {
                    float px = 8 + x * 41, py = 8 + y * 41;
                    g.Rect(px, py, 36, 36, Col(0x7a5230)); g.Rect(px + 9, py + 6, 18, 9, Col(0xe8dcc0));
                    g.Circle(px + 18, py + 25, 3, Col(0xc8a050));
                }
            g.Grain(10);
        });

        static Texture Label() => Paint(128, 64, (g, W, H) =>
        {
            g.Fill(Col(0xe8e0c8));
            g.Text("PWB-" + (100 + Random.Range(0, 899)), 8, 28, 18, Col(0x2a2a2a), TexText.Face.Mono, true);
            g.Text("SESSION TAPES", 8, 48, 12, Col(0x2a2a2a), TexText.Face.Mono);
        });

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(28, 22);
            w.Carve(1, 1, 7, 6);                    // records office
            w[8, 3] = '.'; w[8, 4] = '.';           // into the stacks
            w.Carve(9, 1, 26, 9);                   // the stacks
            foreach (var x in new[] { 11, 14, 17, 20, 23 }) for (int y = 2; y <= 8; y++) if (y != 5) w[x, y] = 'K';
            w.Carve(9, 11, 15, 15);                 // restricted vault
            w[12, 10] = 'G';                        // ...behind a door that isn't there
            w.Carve(17, 11, 26, 20);                // the dark stacks
            w[24, 10] = '.';
            for (int x = 17; x <= 24; x++) w[x, 14] = 'K';
            for (int x = 19; x <= 26; x++) w[x, 17] = 'K';
            w[16, 19] = 'X';                        // the door to Wing C
            w.Carve(1, 8, 7, 12);                   // the listening room
            w[4, 7] = '.';
            w.Carve(1, 14, 7, 20);                  // the deep stacks (dark)
            w[4, 13] = '.';
            for (int x = 1; x <= 5; x++) w[x, 16] = 'K';
            for (int x = 3; x <= 7; x++) w[x, 18] = 'K';

            w.CellBlocks("#", Mats.Std(Color.white, 0.9f, 0, Wall()), "wall");
            w.CellBlocks("X", Mats.Std(Color.white, 0.45f, 0.4f, DoorTex()), "exit", WALL_H, "door to wing c");
            var conc = TextureLibrary.Find(l => l.brushedConcrete);
            var floorM = conc != null ? Mats.Std(MathX.Hex(0xb0aca4), 0.35f, 0, conc.diff, new Vector2(w.W * 0.7f, w.H * 0.7f), conc.nor, 0.6f) : Mats.Std(Color.white, 0.35f, 0, Floor(), new Vector2(w.W / 2f, w.H / 2f));
            w.FloorAndCeiling(floorM, Mats.Std(Color.white, 1, 0, Ceil(), new Vector2(w.W / 2f, w.H / 2f)));

            // ---- the shelving: tall steel frames, crammed with file boxes (merged into a few meshes so it stays fast) ----
            var plate = TextureLibrary.Find(l => l.metalPlate);
            var steel = plate != null ? Mats.Std(MathX.Hex(0x8a8e90), 0.5f, 0.6f, plate.diff, Vector2.one, plate.nor, 0.5f) : Mats.Std(MathX.Hex(0x6a6e70), 0.5f, 0.6f);
            var um = new List<Matrix4x4>(); var us = new List<Vector3>();
            int[] boxCols = { 0xb89a6a, 0xa88a5a, 0xc8b08a, 0x8a7a5a, 0xd8d0b8, 0x9a7a4a };
            var boxM = new List<Matrix4x4>[boxCols.Length]; var boxS = new List<Vector3>[boxCols.Length];
            for (int i = 0; i < boxCols.Length; i++) { boxM[i] = new List<Matrix4x4>(); boxS[i] = new List<Vector3>(); }
            for (int y = 0; y < w.H; y++) for (int x = 0; x < w.W; x++)
                {
                    if (w[x, y] != 'K') continue;
                    float ox = cx(x), oz = cx(y);
                    foreach (var d in new[] { (-0.95f, -0.95f), (0.95f, -0.95f), (-0.95f, 0.95f), (0.95f, 0.95f) }) { um.Add(Matrix4x4.Translate(new Vector3(ox + d.Item1, 1.4f, oz + d.Item2))); us.Add(new Vector3(0.05f, 2.8f, 0.05f)); }
                    for (int s = 0; s < 5; s++)
                    {
                        float sy = 0.12f + s * 0.6f;
                        um.Add(Matrix4x4.Translate(new Vector3(ox, sy, oz))); us.Add(new Vector3(1.95f, 0.03f, 1.95f));
                        float off = -0.9f;
                        for (int b = 0; b < 5; b++)
                        {
                            if (Random.value < 0.18f) { off += 0.38f; continue; }   // gaps you can see through
                            float bw = 0.3f + Random.value * 0.08f, bh = 0.32f + Random.value * 0.12f;
                            int ci = Random.Range(0, boxCols.Length);
                            boxM[ci].Add(Matrix4x4.Translate(new Vector3(ox + off + bw / 2, sy + 0.015f + bh / 2, oz + MathX.Jit(0.3f))));
                            boxS[ci].Add(new Vector3(bw, bh, 0.42f + Random.value * 0.15f));
                            off += bw + 0.05f;
                        }
                    }
                }
            w.Batch(um, us, steel, "wall", "shelving");
            for (int i = 0; i < boxCols.Length; i++) w.Batch(boxM[i], boxS[i], Std(boxCols[i], 0.9f), "wall", "file boxes");

            w.Skirting(Std(0x1a1a1a, 0.9f), "#X");

            // ---- furniture ----
            var wood = Std(0x5a3a22, 0.6f); var darkWood = Std(0x3a2414, 0.55f); var brass = Mats.Std(MathX.Hex(0xb08a3a), 0.35f, 0.7f);
            System.Func<float, float, float, float, float, GameObject> table = (x, z, wdt, dep, rot) =>
            {
                var t = Grp("table");
                Bx(wdt, 0.06f, dep, wood, t).At(0, 0.76f, 0);
                foreach (var sx in new[] { -wdt / 2 + 0.06f, wdt / 2 - 0.06f }) foreach (var sz in new[] { -dep / 2 + 0.06f, dep / 2 - 0.06f }) Bx(0.06f, 0.76f, 0.06f, darkWood, t).At(sx, 0.38f, sz);
                t.At(x, 0, z).RotY(rot); return t;
            };
            // a green-shaded banker's lamp: the only warm light down here
            System.Action<float, float, float, bool> bankersLamp = (x, y, z, shadow) =>
            {
                var lg = Grp("banker's lamp");
                Cy(0.08f, 0.1f, 0.03f, 16, brass, lg);
                Cy(0.012f, 0.012f, 0.32f, 8, brass, lg).At(0, 0.16f, 0);
                Cy(0.06f, 0.14f, 0.12f, 20, Mats.Std(MathX.Hex(0x1a6a3a), 0.2f, 0, null, null, null, 1, MathX.Hex(0x1a6a3a), 0.7f, true), lg, true, 0, PI).At(0, 0.34f, 0).Rot(PI / 2, 0, PI / 2).Scl(1, 1.6f, 1);
                Sp(0.035f, 10, 8, Mats.Unlit(MathX.Hex(0xffe0a0)), lg).At(0, 0.31f, 0);
                lg.At(x, y, z).NoShadow();
                var l = w.PointLight(new Vector3(x, y + 0.25f, z), MathX.Hex(0xffc880), 1.3f, 7, shadow);
            };
            // caged bulbs hanging over the stacks
            System.Action<float, float, float, bool> cagedBulb = (x, z, intensity, flicker) =>
            {
                Cy(0.008f, 0.008f, 0.6f, 5, Std(0x1a1a1a, 0.9f)).At(x, WALL_H - 0.3f, z);
                var bm = Mats.Std(MathX.Hex(0xffeecc), 0.5f, 0, null, null, null, 1, MathX.Hex(0xffe0b0), 2, false, false);
                Sp(0.06f, 12, 10, bm).At(x, WALL_H - 0.66f, z).NoShadow();
                for (int i = 0; i < 4; i++) To(0.09f, 0.005f, 4, 16, steel).At(x, WALL_H - 0.66f, z).RotY(i * PI / 4).NoShadow();
                var l = w.PointLight(new Vector3(x, WALL_H - 0.75f, z), MathX.Hex(0xffe2c0), intensity, 10);
                bulbs.Add(new Bulb { l = l, m = bm, baseI = intensity, flicker = flicker });
            };
            // records office
            Prop(table(cx(3), cx(2) + 0.2f, 1.8f, 0.9f, 0), 3, 2);
            bankersLamp(cx(3) - 0.5f, 0.79f, cx(2) + 0.1f, true);
            var typewriter = Grp("typewriter");
            Bx(0.42f, 0.12f, 0.34f, Std(0x1a1a1a, 0.4f), typewriter).At(0, 0.06f, 0);
            Cy(0.03f, 0.03f, 0.46f, 12, Std(0x1a1a1a, 0.4f), typewriter).At(0, 0.16f, -0.12f).Rot(0, 0, PI / 2);
            typewriter.At(cx(3) + 0.35f, 0.79f, cx(2) + 0.15f);
            var reel = Grp("reel to reel");
            Bx(0.6f, 0.45f, 0.25f, Std(0x3a3a3a, 0.5f), reel).At(0, 0.22f, 0);
            foreach (var sx in new[] { -0.14f, 0.14f }) Cy(0.11f, 0.11f, 0.02f, 24, Mats.Std(MathX.Hex(0x9a9a9a), 0.3f, 0.7f), reel).At(sx, 0.3f, 0.135f).Rot(PI / 2, 0, 0);
            reel.At(cx(6), 0, cx(1) - 0.45f); Prop(reel);
            var catalog = Grp("card catalogue");
            Bx(1.0f, 1.2f, 0.5f, Mats.Std(Color.white, 0.6f, 0, Catalog()), catalog).At(0, 0.6f, 0);
            catalog.At(cx(1) - 0.45f, 0, cx(5)).RotY(PI / 2); Prop(catalog, 1, 5);
            for (int i = 0; i < 2; i++)
            {
                var fc = Grp("filing cabinet");
                Bx(0.5f, 1.3f, 0.6f, Mats.Std(MathX.Hex(0x7a7e74), 0.5f, 0.4f), fc).At(0, 0.65f, 0);
                for (int d = 0; d < 4; d++) Bx(0.12f, 0.02f, 0.02f, brass, fc).At(0, 0.2f + d * 0.31f, 0.31f);
                fc.At(cx(7) + 0.55f, 0, cx(5) + (i - 0.5f) * 0.6f).RotY(-PI / 2);
            }
            Pl(1.0f, 0.38f, Mats.Std(Color.white, 0.6f, 0, Sign("ARCHIVE B", "Records & Session Tapes"))).At(cx(4), 2.5f, C + 0.03f);
            // the stacks: row signs, a reading table, a book cart, a ladder
            string[] rows = { "1", "2", "3", "4", "5" }; int[] rowX = { 11, 14, 17, 20, 23 };
            for (int i = 0; i < 5; i++) Pl(0.5f, 0.2f, Mats.Std(Color.white, 0.6f, 0, Sign("ROW " + rows[i], null))).At(cx(rowX[i]), 2.95f, cx(1) + 0.98f);
            Prop(table(cx(15) + 1, cx(9), 1.6f, 0.8f, 0));
            bankersLamp(cx(15) + 0.5f, 0.79f, cx(9) - 0.1f, false);
            var cart = Grp("book cart");
            foreach (var yy in new[] { 0.25f, 0.75f }) Bx(0.9f, 0.04f, 0.45f, darkWood, cart).At(0, yy, 0);
            foreach (var sx in new[] { -0.42f, 0.42f }) Bx(0.04f, 0.9f, 0.45f, darkWood, cart).At(sx, 0.45f, 0);
            int[] bookC = { 0x6a1f1f, 0x1f3a5a, 0x5a4a2a, 0x2a4a2a };
            for (int i = 0; i < 9; i++) Bx(0.06f, 0.28f, 0.32f, Std(MathX.Pick(bookC), 0.8f), cart).At(-0.36f + i * 0.09f, 0.42f, 0);
            cart.At(cx(25) + 0.3f, 0, cx(2)).RotY(0.3f); Prop(cart);
            var ladder = Grp("ladder");
            foreach (var sx in new[] { -0.22f, 0.22f }) Bx(0.05f, 2.6f, 0.05f, darkWood, ladder).At(sx, 1.3f, 0);
            for (int i = 0; i < 8; i++) Bx(0.44f, 0.03f, 0.05f, darkWood, ladder).At(0, 0.3f + i * 0.3f, 0);
            ladder.At(cx(16) + 0.2f, 0, cx(3)).Rot(-0.25f, PI / 2, 0);
            cagedBulb(cx(12) + 1, cx(3), 1.2f, false); cagedBulb(cx(18) + 1, cx(3), 1.0f, true); cagedBulb(cx(24) + 1, cx(4), 1.0f, false);
            cagedBulb(cx(15) + 1, cx(7), 0.9f, true); cagedBulb(cx(21) + 1, cx(7), 0.9f, false);
            // papers spilling out on the floor of the stacks
            var paperM = Mats.Std(MathX.Hex(0xe0d8c4), 0.95f, 0, null, null, null, 1, null, 1, true);
            for (int i = 0; i < 50; i++)
            {
                int x, y; do { x = 1 + Random.Range(0, w.W - 2); y = 1 + Random.Range(0, w.H - 2); } while (w[x, y] != '.');
                Pl(0.21f, 0.29f, paperM).At(cx(x) + MathX.Jit(1.6f), 0.004f + Random.value * 0.003f, cx(y) + MathX.Jit(1.6f)).Rot(-PI / 2, 0, Random.value * 6).NoShadow();
            }
            // the vault: shelves of tape reels, a warning light
            int[] tapeCols = { 0x2a2a2a, 0x8a2a1a, 0x1a3a5a };
            for (int r = 0; r < 3; r++)
            {
                var rack = Grp("tape rack");
                Bx(1.6f, 2.0f, 0.4f, steel, rack).At(0, 1.0f, 0);
                for (int s = 0; s < 4; s++) for (int k = 0; k < 6; k++) Cy(0.11f, 0.11f, 0.025f, 18, Std(MathX.Pick(tapeCols), 0.4f), rack).At(-0.6f + k * 0.24f, 0.3f + s * 0.45f, 0.22f).Rot(0, 0, PI / 2);
                rack.At(cx(9 + r * 3) + 0.4f, 0, cx(15) + 0.75f).RotY(PI); Prop(rack);
            }
            Prop(table(cx(12), cx(13), 1.4f, 0.8f, 0), 12, 13);
            Pl(1.1f, 0.4f, Mats.Std(Color.white, 0.6f, 0, Sign("RESTRICTED", "Authorised staff only"))).At(cx(12), 2.75f, 10 * C + C - 0.03f).RotY(PI);
            redLight = w.PointLight(new Vector3(cx(12), 2.8f, cx(13)), MathX.Hex(0xff2a14), 0.9f, 9);
            redMat = Mats.Std(MathX.Hex(0x551010), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff2010), 1, false, false);
            Sp(0.1f, 14, 8, redMat, null, 0, PI * 2, 0, PI / 2).At(cx(12), WALL_H, cx(13)).Rot(PI, 0, 0);
            // Teo's corner in the dark stacks: a sleeping bag, a backpack, a lantern turned right down, notes pinned to the shelf
            var camp = Grp("Teo's camp");
            Bx(0.8f, 0.12f, 1.9f, Std(0x2a4a3a, 0.9f), camp).At(0, 0.06f, 0);
            Bx(0.5f, 0.12f, 0.3f, Std(0x6a6a5a, 0.9f), camp).At(0, 0.14f, -0.75f);
            Bx(0.4f, 0.55f, 0.25f, Std(0x6a3a1a, 0.8f), camp).At(0.7f, 0.28f, -0.5f).RotY(0.4f);
            for (int i = 0; i < 3; i++) Cy(0.04f, 0.04f, 0.11f, 10, Mats.Std(MathX.Hex(0x9a9a9a), 0.4f, 0.6f), camp).At(-0.6f + i * 0.12f, 0.055f, 0.6f + (i % 2) * 0.1f);
            Cy(0.07f, 0.08f, 0.22f, 12, Mats.Std(MathX.Hex(0x3a3020), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff9a40), 0.8f), camp).At(0.6f, 0.11f, 0.4f);
            camp.At(cx(18), 0, cx(19)).NoShadow();
            w.PointLight(new Vector3(cx(18) + 0.6f, 0.4f, cx(19) + 0.4f), MathX.Hex(0xff9a40), 0.35f, 4);
            for (int i = 0; i < 5; i++) Pl(0.2f, 0.26f, Std(0xe8e0c8, 0.9f)).At(cx(18) - 0.6f + i * 0.3f, 1.1f + (i % 2) * 0.3f, cx(17) + 1.03f).Rot(0, 0, MathX.Jit(0.3f));
            Pl(0.9f, 0.34f, Mats.Std(Color.white, 0.6f, 0, Sign("WING C", "— the lake wing —"))).At(17 * C + 0.04f, 2.75f, cx(19)).RotY(PI / 2);

            // ---- the listening room: a long desk of tape decks, headphones, a speaker nobody is meant to use ----
            Prop(table(cx(7) + 0.3f, cx(10), 4.2f, 0.9f, PI / 2));
            for (int i = 0; i < 3; i++)
            {
                var deck = Grp("tape deck");
                Bx(0.5f, 0.16f, 0.35f, Std(0x2a2a2a, 0.5f), deck).At(0, 0.08f, 0);
                foreach (var sx in new[] { -0.12f, 0.12f }) Cy(0.08f, 0.08f, 0.02f, 18, Mats.Std(MathX.Hex(0x9a9a9a), 0.3f, 0.7f), deck).At(sx, 0.17f, 0);
                Bx(0.03f, 0.02f, 0.02f, Mats.Unlit(MathX.Hex(i == 1 ? 0x20ff40 : 0x401010)), deck).At(0.2f, 0.12f, 0.18f);
                var hp = Grp("headphones", deck);
                To(0.09f, 0.012f, 6, 16, Std(0x1a1a1a, 0.6f), hp);
                foreach (var sx in new[] { -0.09f, 0.09f }) Cy(0.045f, 0.045f, 0.03f, 12, Std(0x1a1a1a, 0.7f), hp).At(sx, 0, 0).Rot(0, 0, PI / 2);
                hp.At(0.38f, 0.03f, 0.05f).Rot(-PI / 2, 0, 0.3f);
                deck.At(cx(7) + 0.3f, 0.79f, cx(10) - 1.6f + i * 1.6f).RotY(-PI / 2);
                var ch2 = Grp("chair");
                Bx(0.44f, 0.05f, 0.42f, wood, ch2).At(0, 0.45f, 0);
                Bx(0.44f, 0.45f, 0.04f, wood, ch2).At(0, 0.72f, 0.2f);
                foreach (var lx in new[] { -0.19f, 0.19f }) foreach (var lz in new[] { -0.18f, 0.18f }) Bx(0.04f, 0.45f, 0.04f, darkWood, ch2).At(lx, 0.22f, lz);
                ch2.At(cx(7) - 0.65f, 0, cx(10) - 1.6f + i * 1.6f).RotY(-PI / 2 + MathX.Jit(0.3f)); Prop(ch2);
            }
            var speaker = Grp("speaker");
            Bx(0.5f, 0.8f, 0.4f, darkWood, speaker).At(0, 0.4f, 0);
            Cy(0.16f, 0.16f, 0.02f, 20, Std(0x1a1a1a, 0.9f), speaker).At(0, 0.5f, 0.2f).Rot(PI / 2, 0, 0);
            speaker.At(cx(7) + 0.4f, 0, cx(12) + 0.5f).RotY(-PI * 0.75f); Prop(speaker);
            for (int i = 0; i < 2; i++) { var rk = Grp("tape rack"); Bx(1.6f, 2.0f, 0.4f, steel, rk).At(0, 1.0f, 0); for (int sh = 0; sh < 4; sh++) for (int k2 = 0; k2 < 6; k2++) Cy(0.11f, 0.11f, 0.025f, 18, Std(MathX.Pick(tapeCols), 0.4f), rk).At(-0.6f + k2 * 0.24f, 0.3f + sh * 0.45f, 0.22f).Rot(0, 0, PI / 2); rk.At(C + 0.25f, 0, cx(10) + i * 2.4f - 0.6f).RotY(PI / 2); Prop(rk); }
            bankersLamp(cx(7) + 0.4f, 0.79f, cx(10) + 0.8f, true);
            Pl(1.0f, 0.38f, Mats.Std(Color.white, 0.6f, 0, Sign("DEEP STACKS", "Boxes 1 – 40"))).At(cx(4), 2.5f, 13 * C - 0.03f).RotY(PI);
            // ---- the deep stacks: older shelving, boxes on the floor, a second tape machine left running ----
            for (int i = 0; i < 8; i++)
            {
                int x, y; do { x = 1 + Random.Range(0, 7); y = 14 + Random.Range(0, 7); } while (w[x, y] != '.' || y == 17 || (x == 4 && y <= 15) || (x >= 5 && y >= 19));
                Bx(0.45f, 0.35f, 0.4f, Std(MathX.Pick(boxCols), 0.9f)).At(cx(x) + MathX.Jit(1.2f), 0.175f, cx(y) + MathX.Jit(1.2f)).RotY(Random.value * 3);
            }
            var machine = Grp("tape machine");
            Bx(0.6f, 0.45f, 0.3f, Std(0x3a3a3a, 0.5f), machine).At(0, 0.22f, 0);
            foreach (var sx in new[] { -0.14f, 0.14f }) Cy(0.11f, 0.11f, 0.02f, 24, Mats.Std(MathX.Hex(0x9a9a9a), 0.3f, 0.7f), machine).At(sx, 0.3f, 0.155f).Rot(PI / 2, 0, 0);
            Bx(0.03f, 0.02f, 0.02f, Mats.Unlit(MathX.Hex(0x20ff40)), machine).At(0.24f, 0.4f, 0.16f);
            machine.At(cx(6), 0, cx(20) + 0.5f).RotY(PI); Prop(machine);
            Bx(0.5f, 0.4f, 0.45f, Std(0xb89a6a, 0.9f)).At(cx(1), 0.2f, cx(17) + 0.4f);
            Bx(0.5f, 0.4f, 0.45f, Std(0xa88a5a, 0.9f)).At(cx(4), 0.2f, cx(20) + 0.4f);

            // ---- the vault door: a painted-on outline until you look at it properly ----
            var gd = Grp("vault door");
            foreach (var b in new[] { (-0.85f, 1.3f, 0f, 0.15f, 2.6f, C), (0.85f, 1.3f, 0f, 0.15f, 2.6f, C), (0f, 2.65f, 0f, 1.85f, 0.15f, C), (0f, 1.3f, 0.6f, 1.5f, 2.5f, 0.12f) })
                Bx(b.Item4, b.Item5, b.Item6, null, gd).At(b.Item1, b.Item2, b.Item3);
            gd.At(cx(12), 0, cx(10));
            vaultDoor = AddFocusable("door", gd, new[] { new Vector2Int(12, 10) }, new Vector3(cx(12), 1.4f, cx(10)), 2.6f, Std(0x6a6e64));

            // ---- documents and tapes ----
            var paperTex = Chapter2.DiaryPaper();
            System.Action<float, float, float, string> docAt = (x, y, z, id) =>
            {
                var p = Pl(0.26f, 0.33f, Mats.Std(Color.white, 0.9f, 0, paperTex));
                p.At(x, y, z).Rot(-PI / 2, 0, MathX.Jit(0.6f));
                World.Tagged(p, "doc", id);
                AddInteract("doc", "doc", id, p.transform.position, p, "read");
            };
            docAt(cx(3) - 0.1f, 0.8f, cx(2) + 0.3f, "a_log");
            docAt(cx(1) + 0.25f, 1.21f, cx(5), "a_card");
            docAt(cx(15) + 1.2f, 0.8f, cx(9), "a_intake");
            docAt(cx(25) + 0.3f, 0.78f, cx(2), "a_p07");
            docAt(cx(12) + 0.4f, 0.8f, cx(13) + 0.1f, "a_lund");
            docAt(cx(18) + 0.3f, 0.14f, cx(19) - 0.7f, "a_teo");
            docAt(cx(7) + 0.3f, 0.8f, cx(10) - 0.8f, "a_listen");
            docAt(cx(4), 0.41f, cx(20) + 0.4f, "a_deep");
            var labelM = Mats.Std(Color.white, 0.8f, 0, Label());
            System.Action<float, float, float, string> tapeAt = (x, y, z, id) =>
            {
                var t = Grp("tape");
                Bx(0.11f, 0.018f, 0.07f, Std(0x1a1a1a, 0.4f), t);
                Pl(0.08f, 0.04f, labelM, t).At(0, 0.011f, 0).Rot(-PI / 2, 0, 0);
                Sp(0.015f, 6, 6, Mats.Unlit(MathX.Hex(0xfff0d0)), t).At(0.04f, 0.02f, 0);
                t.At(x, y, z).RotY(Random.value * 3);
                World.Tagged(t, "tape", id);
                AddInteract("tape", "tape", id, t.transform.position, t, "play the tape");
            };
            tapeAt(cx(25) + 0.1f, 0.79f, cx(2) + 0.1f, "tape1");
            tapeAt(cx(12) - 0.3f, 0.8f, cx(13) - 0.1f, "tape2");
            tapeAt(cx(18) - 0.15f, 0.13f, cx(19) + 0.2f, "tape3");
            tapeAt(cx(7) + 0.3f, 0.97f, cx(10), "tapeA");    // still in the middle deck
            tapeAt(cx(6) + 0.1f, 0.47f, cx(20) + 0.45f, "tapeB");  // on top of the running machine
            tapeAt(cx(1), 0.41f, cx(17) + 0.4f, "tapeC");
            AddInteract("exit", "exit", null, new Vector3(cx(16) + 0.8f, 1.2f, cx(19)), null, "open the door");

            dust = new DustCloud(w.root, DustCloud.OverCells(w, 380), MathX.Hex(0xffe8c8), 0.03f, 0.55f);
        }

        // ---------------------------------------------------------------- play
        public override void Begin()
        {
            g.px = cx(2); g.pz = cx(3) + 0.5f; g.yaw = -PI / 2; g.pitch = 0;
            g.Hint("Archive B. Every session they ever recorded is down here.", 6);
        }

        public override string Zone(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            if (x <= 8) return y >= 14 ? "hall" : y >= 8 ? "listening" : "office";   // the deep stacks are dark too
            return y <= 10 ? "stacks" : x <= 15 ? "vault" : "hall";   // "hall" = the dark stacks
        }

        public override void Tick(float dt)
        {
            float time = g.time; var au = g.au;
            if (g.zone == "stacks" && !stackHint) { stackHint = true; g.Hint("Rows and rows of them. Every child they ever recorded.", 5); }
            if (g.zone == "stacks" && g.px > cx(10) && g.px < cx(14) && g.pz > cx(8) && !vaultHint && !vaultDoor.done) { vaultHint = true; g.Hint("The vault door is only painted on. Or is it?", 5); }
            if (g.zone == "listening" && !listenHint) { listenHint = true; g.Hint("The listening room. One of the decks is still threaded.", 5); }
            if (g.hall && g.px < 8 * C && !deepHint) { deepHint = true; g.Hint("The deep stacks. Another machine is running somewhere in here.", 5); }
            if (tapeHissT > 0) { tapeHissT -= dt; if (Random.value < dt * 6) au.Noise(0.12f, 4000, 0.03f, Synth.F.Highpass); }   // tape hiss under the voices

            dust.Update(time);
            foreach (var b in bulbs)
            {
                if (!b.flicker) continue;
                bool on = Random.value > 0.04f;
                b.l.intensity = on ? b.baseI * (0.85f + Random.value * 0.15f) * World.LK : 0;
                Mats.SetEmission(b.m, MathX.Hex(0xffe0b0), on ? 2 : 0.1f);
                if (!on && Random.value < 0.3f) au.Flicker();
            }
            float k = 0.55f + Mathf.Sin(time * 2) * 0.4f;
            redLight.intensity = k * World.LK; Mats.SetEmission(redMat, MathX.Hex(0xff2010), 0.3f + k * 1.2f);
            // a tape machine left running in Teo's corner: something to follow in the dark
            au.SetPos(au.radio, g.px < 8 * C ? new Vector3(cx(6), 0.4f, cx(20) + 0.5f) : new Vector3(cx(18), 0.4f, cx(19)));
            au.Set(au.radio, g.hall ? 0.12f + Mathf.Sin(time * 6) * 0.03f : 0);
            if (g.hall && Random.value < 0.01f && MathX.Hypot(g.px - cx(18), g.pz - cx(19)) < 14) au.Whisper(new Vector3(cx(18), 0.5f, cx(19)), 0.18f);
            if (g.hall && MathX.Hypot(g.px - cx(18), g.pz - cx(19)) < 3 && !campHint) { campHint = true; g.Hint("Teo was here. Open your eyes.", 5); }
        }

        public override void OnInteract(Interactable it)
        {
            if (it.kind == "tape")
            {
                it.used = true; it.mesh.SetActive(false);
                var t = GameData.Tapes[it.id];
                tapes.Add(it.id);
                g.au.Tape();
                g.Hint(t.title, 4);
                g.Say(t.lines);
                tapeHissT = 0; foreach (var l in t.lines) tapeHissT += l.dur;
            }
            else if (it.kind == "exit")
            {
                if (tapes.Count < 6) { g.Hint($"Not yet. {6 - tapes.Count} more tape{(6 - tapes.Count == 1 ? "" : "s")}. Teo wanted you to hear all of them.", 4); g.au.Bump(); return; }
                g.EndChapter();
            }
        }

        public override string Objective()
        {
            if (tapes.Count < 6) return $"Find the session tapes. ({tapes.Count} / 6)";
            return "Follow Teo. The door to Wing C.";
        }

        public override EndInfo End()
        {
            return new EndInfo
            {
                title = "END OF CHAPTER THREE", sub = "The Archive", hasNext = true, next = "Chapter Four: The Lake House",
                stats = $"tapes recovered: {tapes.Count} / 6\nforbidden files read: {g.forbiddenRead} / 4\ntimes you looked at it: {g.timesSeen}\ntimes it reached you: {g.timesCaught}"
            };
        }
    }
}
