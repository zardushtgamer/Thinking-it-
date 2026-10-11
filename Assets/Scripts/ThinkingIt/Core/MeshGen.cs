using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Procedural meshes matching the three.js geometries the web version used.
    // Every triangle is oriented automatically so that its face points along the intended normal,
    // so we never have to reason about winding order in a left-handed engine.
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public int Add(Vector3 p, Vector3 normal, Vector2 tex) { v.Add(p); n.Add(normal); uv.Add(tex); return v.Count - 1; }

        public void Tri(int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            Vector3 want = n[a] + n[b] + n[c];
            if (Vector3.Dot(face, want) < 0) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }
        public void QuadIdx(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        // A flat rectangle facing 'normal'. Texture reads left-to-right for a viewer looking at its front.
        public void Face(Vector3 center, Vector3 normal, Vector3 up, float w, float h, float u0 = 0, float u1 = 1, float v0 = 0, float v1 = 1)
        {
            Vector3 right = Vector3.Cross(up, -normal).normalized;   // screen-right for a viewer looking along -normal (Unity is left-handed)
            Vector3 hu = right * (w / 2), hv = up.normalized * (h / 2);
            int a = Add(center - hu - hv, normal, new Vector2(u0, v0));
            int b = Add(center + hu - hv, normal, new Vector2(u1, v0));
            int c = Add(center + hu + hv, normal, new Vector2(u1, v1));
            int d = Add(center - hu + hv, normal, new Vector2(u0, v1));
            QuadIdx(a, b, c, d);
        }

        public void AddBox(Vector3 c, Vector3 size)
        {
            float x = size.x / 2, y = size.y / 2, z = size.z / 2;
            Face(c + new Vector3(0, 0, z), Vector3.forward, Vector3.up, size.x, size.y);
            Face(c + new Vector3(0, 0, -z), Vector3.back, Vector3.up, size.x, size.y);
            Face(c + new Vector3(x, 0, 0), Vector3.right, Vector3.up, size.z, size.y);
            Face(c + new Vector3(-x, 0, 0), Vector3.left, Vector3.up, size.z, size.y);
            Face(c + new Vector3(0, y, 0), Vector3.up, Vector3.forward, size.x, size.z);
            Face(c + new Vector3(0, -y, 0), Vector3.down, Vector3.back, size.x, size.z);
        }

        // Same, but transformed (for batching many boxes into one mesh).
        public void AddBox(Matrix4x4 m, Vector3 size)
        {
            int start = v.Count;
            AddBox(Vector3.zero, size);
            for (int i = start; i < v.Count; i++) { v[i] = m.MultiplyPoint3x4(v[i]); n[i] = m.MultiplyVector(n[i]).normalized; }
        }

        public void Append(Mesh mesh, Matrix4x4 m)
        {
            int start = v.Count;
            var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv; var mt = mesh.triangles;
            for (int i = 0; i < mv.Length; i++) { v.Add(m.MultiplyPoint3x4(mv[i])); n.Add(m.MultiplyVector(mn[i]).normalized); uv.Add(mu.Length > i ? mu[i] : Vector2.zero); }
            bool flip = m.determinant < 0;
            for (int i = 0; i < mt.Length; i += 3)
            {
                if (flip) { t.Add(start + mt[i]); t.Add(start + mt[i + 2]); t.Add(start + mt[i + 1]); }
                else { t.Add(start + mt[i]); t.Add(start + mt[i + 1]); t.Add(start + mt[i + 2]); }
            }
        }

        public Mesh Build(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            try { m.RecalculateTangents(); } catch { }
            return m;
        }
    }

    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = make(); cache[key] = m; return m;
        }
        public static void ClearCache() { cache.Clear(); }

        public static Mesh Box(float w, float h, float d)
        {
            return Cached($"box{w:F3}_{h:F3}_{d:F3}", () => { var b = new MeshBuilder(); b.AddBox(Vector3.zero, new Vector3(w, h, d)); return b.Build("box"); });
        }

        // three.js PlaneGeometry: in the XY plane, facing +Z. Optional segments (for curtains).
        public static Mesh Plane(float w, float h, int sx = 1, int sy = 1)
        {
            return Cached($"plane{w:F3}_{h:F3}_{sx}_{sy}", () =>
            {
                var b = new MeshBuilder();
                var idx = new int[sx + 1, sy + 1];
                for (int j = 0; j <= sy; j++)
                    for (int i = 0; i <= sx; i++)
                    {
                        float u = (float)i / sx, vv = (float)j / sy;
                        // screen-right for a viewer in front (+Z looking -Z) is -X, so u grows toward -X
                        idx[i, j] = b.Add(new Vector3(w / 2 - u * w, -h / 2 + vv * h, 0), Vector3.forward, new Vector2(u, vv));
                    }
                for (int j = 0; j < sy; j++)
                    for (int i = 0; i < sx; i++) b.QuadIdx(idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1]);
                return b.Build("plane");
            });
        }

        // three.js CylinderGeometry (radiusTop, radiusBottom, height, radialSegments, openEnded, thetaStart, thetaLength)
        public static Mesh Cylinder(float rTop, float rBot, float h, int seg = 16, bool open = false, float thetaStart = 0, float thetaLen = Mathf.PI * 2)
        {
            return Cached($"cyl{rTop:F4}_{rBot:F4}_{h:F3}_{seg}_{open}_{thetaStart:F3}_{thetaLen:F3}", () =>
            {
                var b = new MeshBuilder();
                float slope = (rBot - rTop) / h;
                var top = new int[seg + 1]; var bot = new int[seg + 1];
                for (int i = 0; i <= seg; i++)
                {
                    float u = (float)i / seg, th = thetaStart + u * thetaLen;
                    float s = Mathf.Sin(th), c = Mathf.Cos(th);
                    Vector3 nrm = new Vector3(s, slope, c).normalized;
                    top[i] = b.Add(new Vector3(rTop * s, h / 2, rTop * c), nrm, new Vector2(u, 1));
                    bot[i] = b.Add(new Vector3(rBot * s, -h / 2, rBot * c), nrm, new Vector2(u, 0));
                }
                for (int i = 0; i < seg; i++)
                {
                    if (rTop > 0 || true) b.Tri(top[i], bot[i], bot[i + 1]);
                    b.Tri(top[i], bot[i + 1], top[i + 1]);
                }
                if (!open)
                {
                    for (int capSide = 0; capSide < 2; capSide++)
                    {
                        float r = capSide == 0 ? rTop : rBot; if (r <= 0) continue;
                        float y = capSide == 0 ? h / 2 : -h / 2; Vector3 nrm = capSide == 0 ? Vector3.up : Vector3.down;
                        int cIdx = b.Add(new Vector3(0, y, 0), nrm, new Vector2(0.5f, 0.5f));
                        int prev = -1;
                        for (int i = 0; i <= seg; i++)
                        {
                            float th = thetaStart + (float)i / seg * thetaLen; float s = Mathf.Sin(th), c = Mathf.Cos(th);
                            int k = b.Add(new Vector3(r * s, y, r * c), nrm, new Vector2(0.5f + s * 0.5f, 0.5f + c * 0.5f));
                            if (prev >= 0) b.Tri(cIdx, prev, k);
                            prev = k;
                        }
                    }
                }
                return b.Build("cylinder");
            });
        }

        public static Mesh Cone(float r, float h, int seg = 16, bool open = false) => Cylinder(0, r, h, seg, open);

        // three.js SphereGeometry(radius, widthSegments, heightSegments, phiStart, phiLength, thetaStart, thetaLength)
        public static Mesh Sphere(float r, int ws = 16, int hs = 12, float phiStart = 0, float phiLen = Mathf.PI * 2, float thStart = 0, float thLen = Mathf.PI)
        {
            return Cached($"sph{r:F4}_{ws}_{hs}_{phiStart:F3}_{phiLen:F3}_{thStart:F3}_{thLen:F3}", () =>
            {
                var b = new MeshBuilder();
                var g = new int[ws + 1, hs + 1];
                for (int y = 0; y <= hs; y++)
                {
                    float vv = (float)y / hs, th = thStart + vv * thLen;
                    for (int x = 0; x <= ws; x++)
                    {
                        float u = (float)x / ws, ph = phiStart + u * phiLen;
                        var p = new Vector3(-r * Mathf.Cos(ph) * Mathf.Sin(th), r * Mathf.Cos(th), r * Mathf.Sin(ph) * Mathf.Sin(th));
                        g[x, y] = b.Add(p, p.sqrMagnitude > 0 ? p.normalized : Vector3.up, new Vector2(u, 1 - vv));
                    }
                }
                for (int y = 0; y < hs; y++)
                    for (int x = 0; x < ws; x++)
                    {
                        int a = g[x, y], bb = g[x + 1, y], c = g[x + 1, y + 1], d = g[x, y + 1];
                        if (y != 0 || thStart > 0) b.Tri(a, bb, d);
                        if (y != hs - 1 || thStart + thLen < Mathf.PI - 0.001f) b.Tri(bb, c, d);
                    }
                return b.Build("sphere");
            });
        }

        // three.js TorusGeometry(radius, tube, radialSegments, tubularSegments): ring in the XY plane
        public static Mesh Torus(float R, float r, int radialSeg = 8, int tubularSeg = 24)
        {
            return Cached($"tor{R:F4}_{r:F4}_{radialSeg}_{tubularSeg}", () =>
            {
                var b = new MeshBuilder();
                var g = new int[tubularSeg + 1, radialSeg + 1];
                for (int j = 0; j <= radialSeg; j++)
                    for (int i = 0; i <= tubularSeg; i++)
                    {
                        float u = (float)i / tubularSeg * Mathf.PI * 2, vv = (float)j / radialSeg * Mathf.PI * 2;
                        var center = new Vector3(R * Mathf.Cos(u), R * Mathf.Sin(u), 0);
                        var p = new Vector3((R + r * Mathf.Cos(vv)) * Mathf.Cos(u), (R + r * Mathf.Cos(vv)) * Mathf.Sin(u), r * Mathf.Sin(vv));
                        g[i, j] = b.Add(p, (p - center).normalized, new Vector2((float)i / tubularSeg, (float)j / radialSeg));
                    }
                for (int j = 0; j < radialSeg; j++)
                    for (int i = 0; i < tubularSeg; i++) b.QuadIdx(g[i, j], g[i + 1, j], g[i + 1, j + 1], g[i, j + 1]);
                return b.Build("torus");
            });
        }

        // three.js CircleGeometry: a disc in the XY plane facing +Z. UVs read correctly from the front.
        public static Mesh Circle(float r, int seg = 32)
        {
            return Cached($"circ{r:F4}_{seg}", () =>
            {
                var b = new MeshBuilder();
                int c = b.Add(Vector3.zero, Vector3.forward, new Vector2(0.5f, 0.5f));
                int prev = -1;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2, x = Mathf.Cos(a) * r, y = Mathf.Sin(a) * r;
                    int k = b.Add(new Vector3(x, y, 0), Vector3.forward, new Vector2(0.5f - x / (2 * r), 0.5f + y / (2 * r)));
                    if (prev >= 0) b.Tri(c, prev, k);
                    prev = k;
                }
                return b.Build("circle");
            });
        }

        // The 12 edges of a box, as a line mesh (the "unfinished" outline of things you can stare into existence)
        public static Mesh BoxEdges(float w, float h, float d)
        {
            return Cached($"edges{w:F3}_{h:F3}_{d:F3}", () =>
            {
                float x = w / 2, y = h / 2, z = d / 2;
                var p = new[] { new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,y,-z), new Vector3(-x,y,-z),
                                new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(x,y,z), new Vector3(-x,y,z) };
                var idx = new[] { 0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7 };
                var m = new Mesh { name = "edges" };
                m.vertices = p; m.SetIndices(idx, MeshTopology.Lines, 0); m.RecalculateBounds();
                return m;
            });
        }
    }
}
