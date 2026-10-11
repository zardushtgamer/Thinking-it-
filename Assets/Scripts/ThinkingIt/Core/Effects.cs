using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Dust hanging in the air, drifting slowly through the light.
    public class DustCloud
    {
        readonly ParticleSystem ps;
        readonly ParticleSystem.Particle[] parts;
        readonly Vector3[] basePos;
        public float driftX = 0.25f, driftY = 0.18f, driftZ = 0;

        static Texture dustTex;
        public static Texture DustTex
        {
            get
            {
                if (dustTex) return dustTex;
                var p = new Painter(32, 32);
                p.RadialGradient(16, 16, 0, 16, (0, new Color(1, 0.94f, 0.82f, 1)), (1, new Color(1, 0.94f, 0.82f, 0)));
                return dustTex = p.ToTexture2D(false, true);
            }
        }

        public DustCloud(Transform parent, List<Vector3> positions, Color color, float size, float opacity)
        {
            var go = new GameObject("dust");
            go.transform.SetParent(parent, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = positions.Count;
            main.startLifetime = 1e6f; main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = size;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var m = new Material(Mats.ParticleTemplate);
            m.SetTexture("_BaseMap", DustTex); m.mainTexture = DustTex;
            var c = color; c.a = opacity;
            m.SetColor("_BaseColor", c);
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            basePos = positions.ToArray();
            parts = new ParticleSystem.Particle[basePos.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].position = basePos[i];
                parts[i].startSize = size;
                parts[i].startColor = Color.white;
                parts[i].remainingLifetime = 1e6f; parts[i].startLifetime = 1e6f;
            }
            ps.SetParticles(parts, parts.Length);
        }

        public void Update(float time)
        {
            if (!ps) return;
            for (int i = 0; i < parts.Length; i++)
            {
                var b = basePos[i];
                parts[i].position = new Vector3(b.x + Mathf.Sin(time * 0.12f + i) * driftX, b.y + Mathf.Sin(time * 0.2f + i * 1.7f) * driftY, b.z + Mathf.Cos(time * 0.11f + i * 0.7f) * driftZ);
                parts[i].remainingLifetime = 1e6f;
            }
            ps.SetParticles(parts, parts.Length);
        }

        // random positions over the walkable cells of a world
        public static List<Vector3> OverCells(World w, int n, System.Func<int, int, bool> ok = null)
        {
            var l = new List<Vector3>();
            int guard = 0;
            while (l.Count < n && guard++ < n * 50)
            {
                int x = 1 + Random.Range(0, w.W - 2), y = 1 + Random.Range(0, w.H - 2);
                if (ok != null ? !ok(x, y) : w[x, y] == '#') continue;
                l.Add(new Vector3(World.cx(x) + MathX.Jit(World.C), 0.3f + Random.value * 2.7f, World.cx(y) + MathX.Jit(World.C)));
            }
            return l;
        }
    }
}
