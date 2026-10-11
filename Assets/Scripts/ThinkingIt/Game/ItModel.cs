using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThinkingIt
{
    // "It": a tall, starved figure, hunched forward. Arms that reach past its knees, fingers like twigs.
    // A long skull with two pale pinpoint eyes and a mouth that hangs open further the more afraid you are.
    // It never moves while you watch it, but its head does: small, wrong, sudden jerks.
    public class ItModel
    {
        // how frightened the player is (0..1). Game sets it every frame; the jaw, twitching and eyes follow it.
        public static float Intensity;

        public GameObject root;
        public readonly List<GameObject> smears = new List<GameObject>();
        public Material coreMat, rimMat;
        // 0 = idle, 1 = lunging at the camera (used for the jumpscare when it reaches you)
        public float lunge;

        readonly List<Collider> colliders = new List<Collider>();
        readonly List<MeshRenderer> faceParts = new List<MeshRenderer>();
        readonly List<Transform> fingers = new List<Transform>();
        readonly float[] fingerSeed, fingerZ;
        Material eyeMat, mouthMat, toothMat;
        Transform body, chest, head, jaw, mouth, armL, armR, foreL, foreR;
        bool visible;

        // head twitching
        Vector3 headTarget, headCur;
        float twitchT, shudderT;

        public ItModel(Transform parent)
        {
            root = new GameObject("It");
            root.transform.SetParent(parent, false);
            coreMat = new Material(Mats.Unlit(Color.black));
            rimMat = Mats.UnlitTransparent(new Color(0.6f, 0.64f, 0.68f, 0.12f));
            rimMat.SetFloat("_Cull", 1); rimMat.SetFloat("_RenderFace", 1);   // back faces only: a faint outline
            eyeMat = new Material(Mats.Unlit(new Color(0.95f, 0.95f, 0.88f)));
            mouthMat = new Material(Mats.Unlit(new Color(0.22f, 0.01f, 0.01f)));
            toothMat = new Material(Mats.Unlit(new Color(0.62f, 0.6f, 0.52f)));

            body = Node("body", root.transform, Vector3.zero);
            BuildBody();
            fingerSeed = new float[fingers.Count];
            fingerZ = new float[fingers.Count];
            for (int i = 0; i < fingerSeed.Length; i++) { fingerSeed[i] = Random.value * 100; fingerZ[i] = fingers[i].localEulerAngles.z; }

            // two loose copies of its outline that never quite line up with it
            for (int i = 0; i < 2; i++)
            {
                var s = new GameObject("smear"); s.transform.SetParent(root.transform, false);
                var sm = Mats.UnlitTransparent(new Color(0, 0, 0, 0.3f));
                Part(MeshGen.Cylinder(0.2f, 0.12f, 1.3f, 8), s.transform, new Vector3(0, 1.9f, 0.12f), Quaternion.Euler(18, 0, 0), Vector3.one, sm, false, false);
                Part(MeshGen.Sphere(0.16f, 10, 8), s.transform, new Vector3(0, 2.72f, 0.35f), Quaternion.identity, new Vector3(0.85f, 1.35f, 0.95f), sm, false, false);
                Part(MeshGen.Cylinder(0.06f, 0.08f, 1.25f, 6), s.transform, new Vector3(-0.13f, 0.62f, 0), Quaternion.identity, Vector3.one, sm, false, false);
                Part(MeshGen.Cylinder(0.06f, 0.08f, 1.25f, 6), s.transform, new Vector3(0.13f, 0.62f, 0), Quaternion.identity, Vector3.one, sm, false, false);
                smears.Add(s);
            }

            var tag = root.AddComponent<Tag>(); tag.tagName = "it";
            SetVisible(false);
        }

        // ------------------------------------------------------------------ construction
        static Transform Node(string name, Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = rot ?? Quaternion.identity;
            return t;
        }

        // one mesh part. Body parts get a faint back-face outline and a collider (so you can "see" it with the gaze ray).
        MeshRenderer Part(Mesh mesh, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material m, bool outline, bool collide)
        {
            var go = new GameObject("p");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = m == coreMat ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collide) { var c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh; colliders.Add(c); }
            if (outline)
            {
                var o = new GameObject("rim"); o.transform.SetParent(go.transform, false);
                o.transform.localScale = Vector3.one * 1.08f;
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var orr = o.AddComponent<MeshRenderer>(); orr.sharedMaterial = rimMat; orr.shadowCastingMode = ShadowCastingMode.Off;
            }
            return r;
        }

        // a tapered cylinder running between two points in the parent's space
        MeshRenderer Limb(Transform parent, Vector3 a, Vector3 b, float rA, float rB, bool outline = true, bool collide = true, int seg = 7)
        {
            var d = b - a;
            return Part(MeshGen.Cylinder(rB, rA, d.magnitude, seg), parent, (a + b) / 2, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one, coreMat, outline, collide);
        }

        void Joint(Transform parent, Vector3 p, float r) => Part(MeshGen.Sphere(r, 8, 6), parent, p, Quaternion.identity, Vector3.one, coreMat, true, false);

        void BuildBody()
        {
            // legs: thin, knees bent forward, long splayed feet
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 hip = new Vector3(0.12f * s, 1.28f, 0), knee = new Vector3(0.15f * s, 0.72f, 0.16f), ankle = new Vector3(0.16f * s, 0.08f, -0.06f), toe = new Vector3(0.2f * s, 0.02f, 0.32f);
                Limb(body, hip, knee, 0.075f, 0.05f);
                Joint(body, knee, 0.06f);
                Limb(body, knee, ankle, 0.05f, 0.035f);
                Limb(body, ankle, toe, 0.035f, 0.015f, true, false);
            }
            Part(MeshGen.Sphere(0.17f, 10, 8), body, new Vector3(0, 1.3f, 0), Quaternion.identity, new Vector3(1.1f, 0.7f, 0.8f), coreMat, true, true);   // pelvis

            // the chest leans forward over you
            chest = Node("chest", body, new Vector3(0, 1.32f, 0), Quaternion.Euler(20, 0, 0));
            Part(MeshGen.Cylinder(0.21f, 0.11f, 1.05f, 9), chest, new Vector3(0, 0.52f, 0), Quaternion.identity, new Vector3(1, 1, 0.72f), coreMat, true, true);
            for (int i = 0; i < 5; i++)   // ribs showing through
                Part(MeshGen.Torus(0.15f + i * 0.012f, 0.014f, 4, 18), chest, new Vector3(0, 0.45f + i * 0.1f, 0), Quaternion.Euler(90, 0, 0), new Vector3(1, 0.72f, 1), coreMat, false, false);
            for (int i = 0; i < 7; i++)   // the knuckles of its spine
                Part(MeshGen.Sphere(0.035f, 6, 5), chest, new Vector3(0, 0.15f + i * 0.13f, -0.15f + Mathf.Sin(i * 0.5f) * 0.02f), Quaternion.identity, Vector3.one, coreMat, false, false);
            Limb(chest, new Vector3(-0.28f, 1.0f, 0), new Vector3(0.28f, 1.0f, 0), 0.05f, 0.05f);   // shoulders, too wide

            // arms: hanging nearly to the floor
            armL = Arm(-1); armR = Arm(1);

            // neck, stretched forward, and the head
            Limb(chest, new Vector3(0, 1.02f, 0), new Vector3(0, 1.32f, 0.12f), 0.05f, 0.035f);
            head = Node("head", chest, new Vector3(0, 1.32f, 0.12f));
            Part(MeshGen.Sphere(0.15f, 14, 10), head, new Vector3(0, 0.13f, 0.02f), Quaternion.identity, new Vector3(0.82f, 1.4f, 0.95f), coreMat, true, true);
            // eyes: two pale points, too small and too far apart
            foreach (var s in new[] { -1f, 1f })
            {
                faceParts.Add(Part(MeshGen.Sphere(0.032f, 8, 6), head, new Vector3(0.058f * s, 0.2f, 0.14f), Quaternion.identity, Vector3.one, mouthMat, false, false));   // dark socket ring
                faceParts.Add(Part(MeshGen.Sphere(0.016f, 8, 6), head, new Vector3(0.058f * s, 0.2f, 0.16f), Quaternion.identity, Vector3.one, eyeMat, false, false));
            }
            // the mouth: a long dark slot with teeth along the rim. The jaw drops with your fear.
            jaw = Node("jaw", head, new Vector3(0, 0.04f, 0.06f));
            mouth = Part(MeshGen.Sphere(0.1f, 12, 10), head, new Vector3(0, -0.02f, 0.12f), Quaternion.identity, new Vector3(0.5f, 1f, 0.32f), mouthMat, false, false).transform;
            faceParts.Add(mouth.GetComponent<MeshRenderer>());
            Part(MeshGen.Sphere(0.1f, 10, 8), jaw, new Vector3(0, -0.06f, 0.02f), Quaternion.identity, new Vector3(0.7f, 0.5f, 0.75f), coreMat, true, false);   // the jaw itself
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 0.013f;
                faceParts.Add(Part(MeshGen.Cylinder(0, 0.006f, 0.035f, 4), head, new Vector3(x, 0.035f, 0.15f), Quaternion.Euler(180, 0, 0), Vector3.one, toothMat, false, false));   // top teeth, pointing down
                faceParts.Add(Part(MeshGen.Cylinder(0, 0.006f, 0.03f, 4), jaw, new Vector3(x, -0.035f, 0.095f), Quaternion.identity, Vector3.one, toothMat, false, false));             // bottom teeth, pointing up
            }
        }

        Transform Arm(float s)
        {
            var shoulder = Node(s < 0 ? "armL" : "armR", chest, new Vector3(0.3f * s, 0.98f, 0), Quaternion.Euler(0, 0, 6 * s));
            Joint(shoulder, Vector3.zero, 0.06f);
            var elbowP = new Vector3(0.02f * s, -0.86f, 0.06f);
            Limb(shoulder, Vector3.zero, elbowP, 0.05f, 0.035f);
            var fore = Node("fore", shoulder, elbowP, Quaternion.Euler(-14, 0, 0));
            if (s < 0) foreL = fore; else foreR = fore;
            Joint(fore, Vector3.zero, 0.042f);
            var wrist = new Vector3(0, -0.95f, 0);
            Limb(fore, Vector3.zero, wrist, 0.035f, 0.025f);
            var hand = Node("hand", fore, wrist);
            Part(MeshGen.Sphere(0.05f, 8, 6), hand, new Vector3(0, -0.03f, 0), Quaternion.identity, new Vector3(1, 1.4f, 0.5f), coreMat, true, false);
            // five long fingers, two segments each
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * 0.022f;
                var f = Node("finger", hand, new Vector3(a, -0.06f, i == 4 ? 0.035f : 0), Quaternion.Euler(i == 4 ? -25 : -6, 0, (i - 2) * 7 * s));
                float len = i == 4 ? 0.14f : 0.2f + (i == 2 ? 0.04f : 0);
                Limb(f, Vector3.zero, new Vector3(0, -len, 0), 0.011f, 0.008f, false, false, 4);
                var tip = Node("tip", f, new Vector3(0, -len, 0), Quaternion.Euler(-30, 0, 0));
                Limb(tip, Vector3.zero, new Vector3(0, -len * 0.9f, 0), 0.008f, 0.002f, false, false, 4);
                fingers.Add(f); fingers.Add(tip);
            }
            return shoulder;
        }

        // ------------------------------------------------------------------ public API (used by Game and the chapters)
        public bool Visible => visible;
        public void SetVisible(bool v)
        {
            visible = v;
            root.SetActive(v);
            if (!v) lunge = 0;
        }

        // height of the head above the model's feet (scaled), for putting its face right in front of the camera
        public float HeadOffset => head.position.y - root.transform.position.y + 0.12f * root.transform.localScale.y;

        // how far its head leans out in front of its feet, towards whoever it's facing (scaled)
        public float HeadForward => Vector3.Dot(head.position - root.transform.position, root.transform.forward);

        public void Place(float x, float z, float faceX, float faceZ, float smear)
        {
            root.transform.position = new Vector3(x, root.transform.position.y, z);
            root.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(faceX - x, faceZ - z) * Mathf.Rad2Deg, 0);
            foreach (var s in smears)
            {
                s.transform.localPosition = new Vector3(MathX.Jit(smear), 0, MathX.Jit(0.12f));
                s.SetActive(Random.value < 0.7f && lunge < 0.5f);
            }
            Animate();
        }

        // flicker: hide the renderers for a frame but keep it "there"
        public void Flicker(bool show)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = show;
            if (show) UpdateFace();
        }

        public void SetScale(float s) { root.transform.localScale = Vector3.one * s; }
        public void SetHeight(float y) { var p = root.transform.position; root.transform.position = new Vector3(p.x, y, p.z); }

        // ------------------------------------------------------------------ animation
        void Animate()
        {
            float t = Time.time, dt = Mathf.Min(Time.deltaTime, 0.05f), k = Mathf.Clamp01(Intensity), L = Mathf.Clamp01(lunge);

            // slow, deep breathing that moves the whole ribcage
            float br = Mathf.Sin(t * (1.3f + k * 1.5f));
            chest.localScale = new Vector3(1 + br * 0.02f, 1 + br * 0.015f, 1 + br * 0.05f);
            chest.localRotation = Quaternion.Euler(Mathf.Lerp(20 + br * 2, 32, L), 0, 0);

            // the head: still for a moment, then it snaps to a new, wrong angle
            twitchT -= dt;
            if (twitchT <= 0)
            {
                twitchT = Mathf.Lerp(1.8f, 0.25f, k) * (0.4f + Random.value);
                headTarget = new Vector3(MathX.Jit(30), MathX.Jit(40), MathX.Jit(90) + (Random.value < 0.5f ? 25 : -25));
                if (Random.value < 0.08f + k * 0.15f) shudderT = 0.35f + Random.value * 0.4f;
            }
            Vector3 want = Vector3.Lerp(headTarget, new Vector3(-8, 0, 0), L);
            if (shudderT > 0) { shudderT -= dt; want += new Vector3(MathX.Jit(30), MathX.Jit(30), MathX.Jit(30)); }
            headCur = Vector3.Lerp(headCur, want, Mathf.Min(1, dt * 35));   // near-instant: it doesn't turn, it jumps
            head.localRotation = Quaternion.Euler(headCur.x, headCur.y, headCur.z);

            // the jaw hangs open further the more afraid you are, and works silently
            float open = Mathf.Clamp01(0.25f + k * 0.6f + Mathf.PerlinNoise(t * 3, 1.7f) * 0.25f + L);
            jaw.localRotation = Quaternion.Euler(open * 38, 0, 0);
            jaw.localPosition = new Vector3(0, 0.04f - open * 0.05f, 0.06f);
            mouth.localScale = new Vector3(0.45f + open * 0.15f, 0.7f + open * 1.3f, 0.32f);
            mouth.localPosition = new Vector3(0, -0.02f - open * 0.06f, 0.12f);

            // arms hang and sway; lunging, they come up and open towards you
            float sway = Mathf.Sin(t * 0.9f) * 3;
            armL.localRotation = Quaternion.Euler(Mathf.Lerp(sway, -95, L), 0, -Mathf.Lerp(6, 38, L));
            armR.localRotation = Quaternion.Euler(Mathf.Lerp(-sway, -95, L), 0, Mathf.Lerp(6, 38, L));
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(Mathf.Lerp(-14, -35, L), 0, 0);

            // fingers never stop moving
            for (int i = 0; i < fingers.Count; i++)
            {
                float n = Mathf.PerlinNoise(t * (2 + k * 6), fingerSeed[i]);
                bool tip = (i % 2) == 1;
                float curl = Mathf.Max(-80, (tip ? -25 : -6) - n * (tip ? 40 : 30) - L * 15);
                fingers[i].localRotation = Quaternion.Euler(curl, 0, fingerZ[i]);
            }
            UpdateFace();
        }

        // the eyes and mouth belong to the monster; when a chapter turns it pale (a memory, a small boy), they go
        void UpdateFace()
        {
            var c = coreMat.HasProperty("_BaseColor") ? coreMat.GetColor("_BaseColor") : coreMat.color;
            bool monster = c.r + c.g + c.b < 0.6f;
            float k = Mathf.Clamp01(Intensity);
            float glow = 0.75f + k * 0.25f + Mathf.Sin(Time.time * 9) * 0.05f + lunge * 0.3f;
            eyeMat.SetColor("_BaseColor", new Color(glow, glow, glow * 0.92f));
            foreach (var r in faceParts) if (r.enabled) r.enabled = monster;
        }
    }
}
