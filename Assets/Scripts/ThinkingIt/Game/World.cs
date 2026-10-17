using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThinkingIt
{
    // The level: a grid of 2 m cells (like the web version), plus helpers to build geometry into it.
    //   # wall   . floor   k furniture (solid)   K shelving   T scratched wall   G door that isn't there
    //   D powered door   X exit door   P collapsed floor   A/B apartment doors   R the bathroom (black)
    //   W lake water   r reeds / trees   = dock planks
    public class World
    {
        public const float C = 2f, WALL_H = 3.2f, EYE = 1.65f;
        public static float AMB = 4.5f;   // ambient light multiplier (on top of each chapter's Ambient)
        public static float LK = 9f;   // light intensity scale: the web version used non-physical falloff

        public int W, H;
        public char[,] grid;   // [y, x]
        public Transform root;
        public float[] heat;

        public World(string name)
        {
            root = new GameObject(name).transform;
        }

        public void Init(int w, int h, char fill = '#')
        {
            W = w; H = h; grid = new char[h, w]; heat = new float[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = fill;
        }
        public void Carve(int x0, int y0, int x1, int y1, char c = '.')
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) grid[y, x] = c;
        }
        public char this[int x, int y] { get => x < 0 || y < 0 || x >= W || y >= H ? '#' : grid[y, x]; set { if (x >= 0 && y >= 0 && x < W && y < H) grid[y, x] = value; } }

        public static float cx(float x) => x * C + C / 2;
        public char Cell(float wx, float wz) { int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C); return this[x, y]; }
        public bool Solid(float wx, float wz) => "#TGDXPkKABRWr".IndexOf(Cell(wx, wz)) >= 0 || InObstacle(wx, wz);

        // ---- furniture collision: the real footprint of each piece, not the whole 2 m cell it sits in ----
        readonly Dictionary<int, List<Rect>> obstacles = new Dictionary<int, List<Rect>>();
        int Key(int x, int y) => y * 1000 + x;
        public bool InObstacle(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            if (!obstacles.TryGetValue(Key(x, y), out var list)) return false;
            var p = new Vector2(wx, wz);
            foreach (var r in list) if (r.Contains(p)) return true;
            return false;
        }
        public void AddObstacle(Rect r)
        {
            for (int y = Mathf.FloorToInt(r.yMin / C); y <= Mathf.FloorToInt(r.yMax / C); y++)
                for (int x = Mathf.FloorToInt(r.xMin / C); x <= Mathf.FloorToInt(r.xMax / C); x++)
                {
                    if (!obstacles.TryGetValue(Key(x, y), out var list)) obstacles[Key(x, y)] = list = new List<Rect>();
                    list.Add(r);
                }
        }
        // Every piece of furniture at body height blocks you, using the footprint of its actual parts
        // (a table top, a chair seat, a tree trunk), not the whole 2 m grid cell it stands in.
        static readonly string[] passThrough = { "wall", "floor", "doc", "tape", "exit", "water", "it", "recorder", "fuse", "switch", "sdoor" };
        // forget every furniture footprint in a block of cells (for places where collision comes from the grid only)
        public void ClearObstacles(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) obstacles.Remove(Key(x, y));
        }

        public void AutoObstacles()
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr.sharedMaterial || mr.sharedMaterial.renderQueue >= 2500) continue;
                var tag = mr.GetComponentInParent<Tag>(true);
                if (tag && System.Array.IndexOf(passThrough, tag.tagName) >= 0) continue;
                var b = mr.bounds;
                if (b.min.y > 1.2f || b.max.y < 0.2f || b.size.x > 6 || b.size.z > 6) continue;
                AddObstacle(Rect.MinMaxRect(b.min.x - 0.02f, b.min.z - 0.02f, b.max.x + 0.02f, b.max.z + 0.02f));
            }
        }
        public bool Opaque(float wx, float wz) => "#TDXkKABR".IndexOf(Cell(wx, wz)) >= 0;
        public bool Walkable(int x, int y) { char c = this[x, y]; return c == '.' || c == '='; }

        // ------------------------------------------------------------------ building helpers
        public GameObject Group(string name = "group", Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : root, false);
            return go;
        }

        public GameObject MeshObj(Mesh mesh, Material mat, Transform parent = null, string name = null, bool shadows = true)
        {
            var go = new GameObject(name ?? mesh.name);
            go.transform.SetParent(parent ? parent : root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            bool transparent = mat && mat.renderQueue >= 2500;
            r.shadowCastingMode = shadows && !transparent ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public GameObject Box(float w, float h, float d, Material m, Transform parent = null) => MeshObj(MeshGen.Box(w, h, d), m, parent, "box");
        public GameObject Cyl(float rt, float rb, float h, int seg, Material m, Transform parent = null, bool open = false, float th0 = 0, float thL = Mathf.PI * 2)
            => MeshObj(MeshGen.Cylinder(rt, rb, h, seg, open, th0, thL), m, parent, "cyl");
        public GameObject Sph(float r, int ws, int hs, Material m, Transform parent = null, float phi0 = 0, float phiL = Mathf.PI * 2, float th0 = 0, float thL = Mathf.PI)
            => MeshObj(MeshGen.Sphere(r, ws, hs, phi0, phiL, th0, thL), m, parent, "sphere");
        public GameObject Tor(float R, float r, int rs, int ts, Material m, Transform parent = null) => MeshObj(MeshGen.Torus(R, r, rs, ts), m, parent, "torus");
        public GameObject Pl(float w, float h, Material m, Transform parent = null, int sx = 1, int sy = 1) => MeshObj(MeshGen.Plane(w, h, sx, sy), m, parent, "plane");
        public GameObject Disc(float r, int seg, Material m, Transform parent = null) => MeshObj(MeshGen.Circle(r, seg), m, parent, "disc");

        // a horizontal plane lying on the floor (three.js: rotation.x = -PI/2)
        public GameObject FloorPlane(float w, float h, Material m, Vector3 pos, float rotZ = 0, Transform parent = null)
        {
            var go = Pl(w, h, m, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = MathX.Euler3(-Mathf.PI / 2, 0, rotZ);
            return go;
        }

        public Light PointLight(Vector3 pos, Color c, float intensity, float range, bool shadow = false, Transform parent = null)
        {
            var go = new GameObject("light");
            go.transform.SetParent(parent ? parent : root, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity * LK; l.range = range;
            l.shadows = shadow ? LightShadows.Soft : LightShadows.None;
            if (shadow) { l.shadowBias = 0.03f; l.shadowNormalBias = 0.3f; l.shadowNearPlane = 0.1f; }
            return l;
        }

        public Light SpotLight(Vector3 pos, Vector3 target, Color c, float intensity, float range, float angleRad, float penumbra, bool shadow, Transform parent = null)
        {
            var go = new GameObject("spot");
            go.transform.SetParent(parent ? parent : root, false);
            go.transform.localPosition = pos;
            go.transform.rotation = Quaternion.LookRotation(target - pos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.color = c; l.intensity = intensity * LK; l.range = range;
            l.spotAngle = angleRad * 2 * Mathf.Rad2Deg; l.innerSpotAngle = l.spotAngle * (1 - penumbra);
            l.shadows = shadow ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ gaze targets
        public static T Tagged<T>(T c, string tagName, string data = null) where T : Component { Tagged(c.gameObject, tagName, data); return c; }
        public static GameObject Tagged(GameObject go, string tagName, string data = null)
        {
            var t = go.GetComponent<Tag>(); if (!t) t = go.AddComponent<Tag>();
            t.tagName = tagName; t.data = data;
            AddColliders(go);
            return go;
        }

        public static void AddColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<Collider>() || !mf.sharedMesh || mf.sharedMesh.GetTopology(0) != MeshTopology.Triangles) continue;
                if (mf.sharedMesh.name == "box") { var b = mf.gameObject.AddComponent<BoxCollider>(); }
                else { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
            }
        }

        // all the cells of one kind merged into one mesh (walls are thousands of polygons otherwise)
        public GameObject CellBlocks(string chars, Material m, string tagName, float height = WALL_H, string name = "walls")
        {
            var b = new MeshBuilder(); int count = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                    if (chars.IndexOf(grid[y, x]) >= 0) { b.AddBox(new Vector3(cx(x), height / 2, cx(y)), new Vector3(C, height, C)); count++; }
            if (count == 0) return null;
            var go = MeshObj(b.Build(name), m, null, name);
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            var t = go.AddComponent<Tag>(); t.tagName = tagName;
            return go;
        }

        // many boxes (each with its own transform) merged into one mesh
        public GameObject Batch(List<Matrix4x4> mats, List<Vector3> sizes, Material m, string tagName, string name = "batch", bool collide = true)
        {
            if (mats.Count == 0) return null;
            var b = new MeshBuilder();
            for (int i = 0; i < mats.Count; i++) b.AddBox(mats[i], sizes[i]);
            var go = MeshObj(b.Build(name), m, null, name);
            if (collide) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; }
            if (tagName != null) { var t = go.AddComponent<Tag>(); t.tagName = tagName; }
            return go;
        }

        // Walls drawn face by face: each side of a solid cell that faces a walkable cell becomes a quad,
        // and its material depends on what it faces (so a house can be wallpapered inside and clad outside).
        public void WallFaces(string solidChars, string openChars, System.Func<int, int, Material> materialFor, float height = WALL_H)
        {
            var builders = new Dictionary<Material, MeshBuilder>();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    if (solidChars.IndexOf(grid[y, x]) < 0) continue;
                    foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H || openChars.IndexOf(grid[ny, nx]) < 0) continue;
                        var m = materialFor(nx, ny); if (!m) continue;
                        if (!builders.TryGetValue(m, out var b)) builders[m] = b = new MeshBuilder();
                        var nrm = new Vector3(d.x, 0, d.y);
                        b.Face(new Vector3(cx(x) + d.x * C / 2, height / 2, cx(y) + d.y * C / 2), nrm, Vector3.up, C, height);
                    }
                }
            foreach (var kv in builders)
            {
                var go = MeshObj(kv.Value.Build("wall faces"), kv.Key, null, "walls");
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                var t = go.AddComponent<Tag>(); t.tagName = "wall";
            }
        }

        // floor + ceiling slabs over the whole grid (with colliders so the gaze can land on them)
        public void FloorAndCeiling(Material floor, Material ceil, bool withCeiling = true)
        {
            var f = FloorPlane(W * C, H * C, floor, new Vector3(W * C / 2, 0, H * C / 2));
            f.name = "floor"; Tagged(f, "floor");
            f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (withCeiling)
            {
                var c = Pl(W * C, H * C, ceil);
                c.name = "ceiling";
                c.transform.localPosition = new Vector3(W * C / 2, WALL_H, H * C / 2);
                c.transform.localRotation = MathX.Euler3(Mathf.PI / 2, 0, 0);
                Tagged(c, "floor");
            }
        }

        // dark skirting along every wall that faces a walkable cell
        public void Skirting(Material m, string wallChars, float height = 0.12f, float thick = 0.03f, Material crown = null)
        {
            var mats = new List<Matrix4x4>(); var sizes = new List<Vector3>();
            var cm = new List<Matrix4x4>(); var cs = new List<Vector3>();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    if (grid[y, x] != '.' && grid[y, x] != 'k') continue;
                    foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                    {
                        char n = this[x + d.x, y + d.y];
                        if (wallChars.IndexOf(n) < 0) continue;
                        bool along = d.x != 0;
                        float ex = cx(x) + d.x * (C / 2 - thick / 2), ez = cx(y) + d.y * (C / 2 - thick / 2);
                        mats.Add(Matrix4x4.Translate(new Vector3(ex, height / 2, ez)));
                        sizes.Add(new Vector3(along ? thick : C, height, along ? C : thick));
                        if (crown)
                        {
                            cm.Add(Matrix4x4.Translate(new Vector3(ex - d.x * 0.02f, WALL_H - 0.06f, ez - d.y * 0.02f)));
                            cs.Add(new Vector3(along ? 0.08f : C, 0.12f, along ? C : 0.08f));
                        }
                    }
                }
            Batch(mats, sizes, m, null, "skirting", false);
            if (crown) Batch(cm, cs, crown, null, "crown", false);
        }
    }

    public static class GoExt
    {
        public static GameObject At(this GameObject g, float x, float y, float z) { g.transform.localPosition = new Vector3(x, y, z); return g; }
        public static GameObject Rot(this GameObject g, float x, float y, float z) { g.transform.localRotation = MathX.Euler3(x, y, z); return g; }
        public static GameObject RotY(this GameObject g, float y) { g.transform.localRotation = MathX.Euler3(0, y, 0); return g; }
        public static GameObject Scl(this GameObject g, float x, float y, float z) { g.transform.localScale = new Vector3(x, y, z); return g; }
        public static GameObject In(this GameObject g, GameObject parent) { g.transform.SetParent(parent.transform, false); return g; }
        public static GameObject NoShadow(this GameObject g) { foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off; return g; }
    }
}
