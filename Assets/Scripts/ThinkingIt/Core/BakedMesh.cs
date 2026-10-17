using System.IO;
using UnityEngine;

namespace ThinkingIt
{
    // Loads a low-poly model that was converted outside Unity (from Blender) into a tiny binary file:
    // Assets/Resources/ThinkingIt/Models/<name>.bytes (+ <name>_diff.jpg for its texture).
    // The data is already in Unity's axes (Y up, front facing +Z, origin at the bottom centre), so it needs no import settings.
    public static class BakedMesh
    {
        const string Dir = "ThinkingIt/Models/";

        public static Mesh Load(string name)
        {
            var ta = Resources.Load<TextAsset>(Dir + name);
            if (!ta) return null;
            try
            {
                using (var br = new BinaryReader(new MemoryStream(ta.bytes)))
                {
                    int vc = br.ReadInt32(), ic = br.ReadInt32();
                    var p = new Vector3[vc]; var n = new Vector3[vc]; var uv = new Vector2[vc]; var idx = new int[ic];
                    for (int i = 0; i < vc; i++) p[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) n[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < ic; i++) idx[i] = br.ReadInt32();
                    var m = new Mesh { name = name };
                    if (vc > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    m.vertices = p; m.normals = n; m.uv = uv; m.triangles = idx;
                    m.RecalculateBounds(); m.RecalculateTangents();
                    return m;
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[Thinking It] Could not read model {name}: {e.Message}"); return null; }
        }

        // Builds a ready-to-place prop (mesh + its texture). Returns null when the model file is missing, so callers can fall back to a simple shape.
        public static GameObject Spawn(World w, string name, Transform parent = null, float scale = 1f, float roughness = 0.7f)
        {
            var mesh = Load(name);
            if (mesh == null) return null;
            var mat = Mats.Std(Color.white, roughness, 0.1f, Texture(name));
            var go = w.MeshObj(mesh, mat, parent, name);
            go.transform.localScale = new Vector3(scale, scale, scale);
            return go;
        }

        public static Texture2D Texture(string name) => Resources.Load<Texture2D>(Dir + name + "_diff");
    }
}
