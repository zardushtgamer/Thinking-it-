using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 1: Arrival. The Halvard Institute, at 2:14 AM.
    // A corridor that isn't there until you look at it, a door that isn't there either,
    // the power, Teo's recorder, and the dark observation hall you cross with your eyes closed.
    // ======================================================================
    public class Chapter1 : Chapter
    {
        public override int Number => 1;
        public override bool Lit => power;
        public override string HallHint => "Too dark. Too much. Close your eyes and follow the phone.";

        bool power, recorder, eyesTaught, pitHint, doorHint, exitHint, wardHint, isoHint;
        int fuses; readonly List<GameObject> fuseSlots = new List<GameObject>();
        float powerT = -1;
        GameObject doorD;
        Interactable leverIt, recorderIt;
        GameObject leverHandle; Material recorderLed;
        DustCloud dust;
        Focusable bridge, ghostDoor, isoDoor;

        class Fluoro { public Light l; public List<Material> tubes = new List<Material>(); public Material beam; public float baseI, startAt, last; public bool flicker; public Vector3 pos; }
        class Emergency { public Light l; public Material m; public float phase; }
        readonly List<Fluoro> fluoros = new List<Fluoro>();
        readonly List<Emergency> emergencies = new List<Emergency>();
        readonly List<(Material m, float startAt)> screens = new List<(Material, float)>();

        // ---------------------------------------------------------------- textures
        static void InstituteWall(Painter g, int W, int H)
        {
            g.Fill(Col(0xa2b29c)); g.Grain(9);
            float wy = 250;
            g.Rect(0, wy, W, H - wy, Col(0x4c5c49));
            g.Rect(0, wy - 7, W, 7, Col(0x2c362a));
            g.Rect(0, wy - 7, W, 1, new Color(1, 1, 1, 0.12f));
            for (int i = 0; i < 4; i++)   // peeling paint, bare plaster underneath
            {
                float x = Random.value * W, y = 20 + Random.value * 200, rw = 8 + Random.value * 22, rh = 6 + Random.value * 16, rot = Random.value * 3;
                g.Ellipse(x, y, rw, rh, rot, Col(0xcfc9b4));
                g.StrokeEllipse(x, y, rw, rh, rot, 1.5f, Rgba(60, 70, 55, 0.5f));
            }
            for (int i = 0; i < 5; i++)   // water streaks running down
            {
                float sx = Random.value * W, len = H * (0.4f + Random.value * 0.5f), sw = 3 + Random.value * 7;
                g.VGradient(sx, 0, sw, H, 0, len, (0, Rgba(70, 58, 30, 0.35f)), (1, Rgba(70, 58, 30, 0)));
            }
            g.VGradient(0, 0, W, H, H * 0.75f, H, (0, new Color(0, 0, 0, 0)), (1, Rgba(20, 16, 8, 0.45f)));
            for (int i = 0; i < 2; i++)   // hairline cracks
            {
                var pts = new List<Vector2>(); float x = Random.value * W, y = Random.value * 100; pts.Add(new Vector2(x, y));
                for (int k = 0; k < 10; k++) { x += MathX.Jit(18); y += 10; pts.Add(new Vector2(x, y)); }
                g.Polyline(pts, 1, Rgba(40, 40, 30, 0.35f));
            }
        }

        public static Texture WallTex() { var p = new Painter(256, 410); InstituteWall(p, 256, 410); return p.Make(); }

        static Texture TraceTex()
        {
            var g = new Painter(256, 410); InstituteWall(g, 256, 410);
            var ink = new Color(0.96f, 0.96f, 0.93f, 0.85f);
            for (int i = 0; i < 5; i++)   // a crude bear, scratched in over and over
            {
                float ox = 20 + (i % 3) * 75 + Random.value * 10, oy = 40 + (i / 3) * 80 + Random.value * 10;
                g.StrokeEllipse(ox + 22, oy + 26, 20, 13, 0, 2.2f, ink);
                g.StrokeEllipse(ox + 40, oy + 9, 8, 8, 0, 2.2f, ink);
                foreach (var lx in new[] { 8, 18, 28, 36 }) g.Line(ox + lx, oy + 36, ox + lx + MathX.Jit(3), oy + 50, 2.2f, ink);
            }
            g.Text("DON'T", 60, 228, 34, new Color(0.96f, 0.96f, 0.93f, 0.8f), TexText.Face.Serif, true);
            g.Text("dont dont dont dont", 40, 300, 16, new Color(0.96f, 0.96f, 0.93f, 0.8f), TexText.Face.Serif);
            return g.Make();
        }

        static void MetalSheen(Painter g, int W, int H) { for (int y = 0; y < H; y++) g.Rect(0, y, W, 1, new Color(1, 1, 1, Random.value * 0.04f)); }

        static Texture DoorTex() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x5d6266)); MetalSheen(g, W, H);
            g.Rect(28, 20, 200, 390, Col(0x3e4246));
            g.Rect(40, 40, 176, 150, Col(0x6a6f74)); g.Rect(40, 220, 176, 170, Col(0x6a6f74));
            g.Rect(40, 188, 176, 3, new Color(0, 0, 0, 0.35f)); g.Rect(40, 388, 176, 3, new Color(0, 0, 0, 0.35f));
            g.Rect(190, 205, 26, 40, Col(0x1a1c1e)); g.Rect(196, 210, 6, 4, Col(0xd02020));
            g.Rect(60, 60, 136, 26, Col(0xe8c020));
            g.Text("RESTRICTED", 128, 79, 15, Col(0x1a1a1a), TexText.Face.Sans, true, false, 1);
            g.Grain(8);
        });

        static Texture ExitDoorTex() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x4a5450)); MetalSheen(g, W, H);
            g.Rect(28, 60, 200, 350, Col(0x2e3633));
            g.Rect(70, 110, 116, 40, Col(0xd8d8d0));
            g.Text("STAIRS  B", 128, 137, 18, Col(0x1a1a1a), TexText.Face.Sans, true, false, 1);
            g.Rect(40, 240, 176, 10, Col(0x9a9a92));
            g.Grain(8);
        });

        static Texture PosterTex() => Paint(128, 176, (g, W, H) =>
        {
            g.Fill(Col(0xd8d2bc));
            g.Rect(8, 8, W - 16, 70, Col(0x2a4a6a));
            g.Ellipse(64, 46, 24, 16, 0, Col(0xf0ece0)); g.Circle(84, 34, 9, Col(0xf0ece0));   // the white bear
            g.Text("A QUIET MIND", 64, 102, 15, Col(0x2a2a2a), TexText.Face.Serif, true, false, 1);
            g.Text("is a healthy mind.", 64, 120, 10, Col(0x2a2a2a), TexText.Face.Serif, false, false, 1);
            g.Text("Halvard Institute", 64, 158, 10, Col(0x2a2a2a), TexText.Face.Serif, false, false, 1);
            g.Grain(16);
        });

        static Texture WhiteboardTex() => Paint(256, 160, (g, W, H) =>
        {
            g.Fill(Col(0xe8ebe8));
            var blue = Col(0x2a4aa0);
            g.Text("PWB — suppression schedule", 12, 22, 14, blue);
            g.StrokeRect(20, 40, 60, 30, 2, blue); g.StrokeRect(150, 40, 70, 30, 2, blue); g.Line(80, 55, 150, 55, 2, blue);
            g.Text("subj.", 30, 60, 14, blue); g.Text("???", 170, 60, 14, blue);
            g.Text("DO NOT NAME IT", 60, 110, 14, Col(0xb02020));
            for (int i = 0; i < 6; i++) g.Line(Random.value * W, Random.value * H, Random.value * W, Random.value * H, 2, new Color(0.31f, 0.31f, 0.31f, 0.15f));
        });

        public static Texture PaperTex() => Paint(32, 40, (g, W, H) =>
        {
            g.Fill(Col(0xddd6c4));
            for (int y = 6; y < H - 4; y += 4) g.Rect(4, y, 20 + Random.value * 6, 1, Col(0x5a554c));
        });

        static Texture DrawingTex() => Paint(64, 80, (g, W, H) =>
        {
            g.Fill(Col(0xe8e2d0));
            g.Rect(0, 56, W, 24, Col(0x3a6ad0));   // water
            g.Ellipse(32, 40, 18, 12, 0, Col(0xf8f8f8)); g.StrokeEllipse(32, 40, 18, 12, 0, 2, Col(0x222222));   // a white bear standing over it
            g.Circle(46, 26, 8, Col(0xf8f8f8)); g.StrokeEllipse(46, 26, 8, 8, 0, 2, Col(0x222222));
            g.Ellipse(28, 70, 5, 3, 0.3f, Col(0x1a2a5a));   // something small under the water
        });

        static Texture CheckerFloor() => Paint(512, 512, (g, W, H) =>
        {
            int ts = 128;
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                {
                    g.Rect(x * ts, y * ts, ts, ts, (x + y) % 2 == 1 ? Col(0x8e8a7c) : Col(0xbdb6a4));
                    for (int k = 0; k < 14; k++) g.Ellipse(x * ts + Random.value * ts, y * ts + Random.value * ts, 4 + Random.value * 14, 2 + Random.value * 6, Random.value * 3, (x + y) % 2 == 1 ? new Color(1, 0.98f, 0.9f, 0.05f) : new Color(0.24f, 0.22f, 0.16f, 0.05f));
                }
            for (int i = 0; i <= 4; i++) { g.Rect(i * ts - 1, 0, 2, H, Rgba(20, 18, 12, 0.5f)); g.Rect(0, i * ts - 1, W, 2, Rgba(20, 18, 12, 0.5f)); }
            for (int i = 0; i < 12; i++) { float a = Random.value * 6; g.StrokeEllipse(Random.value * W, Random.value * H, 10 + Random.value * 40, 10 + Random.value * 40, 0, 2, Rgba(20, 18, 12, 0.25f), a, a + 0.6f + Random.value * 3); }
            for (int i = 0; i < 5; i++) g.RadialGradient(Random.value * W, Random.value * H, 0, 40, (0, Rgba(50, 38, 20, 0.35f)), (1, Rgba(50, 38, 20, 0)));
            g.Grain(12);
        });

        static Texture CeilTex() => Paint(256, 256, (g, W, H) =>
        {
            g.Fill(Col(0xc8c5b8));
            for (int i = 0; i < 900; i++) g.Rect(Random.value * W, Random.value * H, 1.5f, 1.5f, Rgba(60, 58, 50, 0.35f));
            g.Rect(0, 0, W, 3, Col(0x8a877c)); g.Rect(0, 0, 3, H, Col(0x8a877c)); g.Rect(0, H / 2 - 1, W, 3, Col(0x8a877c)); g.Rect(W / 2 - 1, 0, 3, H, Col(0x8a877c));
            g.RadialGradient(170, 70, 5, 50, (0, Rgba(110, 80, 30, 0.45f)), (0.8f, Rgba(110, 80, 30, 0.25f)), (1, Rgba(110, 80, 30, 0)));
            g.Grain(8);
        });

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(30, 21);
            w.Carve(1, 1, 8, 7);      // lobby
            w.Carve(9, 3, 20, 5);     // main corridor
            w.Carve(21, 1, 28, 8);    // offices
            w.Carve(21, 11, 28, 15);  // generator room
            w.Carve(11, 11, 18, 15);  // security office
            w.Carve(2, 17, 27, 19);   // observation hall
            w.Carve(2, 9, 8, 13);     // patient ward
            w.Carve(2, 15, 4, 15);    // isolation room
            w[4, 8] = '.';            // ward door from the lobby
            w[3, 14] = 'G';           // the isolation door that isn't there
            for (int y = 3; y <= 5; y++) for (int x = 13; x <= 15; x++) w[x, y] = 'P';
            w[25, 9] = 'G'; w[25, 10] = 'G';
            w[19, 13] = 'D'; w[20, 13] = 'D';
            w[14, 16] = '.';
            foreach (var p in new[] { (6, 18), (10, 17), (12, 19), (17, 18), (21, 17), (24, 19) }) w[p.Item1, p.Item2] = '#';
            w[1, 18] = 'X';
            foreach (var p in new[] { (4, 0), (17, 2), (29, 4), (10, 13), (20, 16) }) w[p.Item1, p.Item2] = 'T';

            var wallM = Mats.Std(Color.white, 0.85f, 0, WallTex());
            w.CellBlocks("#", wallM, "wall");
            w.CellBlocks("T", Mats.Std(Color.white, 0.85f, 0, TraceTex()), "trace", WALL_H, "traces");
            doorD = w.CellBlocks("D", Mats.Std(Color.white, 0.45f, 0.4f, DoorTex()), "wall", WALL_H, "powered door");
            w.CellBlocks("X", Mats.Std(Color.white, 0.45f, 0.4f, ExitDoorTex()), "exit", WALL_H, "stairwell door");

            var lino = TextureLibrary.Find(l => l.oldLinoleum);
            var floorM = lino != null ? Mats.Std(Color.white, 0.45f, 0, lino.diff, new Vector2(w.W * 0.75f, w.H * 0.75f), lino.nor, 0.6f)
                                      : Mats.Std(Color.white, 0.45f, 0, CheckerFloor(), new Vector2(w.W / 2f, w.H / 2f));
            var ceilSet = TextureLibrary.Find(l => l.ceilingInterior);
            var ceilM = ceilSet != null ? Mats.Std(MathX.Hex(0xb8b4a8), 0.95f, 0, ceilSet.diff, new Vector2(w.W, w.H), ceilSet.nor, 0.5f)
                                        : Mats.Std(Color.white, 0.95f, 0, CeilTex(), new Vector2(w.W, w.H));
            w.FloorAndCeiling(floorM, ceilM);

            // The collapsed floor: a black hole, rubble, and planks that only half exist
            w.FloorPlane(3 * C, 3 * C, Mats.Unlit(Color.black), new Vector3(cx(14), 0.01f, cx(4)));
            for (int i = 0; i < 8; i++)
                Bx(0.3f + Random.value * 0.4f, 0.15f, 0.3f + Random.value * 0.4f, Std(0x6a665e)).At(cx(12) + 0.8f + Random.value * 0.4f, 0.07f, cx(3) + Random.value * 5).RotY(Random.value * 3);
            var br = Grp("bridge");
            for (int i = 0; i < 9; i++) Bx(6.3f, 0.08f, 0.5f, null, br).At(cx(14), 0.04f, 3 * C + 0.4f + i * 0.65f).RotY(MathX.Jit(0.04f));
            var cells = new List<Vector2Int>(); for (int y = 3; y <= 5; y++) for (int x = 13; x <= 15; x++) cells.Add(new Vector2Int(x, y));
            bridge = AddFocusable("bridge", br, cells, new Vector3(cx(14), 0.3f, cx(4)), 2.2f, Std(0x6a5238));

            // A doorway that isn't there
            var gd = Grp("ghost door");
            foreach (var b in new[] { (-0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0f, 2.65f, 0f, 1.85f, 0.15f, 2 * C) })
                Bx(b.Item4, b.Item5, b.Item6, null, gd).At(b.Item1, b.Item2, b.Item3);
            gd.At(cx(25), 0, cx(9) + C / 2);
            ghostDoor = AddFocusable("door", gd, new[] { new Vector2Int(25, 9), new Vector2Int(25, 10) }, new Vector3(cx(25), 1.4f, cx(9) + C / 2), 2.6f, Std(0x6a6e64));

            // The isolation room door, another thing you have to look at to believe
            var idr = Grp("iso door");
            foreach (var b in new[] { (-0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0f, 2.65f, 0f, 1.85f, 0.15f, 2 * C) })
                Bx(b.Item4, b.Item5, b.Item6, null, idr).At(b.Item1, b.Item2, b.Item3);
            idr.At(cx(3), 0, cx(14)).RotY(PI / 2);
            isoDoor = AddFocusable("door", idr, new[] { new Vector2Int(3, 14) }, new Vector3(cx(3), 1.4f, cx(14)), 3.2f, Std(0x8a8e84));

            // Furniture, documents, the lever, the recorder
            System.Func<int, int, float, GameObject> desk = (x, y, rot) =>
            {
                var g = Grp("desk");
                Bx(1.5f, 0.08f, 0.8f, Std(0x5a4a38), g).At(0, 0.76f, 0);
                foreach (var l in new[] { (-0.65f, -0.32f), (0.65f, -0.32f), (-0.65f, 0.32f), (0.65f, 0.32f) }) Bx(0.06f, 0.76f, 0.06f, Std(0x333333), g).At(l.Item1, 0.38f, l.Item2);
                g.At(cx(x), 0, cx(y)).RotY(rot);
                Prop(g, x, y);
                return g;
            };
            System.Action<int, int, string, Texture> doc = (x, y, id, tex) =>
            {
                var p = Pl(0.32f, 0.42f, Mats.Unlit(MathX.Hex(0xb8b2a4), tex));
                p.At(cx(x), 0.81f, cx(y)).Rot(-PI / 2, 0, Random.value - 0.5f);
                World.Tagged(p, "doc", id);
                AddInteract("doc", "doc", id, p.transform.position, p, "read");
            };
            var paper = PaperTex();
            desk(4, 2, 0); doc(4, 2, "brochure", paper);
            desk(10, 5, 0); doc(10, 5, "teoNote", paper);
            desk(23, 2, 0); doc(23, 2, "session41", paper);
            desk(27, 6, 0.4f); doc(27, 6, "memo", paper);
            desk(16, 12, 0); doc(16, 12, "drawing", DrawingTex());
            desk(22, 5, -0.3f);
            desk(13, 11, 0);
            desk(7, 9, 0); doc(7, 9, "w_chart", paper);
            desk(5, 13, PI); doc(5, 13, "w_nurse", paper);
            { var p = Pl(0.36f, 0.46f, Mats.Unlit(MathX.Hex(0xb8b2a4), paper)); p.At(cx(3), 0.01f, cx(15) - 0.4f).Rot(-PI / 2, 0, 0.4f); World.Tagged(p, "doc", "w_iso"); AddInteract("doc", "doc", "w_iso", p.transform.position, p, "read"); }
            { var p = Pl(0.3f, 0.4f, Mats.Unlit(MathX.Hex(0xb8b2a4), paper)); p.At(29 * C - 0.03f, 1.45f, cx(13) + 1.3f).RotY(-PI / 2); World.Tagged(p, "doc", "w_fuse"); AddInteract("doc", "doc", "w_fuse", p.transform.position, p, "read"); }

            // The three fuses the breaker needs
            System.Action<string, Vector3> fuse = (id, pos) =>
            {
                var f = Grp("fuse");
                Cy(0.035f, 0.035f, 0.16f, 12, Mats.Std(MathX.Hex(0xd8d0b0), 0.4f, 0, null, null, null, 1, MathX.Hex(0xffb040), 0.35f, false, false), f).Rot(0, 0, PI / 2);
                foreach (var sx in new[] { -0.09f, 0.09f }) Cy(0.04f, 0.04f, 0.03f, 12, Mats.Std(MathX.Hex(0xb08a40), 0.3f, 0.8f), f).At(sx, 0, 0).Rot(0, 0, PI / 2);
                f.At(pos.x, pos.y, pos.z).RotY(Random.value * 3);
                World.Tagged(f, "fuse", id);
                var it = AddInteract("fuse", "fuse", id, pos, f, "take the fuse"); it.mesh = f;
            };
            fuse("f1", new Vector3(9 * C - 0.3f, 0.66f, cx(12) + 0.85f));   // ward: on a bedside cabinet
            fuse("f2", new Vector3(cx(22) + 0.3f, 0.84f, cx(5)));    // offices: on the maintenance desk
            fuse("f3", new Vector3(cx(2) + 0.3f, 0.06f, cx(15)));    // isolation: on the floor, in the corner

            // the breaker lever
            var lever = Grp("lever");
            Bx(0.9f, 1.3f, 0.3f, Std(0x4a5048), lever).At(0, 1.2f, 0);
            leverHandle = Bx(0.08f, 0.5f, 0.08f, Std(0xa02020), lever).At(0, 1.35f, 0.2f).Rot(0.6f, 0, 0);
            for (int i = 0; i < 3; i++)   // three empty fuse sockets beside the lever
            {
                Bx(0.14f, 0.1f, 0.06f, Std(0x1a1a1a), lever).At(-0.28f + i * 0.28f, 1.75f, 0.17f);
                var fs = Cy(0.035f, 0.035f, 0.16f, 10, Mats.Std(MathX.Hex(0xd8d0b0), 0.4f, 0, null, null, null, 1, MathX.Hex(0xffb040), 0.6f, false, false), lever).At(-0.28f + i * 0.28f, 1.75f, 0.21f).Rot(0, 0, PI / 2);
                fs.SetActive(false); fuseSlots.Add(fs);
            }
            lever.At(cx(28) + 0.6f, 0, cx(13)).RotY(-PI / 2);
            World.Tagged(lever, "lever");
            leverIt = AddInteract("lever", "lever", null, new Vector3(cx(28), 1.2f, cx(13)), lever, "pull the lever");

            // Teo's recorder, on the floor
            var rec = Bx(0.18f, 0.06f, 0.28f, Std(0x222222)).At(cx(12), 0.03f, cx(14)).RotY(0.5f);
            recorderLed = Mats.Unlit(MathX.Hex(0xff2020)); recorderLed = new Material(recorderLed);
            Bx(0.03f, 0.02f, 0.03f, recorderLed, rec).At(0.05f, 0.04f, 0.08f);
            World.Tagged(rec, "recorder");
            recorderIt = AddInteract("recorder", "recorder", null, rec.transform.position, rec, "play the recorder");

            // the stairwell door
            AddInteract("exit", "exit", null, new Vector3(cx(1) + 0.6f, 1.2f, cx(18)), null, "open the door");

            Decorate();
            Lights();
        }

        void Decorate()
        {
            var metal = Mats.Std(MathX.Hex(0x7a7e80), 0.45f, 0.6f); var darkMetal = Mats.Std(MathX.Hex(0x3a3c3e), 0.5f, 0.5f);
            var plastic = Std(0x5a6a70, 0.7f); var cream = Std(0xd8d2bc, 0.6f); var rubber = Std(0x1a1a1a, 0.9f);
            System.Func<GameObject, int, int, GameObject> add = (o, sx, sy) => Prop(o, sx, sy);

            w.Skirting(rubber, "#TDX");

            // pipes and ducts along the corridor ceiling
            foreach (var p in new[] { (3 * C + 0.35f, 0.06f, 0x6a5a4a), (3 * C + 0.6f, 0.04f, 0x7a7e80), (6 * C - 0.4f, 0.14f, 0x8a8e90) })
            {
                Cy(p.Item2, p.Item2, 12 * C, 12, Mats.Std(MathX.Hex(p.Item3), 0.5f, 0.5f)).At(9 * C + 6 * C, WALL_H - 0.2f - p.Item2, p.Item1).Rot(0, 0, PI / 2);
                for (int i = 0; i < 6; i++) Bx(0.04f, 0.2f, p.Item2 * 2 + 0.06f, darkMetal).At(9 * C + 1 + i * 4, WALL_H - 0.1f, p.Item1);
            }
            System.Action<float, float> vent = (x, z) =>
            {
                Bx(0.6f, 0.03f, 0.6f, darkMetal).At(x, WALL_H - 0.015f, z);
                for (int i = 0; i < 6; i++) Bx(0.5f, 0.02f, 0.03f, rubber).At(x, WALL_H - 0.035f, z - 0.22f + i * 0.09f);
            };
            vent(cx(3), cx(2)); vent(cx(24), cx(7)); vent(cx(16), cx(14)); vent(cx(7), cx(18)); vent(cx(20), cx(18));

            // scattered papers on the floor everywhere
            var paperM = Mats.Std(MathX.Hex(0xe0d8c4), 0.95f, 0, null, null, null, 1, null, 1, true);
            for (int i = 0; i < 70; i++)
            {
                int x, y; do { x = 1 + Random.Range(0, w.W - 2); y = 1 + Random.Range(0, w.H - 2); } while (w[x, y] != '.');
                Pl(0.21f, 0.29f, paperM).At(cx(x) + MathX.Jit(1.6f), 0.004f + Random.value * 0.003f, cx(y) + MathX.Jit(1.6f)).Rot(-PI / 2, 0, Random.value * 6).NoShadow();
            }
            // posters of the institute's motto
            var posterM = Mats.Std(Color.white, 0.9f, 0, PosterTex());
            System.Action<float, float, float> poster = (x, z, ry) => Pl(0.6f, 0.82f, posterM).At(x, 1.75f, z).RotY(ry);
            poster(cx(6), C + 0.02f, 0); poster(cx(12), 3 * C + 0.02f, 0); poster(cx(22), C + 0.02f, 0); poster(cx(16), 11 * C + 0.02f, 0);

            // ---- Lobby: reception, waiting chairs, a wheelchair, a notice board ----
            System.Func<float, float, float, GameObject> waitingChair = (x, z, rot) =>
            {
                var g = Grp("chair");
                Bx(0.48f, 0.06f, 0.46f, plastic, g).At(0, 0.44f, 0);
                Bx(0.48f, 0.42f, 0.05f, plastic, g).At(0, 0.72f, -0.21f).Rot(-0.1f, 0, 0);
                foreach (var sx in new[] { -0.2f, 0.2f }) Bx(0.03f, 0.44f, 0.4f, metal, g).At(sx, 0.22f, 0);
                g.At(x, 0, z).RotY(rot);
                return g;
            };
            for (int i = 0; i < 5; i++) { var c = waitingChair(cx(2) + i * 0.55f - 0.3f, cx(7) + 0.5f, PI); if (i % 2 == 0) add(c, 2 + (i >> 1), 7); else add(c, -1, -1); }
            add(waitingChair(cx(6) + 0.4f, cx(6), PI + 1.9f), -1, -1);   // knocked sideways
            var wheel = Grp("wheelchair");
            foreach (var sx in new[] { -0.3f, 0.3f }) To(0.3f, 0.025f, 8, 24, metal, wheel).At(sx, 0.32f, 0).RotY(PI / 2);
            Bx(0.5f, 0.05f, 0.45f, Std(0x2a2a2e, 0.8f), wheel).At(0, 0.5f, 0);
            Bx(0.5f, 0.45f, 0.04f, Std(0x2a2a2e, 0.8f), wheel).At(0, 0.78f, -0.22f);
            wheel.At(cx(7), 0, cx(1) + 0.3f).RotY(0.7f); add(wheel, -1, -1);
            var board = Grp("noticeboard");
            Bx(1.2f, 0.8f, 0.03f, Std(0x8a6a44, 1), board);
            for (int i = 0; i < 6; i++) Pl(0.22f, 0.28f, cream, board).At(-0.4f + (i % 3) * 0.4f, 0.15f - (i / 3) * 0.32f, 0.02f).Rot(0, 0, MathX.Jit(0.3f));
            board.At(C + 0.03f, 1.6f, cx(3)).RotY(PI / 2);
            var plant = Grp("plant");
            Cy(0.22f, 0.17f, 0.42f, 16, Std(0x6a5a4a), plant).At(0, 0.21f, 0);
            for (int i = 0; i < 7; i++) Cy(0.008f, 0.012f, 0.7f, 4, Std(0x4a3a2a, 0.9f), plant).At(MathX.Jit(0.15f), 0.7f, MathX.Jit(0.15f)).Rot(MathX.Jit(0.9f), 0, MathX.Jit(0.9f));
            plant.At(cx(8) - 0.5f, 0, cx(1) - 0.4f); add(plant, -1, -1);

            // ---- Corridor: an abandoned gurney and a warning sign ----
            var gurney = Grp("gurney");
            Bx(0.7f, 0.12f, 1.9f, Std(0xb8c0c0, 0.8f), gurney).At(0, 0.88f, 0);
            Bx(0.74f, 0.03f, 1.2f, Std(0xe8e8e0, 1), gurney).At(0, 0.96f, 0.3f).Rot(0.04f, 0, 0);
            foreach (var sx in new[] { -0.3f, 0.3f }) foreach (var sz in new[] { -0.85f, 0.85f })
                { Cy(0.02f, 0.02f, 0.8f, 6, metal, gurney).At(sx, 0.42f, sz); Sp(0.05f, 8, 6, rubber, gurney).At(sx, 0.05f, sz); }
            gurney.At(cx(18), 0, cx(5) + 0.45f).RotY(PI / 2 + 0.15f); add(gurney, -1, -1);
            var caution = Grp("caution");
            var signTex = Paint(64, 96, (g2, W2, H2) =>
            {
                g2.Fill(Col(0xe8c020));
                g2.Text("CAUTION", W2 / 2, 30, 12, Col(0x111111), TexText.Face.Sans, true, false, 1);
                g2.Text("FLOOR", W2 / 2, 52, 9, Col(0x111111), TexText.Face.Sans, false, false, 1);
                g2.Text("UNSAFE", W2 / 2, 64, 9, Col(0x111111), TexText.Face.Sans, false, false, 1);
            });
            var signM = Mats.Std(Color.white, 0.6f, 0, signTex, null, null, 1, null, 1, true);
            foreach (var s in new[] { -1, 1 }) Pl(0.32f, 0.6f, signM, caution).At(0, 0.3f, s * 0.1f).Rot(s * 0.17f, s < 0 ? PI : 0, 0);
            caution.At(cx(12) - 0.2f, 0, cx(3) + 0.3f).RotY(0.4f); add(caution, -1, -1);

            // ---- Offices: filing cabinets, office chairs, old computers, a whiteboard ----
            System.Func<float, float, float, int, GameObject> cabinet = (x, z, rot, open) =>
            {
                var g = Grp("cabinet");
                Bx(0.5f, 1.3f, 0.6f, Mats.Std(MathX.Hex(0x8a8e84), 0.5f, 0.4f), g).At(0, 0.65f, 0);
                for (int i = 0; i < 4; i++)
                {
                    float o = open == i ? 0.3f : 0;
                    Bx(0.44f, 0.28f, 0.03f, Mats.Std(MathX.Hex(0x9a9e94), 0.45f, 0.4f), g).At(0, 0.17f + i * 0.31f, 0.3f + o);
                    Bx(0.12f, 0.02f, 0.03f, metal, g).At(0, 0.2f + i * 0.31f, 0.33f + o);
                }
                g.At(x, 0, z).RotY(rot); return g;
            };
            add(cabinet(cx(28) + 0.5f, cx(1) - 0.3f, -PI / 2, 2), 28, 1);
            add(cabinet(cx(28) + 0.5f, cx(2) - 0.6f, -PI / 2, -1), 28, 2);
            add(cabinet(cx(21) - 0.5f, cx(8) + 0.4f, PI / 2, 1), -1, -1);
            System.Func<float, float, float, bool, GameObject> officeChair = (x, z, rot, tipped) =>
            {
                var g = Grp("office chair");
                Bx(0.48f, 0.08f, 0.46f, Std(0x2a2a30, 0.8f), g).At(0, 0.48f, 0);
                Bx(0.46f, 0.5f, 0.06f, Std(0x2a2a30, 0.8f), g).At(0, 0.8f, -0.22f);
                Cy(0.03f, 0.03f, 0.4f, 8, metal, g).At(0, 0.26f, 0);
                for (int i = 0; i < 5; i++) { float a = i / 5f * PI * 2; Bx(0.03f, 0.03f, 0.3f, metal, g).At(Mathf.Sin(a) * 0.15f, 0.06f, Mathf.Cos(a) * 0.15f).RotY(a); }
                g.At(x, tipped ? 0.25f : 0, z).Rot(tipped ? PI / 2 : 0, rot, 0); return g;
            };
            add(officeChair(cx(23), cx(2) + 0.85f, PI + 0.3f, false), -1, -1); add(officeChair(cx(22) + 0.4f, cx(5) + 0.9f, 2.6f, true), -1, -1);
            add(officeChair(cx(27) - 0.2f, cx(6) + 0.9f, PI - 0.2f, false), -1, -1); add(officeChair(cx(13), cx(11) + 0.85f, PI + 0.4f, false), -1, -1);
            System.Func<float, float, float, GameObject> crt = (x, z, rot) =>
            {
                var g = Grp("crt");
                Bx(0.42f, 0.36f, 0.4f, Std(0xc8c2ae, 0.6f), g).At(0, 0.18f, 0);
                Pl(0.32f, 0.25f, Mats.Std(MathX.Hex(0x101414), 0.1f, 0.3f), g).At(0, 0.19f, 0.201f);
                Bx(0.42f, 0.03f, 0.14f, Std(0xc8c2ae, 0.6f), g).At(0, 0, 0.33f);
                g.At(x, 0.8f, z).RotY(rot); return g;
            };
            crt(cx(23) + 0.35f, cx(2) - 0.15f, 0.2f); crt(cx(22), cx(5) - 0.15f, -0.3f);
            var wb = Grp("whiteboard");
            Bx(1.8f, 1.1f, 0.04f, Mats.Std(MathX.Hex(0x8a8e90), 0.4f, 0.5f), wb);
            Pl(1.7f, 1.0f, Mats.Std(Color.white, 0.3f, 0, WhiteboardTex()), wb).At(0, 0, 0.025f);
            wb.At(cx(25), 1.6f, C + 0.04f);
            int[] fileCols = { 0xc8a050, 0xd8d0bc, 0x8a6a3a };
            for (int i = 0; i < 4; i++)
            {
                var st = Grp("files");
                for (int k = 0; k < 6; k++) Bx(0.32f, 0.04f, 0.24f, Std(MathX.Pick(fileCols), 0.9f), st).At(0, 0.02f + k * 0.042f, 0).RotY(MathX.Jit(0.4f));
                st.At(cx(21 + i * 2) + MathX.Jit(1), 0, cx(7) + MathX.Jit(1));
            }

            // ---- Generator room: the generator itself, gauges, barrels, hazard stripes ----
            var gen = Grp("generator");
            Cy(0.8f, 0.8f, 3.0f, 24, Mats.Std(MathX.Hex(0x4a5a4a), 0.5f, 0.5f), gen).At(0, 0.95f, 0).Rot(0, 0, PI / 2);
            Bx(3.4f, 0.2f, 1.8f, darkMetal, gen).At(0, 0.1f, 0);
            for (int i = 0; i < 5; i++) To(0.82f, 0.04f, 6, 24, darkMetal, gen).At(-1.2f + i * 0.6f, 0.95f, 0).RotY(PI / 2);
            for (int i = 0; i < 3; i++)
            {
                Cy(0.1f, 0.1f, 0.04f, 20, Std(0xe8e4d8, 0.4f), gen).At(-0.6f + i * 0.6f, 1.4f, 0.82f).Rot(PI / 2, 0, 0);
                Bx(0.01f, 0.08f, 0.01f, Std(0xb01010, 0.4f), gen).At(-0.6f + i * 0.6f, 1.42f, 0.85f).Rot(0, 0, -0.6f + i * 0.5f);
            }
            Cy(0.12f, 0.12f, 1.4f, 12, metal, gen).At(1.0f, 2.4f, -0.3f);
            gen.At(cx(22) + 1, 0, cx(11) + 0.2f); add(gen, 22, 11);
            var stripeTex = Paint(128, 32, (g2, W2, H2) =>
            {
                g2.Fill(Col(0x1a1a1a));
                for (int x = -H2; x < W2; x += 24) g2.Poly(new List<Vector2> { new Vector2(x, H2), new Vector2(x + 12, H2), new Vector2(x + 12 + H2, 0), new Vector2(x + H2, 0) }, Col(0xe8c020));
            });
            w.FloorPlane(4.2f, 0.2f, Mats.Std(Color.white, 0.7f, 0, stripeTex, new Vector2(6, 1)), new Vector3(cx(22) + 1, 0.006f, cx(12) + 0.3f));
            int[] barrelCols = { 0x2a4a6a, 0x6a2a1a, 0x3a4a2a };
            foreach (var b in new[] { (cx(27) + 0.4f, cx(15) + 0.3f), (cx(26) + 0.5f, cx(15) + 0.5f), (cx(21) - 0.3f, cx(15) + 0.4f) })
                Cy(0.3f, 0.3f, 0.9f, 18, Mats.Std(MathX.Hex(MathX.Pick(barrelCols)), 0.5f, 0.4f)).At(b.Item1, 0.45f, b.Item2);
            for (int i = 0; i < 2; i++) Bx(0.5f, 0.7f, 0.18f, Mats.Std(MathX.Hex(0x6a6e64), 0.5f, 0.4f)).At(cx(28) + 0.85f, 1.4f, cx(11) + i * 0.7f);

            // ---- Security office: a row of lockers, a key rack ----
            for (int i = 0; i < 4; i++)
            {
                var lk = Grp("locker");
                Bx(0.45f, 1.9f, 0.5f, Mats.Std(MathX.Hex(0x4a5a6a), 0.5f, 0.4f), lk).At(0, 0.95f, 0);
                for (int v = 0; v < 4; v++) Bx(0.3f, 0.02f, 0.01f, rubber, lk).At(0, 1.6f + v * 0.05f, 0.255f);
                lk.At(cx(11) - 0.7f, 0, cx(15) - 0.6f - i * 0.48f).RotY(PI / 2);
            }
            Bx(0.5f, 0.3f, 0.04f, Std(0x6a4a2a, 0.8f)).At(cx(18) + 0.95f, 1.6f, cx(12)).RotY(-PI / 2);

            // ---- Observation hall: rows of chairs facing a dark one-way window ----
            Pl(10, 1.4f, Mats.Std(MathX.Hex(0x0a0e12), 0.05f, 0.9f)).At(cx(20), 1.7f, 17 * C + 0.03f);
            Bx(10.2f, 0.08f, 0.06f, darkMetal).At(cx(20), 2.44f, 17 * C + 0.03f);
            Bx(10.2f, 0.08f, 0.06f, darkMetal).At(cx(20), 0.96f, 17 * C + 0.03f);
            for (int r = 0; r < 2; r++) for (int i = 0; i < 6; i++)
                {
                    if (Random.value < 0.15f) continue;
                    waitingChair(cx(17) + i * 1.0f, cx(19) - r * 1.1f + 0.4f, PI + MathX.Jit(0.3f));
                }

            // ---- Patient ward: six iron beds, curtain rails, an IV stand ----
            var sheet = Std(0xd8d4c8, 0.95f); var iron = Mats.Std(MathX.Hex(0x9aa0a0), 0.5f, 0.6f);
            System.Action<float, float, float, bool> bed = (x, z, rot, unmade) =>
            {
                var g = Grp("bed");
                Bx(1.9f, 0.16f, 0.85f, sheet, g).At(0, 0.55f, 0).Rot(0, 0, unmade ? 0.03f : 0);
                Bx(0.35f, 0.1f, 0.6f, Std(0xe8e4d8, 0.95f), g).At(-0.72f, 0.68f, 0);
                if (unmade) Bx(1.0f, 0.04f, 0.9f, Std(0x8a9aa8, 0.95f), g).At(0.5f, 0.63f, 0.1f).Rot(0.2f, 0.3f, 0);
                Bx(1.95f, 0.06f, 0.9f, iron, g).At(0, 0.44f, 0);
                foreach (var sx in new[] { -0.95f, 0.95f }) { Bx(0.05f, sx < 0 ? 1.05f : 0.75f, 0.9f, iron, g).At(sx, sx < 0 ? 0.52f : 0.38f, 0); }
                g.At(x, 0, z).RotY(rot); Prop(g);
                var cab = Bx(0.45f, 0.62f, 0.4f, Std(0xb8bcb0, 0.7f)).At(x + Mathf.Cos(rot) * -0.75f, 0.31f, z + 0.75f); Prop(cab);
            };
            bed(2 * C + 1.05f, cx(9), 0, false); bed(2 * C + 1.05f, cx(11), 0, true); bed(2 * C + 1.05f, cx(13) - 0.3f, 0, false);
            bed(9 * C - 1.05f, cx(10) - 0.3f, PI, true); bed(9 * C - 1.05f, cx(12) + 0.1f, PI, false);
            // bed 3, Subject 14's: the lamp is still clamped to the headboard, and there is a dent at the foot
            Sp(0.12f, 12, 8, Mats.Std(MathX.Hex(0x7a7a6a), 0.5f, 0, null, null, null, 1, MathX.Hex(0xfff0c0), 0.4f, false, false)).At(2 * C + 0.1f, 1.15f, cx(13) - 0.3f);
            Sp(0.35f, 12, 6, sheet).At(2 * C + 1.85f, 0.56f, cx(13) - 0.3f).Scl(1, 0.15f, 1);
            var curtainM = Mats.Std(MathX.Hex(0x8aa09a), 0.95f, 0, null, null, null, 1, null, 1, true);
            foreach (var z in new[] { cx(10), cx(12) })
            {
                Cy(0.015f, 0.015f, 2.6f, 6, iron).At(2 * C + 1.3f, WALL_H - 0.3f, z).Rot(PI / 2, 0, 0);
                Pl(1.2f, 2.0f, curtainM).At(2 * C + 1.6f, 1.6f, z).RotY(PI / 2 + MathX.Jit(0.1f));
            }
            var iv = Grp("iv stand");
            Cy(0.015f, 0.015f, 1.8f, 6, iron, iv).At(0, 0.9f, 0);
            Cy(0.25f, 0.25f, 0.02f, 12, iron, iv).At(0, 0.02f, 0);
            Bx(0.12f, 0.2f, 0.04f, Mats.LitTransparent(new Color(0.8f, 0.9f, 0.9f, 0.5f)), iv).At(0.1f, 1.7f, 0);
            iv.At(cx(6), 0, cx(11) + 0.6f).Rot(0.12f, 0, 0.05f); Prop(iv);
            // ---- Isolation: padded walls, a cot with straps, the same bear scratched over and over ----
            var padTex = Paint(128, 128, (g2, W2, H2) =>
            {
                g2.Fill(Col(0xd8d2c0));
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) g2.RadialGradient(x * 32 + 16, y * 32 + 16, 2, 22, (0, Rgba(255, 255, 255, 0.25f)), (1, Rgba(60, 50, 30, 0.25f)));
                g2.Grain(10);
            });
            var padM = Mats.Std(Color.white, 0.95f, 0, padTex, new Vector2(3, 1.5f));
            Pl(6, WALL_H, padM).At(cx(3), WALL_H / 2, 16 * C - 0.04f).RotY(PI);
            Pl(2, WALL_H, padM).At(5 * C - 0.04f, WALL_H / 2, cx(15)).RotY(-PI / 2);
            Pl(2, WALL_H, padM).At(2 * C + 0.04f, WALL_H / 2, cx(15)).RotY(PI / 2);
            var cot = Grp("cot");
            Bx(1.8f, 0.1f, 0.7f, sheet, cot).At(0, 0.4f, 0);
            Bx(1.85f, 0.35f, 0.75f, iron, cot).At(0, 0.18f, 0);
            foreach (var sx in new[] { -0.5f, 0.3f }) Bx(0.08f, 0.02f, 0.8f, Std(0x5a3a22, 0.8f), cot).At(sx, 0.46f, 0);
            cot.At(cx(4) - 0.2f, 0, cx(15) + 0.4f); Prop(cot);
            Pl(1.4f, 1.4f, Mats.Std(Color.white, 0.85f, 0, TraceTex(), null, null, 1, null, 1, true)).At(cx(3), 1.4f, 16 * C - 0.06f).RotY(PI);

            dust = new DustCloud(w.root, DustCloud.OverCells(w, 350), MathX.Hex(0xdfe6ee), 0.03f, 0.5f);
        }

        void Lights()
        {
            // Fluorescent tubes: dead until the power comes back, then they stutter to life one by one.
            System.Action<int, int, float, float, bool> fluoro = (x, y, intensity, dist, shadow) =>
            {
                Bx(1.5f, 0.08f, 0.4f, Mats.Std(MathX.Hex(0x4a4c48), 0.6f, 0.4f)).At(cx(x), WALL_H - 0.04f, cx(y));
                var F = new Fluoro { baseI = intensity, startAt = 0.3f + Random.value * 2.4f, flicker = Random.value < 0.35f };
                foreach (var dz in new[] { -0.09f, 0.09f })
                {
                    var tm = Mats.Std(MathX.Hex(0x7a7c7a), 0.3f, 0, null, null, null, 1, MathX.Hex(0xeaf2ff), 0, false, false);
                    Bx(1.3f, 0.045f, 0.12f, tm).At(cx(x), WALL_H - 0.1f, cx(y) + dz).NoShadow();
                    F.tubes.Add(tm);
                }
                F.l = w.PointLight(new Vector3(cx(x), WALL_H - 0.4f, cx(y)), MathX.Hex(0xdfe8f2), 0, dist, shadow);
                F.pos = F.l.transform.position;
                F.beam = Mats.Additive(new Color(0.81f, 0.86f, 1f, 0));
                w.Cyl(0.45f, 1.7f, WALL_H - 0.14f, 24, F.beam, null, true).At(cx(x), (WALL_H - 0.14f) / 2, cx(y)).NoShadow();
                fluoros.Add(F);
            };
            fluoro(5, 4, 1.7f, 13, true);
            fluoro(11, 4, 1.3f, 10, false);
            fluoro(18, 4, 1.3f, 10, false);
            fluoro(24, 3, 1.6f, 12, false);
            fluoro(25, 13, 1.7f, 12, true);
            fluoro(15, 13, 1.5f, 11, false);
            fluoro(5, 11, 1.4f, 11, false);
            // Red emergency lamps on the walls, on battery, pulsing slowly
            System.Action<float, float, float> emergency = (x, z, rotY) =>
            {
                var g = Grp("emergency lamp");
                Bx(0.32f, 0.2f, 0.1f, Std(0x2a2a2a, 0.5f), g);
                var lm = Mats.Std(MathX.Hex(0x551010), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff2010), 1, false, false);
                Sp(0.1f, 16, 8, lm, g, 0, PI * 2, 0, PI / 2).At(0, 0, 0.05f).Rot(PI / 2, 0, 0);
                g.At(x, 2.55f, z).RotY(rotY);
                var l = w.PointLight(new Vector3(x + Mathf.Sin(rotY) * 0.45f, 2.35f, z + Mathf.Cos(rotY) * 0.45f), MathX.Hex(0xff2a14), 0.7f, 8);
                emergencies.Add(new Emergency { l = l, m = lm, phase = Random.value * 6 });
            };
            emergency(C + 0.06f, cx(6), PI / 2);
            emergency(29 * C - 0.06f, cx(13), -PI / 2);
            emergency(cx(26), C + 0.06f, 0);
            emergency(cx(9) + 0.5f, 17 * C + 0.06f, 0);
            emergency(cx(8), 9 * C + 0.06f, 0);
            emergency(5 * C - 0.06f, cx(15), -PI / 2);
            // A green EXIT sign over the stairwell door: the only light in the observation hall
            var exitTex = Paint(128, 48, (g2, W2, H2) =>
            {
                g2.Fill(Col(0x0a3a18));
                g2.Text("EXIT", W2 / 2 + 8, 36, 30, Col(0x7dffa6), TexText.Face.Sans, true, false, 1);
                g2.Poly(new List<Vector2> { new Vector2(10, 24), new Vector2(24, 14), new Vector2(24, 34) }, Col(0x7dffa6));
            });
            var sign = Pl(0.75f, 0.28f, Mats.Unlit(Color.white, exitTex)).At(2 * C + 0.04f, 2.75f, cx(18)).RotY(PI / 2);
            w.PointLight(new Vector3(2 * C + 0.5f, 2.6f, cx(18)), MathX.Hex(0x40ff80), 0.6f, 7);
            // Security monitors that flicker on with the power
            for (int i = 0; i < 3; i++)
            {
                var mon = Grp("monitor");
                Bx(0.5f, 0.4f, 0.4f, Std(0x2a2c2a, 0.6f), mon);
                var sm = Mats.Std(MathX.Hex(0x0a120e), 0.2f, 0, null, null, null, 1, MathX.Hex(0x6affc0), 0, false, false);
                Pl(0.4f, 0.3f, sm, mon).At(0, 0, 0.205f);
                mon.At(cx(13) - 0.5f + i * 0.5f, 1.0f, cx(11) + 0.05f).RotY((i - 1) * -0.25f);
                screens.Add((sm, 1.5f + i * 0.4f));
            }
        }

        // ---------------------------------------------------------------- play
        public override void Begin()
        {
            g.px = cx(2); g.pz = cx(4); g.yaw = -PI / 2; g.pitch = 0;
            g.Hint("WASD move  ·  mouse look  ·  E interact  ·  hold SPACE to close your eyes", 8);
        }

        public override string Zone(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            if (y >= 17) return "hall";
            if (x <= 8 && y >= 9) return "wards";
            if (x >= 11 && x <= 18 && y >= 11 && y <= 15) return "security";
            if (x >= 21 && y >= 11) return "generator";
            if (x >= 21) return "offices";
            if (x >= 9) return "corridor";
            return "lobby";
        }

        public override void Tick(float dt)
        {
            float time = g.time; var au = g.au;
            if (g.zone == "corridor" && g.px > cx(10) && !pitHint) { pitHint = true; g.Hint("The floor just... isn't there. But Teo's note said to look at it.", 6); }
            if (g.zone == "offices" && !doorHint && !ghostDoor.done) { doorHint = true; g.Hint("There should be a door here. You're almost sure there was.", 5); }
            if (g.zone == "wards" && !wardHint) { wardHint = true; g.Hint("The patient ward. Somebody left the beds made.", 5); }
            if (g.zone == "wards" && g.pz > cx(13) && !isoHint && !isoDoor.done) { isoHint = true; g.Hint("The chart says there's an isolation room. There's only wall here.", 5); }
            if (recorder && !eyesTaught && !g.HasSubtitles) { eyesTaught = true; g.Hint("Hold SPACE to close your eyes. It can't feed on you if you aren't looking.", 7); }

            dust.Update(time);
            Fluoro nearest = null; float nd = 1e9f;
            foreach (var E in emergencies)
            {
                float k = 0.55f + Mathf.Sin(time * 1.8f + E.phase) * 0.35f;
                E.l.intensity = k * 0.8f * World.LK; Mats.SetEmission(E.m, MathX.Hex(0xff2010), 0.3f + k * 1.2f);
            }
            foreach (var s in screens) Mats.SetEmission(s.m, MathX.Hex(0x6affc0), power && powerT > s.startAt ? 0.55f + Random.value * 0.2f : 0);
            foreach (var F in fluoros)
            {
                float level = 0;
                if (power)
                {
                    float t = powerT - F.startAt;
                    if (t > 0 && t < 1.3f) level = Random.value < 0.4f ? 1 : 0.08f;              // stuttering to life
                    else if (t >= 1.3f) level = F.flicker && Random.value < 0.025f ? 0.1f : 1;    // the odd one never settles
                    if (level != F.last && level == 1 && t < 1.3f && Random.value < 0.6f) au.Flicker();
                    F.last = level;
                }
                F.l.intensity = F.baseI * level * World.LK;
                foreach (var m in F.tubes) Mats.SetEmission(m, MathX.Hex(0xeaf2ff), level * 1.8f);
                Mats.SetAlpha(F.beam, level * 0.04f);
                if (level > 0.5f) { float d = MathX.Hypot(F.pos.x - g.px, F.pos.z - g.pz); if (d < nd) { nd = d; nearest = F; } }
            }
            // the hum of the nearest working tube
            if (nearest != null) au.SetPos(au.buzz, nearest.pos);
            au.Set(au.buzz, nearest != null ? 0.035f : 0);

            if (power && powerT >= 0)
            {
                powerT += dt;
                if (doorD && powerT < 3)
                {
                    doorD.transform.localPosition = new Vector3(0, Mathf.Min(WALL_H, powerT * 1.3f), 0);
                    if (powerT > 1.5f) { w[19, 13] = '.'; w[20, 13] = '.'; }
                }
                RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.5f) * 0.1f;
                if (powerT > 2 && !g.firstSight && g.IT.state == "gone") { g.dread = Mathf.Max(g.dread, 0.36f); g.IT.cool = 0; }
            }

            // the phone ringing past the hall: something to follow with your eyes closed
            float ring = g.hall ? ((time % 3) < 1.2f && (time % 0.4f) < 0.2f ? 0.05f : 0) : 0;
            au.Set(au.phone, ring * (g.eyes ? 1 : 1.6f));
            au.SetPos(au.phone, new Vector3(cx(1) + 0.8f, 1.2f, cx(18)));

            if (g.hall && MathX.Hypot(g.px - cx(1), g.pz - cx(18)) < 2.2f && g.near == null && !exitHint) { exitHint = true; g.Hint("The stairwell door. Open your eyes and press E.", 5); }
        }

        public override void OnInteract(Interactable it)
        {
            var au = g.au;
            if (it.kind == "fuse")
            {
                it.used = true; if (it.mesh) it.mesh.SetActive(false); fuses++; au.Click();
                g.Hint(fuses < 3 ? $"A fuse. {fuses} of 3." : "That's all three. The breaker is in the generator room.", 4);
            }
            else if (it.kind == "lever" && !power && fuses < 3)
            {
                au.Click();
                for (int i = 0; i < 3; i++) fuseSlots[i].SetActive(i < fuses);
                g.Hint(fuses == 0 ? "The breaker's dead. Three fuse sockets, all empty." : $"The breaker's dead. {3 - fuses} fuse{(3 - fuses == 1 ? "" : "s")} still missing.", 5);
            }
            else if (it.kind == "lever" && !power)
            {
                foreach (var fs in fuseSlots) fs.SetActive(true);
                it.used = true; leverHandle.transform.localRotation = MathX.Euler3(-0.6f, 0, 0); au.Lever();
                power = true; powerT = 0; au.PowerUp();
                g.Hint("Something hums awake deep in the building.", 4);
            }
            else if (it.kind == "recorder" && !recorder)
            {
                it.used = true; recorderLed.SetColor("_BaseColor", MathX.Hex(0x20ff40)); recorder = true;
                g.Say(GameData.TeoRecorder);
            }
            else if (it.kind == "exit") g.EndChapter();
        }

        public override string Objective()
        {
            if (!bridge.done) return "Find a way deeper into the institute.";
            if (!power && fuses < 3) return $"Find the three missing fuses for the breaker ({fuses}/3).";
            if (!power) return "Restore the power.";
            if (!recorder) return "Find out what happened to Teo.";
            return "Get through the observation hall. Find the stairs.";
        }

        public override EndInfo End()
        {
            return new EndInfo
            {
                title = "END OF CHAPTER ONE", sub = "Arrival", hasNext = true, next = "Chapter Two: The Waiting Room",
                stats = $"forbidden documents read: {g.forbiddenRead} / 5\ntimes you looked at it: {g.timesSeen}\ntimes it reached you: {g.timesCaught}"
            };
        }
    }
}
