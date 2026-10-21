using UnityEngine;

namespace ThinkingIt
{
    // Mara's body in first person: a rigged character model (17 bones, weights baked in MaraRig.bytes).
    // The camera sits just in front of her chest; her head is shrunk to nothing so you never see inside it.
    // Look down and you see her jacket, her legs and boots walking. Interact and her right arm reaches for the thing.
    public class MaraBody
    {
        readonly Game g;
        World w;
        readonly GameObject bodyRoot;
        Transform rigRoot;
        Transform[] rig;

        const float SCALE = 1.72f;        // the model is 1 unit tall
        const float BACK = 0.12f;         // how far behind the camera her body stands
        const float REACH_TIME = 0.9f, GRAB_AT = 0.42f;

        // 17 bones, in the order the weights file was written
        static readonly string[] BN = { "pelvis", "spine", "chest", "neck", "head", "uArmL", "fArmL", "handL", "thighL", "shinL", "footL", "uArmR", "fArmR", "handR", "thighR", "shinR", "footR" };
        static readonly int[] BP = { -1, 0, 1, 2, 3, 2, 5, 6, 0, 8, 9, 2, 11, 12, 0, 14, 15 };
        static readonly Vector3[] BH = {
            new Vector3(0, .50f, -.02f), new Vector3(0, .58f, -.03f), new Vector3(0, .70f, -.03f), new Vector3(0, .83f, 0), new Vector3(0, .88f, 0),
            new Vector3(-.115f, .79f, -.02f), new Vector3(-.14f, .58f, -.03f), new Vector3(-.145f, .45f, 0), new Vector3(-.065f, .50f, -.02f), new Vector3(-.07f, .27f, -.01f), new Vector3(-.07f, .07f, -.02f),
            new Vector3(.115f, .79f, -.02f), new Vector3(.14f, .58f, -.03f), new Vector3(.145f, .45f, 0), new Vector3(.065f, .50f, -.02f), new Vector3(.07f, .27f, -.01f), new Vector3(.07f, .07f, -.02f) };
        const int PELVIS = 0, SPINE = 1, HEAD = 4, UL = 5, UR = 11, TL = 8, TR = 14;

        // reaching
        float reachT = -1; Vector3 reachTarget; bool grabbed; System.Action onGrab;
        float walkPhase, bodyYaw, lastX, lastZ, moveSpeed;

        public bool Busy => reachT >= 0;
        public void MoveTo(World nw) { w = nw; if (bodyRoot != null) bodyRoot.transform.SetParent(nw.root, false); }

        public MaraBody(Game game)
        {
            g = game; w = game.w;
            bodyRoot = new GameObject("Mara body"); bodyRoot.transform.SetParent(w.root, false);
            BuildRig();
            lastX = g.px; lastZ = g.pz; bodyYaw = (g.yaw + Mathf.PI) * Mathf.Rad2Deg;
        }

