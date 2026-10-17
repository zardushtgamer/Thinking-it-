using UnityEngine;

namespace ThinkingIt
{
    // Mara's body. You see your own arms and hands in front of you (they bob as you walk, pump when you run), and when you
    // press E the right arm reaches out to the thing, closes its fingers, and brings it up to you. Look down and you see
    // your coat, jeans and boots, walking along with you.
    public class MaraBody
    {
        const float L1 = 0.30f, L2 = 0.27f, HAND = 0.16f;

        class Arm
        {
            public GameObject root, upper, fore;
            public Transform hand; public Transform[] fingers = new Transform[5];
            public float side; public Vector3 S;
        }

        readonly Game g;
        readonly World w;
        readonly Transform cam;
        readonly Arm armL = new Arm(), armR = new Arm();
        readonly GameObject armsRoot;

        // reaching
        float reachT = -1; const float REACH_TIME = 0.75f;
        Vector3 reachLocal; bool grabbed; System.Action onGrab;
        GameObject held;
        // the chapters are one map: her legs and coat follow her into whichever level is showing
        public void MoveTo(World nw) { if (bodyRoot != null) bodyRoot.transform.SetParent(nw.root, false); }

        public bool Busy => reachT >= 0;

        // legs and torso
        readonly GameObject bodyRoot;
        Transform hipL, hipR, kneeL, kneeR;
        float walkPhase, bodyYaw, lastX, lastZ, moveSpeed;

        static Material skin, coat, jeans, boot, paper, tapeM;

        public MaraBody(Game game)
        {
            g = game; w = game.w; cam = game.cam.transform;
            skin = Mats.Std(MathX.Hex(0xd9a98e), 0.55f, 0f);
            coat = Mats.Std(MathX.Hex(0x3b4a55), 0.85f, 0f);
            jeans = Mats.Std(MathX.Hex(0x2a3244), 0.9f, 0f);
            boot = Mats.Std(MathX.Hex(0x1e1a18), 0.6f, 0f);
            paper = Mats.Std(MathX.Hex(0xe0d8c4), 0.9f, 0f, null, null, null, 1, null, 1, true);
            tapeM = Mats.Std(MathX.Hex(0x1a1a1a), 0.4f, 0f);

            armsRoot = new GameObject("Mara arms"); armsRoot.transform.SetParent(cam, false);
            MakeArm(armL, -1); MakeArm(armR, 1);

            bodyRoot = new GameObject("Mara body"); bodyRoot.transform.SetParent(w.root, false);
            BuildBody();
            lastX = g.px; lastZ = g.pz; bodyYaw = (g.yaw + Mathf.PI) * Mathf.Rad2Deg;
        }

        GameObject Seg(float rt, float rb, float len, Material m, Transform parent)
        {
            var go = w.Cyl(rt, rb, len, 10, m, parent); go.NoShadow(); return go;
        }

        void MakeArm(Arm a, float side)
        {
            a.side = side; a.S = new Vector3(side * 0.23f, -0.22f, -0.05f);
            a.root = new GameObject(side < 0 ? "arm L" : "arm R"); a.root.transform.SetParent(armsRoot.transform, false);
            a.upper = Seg(0.048f, 0.042f, L1, coat, a.root.transform);
            a.fore = new GameObject("forearm"); a.fore.transform.SetParent(a.root.transform, false);
            Seg(0.043f, 0.036f, L2 * 0.78f, coat, a.fore.transform).At(0, -L2 * 0.11f, 0);          // sleeve
            Seg(0.03f, 0.026f, L2 * 0.26f, skin, a.fore.transform).At(0, L2 * 0.37f, 0);            // wrist
            // hand: a palm and five fingers that can curl
            var hand = new GameObject("hand"); hand.transform.SetParent(a.fore.transform, false); hand.transform.localPosition = new Vector3(0, L2 * 0.5f, 0);
            a.hand = hand.transform;
            w.Box(0.075f, 0.085f, 0.022f, skin, a.hand).At(0, 0.042f, 0).NoShadow();
            for (int i = 0; i < 5; i++)
            {
                var fp = new GameObject("finger"); fp.transform.SetParent(a.hand, false);
                bool thumb = i == 4;
                fp.transform.localPosition = thumb ? new Vector3(-side * 0.04f, 0.02f, 0) : new Vector3(-0.026f + i * 0.0175f, 0.085f, 0);
                if (thumb) fp.transform.localRotation = Quaternion.Euler(0, 0, side * 40f);
                float len = thumb ? 0.05f : (i == 1 || i == 2 ? 0.075f : 0.065f);
                w.Box(0.015f, len, 0.015f, skin, fp.transform).At(0, len / 2, 0).NoShadow();
                a.fingers[i] = fp.transform;
            }
            armsRoot.SetActive(true);
        }

