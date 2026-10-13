using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ThinkingIt
{
    // A debugging tool for the hand-placed level geometry: press F10 while playing a chapter.
    // Every prop part (not walls, floors or ceilings) is treated as an oriented box, and every pair whose boxes
    // push into each other by more than a few centimetres is listed (with its group, position and size),
    // tinted red in the scene, and written to OverlapReport_ChapterN.txt in the project folder.
    // Press F10 again to clear the red tint.
    public static class OverlapScanner
    {
        const float MIN_DEPTH = 0.05f;   // ignore parts that only just touch (legs into a table top, etc.)
        static readonly List<Renderer> tinted = new List<Renderer>();
        static readonly string[] skipTags = { "wall", "floor", "water", "it" };
        static readonly string[] skipNames = { "walls", "wall faces", "floor", "ceiling", "rim", "edges", "light" };

        struct Part { public Renderer r; public Vector3 c; public Vector3 ax, ay, az; public Vector3 e; public Bounds wb; }

        public static bool Active => tinted.Count > 0;

        public static void Clear()
        {
            foreach (var r in tinted) if (r) r.SetPropertyBlock(null);
            tinted.Clear();
        }

        public static string Scan(Transform root, int chapter)
        {
            Clear();
            var parts = new List<Part>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>();
                if (!r || !r.enabled || !mf.sharedMesh || !r.sharedMaterial) continue;
                if (r.sharedMaterial.renderQueue >= 2500) continue;                 // glass, ghosts, glows
                var tag = mf.GetComponentInParent<Tag>();
                if (tag && System.Array.IndexOf(skipTags, tag.tagName) >= 0) continue;
                if (System.Array.IndexOf(skipNames, mf.gameObject.name) >= 0) continue;
                var wb = r.bounds;
                if (wb.size.x > 6 || wb.size.z > 6 || wb.size.y > 4) continue;      // big merged meshes (shelving, batches)
                var t = mf.transform; var b = mf.sharedMesh.bounds; var s = t.lossyScale;
                parts.Add(new Part
                {
                    r = r, wb = wb, c = t.TransformPoint(b.center),
                    ax = t.right, ay = t.up, az = t.forward,
                    e = new Vector3(Mathf.Abs(b.extents.x * s.x), Mathf.Abs(b.extents.y * s.y), Mathf.Abs(b.extents.z * s.z))
                });
            }

            var hits = new List<(float d, Part a, Part b)>();
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    var a = parts[i]; var b = parts[j];
                    if (!a.wb.Intersects(b.wb)) continue;
                    float d = Depth(a, b);
                    if (d > MIN_DEPTH) hits.Add((d, a, b));
                }
            hits.Sort((x, y) => y.d.CompareTo(x.d));

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.1f, 0.1f)); block.SetColor("_Color", new Color(1f, 0.1f, 0.1f));
            var sb = new StringBuilder();
            sb.AppendLine($"Thinking It — overlap report, Chapter {chapter} — {hits.Count} overlapping pairs deeper than {MIN_DEPTH * 100:F0} cm");
            sb.AppendLine("depth | part A (group path, world pos, size) | part B");
            foreach (var h in hits)
            {
                sb.AppendLine($"{h.d:F2} m | {Describe(h.a)} | {Describe(h.b)}");
                foreach (var r in new[] { h.a.r, h.b.r }) if (!tinted.Contains(r)) { r.SetPropertyBlock(block); tinted.Add(r); }
            }
            var text = sb.ToString();
            Debug.Log(text);
#if UNITY_EDITOR
            try { File.WriteAllText(Path.Combine(Application.dataPath, "..", $"OverlapReport_Chapter{chapter}.txt"), text); } catch { }
#endif
            return text;
        }

        static string Describe(Part p)
        {
            var t = p.r.transform; var path = t.name;
            for (var q = t.parent; q && q.parent; q = q.parent) path = q.name + "/" + path;
            return $"{path} @({p.c.x:F2},{p.c.y:F2},{p.c.z:F2}) size({p.e.x * 2:F2},{p.e.y * 2:F2},{p.e.z * 2:F2})";
        }

        // how far two oriented boxes push into each other (separating axis test; 0 if they don't touch)
        static float Depth(Part a, Part b)
        {
            var A = new[] { a.ax, a.ay, a.az }; var B = new[] { b.ax, b.ay, b.az };
            var axes = new List<Vector3>(15);
            axes.AddRange(A); axes.AddRange(B);
            foreach (var u in A) foreach (var v in B) { var c = Vector3.Cross(u, v); if (c.sqrMagnitude > 1e-6f) axes.Add(c.normalized); }
            var d = b.c - a.c; float min = float.MaxValue;
            foreach (var n in axes)
            {
                float ra = a.e.x * Mathf.Abs(Vector3.Dot(a.ax, n)) + a.e.y * Mathf.Abs(Vector3.Dot(a.ay, n)) + a.e.z * Mathf.Abs(Vector3.Dot(a.az, n));
                float rb = b.e.x * Mathf.Abs(Vector3.Dot(b.ax, n)) + b.e.y * Mathf.Abs(Vector3.Dot(b.ay, n)) + b.e.z * Mathf.Abs(Vector3.Dot(b.az, n));
                float o = ra + rb - Mathf.Abs(Vector3.Dot(d, n));
                if (o <= 0) return 0;
                if (o < min) min = o;
            }
            return min;
        }
    }
}
