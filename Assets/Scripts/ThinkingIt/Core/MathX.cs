using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Small helpers shared by everything. Angles are radians unless stated otherwise.
    public static class MathX
    {
        public const float PI = Mathf.PI;

        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Smooth(float k) { k = Clamp01(k); return k * k * (3 - 2 * k); }
        public static float Rand() => Random.value;
        public static float Rand(float a, float b) => a + Random.value * (b - a);
        public static float Jit(float amount) => (Random.value - 0.5f) * amount;
        public static T Pick<T>(IList<T> list) => list[Mathf.Min(list.Count - 1, (int)(Random.value * list.Count))];
        public static float Hypot(float x, float z) => Mathf.Sqrt(x * x + z * z);

        // three.js style Euler (order XYZ, radians) -> Unity quaternion.
        // The rotation matrices of three.js and Unity are identical per axis, only the composition order differs.
        public static Quaternion Euler3(float x, float y, float z)
        {
            return Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right)
                 * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up)
                 * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
        }

        public static Color Hex(int hex, float a = 1f)
        {
            return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a);
        }

        public static Color HSL(float h, float s, float l, float a = 1f)
        {
            h = ((h % 360f) + 360f) % 360f / 360f;
            float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            float Hue(float t)
            {
                if (t < 0) t += 1; if (t > 1) t -= 1;
                if (t < 1f / 6) return p + (q - p) * 6 * t;
                if (t < 0.5f) return q;
                if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
                return p;
            }
            return new Color(Hue(h + 1f / 3), Hue(h), Hue(h - 1f / 3), a);
        }
    }

    // Delayed callbacks (the web version's setTimeout). Owned by whoever ticks it.
    public class Timers
    {
        struct Entry { public float at; public System.Action a; }
        readonly List<Entry> list = new List<Entry>();
        float now;
        public void After(float seconds, System.Action a) { list.Add(new Entry { at = now + seconds, a = a }); }
        public void Clear() { list.Clear(); }
        public void Tick(float dt)
        {
            now += dt;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].at <= now)
                {
                    var a = list[i].a; list.RemoveAt(i); i--;
                    try { a?.Invoke(); } catch (System.Exception e) { Debug.LogException(e); }
                }
            }
        }
    }
}
