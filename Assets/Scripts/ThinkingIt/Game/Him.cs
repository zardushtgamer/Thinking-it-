using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // HIM: a man with no skin. Muscle, tendon, bone, teeth and two bare eyes, two metres tall.
    // He hears you (running is loud) and sees you. Once he is running he cannot steer: he runs in a straight line at full speed
    // until he reaches the spot he was aiming for or hits something, then he has to stop for three seconds to turn. While he
    // is turning he turns to face YOU, and the next run is aimed straight at where you are (or at the first corner of the way to you).
    public class Him
    {
        public const float WALK = 2.2f, RUN = 7.4f, TURN_TIME = 3f, R = 0.42f, SEE_RANGE = 28f, CATCH = 0.95f, SCALE = 1.08f;

        enum S { Hidden, Patrol, Charge, Turn }

        readonly Game g;
        readonly World w;
        public readonly GameObject root;
        Transform pelvis, torso, head, jaw;
        readonly Transform[] hip = new Transform[2], knee = new Transform[2], ankle = new Transform[2], shoulder = new Transform[2], elbow = new Transform[2], wrist = new Transform[2];
        readonly List<Transform> fingers = new List<Transform>();
        Material muscle, muscleDark, tendon, bone, darkM, eyeM, irisM, pupilM, teethM, galea;

        S state = S.Hidden;
        public float x, z;
        float yawDeg, spd, turnT, alertT, tx, tz, spawnT = 30f, runLeft, planT, patrolLeft, stepAcc, phase, twitchT;
        Vector2 planHd; bool havePlan;
        float lastPx, lastPz, noiseR, twitchYaw;
        bool toldPlayer, scareInit;
        float scareYaw;

        public bool Active => state != S.Hidden;
        public string StateName => state.ToString();
        public float TurnLeft => turnT;
        public Vector2 Dir => Face;
        public Vector2 Face => new Vector2(Mathf.Sin(yawDeg * Mathf.Deg2Rad), Mathf.Cos(yawDeg * Mathf.Deg2Rad));
        public Game Owner => g;
        public bool Charging => state == S.Charge;

        public Him(Game game, World world)
        {
            g = game; w = world;
            root = new GameObject("HIM");
            root.transform.SetParent(w.root, false);
            root.transform.localScale = Vector3.one * SCALE;
            Build();
            root.SetActive(false);
            var rg = new GameObject("HIM radar"); rg.transform.SetParent(w.root, false); rg.AddComponent<HimRadar>().him = this;
            lastPx = g.px; lastPz = g.pz;
        }

        // ------------------------------------------------------------------ the body: a real human anatomy, minus the skin
        static Texture MuscleTex()
        {
            var p = new Painter(128, 128, new Color(0.46f, 0.07f, 0.06f));
            var rnd = new System.Random(11);
            for (int i = 0; i < 170; i++)   // muscle fibres running down the length, with thin pale sheets of fascia
            {
                float x0 = (float)rnd.NextDouble() * 128, lw = 0.6f + (float)rnd.NextDouble() * 2.2f, t = (float)rnd.NextDouble();
                Color c = t < 0.10f ? new Color(0.92f, 0.82f, 0.72f, 0.5f) : (t < 0.55f ? new Color(0.80f, 0.17f, 0.13f, 0.6f) : new Color(0.28f, 0.03f, 0.03f, 0.6f));
                p.Line(x0, 0, x0 + ((float)rnd.NextDouble() - 0.5f) * 6, 128, lw, c);
            }
            p.Grain(6);
            return p.Make();
        }

        GameObject Ell(Transform parent, Material m, float rx, float ry, float rz, float x, float y, float z, float ax = 0, float ay = 0, float az = 0)
        {
            var go = w.Sph(1f, 14, 10, m, parent);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.Euler(ax, ay, az);
            go.transform.localScale = new Vector3(rx, ry, rz);
            return go;
        }
        static Transform Node(string n, Transform parent, float x, float y, float z)
        {
            var go = new GameObject(n); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(x, y, z); return go.transform;
        }

        void Build()
        {
            var tex = MuscleTex();
            muscle = Mats.Std(Color.white, 0.3f, 0f, tex, new Vector2(1, 1));
            muscleDark = Mats.Std(new Color(0.62f, 0.45f, 0.45f), 0.3f, 0f, tex, new Vector2(1.7f, 1));
            tendon = Mats.Std(MathX.Hex(0xe4d8c0), 0.4f, 0f);
            bone = Mats.Std(MathX.Hex(0xdcd2b6), 0.45f, 0f);
            darkM = Mats.Std(MathX.Hex(0x3a0807), 0.35f, 0f);
            galea = Mats.Std(MathX.Hex(0xd8b4a4), 0.35f, 0f);
            eyeM = Mats.Std(MathX.Hex(0xf2ead8), 0.12f, 0f, null, null, null, 1, MathX.Hex(0xfff0c0), 0.25f, false, false);
            irisM = Mats.Std(MathX.Hex(0xb09a40), 0.2f, 0f);
            pupilM = Mats.Std(MathX.Hex(0x050505), 0.1f, 0f);
            teethM = Mats.Std(MathX.Hex(0xe8e0cc), 0.25f, 0f);
            var tr = root.transform;

            pelvis = Node("pelvis", tr, 0, 0.98f, 0);
            Ell(pelvis, muscle, 0.17f, 0.10f, 0.11f, 0, 0, 0);
            foreach (var sx in new[] { -1f, 1f }) Ell(pelvis, muscleDark, 0.095f, 0.10f, 0.09f, sx * 0.085f, -0.045f, -0.075f);   // glutes

            // ---- legs
            for (int i = 0; i < 2; i++)
            {
                float sd = i == 0 ? -1 : 1;
                hip[i] = Node("hip", pelvis, sd * 0.095f, -0.02f, 0);
                var th = hip[i];
                Ell(th, muscle, 0.075f, 0.24f, 0.075f, 0, -0.23f, 0.018f);                       // quadriceps
                Ell(th, muscleDark, 0.065f, 0.22f, 0.06f, 0, -0.22f, -0.035f);                   // hamstrings
                Ell(th, muscle, 0.04f, 0.20f, 0.05f, -sd * 0.055f, -0.2f, 0.0f);                // adductors
                Ell(th, tendon, 0.014f, 0.21f, 0.014f, sd * 0.05f, -0.22f, 0.06f, 0, 0, sd * 4);
                knee[i] = Node("knee", th, 0, -0.46f, 0);
                var kn = knee[i];
                Ell(kn, bone, 0.04f, 0.045f, 0.032f, 0, 0.0f, 0.066f);                          // kneecap
                foreach (var cs in new[] { -1f, 1f }) Ell(kn, muscle, 0.045f, 0.14f, 0.05f, cs * 0.03f, -0.13f, -0.035f);   // gastrocnemius
                Ell(kn, muscleDark, 0.05f, 0.12f, 0.045f, 0, -0.21f, -0.02f);                   // soleus
                Ell(kn, muscle, 0.032f, 0.17f, 0.032f, 0, -0.2f, 0.04f);                        // tibialis
                Ell(kn, bone, 0.013f, 0.19f, 0.012f, 0, -0.21f, 0.062f);                        // shin
                Ell(kn, muscleDark, 0.032f, 0.1f, 0.032f, 0, -0.35f, -0.005f);
                ankle[i] = Node("ankle", kn, 0, -0.46f, 0);
                var an = ankle[i];
                Ell(an, tendon, 0.04f, 0.03f, 0.115f, 0, -0.012f, 0.06f);                       // foot
                Ell(an, bone, 0.03f, 0.03f, 0.035f, 0, 0.0f, -0.035f);                          // heel
                for (int t = 0; t < 5; t++) Ell(an, bone, 0.009f, 0.009f, 0.022f, -0.03f + t * 0.015f, -0.018f, 0.17f);
                Ell(an, tendon, 0.01f, 0.12f, 0.01f, 0, 0.1f, -0.04f);                          // achilles
            }

            // ---- torso
            torso = Node("torso", pelvis, 0, 0.05f, 0);
            foreach (var sx in new[] { -1f, 1f })
            {
                for (int row = 0; row < 3; row++) Ell(torso, muscle, 0.032f, 0.03f, 0.022f, sx * 0.036f, 0.1f + row * 0.075f, 0.095f);   // abs
                Ell(torso, muscle, 0.055f, 0.14f, 0.075f, sx * 0.125f, 0.18f, 0.01f);                                                   // obliques
                Ell(torso, muscle, 0.105f, 0.075f, 0.05f, sx * 0.085f, 0.56f, 0.095f, 0, 0, -sx * 12);                                  // pectorals
                Ell(torso, muscleDark, 0.08f, 0.2f, 0.05f, sx * 0.14f, 0.45f, -0.07f);                                                 // lats
                Ell(torso, bone, 0.09f, 0.012f, 0.015f, sx * 0.07f, 0.68f, 0.08f, 0, 0, sx * 8);                                       // collar bones
                Ell(torso, muscle, 0.018f, 0.085f, 0.022f, sx * 0.035f, 0.79f, 0.035f, 0, 0, -sx * 15);                                // neck muscles
            }
            Ell(torso, tendon, 0.006f, 0.13f, 0.006f, 0, 0.18f, 0.1f);
            Ell(torso, muscleDark, 0.17f, 0.25f, 0.07f, 0, 0.45f, -0.085f);
            Ell(torso, muscle, 0.16f, 0.075f, 0.06f, 0, 0.72f, -0.05f);
            Ell(torso, muscle, 0.05f, 0.08f, 0.05f, 0, 0.78f, 0.0f);
            for (int r = 0; r < 5; r++)           // ribs showing below the chest
            {
                var rib = w.Tor(0.145f - Mathf.Abs(r - 2) * 0.006f, 0.0075f, 6, 22, bone, torso);
                rib.transform.localPosition = new Vector3(0, 0.30f + r * 0.045f, -0.01f);
                rib.transform.localRotation = Quaternion.Euler(90, 0, 0);
                rib.transform.localScale = new Vector3(1.0f, 0.62f, 1f);
            }

            // ---- head: skull under a peeled scalp, bare face muscle, teeth, eyes that never close
            head = Node("head", torso, 0, 0.84f, 0.015f);
            Ell(head, galea, 0.092f, 0.115f, 0.10f, 0, 0.12f, -0.01f);
            Ell(head, muscle, 0.075f, 0.045f, 0.03f, 0, 0.19f, 0.075f);
            foreach (var sx in new[] { -1f, 1f })
            {
                Ell(head, muscle, 0.03f, 0.055f, 0.045f, sx * 0.088f, 0.14f, 0.0f);                       // temporalis
                Ell(head, darkM, 0.034f, 0.028f, 0.016f, sx * 0.037f, 0.125f, 0.082f);                   // eye sockets
                Ell(head, eyeM, 0.021f, 0.021f, 0.021f, sx * 0.037f, 0.125f, 0.09f);                     // eyeballs
                Ell(head, irisM, 0.011f, 0.011f, 0.004f, sx * 0.037f, 0.125f, 0.109f);
                Ell(head, pupilM, 0.0055f, 0.0055f, 0.004f, sx * 0.037f, 0.125f, 0.112f);
                Ell(head, muscle, 0.04f, 0.02f, 0.03f, sx * 0.06f, 0.075f, 0.07f, 0, 0, -sx * 25);         // cheeks
                Ell(head, muscleDark, 0.032f, 0.05f, 0.03f, sx * 0.078f, 0.04f, 0.025f);                  // masseter
                Ell(head, pupilM, 0.008f, 0.01f, 0.01f, sx * 0.012f, 0.055f, 0.105f);                    // nostrils
            }
            Ell(head, tendon, 0.013f, 0.03f, 0.02f, 0, 0.075f, 0.105f);                                   // nose cartilage
            Ell(head, darkM, 0.045f, 0.012f, 0.02f, 0, 0.05f, 0.088f);
            for (int t = 0; t < 8; t++)
            {
                float tx0 = -0.0385f + t * 0.011f;
                var tooth = w.Box(0.0105f, 0.017f, 0.008f, teethM, head); tooth.At(tx0, 0.04f, 0.099f - Mathf.Abs(tx0) * 0.6f);
            }
            jaw = Node("jaw", head, 0, 0.032f, 0);
            Ell(jaw, bone, 0.05f, 0.018f, 0.06f, 0, -0.01f, 0.045f);
            Ell(jaw, darkM, 0.026f, 0.011f, 0.04f, 0, 0.002f, 0.05f);                                   // tongue
            Ell(jaw, muscle, 0.03f, 0.02f, 0.02f, 0, -0.022f, 0.098f);
            foreach (var sx in new[] { -1f, 1f }) Ell(jaw, muscleDark, 0.02f, 0.04f, 0.03f, sx * 0.06f, 0.0f, 0.03f);
            for (int t = 0; t < 8; t++)
            {
                float tx0 = -0.0385f + t * 0.011f;
                var tooth = w.Box(0.0105f, 0.015f, 0.008f, teethM, jaw); tooth.At(tx0, 0.012f, 0.097f - Mathf.Abs(tx0) * 0.6f);
            }

            // ---- arms
            for (int i = 0; i < 2; i++)
            {
                float sd = i == 0 ? -1 : 1;
                shoulder[i] = Node("shoulder", torso, sd * 0.205f, 0.66f, 0);
                var sh = shoulder[i];
                Ell(sh, muscle, 0.065f, 0.075f, 0.065f, 0, -0.01f, 0);                                  // deltoid
                Ell(sh, muscle, 0.038f, 0.12f, 0.04f, 0, -0.14f, 0.02f);                                // biceps
                Ell(sh, muscleDark, 0.04f, 0.12f, 0.04f, 0, -0.14f, -0.025f);                           // triceps
                Ell(sh, tendon, 0.01f, 0.1f, 0.01f, sd * 0.032f, -0.14f, 0.02f);
                elbow[i] = Node("elbow", sh, 0, -0.29f, 0);
                var el = elbow[i];
                Ell(el, bone, 0.022f, 0.025f, 0.02f, 0, 0.0f, -0.03f);                                  // elbow point
                Ell(el, muscle, 0.032f, 0.11f, 0.034f, sd * 0.01f, -0.1f, 0.012f);
                Ell(el, muscleDark, 0.03f, 0.11f, 0.03f, 0, -0.11f, -0.012f);
                Ell(el, muscle, 0.028f, 0.1f, 0.028f, 0, -0.1f, -0.032f);
                for (int c = 0; c < 4; c++) { var cord = w.Cyl(0.004f, 0.004f, 0.1f, 5, tendon, el); cord.At(-0.012f + c * 0.008f, -0.23f, 0.02f); }
                wrist[i] = Node("wrist", el, 0, -0.28f, 0);
                var wr = wrist[i];
                Ell(wr, muscleDark, 0.04f, 0.05f, 0.017f, 0, -0.045f, 0);                                // hand
                for (int f = 0; f < 5; f++)
                {
                    bool thumb = f == 4;
                    var fp = Node("finger", wr, thumb ? -sd * 0.04f : -0.027f + f * 0.018f, thumb ? -0.04f : -0.09f, 0);
                    if (thumb) fp.localRotation = Quaternion.Euler(0, 0, sd * 35);
                    float l1 = thumb ? 0.04f : 0.06f + (f == 1 || f == 2 ? 0.01f : 0);
                    var c1 = w.Cyl(0.007f, 0.0085f, l1, 6, tendon, fp); c1.At(0, -l1 / 2, 0);
                    var f2 = Node("f2", fp, 0, -l1, 0);
                    var c2 = w.Cyl(0.005f, 0.007f, l1 * 0.9f, 6, bone, f2); c2.At(0, -l1 * 0.45f, 0);
                    fingers.Add(fp); fingers.Add(f2);
                }
            }
        }

        // ------------------------------------------------------------------ helpers
        bool Blocked(float px, float pz) => w.Solid(px - R, pz - R) || w.Solid(px + R, pz - R) || w.Solid(px - R, pz + R) || w.Solid(px + R, pz + R);

        float ClearLen(Vector2 d, float max)
        {
            float l = 0;
            while (l < max && !Blocked(x + d.x * (l + 0.35f), z + d.y * (l + 0.35f))) l += 0.35f;
            return l;
        }

        // the cells between here and a target, as cell centres (walls in the way are walked around)
        List<Vector2> FindPath(float gx, float gz)
        {
            int W = w.W, H = w.H;
            int sx = Mathf.FloorToInt(x / World.C), sz = Mathf.FloorToInt(z / World.C), ex = Mathf.FloorToInt(gx / World.C), ez = Mathf.FloorToInt(gz / World.C);
            if (sx < 0 || sz < 0 || sx >= W || sz >= H || ex < 0 || ez < 0 || ex >= W || ez >= H) return null;
            int s = sz * W + sx, t = ez * W + ex;
            var prev = new int[W * H]; for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            var q = new Queue<int>(); prev[s] = s; q.Enqueue(s);
            int[] dxs = { 1, -1, 0, 0 }, dzs = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int c = q.Dequeue(); if (c == t) break;
                int cx0 = c % W, cz0 = c / W;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx0 + dxs[k], nz = cz0 + dzs[k]; if (nx < 0 || nz < 0 || nx >= W || nz >= H) continue;
                    int n = nz * W + nx; if (prev[n] != -1) continue;
                    if (n != t && !w.Walkable(nx, nz)) continue;
                    prev[n] = c; q.Enqueue(n);
                }
            }
            if (prev[t] == -1) return null;
            var path = new List<Vector2>();
            for (int c = t; c != s; c = prev[c]) path.Add(new Vector2(World.cx(c % W), World.cx(c / W)));
            path.Reverse();
            return path;
        }

        // Where to run: straight at the target if nothing is in the way, otherwise at the furthest point of the way there that he can see.
        bool Plan(float gx, float gz, out Vector2 hd, out float run)
        {
            hd = Face; run = 0;
            float dx = gx - x, dz = gz - z, d = MathX.Hypot(dx, dz);
            if (d < 0.2f) { run = 1f; return false; }
            var dir = new Vector2(dx / d, dz / d);
            if (ClearLen(dir, d) >= d - 0.35f) { hd = dir; run = d + 1.5f; return true; }
            var path = FindPath(gx, gz);
            if (path == null || path.Count == 0) { hd = dir; run = Mathf.Max(3f, ClearLen(dir, 20f)); return true; }
            int best = -1;
            for (int i = 0; i < path.Count; i++)
            {
                var v = path[i] - new Vector2(x, z); float vd = v.magnitude;
                if (vd < 0.3f) { best = i; continue; }
                if (ClearLen(v / vd, vd) >= vd - 0.35f) best = i; else break;
            }
            if (best < 0) best = 0;
            var wp = path[best] - new Vector2(x, z);
            if (wp.magnitude < 0.4f && best + 1 < path.Count) wp = path[best + 1] - new Vector2(x, z);
            hd = wp.normalized; run = wp.magnitude;
            return true;
        }

        void FaceAngle(Vector2 hd) { yawDeg = Mathf.Atan2(hd.x, hd.y) * Mathf.Rad2Deg; }

        void ChooseWander()
        {
            Vector2 bestD = Face; float bl = -1;
            for (int i = 0; i < 14; i++)
            {
                float a = Random.value * 6.2832f; var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float l = ClearLen(d, 9f);
                if (l > bl) { bl = l; bestD = d; if (l >= 8f && Random.value < 0.5f) break; }
            }
            FaceAngle(bestD); patrolLeft = Mathf.Clamp(bl, 3f, 14f);
        }

        void StartTurn()
        {
            state = S.Turn; turnT = TURN_TIME; planT = 0; havePlan = false;
        }

        void StartCharge(Vector2 hd, float run, float dist)
        {
            state = S.Charge; runLeft = run; FaceAngle(hd);
            g.au.Scream(Mathf.Clamp(0.65f * (1 - dist / 40f), 0.06f, 0.65f));
            g.shake = Mathf.Max(g.shake, Mathf.Clamp01(1 - dist / 20f) * 0.6f);
        }

        void Spawn()
        {
            float bd = -1; Vector2 pick = Vector2.zero; int n = 0;
            for (int cy = 0; cy < w.H; cy++) for (int cxx = 0; cxx < w.W; cxx++)
                {
                    if (!w.Walkable(cxx, cy)) continue;
                    float wx = World.cx(cxx), wz = World.cx(cy);
                    if (Blocked(wx, wz) || g.ch.Zone(wx, wz) == "hall") continue;
                    float d = MathX.Hypot(wx - g.px, wz - g.pz);
                    if (d < 30f) { if (d > bd && n == 0) { bd = d; pick = new Vector2(wx, wz); } continue; }
                    n++; if (Random.value < 1f / n) pick = new Vector2(wx, wz);
                }
            x = pick.x; z = pick.y; spd = WALK; alertT = 0;
            root.SetActive(true);
            ChooseWander(); state = S.Patrol;
            Place(0);
        }

        public void Reset(float delay = 15f)
        {
            state = S.Hidden; spawnT = delay; alertT = 0; spd = 0;
            root.SetActive(false);
            g.au.Set(g.au.breath, 0);
        }

        bool Advance(float dt)
        {
            float step = spd * dt; var f = Face;
            float nx = x + f.x * step, nz = z + f.y * step;
            if (Blocked(nx, nz))
            {
                // slide along walls a little instead of stopping dead on every corner brush
                if (!Blocked(nx, z) && Mathf.Abs(f.x) > 0.35f) { x = nx; return true; }
                if (!Blocked(x, nz) && Mathf.Abs(f.y) > 0.35f) { z = nz; return true; }
                spd = 0; return false;
            }
            x = nx; z = nz;
            return true;
        }

        // ------------------------------------------------------------------ per frame
        public void Update(float dt)
        {
            if (g.state != GState.Play && g.state != GState.Cutscene) { lastPx = g.px; lastPz = g.pz; return; }
            if (state == S.Hidden)
            {
                spawnT -= dt;
                if (spawnT <= 0) Spawn();
                lastPx = g.px; lastPz = g.pz;
                return;
            }

            float px = g.px, pz = g.pz, vx0 = px - x, vz0 = pz - z, d = MathX.Hypot(vx0, vz0);

            // ---- senses
            float pSpeed = dt > 0 ? MathX.Hypot(px - lastPx, pz - lastPz) / dt : 0;
            lastPx = px; lastPz = pz;
            noiseR = g.Sprinting ? 24f : (pSpeed > 0.9f ? (g.eyes ? 7.5f : 3f) : 0f);
            var f = Face;
            bool inCone = d < 5f || ((vx0 * f.x + vz0 * f.y) / Mathf.Max(d, 0.01f)) > 0.15f;
            bool sees = d < SEE_RANGE && inCone && g.Los(x, z, px, pz);
            bool hears = d < noiseR;
            if (sees) { tx = px; tz = pz; alertT = 9f; }
            else if (hears) { tx = px + MathX.Jit(2.5f); tz = pz + MathX.Jit(2.5f); alertT = Mathf.Max(alertT, 7f); }
            else alertT = Mathf.Max(0, alertT - dt);
            if ((sees || hears) && !toldPlayer) { toldPlayer = true; g.Hint("Something heavy is coming. It can't steer: break its line and let it hit a wall.", 8); }

            // ---- behaviour
            switch (state)
            {
                case S.Patrol:
                    {
                        spd = Mathf.MoveTowards(spd, WALK, dt * 8f);
                        if (alertT > 0 && Plan(tx, tz, out var hd, out var run))
                        {
                            if (Vector2.Angle(f, hd) < 20f) { StartCharge(hd, run, d); break; }
                            StartTurn(); break;
                        }
                        patrolLeft -= spd * dt;
                        if (!Advance(dt) || patrolLeft <= 0) StartTurn();
                        break;
                    }
                case S.Charge:
                    {
                        spd = Mathf.MoveTowards(spd, RUN, dt * 14f);
                        float step = spd * dt;
                        bool ok = Advance(dt); runLeft -= step;
                        if (!ok)
                        {
                            float k = Mathf.Clamp01(1 - d / 18f);          // he hit something at full speed
                            g.au.Noise(0.35f, 110, 0.9f * Mathf.Max(0.2f, k), Synth.F.Lowpass, new Vector3(x, 1, z));
                            g.au.Crack(new Vector3(x, 1.4f, z), 0.4f + k * 0.4f);
                            g.shake = Mathf.Max(g.shake, k * 0.9f);
                            StartTurn();
                        }
                        else if (runLeft <= 0) StartTurn();
                        break;
                    }
                case S.Turn:
                    {
                        turnT -= dt;
                        if (spd > 0.05f) { spd = Mathf.MoveTowards(spd, 0, dt * 18f); Advance(dt); }   // skid to a stop
                        planT -= dt;
                        if (planT <= 0) { planT = 0.2f; havePlan = alertT > 0 && Plan(tx, tz, out planHd, out _); }
                        if (havePlan) yawDeg = Mathf.MoveTowardsAngle(yawDeg, Mathf.Atan2(planHd.x, planHd.y) * Mathf.Rad2Deg, 170f * dt);   // turning to face YOU
                        else yawDeg += Mathf.Sin(Time.time * 1.7f) * 14f * dt;
                        if (turnT <= 0)
                        {
                            if (alertT > 0 && Plan(tx, tz, out var hd2, out var run2)) StartCharge(hd2, run2, d);
                            else { ChooseWander(); state = S.Patrol; }
                        }
                        break;
                    }
            }

            // ---- footsteps, breath, fear
            if (spd > 0.5f)
            {
                stepAcc += spd * dt;
                if (stepAcc > (spd > 5f ? 1.9f : 1.0f)) { stepAcc = 0; g.au.Thud(new Vector3(x, 0.1f, z)); }
            }
            g.au.SetPos(g.au.breath, new Vector3(x, 1.8f, z));
            g.au.Set(g.au.breath, Mathf.Clamp01(0.55f - d / 45f) * (state == S.Charge ? 1.2f : 0.7f));
            if (state == S.Charge && d < 22f)
            {
                g.dread = MathX.Clamp01(g.dread + (1 - d / 22f) * 0.22f * dt);
                if (d < 12f) g.shake = Mathf.Max(g.shake, (1 - d / 12f) * 0.45f);
            }

            Place(dt);

            if (g.state == GState.Play && d < CATCH) g.Caught();
        }

        // ------------------------------------------------------------------ animation: a walk cycle and a run cycle, blended by speed
        void Place(float dt)
        {
            var t = root.transform;
            t.position = new Vector3(x, 0, z);
            t.rotation = Quaternion.Euler(0, yawDeg, 0);
            Animate(dt);
        }

        static Quaternion Ex(float deg, float y = 0, float zz = 0) => Quaternion.Euler(deg, y, zz);

        void Animate(float dt)
        {
            float runK = Mathf.Clamp01((spd - WALK * 1.3f) / (RUN * 0.65f - WALK * 1.3f));
            float moveK = Mathf.Clamp01(spd / 1.2f);
            phase += spd * dt * Mathf.Lerp(3.9f, 2.1f, runK);
            float A = Mathf.Lerp(26f, 54f, runK) * moveK, K = Mathf.Lerp(30f, 95f, runK) * moveK, time = Time.time;

            for (int i = 0; i < 2; i++)
            {
                float ph = phase + (i == 0 ? 0 : Mathf.PI), sn = Mathf.Sin(ph);
                float fl = 0.12f + 0.88f * Mathf.Max(0, Mathf.Cos(ph));              // the knee folds as the leg swings forward
                hip[i].localRotation = Ex(-sn * A);
                knee[i].localRotation = Ex(fl * K);
                ankle[i].localRotation = Ex(-sn * A * 0.45f - fl * K * 0.25f);
            }
            float bob = (0.03f + 0.03f * runK) * moveK;
            pelvis.localPosition = new Vector3(0, 0.98f - bob * 0.5f + bob * Mathf.Abs(Mathf.Cos(phase)), 0);
            pelvis.localRotation = Ex(0, Mathf.Sin(phase) * 7f * moveK, Mathf.Sin(phase) * 3f * moveK);

            // standing still (the three-second turn): a slow heave of the chest and twitches of the head and hands
            float idle = 1 - moveK;
            twitchT -= dt; if (twitchT <= 0) { twitchT = 0.15f + Random.value * 0.7f; twitchYaw = state == S.Turn ? MathX.Jit(40f) : MathX.Jit(10f); }
            float lean = Mathf.Lerp(3f, 26f, runK) * moveK + idle * Mathf.Sin(time * 1.5f) * 2.5f;
            torso.localRotation = Ex(lean, -Mathf.Sin(phase) * 9f * moveK + idle * Mathf.Sin(time * 0.9f) * 4f, Mathf.Sin(phase) * 2.5f * moveK);
            head.localRotation = Ex(-lean * 0.7f + (runK > 0.3f ? -8f : 0f), Mathf.Lerp(twitchYaw, 0, moveK) * 0.6f + Mathf.Sin(time * 0.8f) * 4f * idle, 0);
            bool charging = state == S.Charge;
            jaw.localRotation = Ex(charging ? 26f + Mathf.Sin(time * 18f) * 7f : (state == S.Turn ? 12f + Mathf.Abs(Mathf.Sin(time * 25f)) * 8f : 7f + Mathf.Sin(time * 2f) * 3f));

            for (int i = 0; i < 2; i++)
            {
                float sd = i == 0 ? -1 : 1, ph = phase + (i == 0 ? 0 : Mathf.PI), fwd = -Mathf.Sin(ph);   // an arm swings forward as the leg on its side goes back
                float AA = Mathf.Lerp(22f, 55f, runK) * moveK;
                shoulder[i].localRotation = Ex(-(fwd * AA + 6f + runK * 14f), 0, sd * (7f + runK * 6f));
                float e = (14f + 78f * runK + 22f * Mathf.Max(0, fwd) * moveK) + idle * 8f;
                elbow[i].localRotation = Ex(-e);
                wrist[i].localRotation = Ex(-runK * 14f);
            }
            float curl = charging ? 35f : (state == S.Turn ? 55f + Mathf.Sin(time * 11f) * 15f : 28f);
            for (int i = 0; i < fingers.Count; i++) fingers[i].localRotation = Ex(-curl * (i % 2 == 0 ? 0.8f : 1.1f));
        }

        // ------------------------------------------------------------------ the jumpscare
        public void Scare(float t, float dt)
        {
            if (t <= 0f)
            {
                float ddx = x - g.px, ddz = z - g.pz;
                if (MathX.Hypot(ddx, ddz) > 0.05f) g.yaw = Mathf.Atan2(-ddx, -ddz);
                scareYaw = g.yaw; scareInit = true;
                root.SetActive(true); state = S.Hidden; spawnT = 1e9f; alertT = 0;
                g.au.Set(g.au.breath, 0);
            }
            if (!scareInit) return;
            if (t < Game.JUMP)
            {
                float k = MathX.Smooth(Mathf.Clamp01(t / 0.25f));
                float dd = Mathf.Lerp(2.3f, 0.7f, k);
                var f = new Vector2(-Mathf.Sin(scareYaw), -Mathf.Cos(scareYaw));
                var tr = root.transform;
                tr.position = new Vector3(g.px + f.x * dd, -0.18f, g.pz + f.y * dd);
                tr.rotation = Quaternion.Euler(0, scareYaw * Mathf.Rad2Deg, 0);
                float tm = Time.time;
                hip[0].localRotation = hip[1].localRotation = Ex(0);
                pelvis.localPosition = new Vector3(0, 0.98f, 0); pelvis.localRotation = Quaternion.identity;
                torso.localRotation = Ex(28f);
                for (int i = 0; i < 2; i++)
                {
                    float sd = i == 0 ? -1 : 1;
                    shoulder[i].localRotation = Ex(-105f + Mathf.Sin(tm * 30f + i) * 10f, 0, sd * 14f);
                    elbow[i].localRotation = Ex(-30f);
                }
                head.localRotation = Ex(-18f, Mathf.Sin(tm * 40f) * 10f, 0);
                jaw.localRotation = Ex(48f + Mathf.Sin(tm * 36f) * 10f);
            }
            else if (root.activeSelf) { root.SetActive(false); scareInit = false; }
        }
    }

    // A debug radar (top right) that shows where HIM is, relative to where you are facing. Up = the way you look.
    // Set Him.ShowRadar = false to hide it.
    public class HimRadar : MonoBehaviour
    {
        public static bool ShowRadar = true;
        public Him him;
        const float RANGE = 45f, SIZE = 220f;

        void OnGUI()
        {
            if (!ShowRadar || him == null) return;
            var g = him.Owner; if (g == null || g.state == GState.Intro || g.state == GState.End) return;
            float x0 = Screen.width - SIZE - 20, y0 = 20;
            var tex = Texture2D.whiteTexture;
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(x0, y0, SIZE, SIZE), tex);
            GUI.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            float cxp = x0 + SIZE / 2, cyp = y0 + SIZE / 2;
            GUI.DrawTexture(new Rect(x0, cyp - 0.5f, SIZE, 1), tex); GUI.DrawTexture(new Rect(cxp - 0.5f, y0, 1, SIZE), tex);
            foreach (var rr in new[] { 15f, 30f })      // range rings (as squares)
            { float h = rr / RANGE * SIZE / 2; GUI.DrawTexture(new Rect(cxp - h, cyp - h, h * 2, 1), tex); GUI.DrawTexture(new Rect(cxp - h, cyp + h, h * 2, 1), tex); GUI.DrawTexture(new Rect(cxp - h, cyp - h, 1, h * 2), tex); GUI.DrawTexture(new Rect(cxp + h, cyp - h, 1, h * 2), tex); }
            // you
            GUI.color = Color.white; GUI.DrawTexture(new Rect(cxp - 4, cyp - 4, 8, 8), tex);
            string label = "HIM: not here yet";
            if (him.Active)
            {
                var f = g.Fwd; float rx = f.y, rz = -f.x;
                float dx = him.x - g.px, dz = him.z - g.pz, d = Mathf.Sqrt(dx * dx + dz * dz);
                float up = dx * f.x + dz * f.y, right = dx * rx + dz * rz;
                float k = (SIZE / 2 - 8) / RANGE;
                float sx = right * k, sy = -up * k;
                float m = Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy)), lim = SIZE / 2 - 8;
                bool off = m > lim; if (off) { sx *= lim / m; sy *= lim / m; }
                bool charge = him.Charging;
                GUI.color = charge ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.7f, 0.2f);
                float bs = charge ? 14 : 11;
                GUI.DrawTexture(new Rect(cxp + sx - bs / 2, cyp + sy - bs / 2, bs, bs), tex);
                // which way it is facing
                var dd = him.Dir; float fr = dd.x * rx + dd.y * rz, fu = dd.x * f.x + dd.y * f.y;
                GUI.DrawTexture(new Rect(cxp + sx + fr * 12 - 3, cyp + sy - fu * 12 - 3, 6, 6), tex);
                label = $"HIM: {him.StateName.ToUpper()}  {d:F0} m" + (him.StateName == "Turn" ? $"  ({him.TurnLeft:F1}s)" : "");
            }
            GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            st.normal.textColor = new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(x0, y0 + SIZE + 2, SIZE, 24), label, st);
            GUI.color = old;
        }
    }
}