        // ---------------------------------------------------------------- legs and coat (seen when you look down)
        void BuildBody()
        {
            var tr = bodyRoot.transform;
            var chest = w.Box(0.42f, 0.5f, 0.24f, coat, tr).At(0, 1.2f, 0); chest.NoShadow();
            w.Box(0.38f, 0.28f, 0.22f, coat, tr).At(0, 0.88f, 0).NoShadow();
            w.Cyl(0.075f, 0.09f, 0.14f, 10, skin, tr).At(0, 1.52f, 0).NoShadow();
            foreach (var sx in new[] { -1, 1 })
            {
                var hip = new GameObject(sx < 0 ? "hipL" : "hipR"); hip.transform.SetParent(tr, false); hip.transform.localPosition = new Vector3(sx * 0.1f, 0.82f, 0);
                w.Cyl(0.09f, 0.07f, 0.42f, 10, jeans, hip.transform).At(0, -0.21f, 0).NoShadow();
                var knee = new GameObject("knee"); knee.transform.SetParent(hip.transform, false); knee.transform.localPosition = new Vector3(0, -0.42f, 0);
                w.Cyl(0.068f, 0.055f, 0.40f, 10, jeans, knee.transform).At(0, -0.2f, 0).NoShadow();
                w.Box(0.1f, 0.09f, 0.26f, boot, knee.transform).At(0, -0.44f, 0.06f).NoShadow();
                if (sx < 0) { hipL = hip.transform; kneeL = knee.transform; } else { hipR = hip.transform; kneeR = knee.transform; }
            }
        }

        // ---------------------------------------------------------------- reaching
        // reach for something in the world, close the hand on it, and call onGrab when the fingers close
        public void Reach(Vector3 worldTarget, string kind, System.Action grabCb)
        {
            reachLocal = cam.InverseTransformPoint(worldTarget);
            reachT = 0; grabbed = false; onGrab = grabCb;
            if (held) Object.Destroy(held);
            held = null;
            if (kind == "doc") { held = w.Pl(0.1f, 0.13f, paper, armR.hand); held.At(0, 0.06f, 0.02f).Rot(0, 0, 0); }
            else if (kind == "tape" || kind == "recorder") { held = w.Box(0.09f, 0.02f, 0.06f, tapeM, armR.hand); held.At(0, 0.06f, 0.02f); }
            if (held) { held.NoShadow(); held.SetActive(false); }
        }

        // ---------------------------------------------------------------- per frame
        public void Tick(float dt)
        {
            bool show = g.state != GState.Caught && g.state != GState.End && g.state != GState.Intro;
            if (armsRoot.activeSelf != show) armsRoot.SetActive(show);
            if (bodyRoot.activeSelf != show) bodyRoot.SetActive(show);
            if (!show) return;

            // how fast are we moving
            float moved = MathX.Hypot(g.px - lastX, g.pz - lastZ); lastX = g.px; lastZ = g.pz;
            float spd = dt > 0 ? moved / dt : 0; moveSpeed = Mathf.Lerp(moveSpeed, Mathf.Min(spd, 6f), dt * 10f);
            walkPhase += moved * 3.2f;
            bool run = g.Sprinting;

            AnimateBody(dt, run);
            AnimateArms(dt, run);
        }