        void BuildRig()
        {
            var hm = BakedMesh.Load("MaraModel");
            var ta = Resources.Load<TextAsset>("ThinkingIt/Models/MaraRig");
            if (hm == null || ta == null) { Debug.LogWarning("[Thinking It] Mara model or rig missing: no body"); return; }
            try
            {
                var bw = new BoneWeight[hm.vertexCount];
                using (var br = new System.IO.BinaryReader(new System.IO.MemoryStream(ta.bytes)))
                {
                    int vc = br.ReadInt32(), bc = br.ReadInt32();
                    if (vc != hm.vertexCount || bc != BN.Length) { Debug.LogWarning("[Thinking It] Mara rig does not match her mesh"); return; }
                    var idx = br.ReadBytes(vc * 4);
                    for (int i = 0; i < vc; i++) { bw[i].boneIndex0 = idx[i * 4]; bw[i].boneIndex1 = idx[i * 4 + 1]; bw[i].boneIndex2 = idx[i * 4 + 2]; bw[i].boneIndex3 = idx[i * 4 + 3]; }
                    for (int i = 0; i < vc; i++) { bw[i].weight0 = br.ReadSingle(); bw[i].weight1 = br.ReadSingle(); bw[i].weight2 = br.ReadSingle(); bw[i].weight3 = br.ReadSingle(); }
                }
                rigRoot = new GameObject("Mara rig").transform; rigRoot.SetParent(bodyRoot.transform, false);
                rigRoot.localScale = Vector3.one * SCALE;
                rig = new Transform[BN.Length];
                for (int i = 0; i < BN.Length; i++)
                {
                    rig[i] = new GameObject(BN[i]).transform;
                    rig[i].SetParent(BP[i] < 0 ? rigRoot : rig[BP[i]], false);
                    rig[i].localPosition = BP[i] < 0 ? BH[i] : BH[i] - BH[BP[i]];
                }
                var go = new GameObject("Mara model"); go.transform.SetParent(rigRoot, false);
                var mesh = Object.Instantiate(hm); mesh.boneWeights = bw;
                var bind = new Matrix4x4[BN.Length];
                for (int i = 0; i < BN.Length; i++) bind[i] = rig[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
                mesh.bindposes = bind;
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh; smr.bones = rig; smr.rootBone = rig[0];
                smr.sharedMaterial = Mats.Std(Color.white, 0.8f, 0f, BakedMesh.Texture("MaraModel"));
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            catch (System.Exception e) { Debug.LogWarning("[Thinking It] Mara rig failed: " + e.Message); rig = null; }
        }

        // ---------------------------------------------------------------- reaching
        // reach for something in the world; onGrab is called when her fingers would close on it
        public void Reach(Vector3 worldTarget, string kind, System.Action grabCb)
        {
            reachTarget = worldTarget; reachT = 0; grabbed = false; onGrab = grabCb;
        }

        // ---------------------------------------------------------------- per frame
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        public void Tick(float dt)
        {
            bool show = g.state != GState.Caught && g.state != GState.End && g.state != GState.Intro;
            if (bodyRoot.activeSelf != show) bodyRoot.SetActive(show);
            if (reachT >= 0)
            {
                reachT += dt;
                if (!grabbed && reachT >= GRAB_AT) { grabbed = true; var cb = onGrab; onGrab = null; cb?.Invoke(); }
                if (reachT >= REACH_TIME) reachT = -1;
            }
            if (!show) return;

            float moved = MathX.Hypot(g.px - lastX, g.pz - lastZ); lastX = g.px; lastZ = g.pz;
            float spd = dt > 0 ? moved / dt : 0; moveSpeed = Mathf.Lerp(moveSpeed, Mathf.Min(spd, 6f), dt * 10f);
            walkPhase += moved * 3.2f;
            bool run = g.Sprinting;

            var f = g.Fwd;
            bodyRoot.transform.position = new Vector3(g.px - f.x * BACK, 0, g.pz - f.y * BACK);
            bodyYaw = Mathf.LerpAngle(bodyYaw, (g.yaw + Mathf.PI) * Mathf.Rad2Deg, dt * 9f);
            bodyRoot.transform.rotation = Quaternion.Euler(0, bodyYaw, 0);
            if (rig == null) return;

            float k = Mathf.Clamp01(moveSpeed / 1.6f), a = (run ? 42f : 26f) * k, s = Mathf.Sin(walkPhase), t = Time.time;
            float kf = (run ? 62f : 34f) * k;
            Quaternion X(float deg) => Quaternion.Euler(deg, 0, 0);
            rig[PELVIS].localPosition = BH[PELVIS] + new Vector3(0, -Mathf.Abs(Mathf.Cos(walkPhase)) * 0.012f * k, 0);
            rig[PELVIS].localRotation = Quaternion.Euler(0, s * 4f * k, 0);
            rig[SPINE].localRotation = Quaternion.Euler((run ? 10f : 2f) * k, -s * 5f * k, 0);
            rig[HEAD].localScale = Vector3.zero;                     // no head: the camera is inside it
            rig[TL].localRotation = X(-s * a); rig[TR].localRotation = X(s * a);
            rig[TL + 1].localRotation = X(Mathf.Max(0, Mathf.Cos(walkPhase)) * kf); rig[TR + 1].localRotation = X(Mathf.Max(0, -Mathf.Cos(walkPhase)) * kf);
            rig[TL + 2].localRotation = X(s * a * 0.4f); rig[TR + 2].localRotation = X(-s * a * 0.4f);
            float breathe = Mathf.Sin(t * 1.6f) * 1.5f;
            rig[UL].localRotation = X(s * a * 0.7f + breathe); rig[UR].localRotation = X(-s * a * 0.7f - breathe);
            rig[UL + 1].localRotation = X(-(8f + (run ? 40f : 0f) * k)); rig[UR + 1].localRotation = X(-(8f + (run ? 40f : 0f) * k));

            if (reachT >= 0) ReachArm();
        }

        // her right arm goes out to the thing, holds while the fingers close, and comes back
        void ReachArm()
        {
            float p = reachT / REACH_TIME;
            float wgt = p < 0.35f ? Ease(p / 0.35f) : (p < 0.55f ? 1f : Ease(1f - (p - 0.55f) / 0.45f));
            var u = rig[UR]; var fo = rig[UR + 1]; var h = rig[UR + 2];
            Vector3 S = u.position, E = fo.position, Wp = h.position;
            float l1 = (E - S).magnitude, l2 = (Wp - E).magnitude;
            Vector3 dir = reachTarget - S; float dist = Mathf.Max(0.05f, dir.magnitude);
            float d = Mathf.Min(dist, (l1 + l2) * 0.95f);
            Vector3 a = dir / dist, T = S + a * d;
            // the elbow bends out and down
            Vector3 pole = bodyRoot.transform.right * 0.7f + Vector3.down * 0.6f;
            Vector3 pd = pole - Vector3.Dot(pole, a) * a; pd = pd.sqrMagnitude < 1e-5f ? Vector3.down : pd.normalized;
            float x = (d * d + l1 * l1 - l2 * l2) / (2f * d), y = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));
            Vector3 Ep = S + a * x + pd * y;
            u.rotation = Quaternion.Slerp(u.rotation, Quaternion.FromToRotation(E - S, Ep - S) * u.rotation, wgt);
            fo.rotation = Quaternion.Slerp(fo.rotation, Quaternion.FromToRotation(h.position - fo.position, T - fo.position) * fo.rotation, wgt);
        }
    }
}
