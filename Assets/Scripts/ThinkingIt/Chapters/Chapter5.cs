using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 5: The Stairwell.
    // The only way down to the White Bear Room. A concrete switchback stairwell that never ends: every flight
    // leads to the same landing, one floor lower, and the numbers stop making sense. After the fifth floor
    // there is a door on the landing that wasn't there before.
    //
    // Behind it, Sublevel 1: dark storage rooms full of old television sets showing snow. Stare at a set for
    // two seconds and NOISE climbs out of it. NOISE moves only when you move: first to the spot where you were
    // standing when it came out, then along every step you took after that, a little faster than you.
    // Stand still and it stands still. Lead it into the light and it comes apart.
    // "It" never appears in this chapter. NOISE is the only thing down here.
    // ======================================================================
    public class Chapter5 : Chapter
    {
        public override int Number => 5;
        public override string HallZone => null;
        public override AudioReverbPreset Space => AudioReverbPreset.StoneCorridor;
        public override float FogDensity => 0.085f;
        public override Color FogColor => new Color(0.006f, 0.006f, 0.008f);
        public override Color Ambient => new Color(0.42f, 0.45f, 0.5f) * 0.03f;
        public override float FlashBase => inArea ? 1.5f : 2.2f;
        public override bool CustomItActive => true;     // no "it" in this chapter
        public override bool CustomJumpscare => true;    // NOISE catches you instead

        // ---- the stairwell ----
        // Plan (2 m cells): landing L = row 1 (cols 1-3, height 0); flight A = col 1, rows 2-4 (down to -1.6);
        // mid landing M = row 5 (height -1.6); flight B = col 3, rows 4-2 (down to -3.2), which arrives at the
        // landing one floor below. The geometry repeats every 3.2 m, so the camera is simply lifted back up
        // 3.2 m when you step off flight B: the same landing, one floor lower.
        const float FLOOR_H = 3.2f, HALF = 1.6f;
        const int COPY_MIN = -6, COPY_MAX = 3, DOOR_FLOOR = 5;
        int floor, maxFloor;
        string lastPart = "L";
        readonly Dictionary<int, Material> signMats = new Dictionary<int, Material>();
        readonly List<Light> stairLights = new List<Light>();
        GameObject doorPlug, gate, stairsNote;
        Material plugWall, plugDoor;
        Interactable doorIt, noteIt;
        bool doorOpen, inArea, upHint, echoOn;
        float doorT = -1, stepAcc, slamT = 8;
        float lastPx, lastPz;

        // ---- Sublevel 1 ----
        class Tv { public string id; public Vector3 screen, front; public MeshRenderer screenR; public bool used; public float look; }
        readonly List<Tv> tvs = new List<Tv>();
        readonly List<Vector2> lightPools = new List<Vector2>();
        readonly List<(Light l, Material m, float baseI)> poolLights = new List<(Light, Material, float)>();
        readonly Focusable tvFocus = new Focusable { kind = "tv", time = 2 };
        Material staticMat, deadScreen;
        bool keycard, noiseHint, lightHint, tvHint, areaHint, controlHint;
        int noiseCount, dissolved;

        // ---- NOISE ----
        NoiseModel noise;
        bool noiseOn;
        Vector2 noisePos;
        readonly List<Vector2> trail = new List<Vector2>();   // where you've been since it came out
        float noiseTravel, emergeT, dissolveT = -1, crackleAcc, stillT;
        const float NOISE_RATE = 1f;       // it takes exactly as many steps as you do
        const float CLIMB = 3f;            // seconds to climb out of the set
        const float POOL_R = 2.1f;

        // ---------------------------------------------------------------- textures
        static Texture StaticTex() => Paint(128, 128, (g, W, H) =>
        {
            for (int y = 0; y < H; y++)
            {
                float band = Random.value < 0.1f ? 0.25f : 0;
                for (int x = 0; x < W; x += 2)
                {
                    float v = Mathf.Clamp01(Random.value * 0.9f + band);
                    g.Rect(x, y, 2, 1, new Color(v, v, v * 1.05f, 1));
                }
            }
        });

        static Texture DoorTex() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x4a4e50)); g.Grain(10);
            g.Rect(28, 40, 200, 370, Col(0x2e3234));
            g.Rect(58, 80, 140, 60, Col(0xd8d4c8));
            g.Text("SUBLEVEL 1", 128, 108, 20, Col(0x1a1a1a), TexText.Face.Sans, true, false, 1);
            g.Text("staff only", 128, 130, 13, Col(0x1a1a1a), TexText.Face.Sans, false, false, 1);
            g.Rect(190, 230, 22, 40, Col(0x1a1c1e));
            for (int i = 0; i < 4; i++) g.Rect(40, 300 + i * 22, 176, 2, new Color(0, 0, 0, 0.3f));
        });

        static Texture ExitTex() => Paint(256, 410, (g, W, H) =>
        {
            g.Fill(Col(0x5a5e60)); g.Grain(10);
            g.Rect(28, 40, 200, 370, Col(0x3a3e40));
            g.Rect(58, 80, 140, 60, Col(0xc8c4b4));
            g.Text("SUBLEVEL 0", 128, 108, 20, Col(0x8a1010), TexText.Face.Sans, true, false, 1);
            g.Text("WHITE BEAR", 128, 130, 13, Col(0x1a1a1a), TexText.Face.Sans, false, false, 1);
            g.Rect(186, 220, 30, 50, Col(0x1a1c1e)); g.Rect(192, 228, 18, 8, Col(0xd02020));
        });

        static Texture FloorSign(string label) => Paint(256, 128, (g, W, H) =>
        {
            g.Fill(Col(0xc8c4b4));
            g.Rect(6, 6, W - 12, H - 12, Col(0x1e2a3a));
            g.Text(label, W / 2, 88, 64, Col(0xe8e4d4), TexText.Face.Sans, true, false, 1);
            g.Grain(14);
        });

        static string Label(int f)
        {
            if (f < 0) return "G";
            if (f <= 2) return "B" + (f + 1);
            if (f <= 4) return "B3";
            return "B4";
        }

        // ---------------------------------------------------------------- build
        public override void Build()
        {
            w.Init(30, 21);
            // the shaft
            for (int y = 0; y <= 6; y++) for (int x = 0; x <= 4; x++) w[x, y] = 'B';
            w.Carve(1, 1, 3, 5);
            for (int y = 2; y <= 4; y++) w[2, y] = 'B';     // the stairwell's open well
            w[4, 1] = 'D';                                   // the door that only exists on one floor

            // Sublevel 1
            w.Carve(5, 1, 11, 2);      // entry corridor
            w.Carve(12, 1, 20, 6);     // storage
            w.Carve(16, 7, 17, 9);     // passage south
            w.Carve(6, 6, 10, 9);      // the lounge
            w.Carve(7, 10, 21, 11);    // the main hall
            w.Carve(22, 8, 27, 13);    // the control room
            w.Carve(14, 12, 14, 14);   // down to the south corridor
            w.Carve(8, 15, 26, 16);    // the south corridor
            w.Carve(24, 17, 27, 19);   // the exit vestibule
            w[28, 18] = 'X';           // the door to Sublevel 0

            Stairwell();
            Area();
        }

        // ---------------------------------------------------------------- the stairwell
        void Stairwell()
        {
            float bottom = COPY_MIN * FLOOR_H - FLOOR_H, top = (COPY_MAX + 1) * FLOOR_H + FLOOR_H, tall = top - bottom, mid = (top + bottom) / 2;
            System.Func<float, float, Material> wallM = (len, h) => Mats.Std(Color.white, 0.85f, 0, Chapter1.WallTex(), new Vector2(Mathf.Max(1, len / 2.5f), h / FLOOR_H));
            plugWall = wallM(2, FLOOR_H);
            // the shaft walls run the whole height
            w.Box(2, tall, 14, wallM(14, tall)).At(1, mid, 7);              // west
            w.Box(10, tall, 2, wallM(10, tall)).At(5, mid, 1);              // north
            w.Box(10, tall, 2, wallM(10, tall)).At(5, mid, 13);             // south
            w.Box(2, tall, 8, wallM(8, tall)).At(9, mid, 8);                // east, beside the flights
            // east, beside the landing: wall everywhere except the one floor where the door is
            w.Box(2, -bottom, 2, wallM(2, -bottom)).At(9, bottom / 2, 3);
            w.Box(2, top - FLOOR_H, 2, wallM(2, top - FLOOR_H)).At(9, FLOOR_H + (top - FLOOR_H) / 2, 3);
            doorPlug = w.Box(2, FLOOR_H, 2, plugWall); doorPlug.At(9, FLOOR_H / 2, 3);
            plugDoor = Mats.Std(Color.white, 0.45f, 0.4f, DoorTex());
            World.Tagged(doorPlug, "sdoor");
            doorIt = AddInteract("sdoor", "sdoor", null, new Vector3(8.2f, 1.2f, 3), doorPlug, "open the door");
            doorIt.used = true;
            w.FloorPlane(2, 2, Std(0x77746c, 0.9f), new Vector3(9, 0.002f, 3)).NoShadow();   // the door's threshold

            var slabM = Std(0x77746c, 0.92f); var stepM = Std(0x8a867c, 0.85f); var nosingM = Std(0x3a3a34, 0.6f);
            var railM = Mats.Std(MathX.Hex(0x3a4a3a), 0.5f, 0.5f);
            var bulbM = Mats.Std(MathX.Hex(0xffeecc), 0.5f, 0, null, null, null, 1, MathX.Hex(0xffe0b0), 2, false, false);
            var slabs = new List<Matrix4x4>(); var slabS = new List<Vector3>();
            var steps = new List<Matrix4x4>(); var stepS = new List<Vector3>();
            var noses = new List<Matrix4x4>(); var noseS = new List<Vector3>();
            var rails = new List<Matrix4x4>(); var railS = new List<Vector3>();
            System.Action<List<Matrix4x4>, List<Vector3>, Vector3, Vector3> box = (ml, sl, p, s) => { ml.Add(Matrix4x4.Translate(p)); sl.Add(s); };
            // a bar (or slab, when wide) running between two points
            System.Action<List<Matrix4x4>, List<Vector3>, Vector3, Vector3, float, float> bar = (ml, sl, a, b, wdt, th) =>
            {
                var d = b - a; ml.Add(Matrix4x4.TRS((a + b) / 2, Quaternion.LookRotation(d.normalized, Vector3.up), Vector3.one)); sl.Add(new Vector3(wdt, th, d.magnitude));
            };
            for (int k = COPY_MIN; k <= COPY_MAX; k++)
            {
                float y0 = k * FLOOR_H;
                // landings
                box(slabs, slabS, new Vector3(5, y0 - 0.125f, 3), new Vector3(6, 0.25f, 2));
                box(slabs, slabS, new Vector3(5, y0 - HALF - 0.125f, 11), new Vector3(6, 0.25f, 2));
                // flight A (x 2-4) runs down towards +z; flight B (x 6-8) runs down towards -z
                for (int i = 0; i < 10; i++)
                {
                    float topA = y0 - (i + 1) * 0.16f, zA = 4 + 0.6f * i + 0.3f;
                    box(steps, stepS, new Vector3(3, topA - 0.2f, zA), new Vector3(2, 0.4f, 0.6f));
                    box(noses, noseS, new Vector3(3, topA + 0.005f, zA - 0.27f), new Vector3(1.98f, 0.012f, 0.06f));
                    float topB = y0 - HALF - (i + 1) * 0.16f, zB = 10 - 0.6f * i - 0.3f;
                    box(steps, stepS, new Vector3(7, topB - 0.2f, zB), new Vector3(2, 0.4f, 0.6f));
                    box(noses, noseS, new Vector3(7, topB + 0.005f, zB + 0.27f), new Vector3(1.98f, 0.012f, 0.06f));
                }
                // the underside of each flight, seen from below through the well
                bar(slabs, slabS, new Vector3(3, y0 - 0.45f, 4), new Vector3(3, y0 - HALF - 0.45f, 10), 2, 0.2f);
                bar(slabs, slabS, new Vector3(7, y0 - HALF - 0.45f, 10), new Vector3(7, y0 - FLOOR_H - 0.45f, 4), 2, 0.2f);
                // railings around the well
                bar(rails, railS, new Vector3(3.97f, y0 + 0.9f, 4), new Vector3(3.97f, y0 - HALF + 0.9f, 10), 0.05f, 0.05f);
                bar(rails, railS, new Vector3(6.03f, y0 - HALF + 0.9f, 10), new Vector3(6.03f, y0 - FLOOR_H + 0.9f, 4), 0.05f, 0.05f);
                bar(rails, railS, new Vector3(4, y0 + 0.9f, 3.97f), new Vector3(6, y0 + 0.9f, 3.97f), 0.05f, 0.05f);
                bar(rails, railS, new Vector3(4, y0 - HALF + 0.9f, 10.03f), new Vector3(6, y0 - HALF + 0.9f, 10.03f), 0.05f, 0.05f);
                for (int i = 0; i <= 6; i++)
                {
                    float z = 4 + i, hA = y0 - HALF * i / 6f, hB = y0 - HALF - HALF * (6 - i) / 6f;
                    box(rails, railS, new Vector3(3.97f, hA + 0.45f, z), new Vector3(0.035f, 0.9f, 0.035f));
                    box(rails, railS, new Vector3(6.03f, hB + 0.45f, z), new Vector3(0.035f, 0.9f, 0.035f));
                }
                // a caged bulb on each landing
                Sp(0.07f, 10, 8, bulbM).At(5, y0 + 2.6f, 2.1f).NoShadow();
                Sp(0.07f, 10, 8, bulbM).At(5, y0 - HALF + 2.6f, 11.9f).NoShadow();
                // the floor number, on every landing
                var sm = new Material(Mats.Std(Color.white, 0.6f, 0, FloorSign(Label(-k))));
                signMats[k] = sm;
                Pl(0.9f, 0.45f, sm).At(7, y0 + 1.9f, 2.03f);
                // a dark stain running down the wall under each landing
                Pl(6, 0.6f, Mats.Std(MathX.Hex(0x2a2a26), 1, 0, null, null, null, 1, null, 1, true)).At(5, y0 - 0.55f, 2.03f).NoShadow();
            }
            w.Batch(slabs, slabS, slabM, "floor", "landings");
            w.Batch(steps, stepS, stepM, "floor", "steps");
            w.Batch(noses, noseS, nosingM, "floor", "nosing", false);   // tagged, or its bounding box becomes one big invisible wall over both flights
            w.Batch(rails, railS, railM, "wall", "railings");
            // only the nearest landings get real lights
            stairLights.Add(w.PointLight(new Vector3(5, 2.45f, 2.6f), MathX.Hex(0xffe2c0), 1.0f, 9, true));
            stairLights.Add(w.PointLight(new Vector3(5, -HALF + 2.45f, 11.4f), MathX.Hex(0xffe2c0), 0.8f, 8));
            stairLights.Add(w.PointLight(new Vector3(5, -FLOOR_H + 2.45f, 2.6f), MathX.Hex(0xffe2c0), 0.7f, 8));
            // the gate across the next flight down, once you've reached the door
            gate = Grp("gate");
            var gm = Mats.Std(MathX.Hex(0x4a4e48), 0.5f, 0.6f);
            for (int i = 0; i < 9; i++) Bx(0.03f, 1.1f, 0.03f, gm, gate).At(2.1f + i * 0.225f, 0.55f, 0);
            foreach (var y in new[] { 0.08f, 1.08f }) Bx(1.9f, 0.05f, 0.05f, gm, gate).At(3, y, 0);
            Bx(0.2f, 0.15f, 0.1f, Std(0x8a7a3a, 0.4f, 0.7f), gate).At(3.6f, 0.6f, 0.04f);   // a padlock
            gate.At(0, 0, 4.05f); World.Tagged(gate, "wall"); gate.SetActive(false);   // collision comes from the grid, not the bars
            // a message scratched into the paint, on one landing only
            stairsNote = Pl(0.5f, 0.5f, Mats.Std(Color.white, 0.9f, 0, Paint(128, 128, (g2, W2, H2) =>
            {
                g2.Fill(new Color(0.75f, 0.73f, 0.68f, 1)); g2.Grain(16);
                g2.Text("B1 B2 B3", 10, 40, 18, Col(0x2a2a2a), TexText.Face.Mono, true);
                g2.Text("B3 B3", 10, 66, 18, Col(0x2a2a2a), TexText.Face.Mono, true);
                g2.Text("STOP COUNTING", 6, 100, 14, Col(0x8a1010), TexText.Face.Mono, true);
            })));
            stairsNote.At(2.03f, 1.5f, 3).RotY(PI / 2);
            World.Tagged(stairsNote, "doc", "s_stairs");
            noteIt = AddInteract("doc", "doc", "s_stairs", stairsNote.transform.position, stairsNote, "read");
            stairsNote.SetActive(false); noteIt.used = true;
        }

        // ---------------------------------------------------------------- Sublevel 1
        void Area()
        {
            var wall = Mats.Std(Color.white, 0.85f, 0, Chapter1.WallTex());
            w.CellBlocks("#", wall, "wall");
            w.CellBlocks("X", Mats.Std(Color.white, 0.45f, 0.4f, ExitTex()), "exit", WALL_H, "exit door");
            var conc = TextureLibrary.Find(l => l.brushedConcrete);
            var floorM = conc != null ? Mats.Std(MathX.Hex(0x8a8680), 0.5f, 0, conc.diff, new Vector2(20, 16), conc.nor, 0.6f) : Std(0x5a5852, 0.6f);
            var f = w.FloorPlane(50, 42, floorM, new Vector3(35, 0, 21)); World.Tagged(f, "floor"); f.NoShadow();
            var c = Pl(50, 42, Std(0x3a3a36, 0.95f)); c.At(35, WALL_H, 21).Rot(PI / 2, 0, 0); World.Tagged(c, "floor");
            w.Skirting(Std(0x1a1a1a, 0.9f), "#X");

            staticMat = new Material(Mats.Unlit(Color.white, StaticTex()));
            deadScreen = Mats.Unlit(MathX.Hex(0x060708));
            var plastic = Std(0x2a2a2c, 0.6f); var wood = Std(0x4a3626, 0.8f); var metal = Mats.Std(MathX.Hex(0x6a6e70), 0.5f, 0.5f);

            // ---- the television sets ----
            int n = 0;
            System.Action<float, float, float, float> tv = (x, y, z, rot) =>
            {
                var g = Grp("tv");
                Bx(0.62f, 0.5f, 0.52f, plastic, g).At(0, 0.25f, 0);
                Bx(0.4f, 0.3f, 0.2f, plastic, g).At(0, 0.25f, -0.32f);
                var scr = Pl(0.48f, 0.36f, staticMat, g); scr.At(0, 0.27f, 0.262f);
                Bx(0.03f, 0.25f, 0.03f, metal, g).At(-0.12f, 0.62f, -0.1f).Rot(0, 0, 0.4f);   // rabbit ears
                Bx(0.03f, 0.25f, 0.03f, metal, g).At(0.12f, 0.62f, -0.1f).Rot(0, 0, -0.4f);
                g.At(x, y, z).RotY(rot);
                string id = "tv" + (n++);
                World.Tagged(g, "tv", id);
                var fwd = new Vector3(Mathf.Sin(rot), 0, Mathf.Cos(rot));
                var sp = new Vector3(x, y + 0.27f, z) + fwd * 0.27f;
                tvs.Add(new Tv { id = id, screen = sp, front = new Vector3(sp.x, 0, sp.z) + fwd * 0.5f, screenR = scr.GetComponent<MeshRenderer>() });
                // no Prop() here: Prop re-tags the set as "prop" and the gaze would never see it as a TV (it still blocks you: AutoObstacles)
            };
            System.Func<float, float, float, float, float, GameObject> crate = (x, z, sx, sy, rot) =>
            {
                var b = Bx(sx, sy, sx * 0.8f, wood).At(x, sy / 2, z).RotY(rot); Prop(b); return b;
            };
            // entry corridor
            crate(cx(11) + 0.4f, 2 * C + 0.5f, 0.7f, 0.5f, 0.1f);
            tv(cx(11) + 0.4f, 0.5f, 2 * C + 0.5f, -PI / 2);
            // storage: crates, shelving, sets stacked on boxes
            for (int i = 0; i < 4; i++)
            {
                var sh = Grp("shelf");
                Bx(2.2f, 2.0f, 0.5f, metal, sh).At(0, 1.0f, 0);
                for (int k = 0; k < 4; k++) Bx(0.5f, 0.35f, 0.4f, wood, sh).At(-0.75f + k * 0.5f + MathX.Jit(0.05f), 0.25f + (k % 2) * 0.95f, 0.02f);
                sh.At(cx(13) + i * 3.2f, 0, C + 0.3f); Prop(sh);
            }
            crate(cx(19), cx(1) + 1.6f, 0.7f, 0.55f, 0.3f);
            tv(cx(19), 0.55f, cx(1) + 1.6f, PI * 0.85f);
            crate(cx(14), cx(4), 0.9f, 0.6f, 0.2f); crate(cx(14) + 0.9f, cx(4) + 0.3f, 0.6f, 0.45f, -0.4f);
            tv(cx(13) + 0.2f, 0, 7 * C - 0.45f, PI);
            crate(cx(19), cx(5), 0.8f, 0.7f, 0.6f);
            Doc(cx(14), 0.62f, cx(4), "s_maint");
            // the lounge: a sofa facing two sets, a coffee table, a low cabinet
            var sofa = Grp("sofa"); var fabric = Std(0x3a3a2e, 0.95f);
            Bx(2.2f, 0.42f, 0.9f, fabric, sofa).At(0, 0.3f, 0); Bx(2.2f, 0.7f, 0.22f, fabric, sofa).At(0, 0.75f, -0.35f);
            foreach (var sx in new[] { -1.1f, 1.1f }) Bx(0.2f, 0.6f, 0.9f, fabric, sofa).At(sx, 0.45f, 0);
            sofa.At(cx(9), 0, cx(8) + 0.3f).RotY(-PI / 2 - 0.2f); Prop(sofa);
            var table = Grp("coffee table"); Bx(1.0f, 0.05f, 0.6f, wood, table).At(0, 0.42f, 0);
            foreach (var lx in new[] { -0.45f, 0.45f }) foreach (var lz in new[] { -0.25f, 0.25f }) Bx(0.04f, 0.42f, 0.04f, wood, table).At(lx, 0.21f, lz);
            table.At(cx(7) + 0.6f, 0, cx(8)); Prop(table);
            Doc(cx(7) + 0.6f, 0.46f, cx(8), "s_noise");
            var cab = Bx(1.0f, 0.45f, 0.5f, wood).At(6 * C + 0.3f, 0.225f, cx(7) + 1).RotY(PI / 2); Prop(cab);
            tv(6 * C + 0.35f, 0.45f, cx(7) + 1, PI / 2);
            tv(cx(10) + 0.4f, 0, cx(6) - 0.4f, -PI * 0.75f);
            // the main hall: debris, and one set on the floor
            tv(cx(20), 0, 10 * C + 0.4f, 0);
            crate(cx(11), 10 * C + 0.5f, 0.6f, 0.4f, 0.8f);
            crate(cx(18), 12 * C - 0.5f, 0.7f, 0.5f, -0.3f);
            // the south corridor
            tv(cx(9), 0, 15 * C + 0.4f, 0.2f);
            tv(cx(17), 0, 17 * C - 0.4f, PI);
            crate(cx(24), 15 * C + 0.45f, 0.7f, 0.45f, 0);
            tv(cx(24), 0.45f, 15 * C + 0.45f, 0);
            // the control room: a whole wall of sets, and the keycard on the desk
            var rack = Grp("monitor wall");
            Bx(4.6f, 0.62f, 0.6f, metal, rack).At(0, 0.31f, 0);
            Bx(4.6f, 0.05f, 0.6f, metal, rack).At(0, 1.15f, 0);
            foreach (var sx in new[] { -2.27f, 2.27f }) Bx(0.06f, 1.75f, 0.6f, metal, rack).At(sx, 0.875f, 0);
            rack.At(cx(24) + 1, 0, 8 * C + 0.32f); Prop(rack);
            for (int r = 0; r < 2; r++) for (int i = 0; i < 4; i++) tv(cx(24) + 1 - 1.65f + i * 1.1f, r == 0 ? 0.62f : 1.175f, 8 * C + 0.36f, 0);
            var desk = Grp("desk");
            Bx(1.8f, 0.06f, 0.8f, wood, desk).At(0, 0.76f, 0);
            foreach (var lx in new[] { -0.82f, 0.82f }) foreach (var lz in new[] { -0.34f, 0.34f }) Bx(0.05f, 0.76f, 0.05f, metal, desk).At(lx, 0.38f, lz);
            desk.At(cx(25), 0, cx(12) + 0.2f); Prop(desk);
            var card = Grp("keycard");
            Bx(0.09f, 0.004f, 0.055f, Mats.Std(MathX.Hex(0xe8e4d8), 0.5f, 0, null, null, null, 1, MathX.Hex(0xe8e4d8), 0.15f), card);
            Bx(0.09f, 0.0045f, 0.012f, Std(0x8a1010, 0.5f), card).At(0, 0, 0.015f);
            card.At(cx(25) + 0.3f, 0.795f, cx(12) + 0.1f).RotY(0.4f);
            World.Tagged(card, "card");
            AddInteract("card", "card", null, card.transform.position, card, "take the keycard");
            // the exit vestibule
            Pl(1.0f, 0.38f, Mats.Std(Color.white, 0.6f, 0, Chapter3.Sign("SUBLEVEL 0", "authorised staff only"))).At(28 * C - 0.04f, 2.6f, cx(18)).RotY(-PI / 2);
            AddInteract("exit", "exit", null, new Vector3(cx(28) - 0.8f, 1.2f, cx(18)), null, "open the door");

            // ---- the light: a few bulbs that still work. NOISE comes apart in them. ----
            var bulbM = Mats.Std(MathX.Hex(0xffeecc), 0.5f, 0, null, null, null, 1, MathX.Hex(0xffe0b0), 2, false, false);
            foreach (var p in new[] { new Vector2(cx(8), 2 * C), new Vector2(cx(16), cx(3) + 1), new Vector2(cx(14), 11 * C), new Vector2(cx(8), cx(7) + 1),
                                      new Vector2(cx(11), 16 * C), new Vector2(cx(21), 16 * C), new Vector2(cx(26), cx(18)) })
            {
                lightPools.Add(p);
                Cy(0.008f, 0.008f, 0.5f, 5, Std(0x1a1a1a, 0.9f)).At(p.x, WALL_H - 0.25f, p.y);
                var bm = new Material(bulbM);
                Sp(0.07f, 10, 8, bm).At(p.x, WALL_H - 0.55f, p.y).NoShadow();
                var l = w.PointLight(new Vector3(p.x, WALL_H - 0.7f, p.y), MathX.Hex(0xffd8a8), 1.0f, 6.5f);
                poolLights.Add((l, bm, 1.0f));
                // a faint circle on the floor shows where the light is safe
                w.Disc(POOL_R, 28, Mats.UnlitTransparent(new Color(1f, 0.85f, 0.6f, 0.05f))).At(p.x, 0.008f, p.y).Rot(-PI / 2, 0, 0).NoShadow();
            }
        }

        void Doc(float x, float y, float z, string id)
        {
            var p = Pl(0.26f, 0.33f, Mats.Std(Color.white, 0.9f, 0, Chapter2.DiaryPaper()));
            p.At(x, y, z).Rot(-PI / 2, 0, MathX.Jit(0.6f));
            World.Tagged(p, "doc", id);
            AddInteract("doc", "doc", id, p.transform.position, p, "read");
        }

        // ---------------------------------------------------------------- play
        public override void Begin()
        {
            w.ClearObstacles(0, 0, 4, 6);   // the stairwell: walls and the well come from the grid, nothing else blocks
            g.px = 3.4f; g.pz = 3; g.yaw = PI; g.pitch = 0; g.camY = 0;
            lastPx = g.px; lastPz = g.pz;
            noise = new NoiseModel(w.root, StaticTex());
            g.Hint("The stairs down to Sublevel 0. There's no lift.", 6);
            g.au.SetSpace(AudioReverbPreset.Concerthall);
            UpdateSigns(); UpdateDoor();
        }

        // which part of the stairwell a point is on
        string Part(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            if (x >= 4) return "area";
            if (y <= 1) return "L";
            if (y >= 5) return "M";
            return x <= 1 ? "A" : "B";
        }

        float StairHeight(float wx, float wz)
        {
            switch (Part(wx, wz))
            {
                case "A": return -HALF * Mathf.Clamp01((wz - 4) / 6f);
                case "M": return -HALF;
                case "B": return -HALF - HALF * Mathf.Clamp01((10 - wz) / 6f);
                default: return 0;
            }
        }

        public override string Zone(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            if (x <= 4) return x == 4 ? "entry" : "stairs";
            if (x >= 24 && y >= 17) return "exit";
            if (y >= 15) return "south";
            if (x >= 22) return "control";
            if (y >= 10) return "hall";
            if (x <= 10 && y >= 6) return "lounge";
            if (x >= 12) return "storage";
            return "entry";
        }

        public override void OnZone(string zone)
        {
            inArea = zone != "stairs";
            g.au.SetSpace(inArea ? AudioReverbPreset.Hallway : AudioReverbPreset.Concerthall);
            if (inArea && !areaHint) { areaHint = true; g.Hint("Sublevel 1. It's so dark. Somewhere, a television is hissing.", 6); }
            if (zone == "control" && !controlHint) { controlHint = true; g.Hint("A whole wall of them. Don't stand here and watch.", 5); }
        }

        public override float DreadDelta(float dt) => (inArea ? 0.004f : 0.0015f * Mathf.Min(floor, 5)) + (noiseOn ? 0.02f : 0);

        // ---------------------------------------------------------------- per frame
        public override void Tick(float dt)
        {
            Stairs(dt);
            // the static on every screen crawls
            staticMat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));
            // the safe bulbs flicker, but never go out
            foreach (var p in poolLights)
            {
                bool on = Random.value > 0.03f;
                p.l.intensity = (on ? p.baseI * (0.85f + Random.value * 0.15f) : p.baseI * 0.35f) * World.LK;
                Mats.SetEmission(p.m, MathX.Hex(0xffe0b0), on ? 2 : 0.6f);
            }
            // the stairwell door slides up
            if (doorT >= 0)
            {
                doorT += dt;
                doorPlug.transform.localPosition = new Vector3(9, FLOOR_H / 2 + Mathf.Min(FLOOR_H, doorT * 1.3f), 3);
                if (doorT > 1.4f) w[4, 1] = '.';
                if (doorT > 3) doorT = -1;
            }
            if (inArea && !tvHint && tvs.Exists(t => MathX.Hypot(t.screen.x - g.px, t.screen.z - g.pz) < 7)) { tvHint = true; g.Hint("Old televisions, all showing snow. None of them are plugged in.", 5); }
        }

        void Stairs(float dt)
        {
            var au = g.au;
            string part = Part(g.px, g.pz);
            // going down off flight B onto the landing below (or back up)
            if (lastPart == "B" && part == "L") { floor++; OnNewFloor(); }
            else if (lastPart == "L" && part == "B")
            {
                floor--; UpdateSigns(); UpdateDoor();
                if (!upHint) { upHint = true; g.Hint("Going back up feels wrong. Like the stairs are longer this way.", 5); }
            }
            lastPart = part;
            g.camY = StairHeight(g.px, g.pz);

            // the bulbs dim the deeper you go
            float dim = Mathf.Lerp(1, 0.45f, Mathf.Clamp01(floor / 5f));
            for (int i = 0; i < stairLights.Count; i++)
            {
                bool dead = i == 0 && floor == 3 && Random.value < 0.6f;
                stairLights[i].intensity = (dead ? 0.05f : (i == 0 ? 1.0f : 0.75f) * dim * (Random.value > 0.02f ? 1 : 0.3f)) * World.LK;
            }

            if (part == "area") return;
            // from the fourth floor down: footsteps above you, in step with yours
            float moved = MathX.Hypot(g.px - lastPx, g.pz - lastPz);
            if (echoOn && moved < 1)
            {
                stepAcc += moved;
                if (stepAcc > 0.75f)
                {
                    stepAcc = 0;
                    var at = new Vector3(g.px + MathX.Jit(1), g.camY + FLOOR_H + 1.2f, g.pz + MathX.Jit(1));
                    g.timers.After(0.28f, () => { au.Noise(0.06f, 800, 0.22f, Synth.F.Bandpass, at, 1.5f); au.Tone(90, 60, 0.08f, 0.2f, "sine", at); });
                }
            }
            // somewhere far below, a door slams
            slamT -= dt;
            if (slamT <= 0 && floor >= 2) { slamT = 14 + Random.value * 16; au.Thud(new Vector3(5, g.camY - 14, 7)); }
        }

        void OnNewFloor()
        {
            UpdateSigns(); UpdateDoor();
            if (floor <= maxFloor) return;
            maxFloor = floor;
            var au = g.au;
            switch (floor)
            {
                case 1: g.Hint("B2. The stairs keep going.", 4); break;
                case 2: au.Creak(null, 0.3f); g.Hint("B3. How deep is this place?", 4); break;
                case 3: au.Whisper(null, 0.25f); g.Hint("B3 again. You're sure you just passed B3.", 5); break;
                case 4:
                    echoOn = true; au.Whisper(null, 0.35f);
                    g.Hint("B3. Someone is walking down above you. In step with you.", 6);
                    break;
                case DOOR_FLOOR:
                    echoOn = false; au.Sting();
                    g.Hint("There's a door on this landing. There wasn't one on any of the others.", 6);
                    break;
            }
            g.dread = MathX.Clamp01(g.dread + 0.04f);
        }

        void UpdateSigns()
        {
            foreach (var kv in signMats) kv.Value.SetTexture("_BaseMap", FloorSign(Label(floor - kv.Key)));
            bool note = floor == 3;
            stairsNote.SetActive(note); noteIt.used = !note;
        }

        void UpdateDoor()
        {
            bool here = floor == DOOR_FLOOR;
            doorPlug.GetComponent<MeshRenderer>().sharedMaterial = here ? plugDoor : plugWall;
            doorPlug.transform.localPosition = new Vector3(9, FLOOR_H / 2 + (doorOpen && here ? FLOOR_H : 0), 3);
            w[4, 1] = doorOpen && here ? '.' : 'D';
            doorIt.used = !here || doorOpen;
            gate.SetActive(floor >= DOOR_FLOOR);
            w[1, 2] = floor >= DOOR_FLOOR ? 'B' : '.';
            // the hiss behind the door
            g.au.SetPos(g.au.radio, new Vector3(10.5f, 1.2f, 3));
            if (!noiseOn) g.au.Set(g.au.radio, here && !doorOpen ? 0.05f : 0);
        }

        // ---------------------------------------------------------------- the televisions and NOISE
        public override bool CustomIt(float dt)
        {
            float moved = MathX.Hypot(g.px - lastPx, g.pz - lastPz);
            if (moved > 1.5f) moved = 0;   // a respawn, not a step
            lastPx = g.px; lastPz = g.pz;

            // staring at a set
            Tv looking = null;
            if (g.eyes && g.gaze != null && g.gaze.Value.tag == "tv" && g.gaze.Value.dist < 15) looking = tvs.Find(t => t.id == g.gaze.Value.data && !t.used);
            foreach (var t in tvs) if (t != looking) t.look = Mathf.Max(0, t.look - dt * 1.5f);
            if (looking != null && !noiseOn && dissolveT < 0)
            {
                looking.look += dt;
                tvFocus.progress = looking.look / 2f;
                g.focusing = tvFocus;
                if (Random.value < dt * 4) g.au.Noise(0.08f, 3500, 0.06f, Synth.F.Highpass, looking.screen);
                if (looking.look >= 2) SpawnNoise(looking);
            }

            if (noiseOn) UpdateNoise(dt, moved);
            else if (dissolveT >= 0) Dissolve(dt);
            return true;
        }

        void SpawnNoise(Tv t)
        {
            t.used = true; t.look = 0; t.screenR.sharedMaterial = deadScreen;
            noiseOn = true; noiseCount++; emergeT = 0; noiseTravel = 0; stillT = 0;
            noisePos = new Vector2(t.front.x, t.front.z);
            trail.Clear(); trail.Add(new Vector2(g.px, g.pz));   // first: the spot where you were standing when it came out
            noise.SetFrom(t.screen, t.front);
            noise.Show(true);
            g.focusing = null;
            var au = g.au;
            au.Noise(0.8f, 3000, 0.6f, Synth.F.Highpass, t.screen); au.Sting();
            g.shake = Mathf.Max(g.shake, 0.8f);
            g.dread = MathX.Clamp01(g.dread + 0.15f);
            if (!noiseHint)
            {
                noiseHint = true;
                g.Hint("Something is climbing out of the screen.", 3.5f);
                g.timers.After(4, () => { if (noiseOn) g.Hint("It moves when you move. It's walking where you walked.", 6); });
                g.timers.After(11, () => { if (noiseOn && !lightHint) { lightHint = true; g.Hint("The bulbs. Lead it into the light.", 6); } });
            }
        }

        void UpdateNoise(float dt, float moved)
        {
            var au = g.au;
            // record your steps
            var me = new Vector2(g.px, g.pz);
            if (moved > 0 && (trail.Count == 0 || (me - trail[trail.Count - 1]).magnitude > 0.3f)) trail.Add(me);
            // it takes as many steps as you did, a little faster, along exactly your path
            float dist0 = (me - noisePos).magnitude;
            // first it climbs out of the set. It takes its time.
            if (emergeT < CLIMB)
            {
                emergeT += dt;
                noise.Climb(Mathf.Clamp01(emergeT / CLIMB), noisePos, me);
                au.SetPos(au.radio, new Vector3(noisePos.x, 1f, noisePos.y));
                au.Set(au.radio, Mathf.Clamp01(0.12f + (10 - dist0) * 0.02f));
                if (Random.value < dt * 6) au.Noise(0.06f, 3800, 0.16f, Synth.F.Highpass, new Vector3(noisePos.x, 0.6f, noisePos.y));
                if (emergeT >= CLIMB) au.Thud(new Vector3(noisePos.x, 0.2f, noisePos.y));
                return;
            }
            float budget = moved * NOISE_RATE;
            bool moving = budget > 0.0001f;
            while (budget > 0 && trail.Count > 0)
            {
                var to = trail[0]; var d = to - noisePos; float dl = d.magnitude;
                if (dl <= budget) { noisePos = to; budget -= dl; noiseTravel += dl; trail.RemoveAt(0); }
                else { noisePos += d / dl * budget; noiseTravel += budget; budget = 0; }
            }
            stillT = moving ? 0 : stillT + dt;
            noise.Place(noisePos, me, moving, dt);
            // the sound of it: a set left on in the next room
            au.SetPos(au.radio, new Vector3(noisePos.x, 1.3f, noisePos.y));
            float dist = (me - noisePos).magnitude;
            au.Set(au.radio, Mathf.Clamp01(0.08f + (10 - dist) * 0.025f));
            crackleAcc += moved;
            if (crackleAcc > 0.5f) { crackleAcc = 0; au.Noise(0.05f, 4200, 0.18f, Synth.F.Highpass, new Vector3(noisePos.x, 0.2f, noisePos.y)); }
            if (stillT > 4 && Random.value < dt * 0.4f) au.Whisper(new Vector3(noisePos.x, 1.6f, noisePos.y), 0.15f);
            // caught
            if (dist < 0.85f) { g.Caught(); return; }
            // the light tears it apart
            foreach (var p in lightPools)
                if ((noisePos - p).magnitude < POOL_R)
                {
                    noiseOn = false; dissolveT = 0; dissolved++;
                    au.Noise(0.9f, 2500, 0.5f, Synth.F.Highpass, new Vector3(noisePos.x, 1, noisePos.y));
                    au.Tone(1400, 200, 0.7f, 0.12f, "sawtooth", new Vector3(noisePos.x, 1.5f, noisePos.y));
                    if (dissolved == 1) g.Hint("The light tore it apart.", 4);
                    return;
                }
            // and after long enough, the signal just fades
            if (noiseTravel > 75) { noiseOn = false; dissolveT = 0; g.Hint("The static fades. Gone. For now.", 4); }
        }

        void Dissolve(float dt)
        {
            dissolveT += dt;
            noise.Dissolve(dissolveT / 0.9f);
            g.au.Set(g.au.radio, Mathf.Max(0, 0.3f * (1 - dissolveT)));
            if (dissolveT >= 0.9f) { dissolveT = -1; noise.Show(false); trail.Clear(); }
        }

        // ---------------------------------------------------------------- caught
        public override void Jumpscare(float t, float dt)
        {
            if (t == 0)
            {
                noiseOn = false; dissolveT = -1; trail.Clear();
                // you're turned to face it
                float dx = noisePos.x - g.px, dz = noisePos.y - g.pz;
                if (MathX.Hypot(dx, dz) > 0.05f) g.yaw = Mathf.Atan2(-dx, -dz);
                noise.Show(true);
                g.au.Noise(1.2f, 2800, 0.8f, Synth.F.Highpass);
                return;
            }
            if (t < Game.JUMP)
            {
                var f = g.Fwd; float k = MathX.Smooth(Mathf.Clamp01(t / 0.25f)), d = Mathf.Lerp(1.4f, 0.45f, k);
                noise.Lunge(new Vector2(g.px + f.x * d, g.pz + f.y * d), new Vector2(g.px, g.pz), World.EYE + g.camY);
            }
            else if (noise.Visible) noise.Show(false);
        }

        public override void OnRespawn()
        {
            noiseOn = false; dissolveT = -1; trail.Clear(); noise.Show(false);
            g.au.Set(g.au.radio, 0);
            lastPx = g.px; lastPz = g.pz;
        }

        // ---------------------------------------------------------------- interaction
        public override void OnInteract(Interactable it)
        {
            var au = g.au;
            if (it.kind == "sdoor" && floor == DOOR_FLOOR && !doorOpen)
            {
                doorOpen = true; it.used = true; doorT = 0;
                au.DoorCreak(); au.Set(au.radio, 0.12f);
                g.Hint("Warm air. Static. Darkness.", 4);
            }
            else if (it.kind == "card")
            {
                it.used = true; it.mesh.SetActive(false); keycard = true; au.Click();
                g.Hint("A keycard. SUBLEVEL 0 — WHITE BEAR.", 4);
            }
            else if (it.kind == "exit")
            {
                if (!keycard) { au.Bump(); g.Hint("A card reader, blinking red. You need a keycard.", 4); return; }
                au.Click(); g.EndChapter();
            }
        }

        public override string Objective()
        {
            if (!inArea && floor < DOOR_FLOOR && !doorOpen) return "Keep going down.";
            if (!doorOpen) return floor == DOOR_FLOOR ? "The door on the landing." : "Go back down to the door.";
            if (!keycard) return "Cross Sublevel 1. Find a way down to Sublevel 0. Don't stare at the screens.";
            return "The door to Sublevel 0.";
        }

        public override EndInfo End()
        {
            return new EndInfo
            {
                title = "END OF CHAPTER FIVE", sub = "The Stairwell", hasNext = true, next = "Chapter Six: The White Bear Room",
                stats = $"floors descended: {maxFloor}\ntimes NOISE came out: {noiseCount}\ntimes you led it into the light: {dissolved}\ntimes it reached you: {g.timesCaught}"
            };
        }

        // ======================================================================
        // NOISE: a man made of television snow. A smooth, faceless, rounded body, every inch of it crawling static,
        // with a faint white rim where it meets the dark. It climbs out of a screen head and hands first.
        // ======================================================================
        class NoiseModel
        {
            readonly GameObject root;
            readonly Transform pivot, head, shL, shR, elL, elR, hipL, hipR, knL, knR;
            readonly List<Transform> fingers = new List<Transform>();
            readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
            readonly Material mat, rimMat;
            float walk, twitchT;
            Vector3 headTwitch;
            Vector3 screen, outDir;
            const float HIP_H = 0.92f;
            public bool Visible => root.activeSelf;

            // blocky black-and-white snow, like a dead channel
            static Texture SnowTex()
            {
                var p = new Painter(128, 128, Color.black);
                for (int y = 0; y < 128; y += 2) for (int x = 0; x < 128; x += 2)
                    {
                        float v = Random.value; v = v > 0.52f ? 0.92f + Random.value * 0.08f : v < 0.18f ? 0.02f : 0.25f + Random.value * 0.3f;
                        p.Rect(x, y, 2, 2, new Color(v, v, v, 1));
                    }
                return p.Make();
            }

            public NoiseModel(Transform parent, Texture unused)
            {
                root = new GameObject("NOISE"); root.transform.SetParent(parent, false);
                mat = new Material(Mats.Unlit(Color.white, SnowTex()));
                mat.SetTextureScale("_BaseMap", new Vector2(3, 3));
                rimMat = Mats.UnlitTransparent(new Color(1f, 1f, 1f, 0.55f));
                rimMat.SetFloat("_Cull", 1); rimMat.SetFloat("_RenderFace", 1);   // back faces only: a white edge around the silhouette

                pivot = Node(root.transform, new Vector3(0, HIP_H, 0));   // everything hangs off the hips
                // torso: pelvis, belly, chest and shoulders, all soft and rounded
                Ell(pivot, new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.24f, 0.24f));
                Ell(pivot, new Vector3(0, 0.3f, 0), new Vector3(0.38f, 0.5f, 0.24f));
                Ell(pivot, new Vector3(0, 0.56f, 0), new Vector3(0.48f, 0.28f, 0.27f));
                Cap(pivot, new Vector3(0, 0.66f, 0), new Vector3(0, 0.78f, 0), 0.06f);   // neck
                // the head: a long smooth egg, no face at all
                head = Node(pivot, new Vector3(0, 0.78f, 0));
                Ell(head, new Vector3(0, 0.17f, 0.01f), new Vector3(0.25f, 0.36f, 0.27f));
                // arms
                shL = Node(pivot, new Vector3(-0.25f, 0.6f, 0)); shR = Node(pivot, new Vector3(0.25f, 0.6f, 0));
                elL = Arm(shL, -1); elR = Arm(shR, 1);
                // legs
                hipL = Node(pivot, new Vector3(-0.1f, -0.04f, 0)); hipR = Node(pivot, new Vector3(0.1f, -0.04f, 0));
                knL = Leg(hipL); knR = Leg(hipR);
                root.SetActive(false);
            }

            Transform Arm(Transform sh, float s)
            {
                Cap(sh, Vector3.zero, new Vector3(0, -0.3f, 0), 0.058f);
                var el = Node(sh, new Vector3(0, -0.3f, 0));
                Cap(el, Vector3.zero, new Vector3(0, -0.27f, 0), 0.05f);
                var hand = Node(el, new Vector3(0, -0.29f, 0));
                Ell(hand, new Vector3(0, -0.04f, 0), new Vector3(0.09f, 0.11f, 0.05f));
                for (int i = 0; i < 4; i++)   // four fingers and a thumb, slightly curled
                {
                    var f = Node(hand, new Vector3((i - 1.5f) * 0.022f * s, -0.09f, 0), Quaternion.Euler(-12, 0, (i - 1.5f) * 6 * s));
                    Cap(f, Vector3.zero, new Vector3(0, -0.075f, 0), 0.011f, false);
                    var tip = Node(f, new Vector3(0, -0.075f, 0), Quaternion.Euler(-25, 0, 0));
                    Cap(tip, Vector3.zero, new Vector3(0, -0.055f, 0), 0.01f, false);
                    fingers.Add(f); fingers.Add(tip);
                }
                var th = Node(hand, new Vector3(0.045f * s, -0.04f, 0.02f), Quaternion.Euler(-30, 0, 35 * s));
                Cap(th, Vector3.zero, new Vector3(0, -0.06f, 0), 0.012f, false);
                return el;
            }

            Transform Leg(Transform hip)
            {
                Cap(hip, Vector3.zero, new Vector3(0, -0.43f, 0), 0.075f);
                var kn = Node(hip, new Vector3(0, -0.43f, 0));
                Cap(kn, Vector3.zero, new Vector3(0, -0.4f, 0), 0.062f);
                Ell(kn, new Vector3(0, -0.44f, 0.06f), new Vector3(0.12f, 0.09f, 0.26f));   // foot
                return kn;
            }

            static Transform Node(Transform p, Vector3 pos, Quaternion? rot = null)
            {
                var t = new GameObject("j").transform; t.SetParent(p, false); t.localPosition = pos; t.localRotation = rot ?? Quaternion.identity; return t;
            }

            void Mesh(Transform p, Mesh m, Vector3 pos, Quaternion rot, Vector3 scale, bool rim)
            {
                var go = new GameObject("p"); go.transform.SetParent(p, false);
                go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(r);
                if (!rim) return;
                var o = new GameObject("rim"); o.transform.SetParent(go.transform, false); o.transform.localScale = Vector3.one * 1.07f;
                o.AddComponent<MeshFilter>().sharedMesh = m;
                var orr = o.AddComponent<MeshRenderer>(); orr.sharedMaterial = rimMat; orr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.Add(orr);
            }
            void Ell(Transform p, Vector3 pos, Vector3 size, bool rim = true) => Mesh(p, MeshGen.Sphere(0.5f, 16, 12), pos, Quaternion.identity, size, rim);
            // a capsule: a tube with a ball at each end
            void Cap(Transform p, Vector3 a, Vector3 b, float r, bool rim = true)
            {
                var d = b - a;
                Mesh(p, MeshGen.Cylinder(r, r, d.magnitude, 12, true), (a + b) / 2, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one, rim);
                Mesh(p, MeshGen.Sphere(r, 12, 8), a, Quaternion.identity, Vector3.one, rim);
                Mesh(p, MeshGen.Sphere(r, 12, 8), b, Quaternion.identity, Vector3.one, rim);
            }

            public void Show(bool on)
            {
                root.SetActive(on);
                if (on) foreach (var r in renderers) r.enabled = true;
            }

            void Snow()
            {
                mat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));   // the static never holds still
                bool show = Random.value > 0.025f;
                foreach (var r in renderers) r.enabled = show;
            }

            void Fingers(float t, float curl)
            {
                for (int i = 0; i < fingers.Count; i++)
                {
                    float n = Mathf.PerlinNoise(t * 3, i * 1.7f);
                    var e = fingers[i].localEulerAngles;
                    fingers[i].localRotation = Quaternion.Euler((i % 2 == 0 ? -12 : -25) - n * 30 - curl, 0, i % 2 == 0 ? e.z : 0);
                }
            }

            void Pose(float hipPitch, float shoulderX, float shoulderZ, float elbowX, float legSwing, float knee)
            {
                pivot.localRotation = Quaternion.Euler(hipPitch, 0, 0);
                shL.localRotation = Quaternion.Euler(shoulderX + legSwing * 0.6f, 0, -shoulderZ);
                shR.localRotation = Quaternion.Euler(shoulderX - legSwing * 0.6f, 0, shoulderZ);
                elL.localRotation = elR.localRotation = Quaternion.Euler(elbowX, 0, 0);
                hipL.localRotation = Quaternion.Euler(-legSwing, 0, 0); hipR.localRotation = Quaternion.Euler(legSwing, 0, 0);
                knL.localRotation = Quaternion.Euler(knee + Mathf.Max(0, legSwing) * 0.8f, 0, 0);
                knR.localRotation = Quaternion.Euler(knee + Mathf.Max(0, -legSwing) * 0.8f, 0, 0);
            }

            // where it will climb out of: the screen, and the direction the set is facing
            public void SetFrom(Vector3 screenPos, Vector3 front)
            {
                screen = screenPos;
                outDir = new Vector3(front.x - screenPos.x, 0, front.z - screenPos.z).normalized;
                Climb(0, new Vector2(front.x, front.z), new Vector2(front.x, front.z) + new Vector2(outDir.x, outDir.z));
            }

            // climbing out of the set, e = 0..1 over three seconds: hands first, then the head pushing through the glass,
            // the body sliding out and down onto the floor, and finally standing up, full size, facing you
            public void Climb(float e, Vector2 ground, Vector2 face)
            {
                float s = Mathf.Lerp(0.3f, 1f, MathX.Smooth(Mathf.Clamp01(e / 0.85f)));
                pivot.localPosition = new Vector3(0, HIP_H, 0);
                Vector3 hips; float pitch, shX, elX, knee, yawBlend = 0;
                var g3 = new Vector3(ground.x, 0, ground.y);
                if (e < 0.45f)
                {
                    float a = MathX.Smooth(e / 0.45f);
                    hips = screen + outDir * Mathf.Lerp(-0.36f, 0.04f, a) + Vector3.down * 0.04f;
                    pitch = 90; shX = Mathf.Lerp(-175, -150, a); elX = Mathf.Lerp(-10, -20, a); knee = 20;
                }
                else if (e < 0.8f)
                {
                    float b = MathX.Smooth((e - 0.45f) / 0.35f);
                    var from = screen + outDir * 0.04f + Vector3.down * 0.04f;
                    var to = g3 + outDir * -0.1f + Vector3.up * 0.42f * s;
                    hips = Vector3.Lerp(from, to, b) + Vector3.up * Mathf.Sin(b * Mathf.PI) * 0.08f;
                    pitch = Mathf.Lerp(90, 118, b); shX = Mathf.Lerp(-150, -95, b); elX = Mathf.Lerp(-20, -35, b); knee = Mathf.Lerp(20, 95, b);
                }
                else
                {
                    float c = MathX.Smooth((e - 0.8f) / 0.2f);
                    hips = Vector3.Lerp(g3 + outDir * -0.1f + Vector3.up * 0.42f * s, g3 + Vector3.up * HIP_H * s, c);
                    pitch = Mathf.Lerp(118, 4, c); shX = Mathf.Lerp(-95, -18, c); elX = Mathf.Lerp(-35, -28, c); knee = Mathf.Lerp(95, 6, c);
                    yawBlend = c;
                }
                root.transform.localScale = Vector3.one * s;
                float outYaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
                var d = face - ground;
                float faceYaw = d.sqrMagnitude > 0.001f ? Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg : outYaw;
                root.transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(outYaw, faceYaw, yawBlend), 0);
                root.transform.position = hips - Vector3.up * HIP_H * s;
                Pose(pitch, shX, 10, elX, Mathf.Sin(e * 18) * 12 * (1 - yawBlend), knee);
                head.localRotation = Quaternion.Euler(MathX.Jit(8), MathX.Jit(10), MathX.Jit(12));
                Fingers(Time.time * 2, 10);
                Snow();
            }

            // walking (only ever when you are)
            public void Place(Vector2 pos, Vector2 face, bool moving, float dt)
            {
                root.transform.localScale = Vector3.one;
                root.transform.position = new Vector3(pos.x, 0, pos.y);
                var d = face - pos;
                if (d.sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0);
                if (moving) walk += dt * 8.5f;
                float sw = Mathf.Sin(walk) * 26;
                // arms held out a little and forward, hands half-open: reaching for you
                Pose(4, -18, 12, -28, sw, 6);
                pivot.localPosition = new Vector3(0, HIP_H + Mathf.Abs(Mathf.Cos(walk)) * 0.03f, 0);
                // the head twitches when it's waiting
                twitchT -= dt;
                if (twitchT <= 0) { twitchT = 0.3f + Random.value * 1.4f; headTwitch = new Vector3(MathX.Jit(18), MathX.Jit(30), MathX.Jit(35)); }
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(headTwitch), Mathf.Min(1, dt * 30));
                Fingers(Time.time, moving ? 5 : 20);
                Snow();
            }

            public void Dissolve(float k)
            {
                root.transform.localScale = new Vector3(1 + k * 0.6f, Mathf.Max(0.02f, 1 - k), 1 + k * 0.6f);
                mat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));
                bool show = Random.value > k;
                foreach (var r in renderers) r.enabled = show;
            }

            public void Lunge(Vector2 pos, Vector2 face, float eyeY)
            {
                var d = face - pos;
                root.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0);
                root.transform.localScale = Vector3.one * 1.05f;
                pivot.localPosition = new Vector3(0, HIP_H, 0);
                Pose(12, -105, 28, -15, 0, 10);
                // the faceless head right in front of your eyes
                root.transform.position = new Vector3(pos.x, eyeY - 1.86f * 1.05f, pos.y);
                head.localRotation = Quaternion.Euler(MathX.Jit(20), MathX.Jit(20), MathX.Jit(30));
                Fingers(Time.time * 4, 30);
                Snow();
            }
        }
    }
}