        void AnimateBody(float dt, bool run)
        {
            var f = g.Fwd;
            bodyRoot.transform.position = new Vector3(g.px - f.x * 0.17f, 0, g.pz - f.y * 0.17f);
            float want = (g.yaw + Mathf.PI) * Mathf.Rad2Deg;
            bodyYaw = Mathf.LerpAngle(bodyYaw, want, dt * 9f);
            bodyRoot.transform.rotation = Quaternion.Euler(0, bodyYaw, 0);
            float k = Mathf.Clamp01(moveSpeed / 1.6f), a = (run ? 0.75f : 0.45f) * k;
            float s = Mathf.Sin(walkPhase);
            hipL.localRotation = Quaternion.Euler(-s * a * Mathf.Rad2Deg, 0, 0);
            hipR.localRotation = Quaternion.Euler(s * a * Mathf.Rad2Deg, 0, 0);
            float kf = (run ? 1.1f : 0.6f) * k;
            kneeL.localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Cos(walkPhase)) * kf * Mathf.Rad2Deg, 0, 0);
            kneeR.localRotation = Quaternion.Euler(Mathf.Max(0, -Mathf.Cos(walkPhase)) * kf * Mathf.Rad2Deg, 0, 0);
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        void AnimateArms(float dt, bool run)
        {
            float t = Time.time, k = Mathf.Clamp01(moveSpeed / 1.6f);
            for (int i = 0; i < 2; i++)
            {
                var a = i == 0 ? armL : armR;
                float side = a.side;
                // resting pose: hands low and in front, swaying with every step
                float ph = walkPhase + (side > 0 ? 0 : Mathf.PI);
                float bobY = Mathf.Sin(t * 1.6f + side) * 0.004f + Mathf.Abs(Mathf.Sin(walkPhase)) * 0.012f * k;
                float swing = Mathf.Sin(ph) * (run ? 0.09f : 0.025f) * k;
                var T = new Vector3(side * (0.14f + (run ? 0.03f : 0)), -0.31f + bobY + (run ? 0.05f : 0) + Mathf.Max(0, swing) * 0.2f, 0.40f + swing + (run ? 0.03f : 0));
                float curl = run ? 0.7f : 0.25f;

                if (a == armR && reachT >= 0)
                {
                    reachT += dt / REACH_TIME;
                    float u = reachT;
                    var shoulder = a.S;
                    var away = reachLocal - shoulder;
                    float dist = away.magnitude;
                    var tgt = shoulder + away.normalized * Mathf.Min(dist - 0.12f, (L1 + L2) * 0.98f);
                    if (tgt.z < 0.18f) tgt.z = 0.18f;
                    var hold = new Vector3(0.09f, -0.10f, 0.30f);
                    if (u < 0.42f) { T = Vector3.Lerp(T, tgt, Ease(u / 0.42f)); curl = Mathf.Lerp(0.25f, 0.05f, Ease(u / 0.42f)); }
                    else if (u < 0.55f) { T = tgt; curl = Mathf.Lerp(0.05f, 1.35f, Ease((u - 0.42f) / 0.13f)); }
                    else if (u < 0.85f) { T = Vector3.Lerp(tgt, hold, Ease((u - 0.55f) / 0.3f)); curl = 1.35f; }
                    else { T = Vector3.Lerp(hold, T, Ease((u - 0.85f) / 0.15f)); curl = Mathf.Lerp(1.35f, 0.25f, Ease((u - 0.85f) / 0.15f)); }
                    if (!grabbed && u >= 0.5f) { grabbed = true; var cb = onGrab; onGrab = null; if (held) held.SetActive(true); cb?.Invoke(); }
                    if (held && u > 0.86f) held.SetActive(false);
                    if (u >= 1f) { reachT = -1; if (held) { Object.Destroy(held); held = null; } }
                }
                Solve(a, T);
                for (int f = 0; f < 5; f++) a.fingers[f].localRotation = Quaternion.Euler(curl * Mathf.Rad2Deg * (f == 4 ? 0.6f : 1f), 0, f == 4 ? a.side * 40f : 0);
            }
        }

        // two-bone arm: shoulder S, wrist T (both in camera space)
        void Solve(Arm a, Vector3 T)
        {
            var S = a.S; var d = T - S; float dist = Mathf.Clamp(d.magnitude, 0.12f, L1 + L2 - 0.005f); var dir = d.normalized;
            float ax = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist), h = Mathf.Sqrt(Mathf.Max(0, L1 * L1 - ax * ax));
            var pole = new Vector3(a.side * 0.55f, -1f, -0.15f);
            pole -= dir * Vector3.Dot(pole, dir); pole.Normalize();
            var E = S + dir * ax + pole * h;
            var W = S + dir * dist;
            Place(a.upper.transform, S, E);
            var fr = a.fore.transform;
            fr.localPosition = (E + W) * 0.5f;
            fr.localRotation = Quaternion.FromToRotation(Vector3.up, (W - E).normalized);
        }

        static void Place(Transform t, Vector3 from, Vector3 to)
        {
            t.localPosition = (from + to) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
        }
    }
}
