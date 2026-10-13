using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 4: The Lake House.
    // Wing C was built on the shore of Halvard Lake, and Mara's memory has taken it over:
    // the flooded corridor turns into the lake house from that afternoon in 1998.
    // For three chapters the rule was "don't look". Here it turns around.
    //   - Look at the six things from that afternoon until they're real: his sandal, the paper boat, Mum's whistle,
    //     his swim ring, her magazine, Dad's camera.
    //   - The back door appears. The shore. The dock, with planks you have to look back into place.
    //   - At the end of the dock it rises out of the water. Don't close your eyes. Look at it until the end.
    // ======================================================================
    public class Chapter4 : Chapter
    {
        public override int Number => 4;
        public override string HallZone => null;
        public override float FogDensity => 0.05f;
        public override Color FogColor => new Color(0.016f, 0.02f, 0.03f);
        public override Color Ambient => new Color(0.4f, 0.46f, 0.6f) * 0.07f;
        public override bool Wood(float x, float z) { var zn = Zone(x, z); return zn == "house" || w.Cell(x, z) == '='; }
        public override bool CustomItActive => finale;
        public override bool AllowEyes => !cutscene;

        int memories;
        GameObject doorPlug;
        readonly Dictionary<string, Light> memoryGlow = new Dictionary<string, Light>();
        bool tapeHeard, houseHint, shoreHint, dockHint, finale, cutscene, wingHint, bedroomHint;
        float finaleP, finaleRise, finaleStep, cutT, tapeHissT;
        int finaleLine;
        Focusable backDoor, dock1, dock2;
        Focusable finaleFocus = new Focusable { kind = "it", time = 14 };
        Light moon, lantern; Material fireMat; Light fire;
        DustCloud mist, dust;
        bool indoors = true;
        readonly List<(Light l, Material m, float baseI)> corridorLights = new List<(Light, Material, float)>();

        // ---------------------------------------------------------------- textures
        static Texture WoodPanel() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x6a4a2e));
            for (int x = 0; x < W; x += 32)
            {
                float b = 0.85f + Random.value * 0.3f;
                g.Rect(x, 0, 32, H, new Color(0.42f * b, 0.29f * b, 0.18f * b, 1));
                for (int k = 0; k < 6; k++) { float xx = x + 4 + Random.value * 24; g.Line(xx, 0, xx + MathX.Jit(4), H, 1, Rgba(30, 18, 8, 0.15f + Random.value * 0.1f)); }
                g.Rect(x, 0, 2, H, Rgba(20, 12, 5, 0.7f));
            }
            g.Rect(0, 280, W, 8, Col(0x3a2414)); g.Rect(0, 288, W, H - 288, new Color(0, 0, 0, 0.18f));
            g.VGradient(0, 0, W, H, H * 0.6f, H, (0, new Color(0, 0, 0, 0)), (1, Rgba(10, 6, 2, 0.4f)));
            g.Grain(10);
        });

        static Texture Siding() => Paint(256, 410, (g, W, H) =>
        {
            for (int y = 0; y < H; y += 26) { float b = 0.8f + Random.value * 0.25f; g.Rect(0, y, W, 26, new Color(0.24f * b, 0.22f * b, 0.2f * b, 1)); g.Rect(0, y + 23, W, 3, new Color(0, 0, 0, 0.45f)); }
            for (int i = 0; i < 5; i++) { float sx = Random.value * W; g.VGradient(sx, 0, 4 + Random.value * 8, H, 0, H, (0, Rgba(40, 50, 30, 0.3f)), (1, Rgba(40, 50, 30, 0))); }
            g.Grain(12);
        });

        static Texture Concrete() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x6a6c66)); g.Grain(18);
            for (int y = 0; y < H; y += 100) g.Rect(0, y, W, 2, new Color(0, 0, 0, 0.3f));
            for (int i = 0; i < 6; i++) { float sx = Random.value * W; g.VGradient(sx, 0, 6 + Random.value * 10, H, 0, H, (0, Rgba(30, 30, 20, 0.35f)), (1, Rgba(30, 30, 20, 0))); }
        });

        static Texture RoomDoor(string n) => Paint(128, 205, (g, W, H) =>
        {
            g.Fill(Col(0x5a6266)); g.Grain(10);
            g.Rect(12, 10, 104, 195, Col(0x3a4246));
            g.Rect(34, 40, 60, 28, Col(0xd8d4c8));
            g.Text(n, 64, 62, 22, Col(0x1a1a1a), TexText.Face.Sans, true, false, 1);
            g.Rect(96, 110, 10, 4, Col(0x9a9a92));
        });

        static Texture BearDrawing() => Paint(96, 120, (g, W, H) =>
        {
            g.Fill(Col(0xece6d4));
            g.Rect(0, 80, W, 40, Col(0x3a6ad0));
            g.Ellipse(48, 62, 26, 16, 0, Col(0xfafafa)); g.StrokeEllipse(48, 62, 26, 16, 0, 2, Col(0x222222));
            g.Circle(70, 44, 11, Col(0xfafafa)); g.StrokeEllipse(70, 44, 11, 11, 0, 2, Col(0x222222));
            g.Ellipse(30, 102, 7, 4, 0.3f, Col(0x1a2a5a));
            g.Text("TEO", 10, 20, 14, Col(0xc02020), TexText.Face.Sans, true);
        });

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(32, 28);
            w.Carve(1, 1, 12, 2);                      // Wing C corridor, flooded
            w.Carve(6, 4, 8, 6); w[7, 3] = '.';        // room 14
            w.Carve(13, 1, 24, 2);                     // ...which turns into the lake house hall
            w.Carve(16, 3, 18, 4);                     // vestibule
            w.Carve(19, 3, 24, 4);                     // kitchen
            w[17, 5] = '.'; w[22, 5] = '.';            // doorways into the living room
            w.Carve(15, 6, 24, 8);                     // living room
            w.Carve(26, 1, 30, 5); w[25, 2] = '.';     // Mara's bedroom, off the end of the hall
            w[20, 9] = 'G';                            // the back door (not there yet)
            w.Carve(18, 10, 22, 10, '=');              // the porch
            w.Carve(1, 11, 30, 17);                    // the shore
            for (int y = 9; y <= 17; y++) { w[0, y] = 'r'; w[31, y] = 'r'; }
            for (int x = 26; x <= 31; x++) for (int y = 7; y <= 10; y++) w[x, y] = 'r';
            for (int y = 7; y <= 10; y++) w[25, y] = '#';   // the living room's east wall (the trees used to stand right against the room, with no wall at all)
            foreach (var t in new[] { (3, 15), (9, 12), (11, 16), (26, 12), (28, 15), (24, 16), (1, 12), (30, 11) }) w[t.Item1, t.Item2] = 'r';
            for (int y = 18; y < 28; y++) for (int x = 0; x < 32; x++) w[x, y] = 'W';   // the lake
            for (int y = 18; y <= 24; y++) w[16, y] = '=';                              // the dock
            w.Carve(15, 25, 17, 26, '=');
            w[16, 20] = 'W'; w[16, 21] = 'W'; w[16, 23] = 'W';                          // ...with planks missing

            // ---- materials ----
            var instWall = Mats.Std(Color.white, 0.85f, 0, Chapter1.WallTex());
            var planks = TextureLibrary.Find(l => l.brownPlanks);
            var panel = Mats.Std(Color.white, 0.8f, 0, WoodPanel());
            var siding = planks != null ? Mats.Std(MathX.Hex(0x707070), 0.9f, 0, planks.diff, new Vector2(1, 1.6f), planks.nor, 1) : Mats.Std(Color.white, 0.9f, 0, Siding());
            var concrete = Mats.Std(Color.white, 0.95f, 0, Concrete());
            // walls also get an outside face towards the trees, or you could see straight through them into the next room
            w.WallFaces("#", ".=krG", (nx, ny) =>
            {
                if (w[nx, ny] == 'r') return siding;
                string z = ZoneCell(nx, ny);
                if (z == "wing") return instWall;
                if (z == "house") return panel;
                return nx <= 13 ? concrete : siding;
            });

            // floors and ceilings, room by room
            var lino = TextureLibrary.Find(l => l.oldLinoleum);
            var linoM = lino != null ? Mats.Std(Color.white, 0.25f, 0, lino.diff, new Vector2(10, 4), lino.nor, 0.6f) : Std(0x8e8a7c, 0.4f);
            Slab(0, 0, 13, 8, linoM, 0, "floor");
            var lam = TextureLibrary.Find(l => l.laminateFloor);
            var lamM = lam != null ? Mats.Std(Color.white, 0.5f, 0, lam.diff, new Vector2(8, 7), lam.nor, 0.7f) : Std(0x6a4a2a, 0.6f);
            Slab(13, 0, 12, 10, lamM, 0, "floor");
            Slab(25, 0, 6, 7, lamM, 0, "floor");
            var ceilSet = TextureLibrary.Find(l => l.ceilingInterior);
            var ceilM = ceilSet != null ? Mats.Std(MathX.Hex(0xb0aca0), 0.95f, 0, ceilSet.diff, new Vector2(13, 8), ceilSet.nor, 0.5f) : Std(0xb0aca0, 0.95f);
            Slab(0, 0, 13, 8, ceilM, WALL_H, "ceiling");
            var plaster = TextureLibrary.Find(l => l.beigeWall);
            Slab(25, 0, 6, 7, plaster != null ? Mats.Std(MathX.Hex(0xd8d0c0), 1, 0, plaster.diff, new Vector2(6, 7), plaster.nor, 0.5f) : Std(0xd8d0c0, 1), WALL_H, "ceiling");
            Slab(13, 0, 12, 10, plaster != null ? Mats.Std(MathX.Hex(0xd8d0c0), 1, 0, plaster.diff, new Vector2(12, 10), plaster.nor, 0.5f) : Std(0xd8d0c0, 1), WALL_H, "ceiling");

            Wing();
            House();
            Outside();
            Lights();
        }

        // a floor or ceiling over a block of cells
        GameObject Slab(int x0, int y0, int cw, int ch, Material m, float y, string tagName)
        {
            var go = Pl(cw * C, ch * C, m);
            go.At(x0 * C + cw * C / 2, y, y0 * C + ch * C / 2).Rot(y > 0 ? PI / 2 : -PI / 2, 0, 0);
            go.GetComponent<MeshRenderer>().shadowCastingMode = y > 0 ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.Off;
            World.Tagged(go, tagName == "ceiling" ? "floor" : tagName);
            return go;
        }

        void Wing()
        {
            var metal = Mats.Std(MathX.Hex(0x7a7e80), 0.45f, 0.6f);
            // water on the floor, more of it the further you go
            var wet = Mats.Std(MathX.Hex(0x0c1418), 0.02f, 0.2f);
            for (int i = 0; i < 18; i++)
            {
                float x = 2 + Random.value * 24 * Mathf.Sqrt(Random.value);
                w.FloorPlane(0.8f + x * 0.06f + Random.value, 0.6f + Random.value * 1.2f, wet, new Vector3(x, 0.004f + i * 0.0003f, 2.2f + Random.value * 3.6f), Random.value * 3).NoShadow();
            }
            // numbered doors along the corridor, all shut
            int n = 7;
            foreach (var x in new[] { 2, 4, 9, 11 })
            {
                Pl(1.1f, 2.3f, Mats.Std(Color.white, 0.5f, 0.3f, RoomDoor((n++).ToString("00")))).At(cx(x), 1.15f, C + 0.02f);
                Pl(1.1f, 2.3f, Mats.Std(Color.white, 0.5f, 0.3f, RoomDoor((n++).ToString("00")))).At(cx(x), 1.15f, 3 * C - 0.02f).RotY(PI);
            }
            Pl(1.0f, 0.3f, Mats.Std(Color.white, 0.6f, 0, Chapter3.Sign("WING C", "Residential"))).At(C + 0.02f, 2.6f, cx(1) + 1).RotY(PI / 2);
            Pl(0.7f, 0.26f, Mats.Std(Color.white, 0.6f, 0, Chapter3.Sign("14", null))).At(cx(8) - 0.3f, 2.5f, 3 * C - 0.02f).RotY(PI);
            // room 14: a bed, a chair, a drawing taped to the wall, the window boarded up
            var bed = Grp("bed");
            Bx(0.95f, 0.08f, 2.0f, metal, bed).At(0, 0.42f, 0);
            Bx(0.9f, 0.16f, 1.9f, Std(0xc8c8c0, 0.95f), bed).At(0, 0.54f, 0);
            Bx(0.92f, 0.05f, 1.2f, Std(0x8a9aa0, 1), bed).At(0, 0.64f, 0.35f).Rot(0.03f, 0, 0.02f);
            Bx(0.5f, 0.1f, 0.3f, Std(0xe8e8e0, 1), bed).At(0, 0.67f, -0.75f);
            foreach (var sx in new[] { -0.45f, 0.45f }) foreach (var sz in new[] { -0.95f, 0.95f }) Cy(0.02f, 0.02f, 0.42f, 6, metal, bed).At(sx, 0.21f, sz);
            bed.At(cx(6) - 0.2f, 0, cx(5) + 0.3f); Prop(bed, 6, 5);
            var stand = Grp("nightstand");
            Bx(0.45f, 0.55f, 0.4f, Std(0x8a8e84, 0.6f), stand).At(0, 0.28f, 0);
            stand.At(cx(7) - 0.3f, 0, cx(6) + 0.55f); Prop(stand);
            Pl(0.4f, 0.5f, Mats.Std(Color.white, 0.9f, 0, BearDrawing())).At(cx(8) + 0.98f, 1.5f, cx(5)).RotY(-PI / 2);
            for (int i = 0; i < 4; i++) Bx(1.2f, 0.12f, 0.03f, Std(0x5a4a38, 0.9f)).At(cx(7), 1.4f + i * 0.22f, 7 * C - 0.02f).Rot(0, 0, MathX.Jit(0.1f));
            var docM = Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper());
            Doc(cx(7) - 0.3f, 0.57f, cx(6) + 0.55f, "c_nurse", docM);
            w.FloorPlane(2.6f, 2.2f, wet, new Vector3(cx(7), 0.005f, cx(5)), 0.4f).NoShadow();
            // the threshold: institutional green stops, and the wood panelling of a summer house begins
            var frame = Std(0x3a2414, 0.6f);
            foreach (var z in new[] { C + 0.08f, 3 * C - 0.08f }) Bx(0.16f, WALL_H, 0.16f, frame).At(13 * C, WALL_H / 2, z);
            Bx(0.16f, 0.3f, 2 * C, frame).At(13 * C, WALL_H - 0.15f, 2 * C);
        }

        void Doc(float x, float y, float z, string id, Material m, bool wall = false, float rotY = 0)
        {
            var p = Pl(0.26f, 0.33f, m);
            if (wall) p.At(x, y, z).RotY(rotY); else p.At(x, y, z).Rot(-PI / 2, 0, MathX.Jit(0.6f));
            World.Tagged(p, "doc", id);
            AddInteract("doc", "doc", id, p.transform.position, p, "read");
        }

        void House()
        {
            var wood = Std(0x5a3a22, 0.6f); var darkWood = Std(0x3a2414, 0.55f); var cream = Std(0xd8d0bc, 0.6f);
            w.Skirting(darkWood, "#");
            // ---- hall: coat hooks, a bench, the whistle hanging on a hook ----
            var hooks = Grp("coat hooks");
            Bx(1.2f, 0.1f, 0.04f, wood, hooks);
            for (int i = 0; i < 4; i++) Cy(0.015f, 0.015f, 0.12f, 6, Mats.Std(MathX.Hex(0xb08a3a), 0.35f, 0.7f), hooks).At(-0.45f + i * 0.3f, 0, 0.06f).Rot(PI / 2, 0, 0);
            Cy(0.12f, 0.25f, 0.9f, 10, Mats.Std(MathX.Hex(0x8a2a2a), 0.95f, 0, null, null, null, 1, null, 1, true), hooks, true).At(-0.45f, -0.45f, 0.24f);
            hooks.At(cx(15), 1.7f, C + 0.04f);
            var bench = Grp("bench");
            Bx(1.3f, 0.06f, 0.4f, wood, bench).At(0, 0.45f, 0);
            foreach (var sx in new[] { -0.55f, 0.55f }) Bx(0.06f, 0.45f, 0.36f, wood, bench).At(sx, 0.22f, 0);
            bench.At(cx(17), 0, C + 0.3f); Prop(bench);
            // whistle: a ghost until you look at it
            var whistle = Grp("whistle");
            Cy(0.012f, 0.012f, 0.6f, 6, null, whistle).At(0, 0.25f, 0);
            Bx(0.09f, 0.03f, 0.03f, null, whistle).At(0.02f, -0.06f, 0);
            Cy(0.022f, 0.022f, 0.035f, 10, null, whistle).At(-0.035f, -0.06f, 0).Rot(PI / 2, 0, 0);
            whistle.At(cx(15) + 0.15f, 1.45f, C + 0.12f);
            AddFocusable("whistle", whistle, null, whistle.transform.position, 2.5f, Mats.Std(MathX.Hex(0xc8ccd0), 0.25f, 0.9f));
            memoryGlow["whistle"] = w.PointLight(whistle.transform.position + new Vector3(0, 0, 0.3f), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);

            // ---- kitchen: table with the tape and the paper boat, stove, sink, a radio of static ----
            var table = Grp("kitchen table");
            Bx(1.5f, 0.06f, 0.9f, wood, table).At(0, 0.74f, 0);
            foreach (var l in new[] { (-0.65f, -0.36f), (0.65f, -0.36f), (-0.65f, 0.36f), (0.65f, 0.36f) }) Bx(0.06f, 0.74f, 0.06f, darkWood, table).At(l.Item1, 0.37f, l.Item2);
            Bx(1.5f, 0.005f, 0.9f, Std(0xc8d8e8, 0.8f), table).At(0, 0.775f, 0);
            table.At(cx(21) + 0.5f, 0, cx(3) + 0.5f); Prop(table, 21, 3);
            foreach (var c in new[] { (cx(21) - 0.4f, cx(3) - 0.2f, 0.3f), (cx(21) + 0.7f, cx(3) + 1.2f, PI - 0.2f) })
            {
                var ch = Grp("chair");
                Bx(0.45f, 0.05f, 0.45f, wood, ch).At(0, 0.46f, 0);
                Bx(0.45f, 0.5f, 0.04f, wood, ch).At(0, 0.74f, -0.21f);
                foreach (var sx in new[] { -0.19f, 0.19f }) foreach (var sz in new[] { -0.19f, 0.19f }) Bx(0.035f, 0.46f, 0.035f, wood, ch).At(sx, 0.23f, sz);
                ch.At(c.Item1, 0, c.Item2).RotY(c.Item3);
            }
            var boat = Grp("paper boat");
            Bx(0.2f, 0.035f, 0.07f, null, boat).At(0, 0.018f, 0);
            Bx(0.03f, 0.08f, 0.06f, null, boat).At(0, 0.06f, 0).Rot(0, 0, 0.1f);
            foreach (var s in new[] { -1, 1 }) Bx(0.07f, 0.04f, 0.072f, null, boat).At(s * 0.11f, 0.03f, 0).Rot(0, 0, -s * 0.6f);
            boat.At(cx(21) + 0.3f, 0.78f, cx(3) + 0.6f).RotY(0.6f);
            AddFocusable("boat", boat, null, boat.transform.position, 2.5f, Std(0xf0ece0, 0.9f));
            memoryGlow["boat"] = w.PointLight(boat.transform.position + new Vector3(0, 0.3f, 0), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);
            // Teo's tape
            var tape = Grp("tape");
            Bx(0.11f, 0.018f, 0.07f, Std(0x1a1a1a, 0.4f), tape);
            Sp(0.015f, 6, 6, Mats.Unlit(MathX.Hex(0xfff0d0)), tape).At(0.04f, 0.02f, 0);
            tape.At(cx(21) + 0.95f, 0.79f, cx(3) + 0.3f).RotY(1.2f);
            World.Tagged(tape, "tape", "tape4");
            AddInteract("tape", "tape", "tape4", tape.transform.position, tape, "play the tape");
            // counters along the north wall
            var counter = Grp("counter");
            Bx(4.2f, 0.9f, 0.6f, cream, counter).At(0, 0.45f, 0);
            Bx(4.3f, 0.05f, 0.65f, Std(0x3a3a3a, 0.3f), counter).At(0, 0.92f, 0);
            Bx(0.6f, 0.04f, 0.42f, Mats.Std(MathX.Hex(0x9a9ea0), 0.2f, 0.8f), counter).At(-1.2f, 0.95f, 0);
            for (int i = 0; i < 4; i++) Cy(0.09f, 0.09f, 0.02f, 16, Std(0x1a1a1a, 0.6f), counter).At(1.1f + (i % 2) * 0.3f, 0.955f, -0.12f + (i / 2) * 0.25f);
            for (int i = 0; i < 6; i++) Bx(0.6f, 0.75f, 0.02f, Std(0xc8c0ac, 0.6f), counter).At(-1.75f + i * 0.7f, 0.45f, 0.31f);
            counter.At(cx(21) + 1, 0, C + 0.32f);
            var fridge = Grp("fridge");
            Bx(0.75f, 1.75f, 0.7f, Std(0xe8e4d8, 0.4f), fridge).At(0, 0.875f, 0);
            Bx(0.04f, 0.4f, 0.04f, Mats.Std(MathX.Hex(0xb0b0b0), 0.3f, 0.8f), fridge).At(0.3f, 1.2f, 0.37f);
            fridge.At(cx(24) + 0.55f, 0, cx(3)).RotY(-PI / 2); Prop(fridge, 24, 3);
            // kitchen window, black water beyond
            KitchenWindow(cx(21), C + 0.02f, 0);
            // the radio, tuned to nothing
            var radio = Grp("radio");
            Bx(0.4f, 0.24f, 0.18f, Std(0x6a4426, 0.45f), radio);
            Bx(0.18f, 0.15f, 0.01f, Std(0x2a2014, 1), radio).At(-0.08f, 0, 0.095f);
            radio.At(cx(21) - 0.8f, 1.07f, C + 0.4f);

            // ---- living room: sofa, armchair, fireplace, the painting, the photo, the letter, the sandal ----
            var sofa = Grp("sofa"); var fabric = Std(0x3a4a5a, 0.95f);
            Bx(2.6f, 0.42f, 0.95f, fabric, sofa).At(0, 0.32f, 0);
            Bx(2.6f, 0.75f, 0.25f, fabric, sofa).At(0, 0.8f, -0.38f);
            foreach (var sx in new[] { -1.35f, 1.35f }) Bx(0.2f, 0.6f, 0.95f, fabric, sofa).At(sx, 0.5f, 0);
            sofa.At(cx(17), 0, cx(8) + 0.4f).RotY(PI); Prop(sofa, 17, 8);
            var leather = TextureLibrary.Find(l => l.brownLeather);
            var chairM = leather != null ? Mats.Std(Color.white, 0.9f, 0, leather.diff, Vector2.one, leather.nor, 0.7f) : Std(0x5a3a22, 0.9f);
            var arm = Grp("armchair");
            Bx(1.0f, 0.42f, 0.9f, chairM, arm).At(0, 0.3f, 0);
            Bx(1.0f, 0.8f, 0.2f, chairM, arm).At(0, 0.8f, -0.38f);
            foreach (var sx in new[] { -0.5f, 0.5f }) Bx(0.16f, 0.55f, 0.9f, chairM, arm).At(sx, 0.48f, 0);
            arm.At(cx(23), 0, cx(7)).RotY(-PI / 2 - 0.4f); Prop(arm, 23, 7);
            w.FloorPlane(3.6f, 2.4f, Mats.Std(MathX.Hex(0x8a6a4a), 1), new Vector3(cx(19) + 1, 0.006f, cx(7)), 0).NoShadow();
            // fireplace on the north wall of the living room
            var fp = Grp("fireplace"); var stone = Std(0x6a665e, 0.95f);
            Bx(1.8f, 1.2f, 0.5f, stone, fp).At(0, 0.6f, 0);
            Bx(2.0f, 0.1f, 0.6f, darkWood, fp).At(0, 1.25f, 0.02f);
            Bx(1.0f, 0.75f, 0.1f, Mats.Unlit(Color.black), fp).At(0, 0.42f, 0.22f);
            fireMat = Mats.Std(MathX.Hex(0x301008), 1, 0, null, null, null, 1, MathX.Hex(0xff5010), 1.5f, false, false);
            for (int i = 0; i < 5; i++) Bx(0.5f, 0.06f, 0.1f, fireMat, fp).At(MathX.Jit(0.4f), 0.1f + i * 0.02f, 0.3f).RotY(MathX.Jit(1));
            fp.At(cx(19), 0, 6 * C + 0.25f); Prop(fp, 19, 6);
            fire = w.PointLight(new Vector3(cx(19), 0.5f, 6 * C + 0.9f), MathX.Hex(0xff6a20), 0.6f, 6);
            var painting = Grp("painting");
            var gold = Mats.Std(MathX.Hex(0xb08a3a), 0.35f, 0.7f);
            foreach (var b in new[] { (1.3f, 0.08f, 0f, 0.48f), (1.3f, 0.08f, 0f, -0.48f), (0.08f, 1.04f, 0.65f, 0f), (0.08f, 1.04f, -0.65f, 0f) }) Bx(b.Item1, b.Item2, 0.06f, gold, painting).At(b.Item3, b.Item4, 0);
            Pl(1.22f, 0.9f, Mats.Std(Color.white, 0.85f, 0, Chapter2.LakePainting()), painting).At(0, 0, 0.01f);
            painting.At(cx(19), 2.1f, 6 * C + 0.03f);
            var photo = Grp("photo");
            Bx(0.34f, 0.28f, 0.03f, Std(0x2a1a10, 0.5f), photo);
            Pl(0.3f, 0.235f, Mats.Std(Color.white, 0.6f, 0, Chapter2.Photo(false)), photo).At(0, 0, 0.017f);
            photo.At(cx(19) + 0.6f, 1.44f, 6 * C + 0.1f).Rot(-0.1f, 0, 0);
            var side = Grp("side table");
            Cy(0.3f, 0.3f, 0.04f, 20, wood, side).At(0, 0.6f, 0);
            Cy(0.04f, 0.06f, 0.58f, 10, wood, side).At(0, 0.29f, 0);
            side.At(cx(15) + 0.2f, 0, cx(7)); Prop(side);
            Doc(cx(15) + 0.2f, 0.63f, cx(7), "c_letter", Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper()));
            LivingWindow(cx(16) + 1, 9 * C - 0.02f); LivingWindow(cx(23), 9 * C - 0.02f);
            // the sandal, kicked off by the back door
            var sandal = Grp("sandal");
            Bx(0.09f, 0.025f, 0.2f, null, sandal).At(0, 0.012f, 0);
            Bx(0.09f, 0.03f, 0.02f, null, sandal).At(0, 0.04f, 0.04f);
            Bx(0.02f, 0.03f, 0.07f, null, sandal).At(0.04f, 0.04f, -0.05f);
            sandal.At(cx(20) - 0.6f, 0, cx(8) + 0.3f).RotY(0.9f);
            AddFocusable("sandal", sandal, null, sandal.transform.position, 2.5f, Std(0xc81e1e, 0.6f));
            memoryGlow["sandal"] = w.PointLight(sandal.transform.position + new Vector3(0, 0.3f, 0), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);

            // ---- the vestibule: his swim ring, leaning where he dropped it ----
            var ring = Grp("swim ring");
            To(0.2f, 0.075f, 10, 24, null, ring);
            ring.At(16 * C + 0.12f, 0.28f, cx(4) - 0.3f).Rot(-PI / 2, PI / 2 - 0.12f, 0);
            AddFocusable("ring", ring, null, ring.transform.position, 2.8f, Std(0xf0c820, 0.5f));
            memoryGlow["ring"] = w.PointLight(ring.transform.position + new Vector3(0.4f, 0.3f, 0), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);

            // ---- Mara's bedroom: a narrow bed, a desk, the magazine she was reading, Dad's camera ----
            var mbed = Grp("bed");
            Bx(0.95f, 0.3f, 2.0f, darkWood, mbed).At(0, 0.2f, 0);
            Bx(0.9f, 0.16f, 1.9f, Std(0xd8d4c8, 0.95f), mbed).At(0, 0.43f, 0);
            Bx(0.92f, 0.05f, 1.3f, Std(0x6a8ab0, 1), mbed).At(0, 0.53f, 0.3f).Rot(0.02f, 0, 0.02f);
            Bx(0.5f, 0.1f, 0.3f, Std(0xe8e8e0, 1), mbed).At(0, 0.56f, -0.75f);
            Bx(0.95f, 0.9f, 0.06f, darkWood, mbed).At(0, 0.45f, -1.0f);
            mbed.At(31 * C - 0.6f, 0, cx(2) + 0.2f); Prop(mbed);
            var mag = Grp("magazine");
            Bx(0.28f, 0.012f, 0.36f, null, mag);
            Bx(0.27f, 0.008f, 0.35f, null, mag).At(0.14f, 0.01f, 0).Rot(0, 0, 0.25f);
            mag.At(31 * C - 0.65f, 0.57f, cx(2) + 0.5f).RotY(0.5f);
            AddFocusable("magazine", mag, null, mag.transform.position, 2.8f, Std(0xd04a8a, 0.7f));
            memoryGlow["magazine"] = w.PointLight(mag.transform.position + new Vector3(0, 0.4f, 0), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);
            var mdesk = Grp("desk");
            Bx(1.3f, 0.05f, 0.6f, wood, mdesk).At(0, 0.74f, 0);
            foreach (var l in new[] { (-0.6f, -0.25f), (0.6f, -0.25f), (-0.6f, 0.25f), (0.6f, 0.25f) }) Bx(0.05f, 0.74f, 0.05f, darkWood, mdesk).At(l.Item1, 0.37f, l.Item2);
            mdesk.At(cx(28), 0, 6 * C - 0.35f); Prop(mdesk);
            var mchair = Grp("chair");
            Bx(0.42f, 0.05f, 0.42f, wood, mchair).At(0, 0.46f, 0);
            Bx(0.42f, 0.5f, 0.04f, wood, mchair).At(0, 0.74f, -0.2f);
            foreach (var sx in new[] { -0.18f, 0.18f }) foreach (var sz in new[] { -0.18f, 0.18f }) Bx(0.035f, 0.46f, 0.035f, wood, mchair).At(sx, 0.23f, sz);
            mchair.At(cx(28) + 0.2f, 0, 6 * C - 1.0f).RotY(0.3f); Prop(mchair);
            var cam = Grp("camera");
            Bx(0.14f, 0.09f, 0.07f, null, cam);
            Cy(0.03f, 0.03f, 0.05f, 12, null, cam).At(0, 0, 0.055f).Rot(PI / 2, 0, 0);
            Bx(0.03f, 0.02f, 0.03f, null, cam).At(-0.04f, 0.055f, 0);
            cam.At(cx(28) - 0.3f, 0.81f, 6 * C - 0.4f).RotY(PI + 0.4f);
            AddFocusable("camera", cam, null, cam.transform.position, 2.8f, Mats.Std(MathX.Hex(0x2a2a2a), 0.4f, 0.5f));
            memoryGlow["camera"] = w.PointLight(cam.transform.position + new Vector3(0, 0.35f, 0), MathX.Hex(0xc8d8ff), 0.12f, 1.6f);
            var dlamp = Grp("desk lamp");
            Cy(0.07f, 0.08f, 0.02f, 12, darkWood, dlamp);
            Cy(0.01f, 0.01f, 0.35f, 6, darkWood, dlamp).At(0, 0.17f, 0);
            Cy(0.05f, 0.12f, 0.12f, 16, Mats.Std(MathX.Hex(0xe8d8b0), 1, 0, null, null, null, 1, MathX.Hex(0xffb060), 0.8f, true), dlamp, true).At(0, 0.36f, 0);
            dlamp.At(cx(28) + 0.45f, 0.77f, 6 * C - 0.45f).NoShadow();
            w.PointLight(new Vector3(cx(28) + 0.45f, 1.2f, 6 * C - 0.6f), MathX.Hex(0xffb070), 1.1f, 8, true);
            var wardrobe = Grp("wardrobe");
            Bx(1.2f, 2.0f, 0.55f, wood, wardrobe).At(0, 1.0f, 0);
            Bx(0.01f, 1.9f, 0.01f, darkWood, wardrobe).At(0, 1.0f, 0.28f);
            wardrobe.At(cx(28), 0, C + 0.3f); Prop(wardrobe);
            Pl(0.42f, 0.56f, Mats.Std(Color.white, 0.8f, 0, BearDrawing())).At(31 * C - 0.02f, 1.5f, cx(4)).RotY(-PI / 2);
            Bx(6 * C, 0.4f, 6 * C, roofLike()).At(28 * C, WALL_H + 0.21f, 3.5f * C);

            // ---- the back door: only an outline, and only once you've remembered ----
            var gd = Grp("back door");
            foreach (var b in new[] { (-0.62f, 1.2f, 0f, 0.12f, 2.4f, 0.3f), (0.62f, 1.2f, 0f, 0.12f, 2.4f, 0.3f), (0f, 2.45f, 0f, 1.36f, 0.12f, 0.3f), (0.3f, 1.15f, 0.5f, 0.05f, 2.3f, 1.0f) })
                Bx(b.Item4, b.Item5, b.Item6, null, gd).At(b.Item1, b.Item2, b.Item3);
            gd.At(cx(20), 0, 9 * C - 0.1f);
            backDoor = AddFocusable("door", gd, new[] { new Vector2Int(20, 9) }, new Vector3(cx(20), 1.4f, 9 * C), 2.6f, wood);
            gd.SetActive(false);
            // until then, just more wall
            doorPlug = Bx(C, WALL_H, C, Mats.Std(Color.white, 0.8f, 0, WoodPanel()));
            doorPlug.At(cx(20), WALL_H / 2, cx(9)); World.Tagged(doorPlug, "wall");

            // the house's roof and the wing's flat roof, seen from the shore
            var roofTex = TextureLibrary.Find(l => l.greyRoofTiles);
            var roofM = roofTex != null ? Mats.Std(Color.white, 0.9f, 0, roofTex.diff, new Vector2(4, 2), roofTex.nor, 0.8f) : Std(0x3a3a40, 0.9f);
            foreach (var s in new[] { -1, 1 })
                Bx(12 * C + 0.6f, 0.15f, 10.8f, roofM).At(19 * C, WALL_H + 1.7f, 5 * C + s * 5f).Rot(s * 0.33f, 0, 0);
            Bx(14 * C, 0.4f, 11 * C, Std(0x2a2c2a, 0.95f)).At(7 * C, WALL_H + 0.21f, 5.5f * C);
        }

        void KitchenWindow(float x, float z, float rotY)
        {
            var win = Grp("window");
            Pl(1.1f, 0.9f, Mats.Unlit(MathX.Hex(0x0c1626)), win);
            foreach (var b in new[] { (1.2f, 0.06f, 0f, 0.45f), (1.2f, 0.06f, 0f, -0.45f), (0.06f, 0.96f, 0.57f, 0f), (0.06f, 0.96f, -0.57f, 0f), (0.04f, 0.9f, 0f, 0f) })
                Bx(b.Item1, b.Item2, 0.06f, Std(0xe8dcc4, 0.6f), win).At(b.Item3, b.Item4, 0.01f);
            win.At(x, 1.75f, z).RotY(rotY);
        }

        void LivingWindow(float x, float z)
        {
            var win = Grp("window");
            Pl(1.3f, 1.4f, Mats.Unlit(MathX.Hex(0x101c30)), win).RotY(PI);
            foreach (var b in new[] { (1.4f, 0.07f, 0f, 0.7f), (1.4f, 0.07f, 0f, -0.7f), (0.07f, 1.47f, 0.68f, 0f), (0.07f, 1.47f, -0.68f, 0f), (1.3f, 0.04f, 0f, 0f) })
                Bx(b.Item1, b.Item2, 0.06f, Std(0xe8dcc4, 0.6f), win).At(b.Item3, b.Item4, -0.01f);
            win.At(x, 1.7f, z);
        }

        void Outside()
        {
            // ground: grass running down to a strip of sand, then the black lake
            var grass = TextureLibrary.Find(l => l.leafyGrass);
            var grassM = grass != null ? Mats.Std(MathX.Hex(0x7a8a7a), 0.95f, 0, grass.diff, new Vector2(30, 10), grass.nor, 0.8f) : Std(0x2a3424, 0.95f);
            var g1 = w.FloorPlane(120, 26, grassM, new Vector3(16 * C, -0.02f, 23)); World.Tagged(g1, "floor"); g1.NoShadow();
            var sand = TextureLibrary.Find(l => l.coastSand);
            var sandM = sand != null ? Mats.Std(MathX.Hex(0x9a9488), 0.9f, 0, sand.diff, new Vector2(40, 2), sand.nor, 0.8f) : Std(0x6a6254, 0.9f);
            var s1 = w.FloorPlane(120, 4.6f, sandM, new Vector3(16 * C, 0.01f, 17 * C + 1.4f)); World.Tagged(s1, "floor"); s1.NoShadow();
            var lake = Mats.Std(MathX.Hex(0x060a10), 0.04f, 0.3f);
            var water = w.FloorPlane(240, 200, lake, new Vector3(16 * C, -0.12f, 18 * C + 100)); World.Tagged(water, "water"); water.NoShadow();

            // the dock: weathered planks on posts, two stretches missing
            var plankSet = TextureLibrary.Find(l => l.brownPlanks);
            var plankM = plankSet != null ? Mats.Std(MathX.Hex(0x9a8a7a), 0.85f, 0, plankSet.diff, new Vector2(1, 0.25f), plankSet.nor, 0.8f) : Std(0x5a4632, 0.85f);
            var postM = Std(0x2a2018, 0.9f);
            for (int y = 18; y <= 26; y++)
                for (int x = 15; x <= 17; x++)
                {
                    if (w[x, y] != '=') continue;
                    var cell = Grp("dock");
                    for (int i = 0; i < 4; i++) Bx(C - 0.05f, 0.07f, 0.44f, plankM, cell).At(MathX.Jit(0.04f), 0.05f, -0.75f + i * 0.5f).RotY(MathX.Jit(0.03f));
                    foreach (var sx in new[] { -0.9f, 0.9f }) Cy(0.08f, 0.08f, 1.6f, 8, postM, cell).At(sx, -0.7f, 0);
                    cell.At(cx(x), 0, cx(y)); World.Tagged(cell, "floor");
                }
            System.Func<int[], string, float, Focusable> gap = (ys, kind, time) =>
            {
                var grp = Grp(kind);
                var cells = new List<Vector2Int>();
                foreach (var y in ys)
                {
                    cells.Add(new Vector2Int(16, y));
                    for (int i = 0; i < 4; i++) Bx(C - 0.05f, 0.07f, 0.44f, null, grp).At(cx(16), 0.05f, cx(y) - 0.75f + i * 0.5f);
                }
                return AddFocusable(kind, grp, cells, new Vector3(cx(16), 0.2f, cx(ys[0])), time, plankM, '=');
            };
            dock1 = gap(new[] { 20, 21 }, "dock1", 3f);
            dock2 = gap(new[] { 23 }, "dock2", 3.5f);
            // Teo's lantern at the very end
            var lan = Grp("lantern");
            Cy(0.07f, 0.08f, 0.22f, 12, Mats.Std(MathX.Hex(0x3a3020), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff9a40), 1.2f), lan).At(0, 0.11f, 0);
            Cy(0.03f, 0.08f, 0.06f, 12, Std(0x1a1a1a, 0.5f), lan).At(0, 0.25f, 0);
            lan.At(cx(16) + 0.6f, 0.09f, cx(26) + 0.5f);
            lantern = w.PointLight(new Vector3(cx(16) + 0.6f, 0.6f, cx(26) + 0.5f), MathX.Hex(0xff9a40), 1.0f, 9, true);

            // trees along the edge and scattered over the shore
            var bark = TextureLibrary.Find(l => l.barkWillow);
            var barkM = bark != null ? Mats.Std(Color.white, 0.95f, 0, bark.diff, new Vector2(1, 3), bark.nor, 1) : Std(0x2a2420, 0.95f);
            var leaves = Std(0x18241a, 0.95f);
            for (int y = 0; y < w.H; y++) for (int x = 0; x < w.W; x++)
                {
                    if (w[x, y] != 'r') continue;
                    var tr = Grp("tree");
                    float h = 4 + Random.value * 3;
                    Cy(0.12f + Random.value * 0.08f, 0.25f + Random.value * 0.1f, h, 10, barkM, tr).At(0, h / 2, 0).Rot(MathX.Jit(0.1f), 0, MathX.Jit(0.1f));
                    if (Random.value < 0.7f) for (int k = 0; k < 4; k++) Sp(0.9f + Random.value * 0.7f, 10, 8, leaves, tr).At(MathX.Jit(1.2f), h - 0.5f + Random.value * 1.5f, MathX.Jit(1.2f)).Scl(1, 0.7f, 1);
                    else for (int k = 0; k < 3; k++) Cy(0.03f, 0.07f, 1.4f, 5, barkM, tr).At(MathX.Jit(0.3f), h - 0.6f - k * 0.7f, 0).Rot(0, k * 2.1f, 0.9f + MathX.Jit(0.3f));
                    tr.At(cx(x) + MathX.Jit(0.6f), 0, cx(y) + MathX.Jit(0.6f));
                    Prop(tr);
                }
            // a treeline far beyond the edges, so the shore doesn't just stop
            for (int i = 0; i < 40; i++)
            {
                float x = -20 + Random.value * 104, z = 18 + Random.value * 20;
                if (x > 2 && x < 62) continue;
                Cy(0.2f, 0.3f, 6, 6, barkM).At(x, 3, z);
                Sp(1.6f, 8, 6, leaves).At(x, 6, z).Scl(1, 0.8f, 1);
            }
            // reeds at the water's edge
            var reedM = Std(0x3a4a2a, 0.9f);
            for (int i = 0; i < 160; i++)
            {
                float x = 1 + Random.value * 60; if (Mathf.Abs(x - cx(16)) < 1.6f) continue;
                Cy(0.006f, 0.012f, 0.8f + Random.value * 0.9f, 4, reedM).At(x, 0.4f, 17 * C + 1.3f + Random.value * 1.8f).Rot(MathX.Jit(0.3f), 0, MathX.Jit(0.3f)).NoShadow();
            }

            // the boathouse: an open shed, an upturned rowboat, the noticeboard
            var shed = Grp("boathouse"); var shedWood = Std(0x3a2e24, 0.9f);
            Bx(4.2f, 2.6f, 0.12f, shedWood, shed).At(0, 1.3f, -1.4f);
            foreach (var sx in new[] { -2.05f, 2.05f }) Bx(0.12f, 2.6f, 2.9f, shedWood, shed).At(sx, 1.3f, 0);
            Bx(4.6f, 0.12f, 3.4f, roofLike(), shed).At(0, 2.66f, 0);
            var boatG = Grp("rowboat", shed);
            Cy(0.5f, 0.5f, 3.0f, 12, Std(0x4a3a2a, 0.8f), boatG, true, 0, PI).At(0, 0.02f, 0).Rot(0, 0, PI / 2).Scl(1, 1, 0.6f);
            boatG.At(0, -0.07f, 0.2f).RotY(0.08f);
            shed.At(cx(5), 0, cx(12) + 0.6f); Prop(shed, 4, 12);
            Bx(0.8f, 0.6f, 0.04f, Std(0x8a6a44, 1)).At(cx(5) + 1, 1.5f, cx(12) + 0.6f - 1.32f);
            Doc(cx(5) + 1, 1.5f, cx(12) + 0.6f - 1.29f, "c_schedule", Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper()), true, 0);
            // a bench at the foot of the dock, with Dr. Lund's last page on it
            var bn = Grp("bench");
            Bx(1.4f, 0.06f, 0.4f, shedWood, bn).At(0, 0.45f, 0);
            foreach (var sx in new[] { -0.6f, 0.6f }) Bx(0.06f, 0.45f, 0.36f, shedWood, bn).At(sx, 0.22f, 0);
            bn.At(cx(14), 0, cx(17) - 0.2f); Prop(bn, 14, 17);
            Doc(cx(14) + 0.2f, 0.49f, cx(17) - 0.2f, "c_lund", Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper()));
            // the porch: boards, a rail, a light by the door
            var porchM = plankM;
            var porch = Grp("porch");
            Bx(5 * C, 0.08f, C, porchM, porch).At(0, 0.04f, 0);
            foreach (var sx in new[] { -5f, 5f }) Bx(0.08f, 0.9f, C, darkRail(), porch).At(sx, 0.45f, 0);
            porch.At(cx(20), 0, cx(10)); World.Tagged(porch, "floor");

            // mist over the water
            var pos = new List<Vector3>();
            for (int i = 0; i < 500; i++) pos.Add(new Vector3(Random.value * 64, 0.2f + Random.value * 1.4f, 30 + Random.value * 30));
            mist = new DustCloud(w.root, pos, MathX.Hex(0x9aa8c0), 0.5f, 0.05f);
            mist.driftX = 1.2f; mist.driftY = 0.1f; mist.driftZ = 0.6f;
            dust = new DustCloud(w.root, DustCloud.OverCells(w, 200, (x, y) => y <= 9 && w[x, y] == '.'), MathX.Hex(0xffe8c8), 0.03f, 0.5f);
        }

        Material roofLike() => Std(0x2a2a2c, 0.9f);
        Material darkRail() => Std(0x3a2414, 0.6f);

        void Lights()
        {
            // corridor: two tubes, one of them dying
            foreach (var x in new[] { 3, 9 })
            {
                Bx(1.5f, 0.08f, 0.4f, Mats.Std(MathX.Hex(0x4a4c48), 0.6f, 0.4f)).At(cx(x), WALL_H - 0.04f, cx(1) + 1);
                var tm = Mats.Std(MathX.Hex(0x7a7c7a), 0.3f, 0, null, null, null, 1, MathX.Hex(0xeaf2ff), 1.8f, false, false);
                Bx(1.3f, 0.045f, 0.12f, tm).At(cx(x), WALL_H - 0.1f, cx(1) + 1).NoShadow();
                corridorLights.Add((w.PointLight(new Vector3(cx(x), WALL_H - 0.4f, cx(1) + 1), MathX.Hex(0xdfe8f2), x == 3 ? 1.2f : 0.8f, 10, x == 3), tm, x == 3 ? 1.2f : 0.8f));
            }
            w.PointLight(new Vector3(cx(7), 2.4f, cx(5)), MathX.Hex(0xff2a14), 0.4f, 6);
            // the house: warm and lived-in, the way she remembers it
            Cy(0.01f, 0.01f, 0.7f, 6, Std(0x3a2414, 0.5f)).At(cx(21) + 0.5f, WALL_H - 0.35f, cx(3) + 0.5f);
            Cy(0, 0.3f, 0.26f, 20, Mats.Std(MathX.Hex(0x2a4a3a), 0.6f, 0, null, null, null, 1, null, 1, true), null, true).At(cx(21) + 0.5f, WALL_H - 0.8f, cx(3) + 0.5f);
            Sp(0.07f, 12, 10, Mats.Unlit(MathX.Hex(0xffd9a0))).At(cx(21) + 0.5f, WALL_H - 0.88f, cx(3) + 0.5f).NoShadow();
            w.PointLight(new Vector3(cx(21) + 0.5f, WALL_H - 1.0f, cx(3) + 0.5f), MathX.Hex(0xffb070), 1.8f, 10, true);
            w.PointLight(new Vector3(cx(16), WALL_H - 0.8f, cx(1) + 1), MathX.Hex(0xffb070), 0.9f, 8);
            var fl = Grp("floor lamp"); var lm = Mats.Std(MathX.Hex(0x2a2018), 0.4f, 0.6f);
            Cy(0.025f, 0.025f, 1.6f, 8, lm, fl).At(0, 0.8f, 0);
            Cy(0.2f, 0.22f, 0.04f, 16, lm, fl).At(0, 0.02f, 0);
            Cy(0.22f, 0.32f, 0.36f, 20, Mats.Std(MathX.Hex(0xf0d8b0), 1, 0, null, null, null, 1, MathX.Hex(0xffb060), 0.9f, true), fl, true).At(0, 1.68f, 0).NoShadow();
            fl.At(cx(15) - 0.3f, 0, cx(8) + 0.4f); Prop(fl);
            w.PointLight(new Vector3(cx(15) - 0.3f, 1.6f, cx(8) + 0.4f), MathX.Hex(0xffa860), 1.8f, 10, true);
            // porch light
            Sp(0.1f, 12, 8, Mats.Unlit(MathX.Hex(0xffd090))).At(cx(20) + 1, 2.4f, 10 * C + 0.12f).NoShadow();
            w.PointLight(new Vector3(cx(20) + 1, 2.3f, 10 * C + 0.5f), MathX.Hex(0xffb070), 1.0f, 9, true);
            // the moon, low over the lake
            var mg = new GameObject("moon");
            mg.transform.SetParent(w.root, false);
            mg.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.45f, -1f));
            moon = mg.AddComponent<Light>(); moon.type = LightType.Directional; moon.color = MathX.Hex(0x8fa4d0); moon.intensity = 0.22f;
            moon.shadows = LightShadows.Soft; moon.shadowStrength = 0.9f;
            var moonDisc = w.Disc(3.5f, 32, Mats.Unlit(MathX.Hex(0xc8d4e8)));
            moonDisc.At(16 * C + 18, 26, 18 * C + 75).RotY(PI).NoShadow();
        }

        // ---------------------------------------------------------------- zones
        string ZoneCell(int x, int y)
        {
            if (y >= 25) return "dockend";
            if (y >= 18) return "dock";
            if (y >= 11) return "shore";
            if (y == 10) return "porch";
            if (x <= 12 && y <= 8) return "wing";
            return "house";
        }
        public override string Zone(float wx, float wz) => ZoneCell(Mathf.FloorToInt(wx / C), Mathf.FloorToInt(wz / C));

        public override void Begin()
        {
            g.px = cx(1) + 0.2f; g.pz = cx(1) + 1; g.yaw = -PI / 2; g.pitch = 0;
            g.Hint("Wing C. The floor is wet.", 6);
            g.au.SetSpace(AudioReverbPreset.Hallway);
        }

        public override void OnZone(string zone)
        {
            bool outside = zone == "shore" || zone == "dock" || zone == "dockend" || zone == "porch";
            var preset = outside ? AudioReverbPreset.Plain : zone == "wing" ? AudioReverbPreset.Hallway : AudioReverbPreset.Livingroom;
            g.au.SetSpace(preset);
            indoors = !outside;
            if (zone == "house" && !houseHint)
            {
                houseHint = true; g.au.Creak(null, 0.4f);
                g.Hint("This isn't Wing C anymore. You know this house.", 5);
                g.timers.After(5.5f, () => { if (!tapeHeard) g.Hint("Someone left a tape on the kitchen table.", 5); });
            }
            if (zone == "shore" && !shoreHint) { shoreHint = true; g.Hint("Halvard Lake. It's so quiet.", 5); }
            if (zone == "dock" && !dockHint) { dockHint = true; g.Hint("The dock. Of course it's the dock.", 5); }
            if (zone == "dockend" && !finale && !cutscene) StartFinale();
        }

        public override float DreadDelta(float dt)
        {
            float d = 0;
            if (g.eyes && g.gaze != null && g.gaze.Value.tag == "water") d += 0.02f;
            if (g.zone == "wing") d += 0.004f;
            return d;
        }

        // ---------------------------------------------------------------- the finale
        void StartFinale()
        {
            finale = true; finaleP = 0; finaleRise = 0; finaleStep = 2; finaleLine = 0;
            g.flashOverride = 0;
            g.au.Flicker();
            g.HideIt(999);
            g.IT.state = "rising"; g.IT.x = cx(16); g.IT.z = cx(26) + 3.2f;
            g.itModel.SetVisible(true); g.itModel.SetScale(1.15f); g.itModel.SetHeight(-3.2f);
            g.itModel.coreMat.color = Color.black; g.itModel.coreMat.SetColor("_BaseColor", Color.black);
            g.dread = Mathf.Max(g.dread, 0.5f);
            g.au.Splash(new Vector3(g.IT.x, 0, g.IT.z), 0.7f);
            g.Hint("Your flashlight dies.", 3);
            g.timers.After(3.2f, () => { if (finale) g.Hint("Don't close your eyes. Not this time. Look at it.", 7); });
        }

        public override bool CustomIt(float dt)
        {
            if (!finale && !cutscene) return false;
            var IT = g.IT;
            if (cutscene) { Cutscene(dt); return true; }

            // it rises out of the water at the end of the dock
            if (finaleRise < 1)
            {
                finaleRise = Mathf.Min(1, finaleRise + dt / 3f);
                g.itModel.SetHeight(Mathf.Lerp(-3.2f, -0.15f, MathX.Smooth(finaleRise)));
                if (Random.value < dt * 3) g.au.Splash(new Vector3(IT.x, 0, IT.z), 0.25f);
            }
            bool seen = g.InView(IT.x, IT.z, 0.9f);
            if (seen && finaleRise >= 1)
            {
                finaleP = Mathf.Min(1, finaleP + dt / 15f);
                if (!IT.wasSeen) { IT.wasSeen = true; g.timesSeen++; g.au.Sting(); }
            }
            else
            {
                IT.wasSeen = false;
                finaleP = Mathf.Max(0, finaleP - dt * 0.04f);
                // look away, or close your eyes, and it comes closer
                finaleStep -= dt;
                if (finaleStep <= 0 && finaleRise >= 1)
                {
                    finaleStep = 1.4f;
                    float vx = g.px - IT.x, vz = g.pz - IT.z, vd = Mathf.Max(0.001f, MathX.Hypot(vx, vz)), j = Mathf.Min(vd - 0.6f, 0.8f);
                    IT.x += vx / vd * j; IT.z += vz / vd * j;
                    g.au.Splash(new Vector3(IT.x, 0, IT.z), 0.4f);
                    if (!g.eyes) g.Hint("It doesn't work anymore. You have to look.", 3);
                }
            }
            // as you look, it gets smaller: a memory, not a monster
            float scale = Mathf.Lerp(1.15f, 0.36f, MathX.Smooth(finaleP));
            g.itModel.SetScale(scale);
            g.dread = Mathf.Lerp(0.75f, 0.12f, finaleP) + (seen ? 0 : 0.1f);
            while (finaleLine < GameData.Finale.Length && finaleP >= GameData.Finale[finaleLine].at)
            {
                g.Say(new[] { GameData.Finale[finaleLine].line });
                finaleLine++;
            }
            finaleFocus.progress = finaleP;
            g.focusing = seen ? finaleFocus : null;
            if (MathX.Hypot(IT.x - g.px, IT.z - g.pz) < 1.1f) { g.Caught(); return true; }

            g.itModel.Place(IT.x, IT.z, g.px, g.pz, 0.12f * (1 - finaleP));
            g.itModel.Flicker(Random.value > 0.02f);
            g.au.SetPos(g.au.breath, new Vector3(IT.x, 2.4f * scale, IT.z));
            g.au.Set(g.au.breath, (0.3f + Mathf.Sin(g.time * 1.7f) * 0.15f) * (1 - finaleP * 0.8f));

            if (finaleP >= 1) BeginCutscene();
            return true;
        }

        void BeginCutscene()
        {
            finale = false; cutscene = true; cutT = 0;
            g.state = GState.Cutscene;
            g.eyes = true;
            g.au.Set(g.au.breath, 0);
            g.au.Materialize();
            g.Say(GameData.Ending);
        }

        void Cutscene(float dt)
        {
            cutT += dt;
            var IT = g.IT;
            // turn gently to face it
            float targetYaw = Mathf.Atan2(-(IT.x - g.px), -(IT.z - g.pz));
            g.yaw = Mathf.LerpAngle(g.yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg, dt * 1.5f) * Mathf.Deg2Rad;
            g.pitch = Mathf.Lerp(g.pitch, -0.12f, dt);
            // the black figure turns pale: a small boy, standing in the shallows
            float k = Mathf.Clamp01(cutT / 4f);
            var c = Color.Lerp(Color.black, new Color(0.85f, 0.88f, 0.92f), k);
            g.itModel.coreMat.SetColor("_BaseColor", c);
            foreach (var s in g.itModel.smears) s.SetActive(false);
            g.itModel.Place(IT.x, IT.z, g.px, g.pz, 0);
            g.itModel.Flicker(cutT < 8 || Random.value < Mathf.Clamp01(1 - (cutT - 8) / 3f));
            if (cutT > 11) g.itModel.SetVisible(false);
            // the memory lets go of the lake: the light drains out, the water goes still, and Teo's voice is somewhere else
            float d = Mathf.Clamp01((cutT - 6) / 14f);
            moon.intensity = Mathf.Lerp(0.22f, 0.04f, d);
            lantern.intensity = Mathf.Lerp(1.0f, 0.1f, d) * World.LK;
            RenderSettings.ambientLight = Color.Lerp(Ambient, Color.black, d);
            RenderSettings.fogDensity = Mathf.Lerp(FogDensity, 0.12f, d);
            g.exposure = -d * 0.4f;
            g.dread = Mathf.Lerp(0.1f, 0.45f, d);
            g.au.Set(g.au.lake, 0.06f * (1 - d));
            if (cutT > 9 && cutT - dt <= 9) g.au.Whisper(null, 0.4f);
            if (cutT > 22) { g.hud.SetBlack((cutT - 22) / 3f); }
            if (cutT > 25.5f) { cutscene = false; g.state = GState.Play; g.EndChapter(); }
        }

        public override void OnCaught()
        {
            if (finale) { finale = false; finaleP = 0; g.flashOverride = -1; }
        }

        public override void OnRespawn()
        {
            if (g.zone == "dockend") g.timers.After(1.5f, () => { if (!finale && !cutscene && g.zone == "dockend") StartFinale(); });
            else g.flashOverride = -1;
        }

        // ---------------------------------------------------------------- per frame
        public override void Tick(float dt)
        {
            float time = g.time; var au = g.au;
            dust.Update(time); mist.Update(time);
            if (tapeHissT > 0) { tapeHissT -= dt; if (Random.value < dt * 6) au.Noise(0.12f, 4000, 0.03f, Synth.F.Highpass); }
            if (!bedroomHint && g.px > 26 * C) { bedroomHint = true; g.au.Creak(null, 0.3f); g.Hint("Your room. Your summer room. You haven't thought about it in twenty years.", 6); }
            if (!wingHint && g.zone == "wing" && g.px > cx(6)) { wingHint = true; g.Hint("Room 14 is open. It's the only one.", 5); }

            // the dying tube in the corridor
            var cl = corridorLights[1];
            bool on = Random.value > 0.08f;
            cl.l.intensity = on ? cl.baseI * World.LK : 0; Mats.SetEmission(cl.m, MathX.Hex(0xeaf2ff), on ? 1.8f : 0.05f);
            if (!on && Random.value < 0.2f) au.Flicker();
            fire.intensity = (0.5f + Mathf.PerlinNoise(time * 3, 0) * 0.4f) * World.LK;
            Mats.SetEmission(fireMat, MathX.Hex(0xff5010), 1 + Mathf.PerlinNoise(time * 4, 3) * 1.2f);
            lantern.intensity = (0.9f + Mathf.PerlinNoise(time * 5, 7) * 0.25f) * World.LK;

            // the lake, lapping; the radio's static in the kitchen
            bool outside = g.zone == "shore" || g.zone == "dock" || g.zone == "dockend" || g.zone == "porch";
            if (!cutscene) au.Set(au.lake, outside ? 0.09f : 0.02f);
            au.SetPos(au.radio, new Vector3(cx(20), 1.07f, C + 0.4f));
            au.Set(au.radio, 0.08f + Random.value * 0.03f);
            if (g.zone == "house" && !g.HasSubtitles && Random.value < dt * 0.05f && MathX.Hypot(g.px - cx(20), g.pz - cx(1)) < 7)
                g.Say(new[] { new Line("RADIO", MathX.Pick(new[] { "...high of twenty-six, clear skies over Halvard Lake...", "...a perfect day for the water...", "...stay where I can see you...", "...he was right there..." }), 4) });
            if (outside && Random.value < dt * 0.15f) au.Splash(new Vector3(g.px + MathX.Jit(30), 0, 18 * C + 4 + Random.value * 20), 0.08f);

            // remembering: all six things, then the back door appears
            if (memories >= 6 && !backDoor.group.activeSelf)
            {
                backDoor.group.SetActive(true); doorPlug.SetActive(false);
                g.timers.After(1.2f, () => g.Hint("There. The back door. It was open all afternoon.", 6));
            }
        }

        public override void OnFocusDone(Focusable f)
        {
            g.Materialize(f);
            if (memoryGlow.ContainsKey(f.kind))
            {
                memories++;
                if (memoryGlow.TryGetValue(f.kind, out var glow)) glow.enabled = false;
                g.Say(GameData.Memories[f.kind]);
                g.dread = MathX.Clamp01(g.dread + 0.04f);
                if (memories < 6) g.timers.After(9.5f, () => g.Hint($"{memories} of 6. What else was there?", 4));
            }
            else if (f.kind == "door") g.Hint("The porch. The shore. The dock.", 4);
            else if (f.kind == "dock1") g.Hint("It holds.", 3);
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
        }

        public override string Objective()
        {
            if (finale) return "Look at it. Don't look away.";
            if (!tapeHeard && memories == 0) return "Go further into Wing C.";
            if (memories < 6) return $"Remember that afternoon. Look at what was left behind. ({memories} / 6)";
            if (!backDoor.done) return "The back door. Look at it until it's there.";
            if (!dock2.done) return "Go down to the dock.";
            return "The end of the dock.";
        }

        public override EndInfo End()
        {
            return new EndInfo
            {
                title = "END OF CHAPTER FOUR", sub = "The Lake House", hasNext = true,
                next = "Chapter Five: The White Bear Room",
                note = "Mara Venn, 14, pulled her brother out of Halvard Lake on 14 July 1998. He was fine.",
                stats = $"memories recovered: {memories} / 6\ntimes you looked at it: {g.timesSeen}\ntimes it reached you: {g.timesCaught}"
            };
        }
    }
}
