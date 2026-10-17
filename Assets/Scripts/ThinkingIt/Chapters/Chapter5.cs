using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 5: The White Bear Room.
    // The original experiment chamber, deep under the institute. One chair. One bell. One instruction.
    // Teo is in the chair, and "it" is standing over him, holding him the way it held everything else.
    // The confrontation isn't a fight. It's a choice about what to do with the thought:
    //   FORGET   — close your eyes and push it down again (hold SPACE)
    //   REMEMBER — look at it, all of it, until the end (keep your gaze on it)
    //   THINK IT — hidden: only if you have read every forbidden document in the game. Say its name (E).
    // ======================================================================
    public class Chapter5 : Chapter
    {
        public override int Number => 5;
        public override string HallZone => null;
        public override AudioReverbPreset Space => AudioReverbPreset.Hallway;
        public override float FogDensity => 0.06f;
        public override Color FogColor => new Color(0.01f, 0.012f, 0.014f);
        public override Color Ambient => new Color(0.42f, 0.45f, 0.5f) * 0.05f;
        public override bool CustomItActive => finale || ending != null;
        public override bool AllowEyes => ending == null;

        bool tapeHeard, opened, pitHint, boothHint, finale, eligible, forgetHint, rememberHint, trialHint, bearHint;

        // the three trial rooms: reach the switch without looking at the white bear
        class Trial { public Vector3 bear, start; public float look; public bool done; public Light lamp; public Material lampMat, indicator; public GameObject bearGo; }
        readonly Trial[] trials = new Trial[3];
        int switches;
        string ending;
        float doorT = -1, rememberP, forgetP, endT, bellT = 4, tapeHissT;
        int lineIdx;
        GameObject doorD, teo;
        Focusable bridgeA, bridgeB;
        Interactable nameIt;
        readonly Focusable finaleFocus = new Focusable { kind = "it", time = 18 };
        Light ringLight; Material ringMat, buttonMat;
        DustCloud dust;
        readonly List<MeshRenderer> teoParts = new List<MeshRenderer>();
        Vector3 center, bellPos;

        // ---------------------------------------------------------------- textures
        static Texture Tiles() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0xd8dcd8));
            for (int y = 0; y < H; y += 32) for (int x = 0; x < W; x += 32)
                {
                    float b = 0.93f + Random.value * 0.07f;
                    g.Rect(x + 1, y + 1, 30, 30, new Color(0.86f * b, 0.88f * b, 0.86f * b, 1));
                }
            for (int i = 0; i <= W; i += 32) g.Rect(i - 1, 0, 2, H, Rgba(120, 125, 120, 0.8f));
            for (int i = 0; i <= H; i += 32) g.Rect(0, i - 1, W, 2, Rgba(120, 125, 120, 0.8f));
            for (int i = 0; i < 6; i++) { float sx = Random.value * W; g.VGradient(sx, 0, 3 + Random.value * 6, H, 0, H * 0.8f, (0, Rgba(90, 80, 50, 0.3f)), (1, Rgba(90, 80, 50, 0))); }
            g.VGradient(0, 0, W, H, H * 0.7f, H, (0, new Color(0, 0, 0, 0)), (1, Rgba(30, 30, 25, 0.4f)));
            g.Grain(8);
        });

        static Texture FloorTiles() => Paint(256, 256, (g, W, H) =>
        {
            g.Fill(Col(0xb8bcb8));
            for (int y = 0; y < H; y += 64) for (int x = 0; x < W; x += 64)
                {
                    float b = 0.9f + Random.value * 0.1f;
                    g.Rect(x + 2, y + 2, 60, 60, new Color(0.76f * b, 0.78f * b, 0.76f * b, 1));
                }
            for (int i = 0; i < 4; i++) g.RadialGradient(Random.value * W, Random.value * H, 0, 50, (0, Rgba(40, 35, 25, 0.25f)), (1, new Color(0, 0, 0, 0)));
            g.Grain(10);
        });

        static Texture BearPoster() => Paint(128, 176, (g, W, H) =>
        {
            g.Fill(Col(0xe8e4d8));
            g.Ellipse(64, 70, 34, 22, 0, Col(0xfafafa)); g.StrokeEllipse(64, 70, 34, 22, 0, 2, Col(0x333333));
            g.Circle(94, 52, 12, Col(0xfafafa)); g.StrokeEllipse(94, 52, 12, 12, 0, 2, Col(0x333333));
            foreach (var lx in new[] { 40, 54, 74, 86 }) g.Line(lx, 86, lx, 106, 3, Col(0x333333));
            g.Line(10, 30, 118, 120, 6, Col(0xb02020)); g.Line(118, 30, 10, 120, 6, Col(0xb02020));
            g.Text("DON'T.", 64, 150, 22, Col(0x1a1a1a), TexText.Face.Serif, true, false, 1);
            g.Grain(14);
        });

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(26, 20);
            w.Carve(1, 9, 9, 11);                          // prep corridor
            w.Carve(3, 5, 6, 7); w[4, 8] = '.';            // consent room
            w.Carve(5, 13, 8, 15); w[6, 12] = '.';         // locker room
            w.Carve(10, 9, 13, 11);                        // the collapsed walkway...
            for (int y = 9; y <= 11; y++) for (int x = 10; x <= 13; x++) w[x, y] = 'P';
            w.Carve(14, 9, 16, 12);                        // observation booth
            for (int y = 9; y <= 11; y++) w[17, y] = 'V';  // the one-way window
            w[17, 12] = 'D';                               // the chamber door
            w.Carve(18, 5, 24, 16);                        // the White Bear Room
            w.Carve(10, 13, 16, 13);                       // the trial corridor, under the booth
            w.Carve(10, 15, 11, 18); w[10, 14] = '.';      // trial room A
            w.Carve(13, 15, 14, 18); w[13, 14] = '.';      // trial room B
            w.Carve(16, 15, 16, 18); w[16, 14] = '.';      // trial room C
            center = new Vector3(cx(21), 0, cx(10) + 1);

            var inst = Chapter1.WallMat();
            var tiles = Mats.Std(Color.white, 0.35f, 0, Tiles());
            w.WallFaces("#", ".PVD", (nx, ny) => ZoneCell(nx, ny) == "chamber" ? tiles : inst);
            var lino = TextureLibrary.Find(l => l.oldLinoleum);
            var floorM = Chapter1.FloorMat(new Vector2(w.W * 0.75f, w.H * 0.75f), 0.5f);
            var ceilSet = TextureLibrary.Find(l => l.ceilingInterior);
            var ceilM = ceilSet != null ? Mats.Std(MathX.Hex(0xa8a49a), 0.95f, 0, ceilSet.diff, new Vector2(w.W, w.H), ceilSet.nor, 0.5f) : Std(0xa8a49a, 0.95f);
            w.FloorAndCeiling(floorM, ceilM);
            w.FloorPlane(7 * C, 12 * C, Mats.Std(Color.white, 0.3f, 0, FloorTiles(), new Vector2(7, 12)), new Vector3(cx(21), 0.004f, 5 * C + 6 * C)).NoShadow();
            w.Skirting(Std(0x1a1a1a, 0.9f), "#");

            // the chamber door, which slides up when the console opens it
            doorD = w.CellBlocks("D", Mats.Std(MathX.Hex(0x8a9094), 0.4f, 0.6f), "wall", WALL_H, "chamber door");

            Prep();
            Pit();
            Booth();
            Chamber();
            Trials();
            dust = new DustCloud(w.root, DustCloud.OverCells(w, 300, (x, y) => w[x, y] != '#' && w[x, y] != 'V'), MathX.Hex(0xdfe6ee), 0.03f, 0.5f);
        }

        void Doc(float x, float y, float z, string id, bool wall = false, float rotY = 0)
        {
            var p = Pl(0.26f, 0.33f, Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper()));
            if (wall) p.At(x, y, z).RotY(rotY); else p.At(x, y, z).Rot(-PI / 2, 0, MathX.Jit(0.6f));
            World.Tagged(p, "doc", id);
            AddInteract("doc", "doc", id, p.transform.position, p, "read");
        }

        GameObject Desk(float x, float z, float rot)
        {
            var d = Grp("desk");   // two school desks side by side; their open ends face -Z
            foreach (var dx in new[] { -0.37f, 0.37f }) { var sd = BakedMesh.Spawn(w, "SchoolDesk", d.transform, 1.02f); if (sd != null) sd.At(dx, 0, 0); }
            d.At(x, 0, z).RotY(rot);
            return Prop(d);
        }

        void Prep()
        {
            var poster = Mats.Std(Color.white, 0.9f, 0, BearPoster());
            foreach (var x in new[] { 2, 5, 8 }) Pl(0.6f, 0.82f, poster).At(cx(x), 1.7f, 9 * C + 0.02f);
            Pl(1.0f, 0.38f, Mats.Std(Color.white, 0.6f, 0, Chapter3.Sign("SUBLEVEL 0", "White Bear — Room 1"))).At(C + 0.02f, 2.5f, cx(10)).RotY(PI / 2);
            // the consent room: a desk, a chair, the form
            Desk(cx(4) + 1, cx(6), 0);
            Doc(cx(4) + 1.2f, 0.8f, cx(6), "e_consent");
            Prop(ChairModel(cx(4) + 1, cx(6) - 0.7f, 0));
            // the locker room: lockers, a bench, a page someone tore out of a notebook
            for (int i = 0; i < 4; i++)
            {
                var lk = Grp("locker");
                Bx(0.45f, 1.9f, 0.5f, Mats.Std(MathX.Hex(0x4a5a6a), 0.5f, 0.4f), lk).At(0, 0.95f, 0);
                lk.At(cx(5) - 0.7f, 0, cx(13) + 0.3f + i * 0.5f).RotY(PI / 2); Prop(lk);
            }
            var bench = Grp("bench");
            Bx(1.6f, 0.06f, 0.4f, Std(0x5a4a38), bench).At(0, 0.45f, 0);
            foreach (var sx in new[] { -0.7f, 0.7f }) Bx(0.06f, 0.45f, 0.36f, Std(0x333333), bench).At(sx, 0.22f, 0);
            bench.At(cx(7), 0, cx(15) - 0.2f); Prop(bench);
            Doc(cx(7) + 0.3f, 0.49f, cx(15) - 0.2f, "e_name");
        }

        void Pit()
        {
            w.FloorPlane(4 * C, 3 * C, Mats.Unlit(Color.black), new Vector3(10 * C + 2 * C, 0.01f, 9 * C + 1.5f * C));
            var plankM = Std(0x6a5238);
            System.Func<int, int, string, Focusable> span = (x0, x1, kind) =>
            {
                var grp = Grp(kind);
                float len = (x1 - x0 + 1) * C + 0.2f, mid = (cx(x0) + cx(x1)) / 2;
                for (int i = 0; i < 9; i++) Bx(len, 0.08f, 0.5f, null, grp).At(mid, 0.04f, 9 * C + 0.4f + i * 0.65f).RotY(MathX.Jit(0.04f));
                var cells = new List<Vector2Int>();
                for (int y = 9; y <= 11; y++) for (int x = x0; x <= x1; x++) cells.Add(new Vector2Int(x, y));
                return AddFocusable(kind, grp, cells, new Vector3(mid, 0.3f, cx(10)), 2.6f, plankM);
            };
            bridgeA = span(10, 11, "bridgeA");
            bridgeB = span(12, 13, "bridgeB");
            for (int i = 0; i < 6; i++) Bx(0.3f + Random.value * 0.4f, 0.15f, 0.3f + Random.value * 0.4f, Std(0x6a665e)).At(cx(9) + 0.6f + Random.value * 0.3f, 0.07f, cx(9) - 0.6f + Random.value * 4.2f).RotY(Random.value * 3);
        }

        void Booth()
        {
            // the one-way window, looking into the chamber
            var glass = Mats.LitTransparent(new Color(0.12f, 0.16f, 0.18f, 0.35f), 0.05f, 0.5f);
            var frame = Mats.Std(MathX.Hex(0x3a3c3e), 0.5f, 0.5f);
            for (int y = 9; y <= 11; y++)
            {
                Pl(C, 1.6f, glass).At(17 * C + 0.02f, 1.6f, cx(y)).RotY(-PI / 2);
                Pl(C, 1.6f, glass).At(18 * C - 0.02f, 1.6f, cx(y)).RotY(PI / 2);
            }
            Bx(C, 0.8f, 3 * C, Chapter1.WallMat()).At(17 * C + 1, 0.4f, 9 * C + 3);
            Bx(C, 0.8f, 3 * C, Chapter1.WallMat()).At(17 * C + 1, WALL_H - 0.4f, 9 * C + 3);
            foreach (var z in new[] { 9 * C, 10 * C, 11 * C, 12 * C }) Bx(0.12f, 1.6f, 0.12f, frame).At(17 * C + 1, 1.6f, z);
            // the console
            var console = Grp("console");
            Bx(0.8f, 0.9f, 2.6f, Std(0x4a4e4a, 0.6f), console).At(0, 0.45f, 0);
            Bx(0.85f, 0.05f, 2.7f, Std(0x2a2c2a, 0.5f), console).At(0, 0.92f, 0).Rot(0, 0, 0.12f);
            buttonMat = Mats.Std(MathX.Hex(0x601010), 0.4f, 0, null, null, null, 1, MathX.Hex(0xff2010), 1.2f, false, false);
            var button = Cy(0.06f, 0.06f, 0.04f, 16, buttonMat, console).At(0.05f, 0.96f, 0.8f);
            for (int i = 0; i < 8; i++) Cy(0.02f, 0.02f, 0.03f, 8, Mats.Std(MathX.Hex(i % 3 == 0 ? 0x30ff60 : 0xffc040), 0.4f, 0, null, null, null, 1, MathX.Hex(i % 3 == 0 ? 0x30ff60 : 0xffc040), 0.8f), console).At(0.05f, 0.955f, -0.9f + i * 0.18f);
            console.At(cx(16) + 0.45f, 0, cx(10)); Prop(console);
            World.Tagged(button, "console");
            AddInteract("console", "console", null, button.transform.position, button, "open the chamber");
            // the last tape, Teo's note, the protocol
            var tape = Grp("tape");
            Bx(0.11f, 0.018f, 0.07f, Std(0x1a1a1a, 0.4f), tape);
            Sp(0.015f, 6, 6, Mats.Unlit(MathX.Hex(0xfff0d0)), tape).At(0.04f, 0.02f, 0);
            tape.At(cx(16) + 0.4f, 0.95f, cx(10) - 0.6f).RotY(0.4f);
            World.Tagged(tape, "tape", "tape5");
            AddInteract("tape", "tape", "tape5", tape.transform.position, tape, "play the tape");
            Doc(cx(16) + 0.35f, 0.955f, cx(9) + 0.85f, "e_teo");
            Desk(cx(14) + 0.3f, cx(12) + 0.4f, 0);
            Doc(cx(14) + 0.3f, 0.8f, cx(12) + 0.4f, "e_protocol");
            w.PointLight(new Vector3(cx(15), 2.6f, cx(10)), MathX.Hex(0xffc880), 0.8f, 7, true);
        }

        void Chamber()
        {
            // the chair, bolted to the floor, with straps
            var chair = Grp("the chair");   // the old barber's chair, bolted down
            BakedMesh.Spawn(w, "BarberShopChair", chair.transform, 0.9f);
            chair.At(center.x, 0, center.z).RotY(-PI / 2); Prop(chair);
            // Teo, in the chair
            teo = Grp("Teo");
            var skin = Mats.Std(MathX.Hex(0xc8c4bc), 0.9f, 0, null, null, null, 1, MathX.Hex(0xd8e0e8), 0.12f);
            var cloth = Mats.Std(MathX.Hex(0x3a4250), 0.9f, 0, null, null, null, 1, MathX.Hex(0x8090a0), 0.05f);
            System.Action<GameObject> keep = go => teoParts.Add(go.GetComponent<MeshRenderer>());
            keep(Cy(0.17f, 0.2f, 0.62f, 10, cloth, teo).At(0, 0.88f, -0.08f).Rot(-0.15f, 0, 0));         // torso, slumped
            keep(Sp(0.12f, 12, 10, skin, teo).At(0, 1.32f, 0.02f));                                     // head, bowed
            foreach (var sx in new[] { -0.1f, 0.1f })
            {
                keep(Cy(0.065f, 0.065f, 0.46f, 8, cloth, teo).At(sx, 0.58f, 0.2f).Rot(PI / 2, 0, 0));    // thighs
                keep(Cy(0.055f, 0.055f, 0.5f, 8, cloth, teo).At(sx, 0.28f, 0.44f));                    // shins
                keep(Cy(0.045f, 0.045f, 0.5f, 8, skin, teo).At(sx * 3.2f, 0.82f, 0.05f).Rot(0.3f, 0, 0)); // arms on the armrests
            }
            teo.At(center.x, 0.06f, center.z).RotY(-PI / 2);
            // the bell, high on the north wall
            bellPos = new Vector3(cx(21), 2.6f, 5 * C + 0.2f);
            var bell = Grp("bell");
            Sp(0.18f, 16, 10, Mats.Std(MathX.Hex(0xb08a3a), 0.3f, 0.8f), bell, 0, PI * 2, 0, PI / 2);
            Bx(0.1f, 0.3f, 0.2f, Std(0x2a2a2a), bell).At(0, 0.1f, -0.1f);
            bell.At(bellPos.x, bellPos.y, bellPos.z);
            // a ring of cold light overhead
            ringMat = Mats.Std(MathX.Hex(0xe8f0ff), 0.3f, 0, null, null, null, 1, MathX.Hex(0xe8f0ff), 2.2f, false, false);
            To(1.6f, 0.05f, 8, 48, ringMat).At(center.x, WALL_H - 0.15f, center.z).Rot(PI / 2, 0, 0).NoShadow();
            ringLight = w.PointLight(new Vector3(center.x, WALL_H - 0.5f, center.z), MathX.Hex(0xdfe8ff), 1.6f, 15, true);
            // a drain in the floor, and the observation glass from this side
            w.Disc(0.25f, 20, Std(0x2a2a2a, 0.4f)).At(center.x + 1.5f, 0.006f, center.z + 1.2f).Rot(-PI / 2, 0, 0).NoShadow();
            // the hidden ending: say its name
            nameIt = AddInteract("nameit", "it", null, center, null, "say its name");
            nameIt.range = 30; nameIt.maxGaze = 30; nameIt.used = true;
        }

        // the white bear: a statue nobody is allowed to think about
        GameObject BearStatue(Vector3 at, float rotY)
        {
            var b = Grp("white bear");
            var white = Mats.Std(MathX.Hex(0xf2f2ee), 0.55f, 0, null, null, null, 1, MathX.Hex(0xe8eef4), 0.15f);
            Sp(0.5f, 16, 12, white, b).At(0, 0.75f, 0).Scl(0.8f, 0.75f, 1.3f);
            Sp(0.27f, 14, 10, white, b).At(0, 1.0f, 0.68f);
            Sp(0.12f, 10, 8, white, b).At(0, 0.95f, 0.92f).Scl(1, 0.8f, 1.2f);
            foreach (var sx in new[] { -0.16f, 0.16f }) Sp(0.08f, 8, 6, white, b).At(sx, 1.22f, 0.62f);
            foreach (var sx in new[] { -0.25f, 0.25f }) foreach (var sz in new[] { -0.38f, 0.38f }) Cy(0.11f, 0.12f, 0.55f, 10, white, b).At(sx, 0.27f, sz);
            foreach (var sx in new[] { -0.08f, 0.08f }) Sp(0.025f, 6, 6, Mats.Unlit(Color.black), b).At(sx, 1.06f, 0.92f);
            Cy(0.6f, 0.65f, 0.08f, 20, Std(0x6a6a66, 0.8f), b).At(0, 0.04f, 0);
            b.At(at.x, 0, at.z).RotY(rotY); World.Tagged(b, "bear");
            return b;
        }

        void Trials()
        {
            var sign = new[] { "A", "B", "C" };
            float[] roomX = { 11 * C, 14 * C, cx(16) };
            int[] doorX = { 10, 13, 16 };
            var switchM = Mats.Std(MathX.Hex(0x4a5048), 0.5f, 0.4f);
            for (int i = 0; i < 3; i++)
            {
                var t = trials[i] = new Trial();
                Bx(C, 0.9f, C, Chapter1.WallMat()).At(cx(doorX[i]), WALL_H - 0.45f, cx(14));   // a lintel over the doorway, for the sign
                t.start = new Vector3(cx(doorX[i]), 0, 15 * C + 0.5f);
                Pl(0.7f, 0.3f, Mats.Std(Color.white, 0.6f, 0, Chapter3.Sign("TRIAL " + sign[i], null))).At(cx(doorX[i]), 2.6f, 14 * C - 0.03f).RotY(PI);
                t.lampMat = Mats.Std(MathX.Hex(0x301010), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff3020), 1.2f, false, false);
                Sp(0.08f, 10, 8, t.lampMat).At(cx(doorX[i]) + 0.7f, 2.4f, 14 * C - 0.08f);
                t.lamp = w.PointLight(new Vector3(roomX[i], WALL_H - 0.4f, cx(16) + 1), MathX.Hex(0xffe8d0), 0.45f, 8);
            }
            // A: the bear at the far end; the switch on the side wall beside it
            trials[0].bear = new Vector3(11 * C + 0.6f, 1.0f, 18 * C + 1.2f);
            trials[0].bearGo = BearStatue(trials[0].bear, PI);
            var swA = Grp("switch"); Bx(0.3f, 0.45f, 0.1f, switchM, swA); Bx(0.06f, 0.2f, 0.06f, Std(0xa02020), swA).At(0, 0.05f, 0.08f).Rot(0.5f, 0, 0);
            swA.At(10 * C + 0.06f, 1.3f, 18 * C + 0.6f).RotY(PI / 2); World.Tagged(swA, "switch", "A");
            AddInteract("switch", "switch", "A", swA.transform.position, swA, "throw the switch");
            // B: the bear stands in the middle of the room, watching the door; the switch is on the far wall behind it
            trials[1].bear = new Vector3(14 * C, 1.0f, cx(16) + 1);
            trials[1].bearGo = BearStatue(trials[1].bear, PI);
            var swB = Grp("switch"); Bx(0.3f, 0.45f, 0.1f, switchM, swB); Bx(0.06f, 0.2f, 0.06f, Std(0xa02020), swB).At(0, 0.05f, 0.08f).Rot(0.5f, 0, 0);
            swB.At(14 * C + 1.2f, 1.3f, 19 * C - 0.06f).RotY(PI); World.Tagged(swB, "switch", "B");
            AddInteract("switch", "switch", "B", swB.transform.position, swB, "throw the switch");
            // C: a narrow room; the bear stands right under the pull-cord at the end
            trials[2].bear = new Vector3(cx(16), 1.0f, 19 * C - 0.8f);
            trials[2].bearGo = BearStatue(trials[2].bear, PI);
            var cord = Grp("switch");
            Cy(0.006f, 0.006f, 0.7f, 5, Std(0xd8d0bc, 0.9f), cord).At(0, -0.35f, 0);
            Cy(0.03f, 0.03f, 0.1f, 10, Std(0xa02020, 0.6f), cord).At(0, -0.72f, 0);
            Cy(0.08f, 0.08f, 0.04f, 12, switchM, cord).At(0, 0, 0);
            cord.At(cx(16), WALL_H - 0.02f, 18 * C + 0.2f); World.Tagged(cord, "switch", "C");
            AddInteract("switch", "switch", "C", new Vector3(cx(16), 2.6f, 18 * C + 0.2f), cord, "pull the cord");
            Doc(cx(12), 1.5f, 14 * C - 0.03f, "e_trials", true, PI);
            // the three trial indicators on the console
            for (int i = 0; i < 3; i++)
            {
                trials[i].indicator = Mats.Std(MathX.Hex(0x401010), 0.4f, 0, null, null, null, 1, MathX.Hex(0xff2010), 0.9f, false, false);
                Sp(0.035f, 10, 8, trials[i].indicator).At(cx(16) + 0.5f, 0.98f, cx(10) + 0.45f + i * 0.1f);
            }
        }

        void TrialTick(float dt)
        {
            if (g.zone != "trial" || !g.eyes) { foreach (var t in trials) t.look = Mathf.Max(0, t.look - dt); return; }
            int i = g.px < 12.5f * C ? 0 : g.px < 15.5f * C ? 1 : 2;
            var T = trials[i];
            if (T.done) return;
            var f = new Vector3(-Mathf.Sin(g.yaw) * Mathf.Cos(g.pitch), Mathf.Sin(g.pitch), -Mathf.Cos(g.yaw) * Mathf.Cos(g.pitch));
            var to = T.bear - new Vector3(g.px, World.EYE, g.pz);
            bool looking = to.magnitude > 0.01f && Vector3.Dot(f, to.normalized) > 0.93f && g.Los(g.px, g.pz, T.bear.x, T.bear.z);
            T.look = looking ? T.look + dt : Mathf.Max(0, T.look - dt * 0.5f);
            if (looking && Random.value < dt * 8) g.au.Whisper(T.bear, 0.12f);
            if (T.look > 0.6f)
            {
                T.look = 0;
                g.au.Tone(880, 876, 2.6f, 0.2f, "sine", T.bear, 0.004f);
                g.au.Tone(2210, 2200, 1.4f, 0.08f, "sine", T.bear, 0.004f);
                g.au.Sting();
                g.dread = MathX.Clamp01(g.dread + 0.12f);
                g.px = T.start.x; g.pz = T.start.z; g.yaw = PI; g.pitch = 0;
                g.Hint(MathX.Pick(new[] { "The bell. You thought of it. Begin again.", "You looked. Begin again.", "Don't think of the bear." }), 3);
            }
        }

        // ---------------------------------------------------------------- zones
        string ZoneCell(int x, int y) => y >= 13 && x >= 10 && x <= 17 ? (y >= 15 ? "trial" : "trialhall") : x <= 9 ? "prep" : x <= 13 ? "pit" : x <= 17 ? "booth" : "chamber";
        public override string Zone(float wx, float wz) => ZoneCell(Mathf.FloorToInt(wx / C), Mathf.FloorToInt(wz / C));

        public override void Begin()
        {
            g.px = cx(1) + 0.3f; g.pz = cx(10); g.yaw = -PI / 2; g.pitch = 0;
            g.Hint("Sublevel 0. The bell is ringing somewhere ahead.", 6);
            eligible = Settings.AllForbiddenRead;
        }

        public override void OnZone(string zone)
        {
            if (zone == "pit" && !pitHint) { pitHint = true; g.Hint("The floor is gone. Look at it until it isn't.", 5); }
            if (zone == "booth" && !boothHint) { boothHint = true; g.Hint("Through the glass: the chair. Someone is sitting in it.", 6); }
            if (zone == "trialhall" && !trialHint) { trialHint = true; g.Hint("Three doors. TRIAL A, B, C. The bell rings behind every one of them.", 6); }
            if (zone == "trial" && !bearHint) { bearHint = true; g.Hint("A white bear. Whatever you do, don't look at it.", 4); }
            if (zone == "chamber" && !finale && ending == null) StartFinale();
        }

        public override float DreadDelta(float dt) => g.zone == "chamber" ? 0.01f : 0;

        // ---------------------------------------------------------------- the choice
        void StartFinale()
        {
            finale = true; rememberP = 0; forgetP = 0; lineIdx = 0;
            g.HideIt(999);
            g.IT.state = "final5"; g.IT.x = center.x; g.IT.z = center.z - 0.15f;
            g.itModel.SetVisible(true); g.itModel.SetScale(1.15f); g.itModel.SetHeight(0);
            g.itModel.coreMat.SetColor("_BaseColor", Color.black);
            g.dread = Mathf.Max(g.dread, 0.45f);
            eligible = Settings.AllForbiddenRead;
            nameIt.used = !eligible;
            g.au.Sting();
            g.Hint("Teo is in the chair. It is standing over him.", 5);
            g.timers.After(5.5f, () => { if (finale) g.Hint("Look at it, and remember. Or close your eyes, and forget.", 8); });
            if (eligible) g.timers.After(14, () => { if (finale) g.Hint("You know its name. You read all of it.", 6); });
        }

        public override bool CustomIt(float dt)
        {
            if (ending != null) { Ending(dt); return true; }
            if (!finale) return false;
            var IT = g.IT;
            bool seen = g.InView(IT.x, IT.z, 0.88f);
            if (seen) rememberP = Mathf.Min(1, rememberP + dt / 18f);
            if (!g.eyes)
            {
                forgetP = Mathf.Min(1, forgetP + dt / 7f);
                if (!forgetHint) { forgetHint = true; g.Hint("Keep them closed. Push it down. Like they taught you.", 5); }
            }
            else forgetP = Mathf.Max(0, forgetP - dt * 0.3f);
            if (seen && !rememberHint && rememberP > 0.03f) { rememberHint = true; g.au.Sting(); }
            // ignoring it, eyes open, is the one thing you can't do
            if (g.eyes && !seen) g.dread = MathX.Clamp01(g.dread + 0.12f * dt);
            else g.dread = Mathf.Lerp(g.dread, Mathf.Lerp(0.7f, 0.15f, rememberP), dt);
            if (g.dread >= 1) { g.Caught(); return true; }
            while (lineIdx < GameData.Remember.Length && rememberP >= GameData.Remember[lineIdx].at) { g.Say(new[] { GameData.Remember[lineIdx].line }); lineIdx++; }

            float scale = Mathf.Lerp(1.15f, 0.4f, MathX.Smooth(rememberP)) * (1 + forgetP * 0.35f);
            g.itModel.SetScale(scale);
            g.itModel.Place(IT.x, IT.z, g.px, g.pz, 0.12f * (1 + forgetP));
            g.itModel.Flicker(Random.value > 0.02f);
            foreach (var r in teoParts) r.enabled = Random.value > forgetP;   // forgetting him, too
            g.au.SetPos(g.au.breath, new Vector3(IT.x, 2.4f * scale, IT.z));
            g.au.Set(g.au.breath, (0.3f + Mathf.Sin(g.time * 1.7f) * 0.15f) * (1 - rememberP * 0.8f) * (g.eyes ? 1 : 1.6f));
            finaleFocus.progress = rememberP;
            g.focusing = seen ? finaleFocus : null;

            if (rememberP >= 1) BeginEnding("remember");
            else if (forgetP >= 1) BeginEnding("forget");
            return true;
        }

        void BeginEnding(string kind)
        {
            ending = kind; finale = false; endT = 0;
            g.state = GState.Cutscene;
            nameIt.used = true;
            g.au.Set(g.au.breath, 0);
            if (kind == "forget") { g.au.Sting(); g.au.CaughtSound(); }
            else g.au.Materialize();
            if (kind == "thinkit") g.Say(new[] { new Line("MARA", "I know what you are. I know your name.", 4) });
        }

        void Ending(float dt)
        {
            endT += dt;
            var IT = g.IT;
            float k = Mathf.Clamp01(endT / 5f);
            if (ending != "forget")
            {
                float targetYaw = Mathf.Atan2(-(IT.x - g.px), -(IT.z - g.pz));
                g.yaw = Mathf.LerpAngle(g.yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg, dt * 1.5f) * Mathf.Deg2Rad;
            }
            if (ending == "remember")
            {
                g.itModel.SetScale(Mathf.Lerp(0.4f, 0.05f, k));
                g.itModel.Flicker(Random.value > k);
                g.itModel.coreMat.SetColor("_BaseColor", Color.Lerp(Color.black, new Color(0.8f, 0.82f, 0.86f), k));
                teo.transform.localPosition = new Vector3(center.x, Mathf.Lerp(0, 0.05f, k), center.z);
                foreach (var r in teoParts) r.enabled = true;
                ringLight.color = Color.Lerp(MathX.Hex(0xdfe8ff), new Color(1f, 0.85f, 0.7f), k);
                g.dread = Mathf.Lerp(g.dread, 0, dt);
            }
            else if (ending == "thinkit")
            {
                g.itModel.SetScale(Mathf.Lerp(g.itModel.root.transform.localScale.x, 0.85f, dt));
                g.itModel.coreMat.SetColor("_BaseColor", Color.Lerp(Color.black, new Color(0.3f, 0.32f, 0.34f), k));
                g.itModel.Place(IT.x, IT.z, g.px, g.pz, 0);
                g.itModel.Flicker(true);
                foreach (var r in teoParts) r.enabled = true;
                g.dread = Mathf.Lerp(g.dread, 0.2f, dt);
            }
            else
            {
                g.itModel.SetScale(Mathf.Lerp(1.4f, 1.8f, k));
                foreach (var r in teoParts) r.enabled = false;
                ringLight.intensity = Mathf.Lerp(1.6f, 0, k) * World.LK;
                g.exposure = -k;
                g.dread = Mathf.Lerp(g.dread, 0.9f, dt);
            }
            if (endT > 6 && endT - dt <= 6)
            {
                var cards = ending == "remember" ? GameData.EndRemember : ending == "forget" ? GameData.EndForget : GameData.EndThinkIt;
                g.hud.PlayCards(cards, () => { g.state = GState.Play; g.EndChapter(); });
            }
        }

        public override void OnCaught() { if (finale) { finale = false; rememberP = 0; forgetP = 0; } }
        public override void OnRespawn()
        {
            if (g.zone == "chamber") g.timers.After(1.2f, () => { if (!finale && ending == null) StartFinale(); });
        }

        // ---------------------------------------------------------------- per frame
        public override void Tick(float dt)
        {
            float time = g.time; var au = g.au;
            dust.Update(time);
            TrialTick(dt);
            if (tapeHissT > 0) { tapeHissT -= dt; if (Random.value < dt * 6) au.Noise(0.12f, 4000, 0.03f, Synth.F.Highpass); }
            // the chamber door slides up
            if (doorT >= 0)
            {
                doorT += dt;
                doorD.transform.localPosition = new Vector3(0, Mathf.Min(WALL_H + 0.1f, doorT * 1.2f), 0);
                if (doorT > 1.5f) w[17, 12] = '.';
                if (doorT > 3) doorT = -1;
            }
            // the bell rings on its own, faster as dread rises
            if (ending != "forget" || endT < 2)
            {
                bellT -= dt;
                if (bellT <= 0)
                {
                    bellT = (g.zone == "chamber" ? 3.2f : 6f) - g.dread * 2 + Random.value * 1.5f;
                    au.Tone(880, 876, 2.6f, 0.14f, "sine", bellPos, 0.004f);
                    au.Tone(2210, 2200, 1.4f, 0.05f, "sine", bellPos, 0.004f);
                }
            }
            // the ring light hums and stutters
            bool on = ending == "forget" || Random.value > 0.03f;
            if (ending != "forget") ringLight.intensity = (on ? 1.6f : 0.2f) * World.LK;
            Mats.SetEmission(ringMat, MathX.Hex(0xe8f0ff), on ? 2.2f : 0.2f);
            Mats.SetEmission(buttonMat, MathX.Hex(opened ? 0x30ff60 : 0xff2010), 0.8f + Mathf.Sin(time * 4) * 0.4f);
        }

        public override void OnInteract(Interactable it)
        {
            if (it.kind == "tape")
            {
                it.used = true; it.mesh.SetActive(false); tapeHeard = true;
                var t = GameData.Tapes[it.id];
                g.au.Tape(); g.Hint(t.title, 4); g.Say(t.lines);
                tapeHissT = 0; foreach (var l in t.lines) tapeHissT += l.dur;
            }
            else if (it.kind == "switch")
            {
                int i = it.id == "A" ? 0 : it.id == "B" ? 1 : 2;
                if (trials[i].done) return;
                it.used = true; trials[i].done = true; switches++;
                g.au.Lever(); g.au.Click();
                Mats.SetEmission(trials[i].lampMat, MathX.Hex(0x30ff60), 1.4f);
                Mats.SetEmission(trials[i].indicator, MathX.Hex(0x30ff60), 1.2f);
                trials[i].lamp.color = MathX.Hex(0xc8ffd0);
                g.dread = Mathf.Max(0, g.dread - 0.1f);
                g.Hint(switches < 3 ? $"A switch clunks over. {switches} of 3." : "All three. Up in the booth, a light turns green.", 4);
            }
            else if (it.kind == "console" && !opened && switches < 3)
            {
                g.au.Bump();
                g.Hint($"The door won't open. {3 - switches} trial{(3 - switches == 1 ? " is" : "s are")} still locked down. Below the booth.", 5);
            }
            else if (it.kind == "console" && !opened)
            {
                opened = true; it.used = true; doorT = 0;
                g.au.Lever(); g.au.PowerUp();
                g.Hint("Somewhere below the glass, a door grinds open.", 4);
            }
            else if (it.kind == "nameit" && finale && eligible) BeginEnding("thinkit");
        }

        public override string Objective()
        {
            if (finale) return eligible ? "Remember it. Forget it. Or say its name." : "Look at it and remember, or close your eyes and forget.";
            if (!bridgeB.done) return "Find a way across.";
            if (switches < 3) return $"Pass the trials below the booth. Reach each switch without looking at the bear. ({switches} / 3)";
            if (!opened) return "The observation booth. Open the chamber.";
            return "The White Bear Room. Reach Teo.";
        }

        public override EndInfo End()
        {
            int total = Settings.ForbiddenTotal, read = Settings.ForbiddenEverRead.Count;
            string title = ending == "remember" ? "ENDING: REMEMBER" : ending == "forget" ? "ENDING: FORGET" : "ENDING: THINK IT";
            return new EndInfo
            {
                title = title, sub = "Thinking It", hasNext = false,
                next = "Thank you for playing.",
                note = ending != "thinkit" && read < total ? $"forbidden documents read across the whole game: {read} / {total}.  There is another way this can end." : null,
                stats = $"times you looked at it: {g.timesSeen}\ntimes it reached you: {g.timesCaught}\nforbidden documents read this chapter: {g.forbiddenRead}"
            };
        }
    }
}
