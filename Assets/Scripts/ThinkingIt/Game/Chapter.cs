using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    public class Interactable
    {
        public string kind, tagName, id, label;
        public Vector3 pos;
        public GameObject mesh;
        public bool used;
        public float range = 2.6f;
        public float maxGaze = 2.6f;
        public object data;
    }

    public class Focusable
    {
        public string kind, key;
        public GameObject group;
        public List<Vector2Int> cells = new List<Vector2Int>();
        public Vector3 center;
        public float progress, time = 2f;
        public bool done, suspect;
        public char cellAfter = '.';
        public Material real;
        public readonly List<MeshRenderer> parts = new List<MeshRenderer>();
        public readonly List<Material> ghostMats = new List<Material>(), edgeMats = new List<Material>();
        public readonly List<GameObject> edges = new List<GameObject>();
        public float baseY;
    }

    public class EndInfo
    {
        public string title, sub, stats, next, note, nextLabel = "CONTINUE";
        public bool hasNext;
    }

    // Each chapter builds its level and adds its own rules on top of the shared ones in Game.
    public abstract class Chapter
    {
        public Game g;
        public World w;
        public readonly List<Interactable> interactables = new List<Interactable>();
        public readonly List<Focusable> focusables = new List<Focusable>();

        public abstract int Number { get; }
        public virtual AudioReverbPreset Space => AudioReverbPreset.Hallway;
        public virtual string HallZone => "hall";
        public virtual string HallHint => "Too dark. Too much. Close your eyes.";
        public virtual bool Lit => false;              // has the power come back? (looking into darkness is worse before)
        public virtual float FlashBase => 2.2f;
        public virtual bool Wood(float x, float z) => false;
        public virtual float FogDensity => 0.07f;
        public virtual Color FogColor => new Color(0.008f, 0.012f, 0.012f);
        public virtual Color Ambient => new Color(0.42f, 0.45f, 0.5f) * 0.06f;

        public abstract void Build();
        public abstract void Begin();
        public abstract string Zone(float x, float z);
        public virtual void OnZone(string zone) { }
        public virtual void Tick(float dt) { }
        public virtual bool CustomIt(float dt) => false;
        public virtual float DreadDelta(float dt) => 0;
        public virtual void OnInteract(Interactable it) { }
        public virtual void OnFocusDone(Focusable f) { g.Materialize(f); }
        public virtual void OnRespawn() { }
        public virtual void OnCaught() { }
        public abstract string Objective();
        public abstract EndInfo End();
        public virtual bool AllowEyes => true;
        public virtual bool CustomItActive => false;
        // a chapter with its own monster can replace the "it" jumpscare when you're caught (t = seconds since caught)
        public virtual bool CustomJumpscare => false;
        public virtual void Jumpscare(float t, float dt) { }

        // ---- shorthand used by the level builders (mirrors the web version's helpers) ----
        protected const float C = World.C, WALL_H = World.WALL_H, PI = Mathf.PI;
        protected static float cx(float x) => World.cx(x);
        protected static Material Std(int hex, float rough = 0.8f, float metal = 0f) => Mats.Std(MathX.Hex(hex), rough, metal);
        protected static Material StdT(int hex, float rough, Texture map, float metal = 0f, bool doubleSided = false) => Mats.Std(MathX.Hex(hex), rough, metal, map, null, null, 1, null, 1, doubleSided);
        protected GameObject Bx(float x, float y, float z, Material m, GameObject parent = null) => w.Box(x, y, z, m, parent ? parent.transform : null);
        protected GameObject Cy(float rt, float rb, float h, int seg, Material m, GameObject parent = null, bool open = false, float th0 = 0, float thL = Mathf.PI * 2) => w.Cyl(rt, rb, h, seg, m, parent ? parent.transform : null, open, th0, thL);
        protected GameObject Sp(float r, int ws, int hs, Material m, GameObject parent = null, float phi0 = 0, float phiL = Mathf.PI * 2, float th0 = 0, float thL = Mathf.PI) => w.Sph(r, ws, hs, m, parent ? parent.transform : null, phi0, phiL, th0, thL);
        protected GameObject To(float R, float r, int rs, int ts, Material m, GameObject parent = null) => w.Tor(R, r, rs, ts, m, parent ? parent.transform : null);
        protected GameObject Pl(float pw, float ph, Material m, GameObject parent = null, int sx = 1, int sy = 1) => w.Pl(pw, ph, m, parent ? parent.transform : null, sx, sy);
        protected GameObject Grp(string name = "group", GameObject parent = null) => w.Group(name, parent ? parent.transform : null);
        protected static Texture Paint(int pw, int ph, System.Action<Painter, int, int> draw) { var p = new Painter(pw, ph, Color.black); draw(p, pw, ph); return p.Make(); }
        protected static Color Col(int hex, float a = 1) => MathX.Hex(hex, a);
        protected static Color Rgba(float r, float gg, float b, float a) => new Color(r / 255f, gg / 255f, b / 255f, a);

        // ---- helpers for building ----
        protected Interactable AddInteract(string kind, string tagName, string id, Vector3 pos, GameObject mesh = null, string label = null)
        {
            var it = new Interactable { kind = kind, tagName = tagName, id = id, pos = pos, mesh = mesh, label = label };
            interactables.Add(it);
            return it;
        }

        // The shared hand-made models. All of them face +Z (a chair's seat looks toward +Z, the TV's glass too), origin at the floor.
        protected GameObject ChairModel(float x, float z, float face, float y = 0)
        {
            var g = BakedMesh.Spawn(w, "SchoolChair") ?? w.Group("chair");
            g.At(x, y, z).RotY(face);
            return g;
        }

        // A television model with an optional glowing screen laid over its glass (the model faces +Z).
        protected GameObject TvModel(GameObject parent, Material screen)
        {
            var m = BakedMesh.Spawn(w, "Television", parent ? parent.transform : null) ?? w.Group("tv", parent ? parent.transform : null);
            if (screen != null) Pl(0.40f, 0.32f, screen, m).At(0.06f, 0.26f, 0.247f);
            return m;
        }

        protected GameObject Prop(GameObject go, int sx = -1, int sy = -1)
        {
            World.Tagged(go, "prop");
            // collision now comes from the object's real shape (World.AutoObstacles); sx/sy are kept for reference only
            return go;
        }

        // something you can stare into existence: a pale outline until you look at it long enough
        protected Focusable AddFocusable(string kind, GameObject group, IEnumerable<Vector2Int> cells, Vector3 center, float time, Material real, char cellAfter = '.')
        {
            var f = new Focusable { kind = kind, key = kind, group = group, center = center, time = time, real = real, cellAfter = cellAfter, baseY = group.transform.localPosition.y };
            if (cells != null) f.cells.AddRange(cells);
            foreach (var mr in group.GetComponentsInChildren<MeshRenderer>())
            {
                var gm = Mats.UnlitTransparent(new Color(0.87f, 0.91f, 0.94f, 0.12f));
                mr.sharedMaterial = gm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.parts.Add(mr); f.ghostMats.Add(gm);
                var mf = mr.GetComponent<MeshFilter>();
                var b = mf.sharedMesh.bounds;
                var e = new GameObject("edges");
                e.transform.SetParent(mr.transform, false);
                e.transform.localPosition = b.center;
                e.AddComponent<MeshFilter>().sharedMesh = MeshGen.BoxEdges(b.size.x, b.size.y, b.size.z);
                var em = Mats.UnlitTransparent(new Color(1, 1, 1, 0.35f));
                var er = e.AddComponent<MeshRenderer>(); er.sharedMaterial = em; er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.edgeMats.Add(em); f.edges.Add(e);
            }
            World.Tagged(group, "focus", kind);
            focusables.Add(f);
            return f;
        }

        // a thing in the waiting room that might have changed since last time
        protected Focusable AddSuspect(string key, GameObject group, float time = 2f)
        {
            var f = new Focusable { kind = "suspect", key = key, group = group, time = time, suspect = true };
            World.Tagged(group, "focus", key);
            focusables.Add(f);
            return f;
        }

        public static Chapter Create(int n)
        {
            switch (n)
            {
                case 2: return new Chapter2();
                case 3: return new Chapter3();
                case 4: return new Chapter4();
                case 5: return new Chapter5();
                case 6: return new Chapter6();
                default: return new Chapter1();
            }
        }
    }
}
