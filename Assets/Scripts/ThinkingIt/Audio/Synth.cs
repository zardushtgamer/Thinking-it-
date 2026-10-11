using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Offline synthesis of every sound in the game (the web version generated them with Web Audio).
    // Produces AudioClips; Sfx plays them.
    public static class Synth
    {
        public const int SR = 22050;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static float[] noise;
        static System.Random rng = new System.Random(7);

        static float[] Noise
        {
            get
            {
                if (noise != null) return noise;
                noise = new float[SR * 2];
                for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rng.NextDouble() * 2 - 1);
                return noise;
            }
        }
        static float R() => (float)rng.NextDouble();

        public enum F { Lowpass, Highpass, Bandpass }

        public class Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public Biquad(F type, float freq, float q) { Set(type, freq, q); }
            public void Set(F type, float freq, float q)
            {
                freq = Mathf.Clamp(freq, 10, SR * 0.49f);
                float w0 = 2 * Mathf.PI * freq / SR, cs = Mathf.Cos(w0), sn = Mathf.Sin(w0);
                if (type != F.Bandpass) q = Mathf.Pow(10, q / 20f);   // Web Audio treats lowpass/highpass Q in dB
                float alpha = sn / (2 * Mathf.Max(0.0001f, q));
                float a0;
                if (type == F.Lowpass) { b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; }
                else if (type == F.Highpass) { b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; }
                else { b0 = alpha; b1 = 0; b2 = -alpha; }
                a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        // Web Audio style envelope: exponential ramp 0.0001 -> gain over 'attack', then exponential decay to 0.0001 over 'dur'
        static float Env(float t, float gain, float dur, float attack)
        {
            gain = Mathf.Max(gain, 0.0002f);
            if (t < attack) return 0.0001f * Mathf.Pow(gain / 0.0001f, t / attack);
            float k = (t - attack) / dur;
            if (k >= 1) return 0;
            return gain * Mathf.Pow(0.0001f / gain, k);
        }

        static float Osc(string type, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (type)
            {
                case "square": return p < 0.5f ? 1 : -1;
                case "sawtooth": return 2 * p - 1;
                case "triangle": return p < 0.5f ? 4 * p - 1 : 3 - 4 * p;
                default: return Mathf.Sin(2 * Mathf.PI * p);
            }
        }

        static AudioClip Make(string name, float[] data, bool loop = false)
        {
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Cached(string key, System.Func<float[]> gen)
        {
            if (cache.TryGetValue(key, out var c) && c) return c;
            c = Make(key, gen()); cache[key] = c; return c;
        }

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * SR))];

        public static AudioClip NoiseBurst(float dur, float freq, float gain, F type = F.Bandpass, float q = 1, float attack = 0.005f, int variant = 0)
        {
            return Cached($"n{dur:F3}_{freq:F0}_{gain:F3}_{type}_{q:F2}_{attack:F3}_{variant}", () =>
            {
                var b = Buf(attack + dur + 0.05f); var f = new Biquad(type, freq, q);
                int off = (int)(R() * SR);
                for (int i = 0; i < b.Length; i++) b[i] = f.Run(Noise[(i + off) % Noise.Length]) * Env(i / (float)SR, gain, dur, attack);
                return b;
            });
        }

        public static AudioClip Tone(float f0, float f1, float dur, float gain, string type = "sine", float attack = 0.005f)
        {
            return Cached($"t{f0:F1}_{f1:F1}_{dur:F3}_{gain:F3}_{type}_{attack:F3}", () =>
            {
                var b = Buf(attack + dur + 0.05f); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, fr = f0 * Mathf.Pow(Mathf.Max(f1, 1) / f0, Mathf.Min(1, t / dur));
                    ph += fr / SR;
                    b[i] = Osc(type, (float)ph) * Env(t, gain, dur, attack);
                }
                return b;
            });
        }

        // several tones mixed into one clip, each with its own start delay
        public static AudioClip Chord(string key, params (float f0, float f1, float dur, float gain, string type, float attack, float delay)[] parts)
        {
            return Cached("ch" + key, () =>
            {
                float len = 0; foreach (var p in parts) len = Mathf.Max(len, p.delay + p.attack + p.dur + 0.05f);
                var b = Buf(len);
                foreach (var p in parts)
                {
                    double ph = 0; int start = (int)(p.delay * SR);
                    for (int i = start; i < b.Length; i++)
                    {
                        float t = (i - start) / (float)SR, fr = p.f0 * Mathf.Pow(Mathf.Max(p.f1, 1) / p.f0, Mathf.Min(1, t / p.dur));
                        ph += fr / SR;
                        b[i] += Osc(p.type, (float)ph) * Env(t, p.gain, p.dur, p.attack);
                    }
                }
                return b;
            });
        }

        public static AudioClip Mix(string key, params AudioClip[] clips)
        {
            return Cached("mix" + key, () =>
            {
                int len = 0; foreach (var c in clips) len = Mathf.Max(len, c.samples);
                var b = new float[len]; var tmp = new float[len];
                foreach (var c in clips) { var d = new float[c.samples]; c.GetData(d, 0); for (int i = 0; i < d.Length; i++) b[i] += d[i]; }
                return b;
            });
        }

        public static AudioClip Whisper(int variant)
        {
            return Cached("whisper" + variant, () =>
            {
                var vowels = new[] { new[] { 700f, 1200f }, new[] { 400f, 2000f }, new[] { 300f, 900f }, new[] { 600f, 1700f } };
                var v = vowels[variant % 4]; float dur = 0.5f + (variant % 3) * 0.15f;
                var b = Buf(1.2f); var f1 = new Biquad(F.Bandpass, v[0], 9); var f2 = new Biquad(F.Bandpass, v[1], 9);
                int off = (int)(R() * SR);
                for (int i = 0; i < b.Length; i++)
                {
                    float x = Noise[(i + off) % Noise.Length];
                    b[i] = (f1.Run(x) + f2.Run(x)) * Env(i / (float)SR, 1f, dur, 0.12f);
                }
                return b;
            });
        }

        public static AudioClip Creak(int variant)
        {
            return Cached("creak" + variant, () =>
            {
                var r = new System.Random(variant * 31 + 5);
                float baseF = 70 + (float)r.NextDouble() * 60, lfoF = 9 + (float)r.NextDouble() * 14, depth = 20 + (float)r.NextDouble() * 30;
                float bp = 600 + (float)r.NextDouble() * 700, dur = 0.4f + (float)r.NextDouble() * 0.7f;
                var b = Buf(dur + 0.3f); var f = new Biquad(F.Bandpass, bp, 10); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, fr = baseF + Mathf.Sin(2 * Mathf.PI * lfoF * t) * depth;
                    ph += fr / SR;
                    b[i] = f.Run(Osc("sawtooth", (float)ph)) * Env(t, 1f, dur, 0.08f);
                }
                return b;
            });
        }

        public static AudioClip Caught()
        {
            return Cached("caught", () =>
            {
                var b = Buf(2.2f); var f = new Biquad(F.Bandpass, 3200, 4);
                double p1 = 0, p2 = 0, p3 = 0, p4 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    if (i % 64 == 0) f.Set(F.Bandpass, 3200 * Mathf.Pow(500f / 3200, Mathf.Min(1, t / 1.4f)), 4);
                    float s = f.Run(Noise[i % Noise.Length]) * Env(t, 1.1f, 1.6f, 0.01f);
                    float sw = Mathf.Pow(0.5f, Mathf.Min(1, t / 1.6f));
                    p1 += 180 * sw / SR; p2 += 191 * sw / SR; p3 += 270 * sw / SR;
                    s += (Osc("sawtooth", (float)p1) + Osc("sawtooth", (float)p2) + Osc("sawtooth", (float)p3)) * Env(t, 0.18f, 1.6f, 0.005f);
                    p4 += 60 * Mathf.Pow(28f / 60, Mathf.Min(1, t / 2f)) / SR;
                    s += Mathf.Sin(2 * Mathf.PI * (float)p4) * Env(t, 0.8f, 2f, 0.005f);
                    b[i] = s;
                }
                return b;
            });
        }

        // the jumpscare: a tearing, warbling shriek with a sub-bass drop under it
        public static AudioClip Scream()
        {
            return Cached("scream", () =>
            {
                var b = Buf(1.6f);
                var f1 = new Biquad(F.Bandpass, 2400, 3); var f2 = new Biquad(F.Bandpass, 900, 2);
                double p1 = 0, p2 = 0, p3 = 0, p4 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float wob = Mathf.Sin(t * 2 * Mathf.PI * 17) * 45 + Mathf.Sin(t * 2 * Mathf.PI * 5.3f) * 30;
                    float drop = Mathf.Lerp(1, 0.55f, Mathf.Clamp01((t - 0.6f) / 0.9f));
                    p1 += (520 + wob) * drop / SR; p2 += (557 + wob * 1.3f) * drop / SR; p3 += (1040 - wob) * drop / SR;
                    float v = Osc("sawtooth", (float)p1) + Osc("sawtooth", (float)p2) * 0.8f + Osc("square", (float)p3) * 0.25f;
                    float n = Noise[(i * 3) % Noise.Length];
                    v = f1.Run(v * 0.6f + n * 0.9f) * 1.6f + f2.Run(v) * 0.8f;
                    v = (float)System.Math.Tanh(v * 2.2f);   // distortion
                    float env = t < 0.015f ? t / 0.015f : Mathf.Pow(1 - Mathf.Clamp01((t - 0.015f) / 1.55f), 1.4f);
                    p4 += 70 * Mathf.Pow(25f / 70, Mathf.Min(1, t / 1.2f)) / SR;
                    b[i] = v * env * 0.75f + Mathf.Sin(2 * Mathf.PI * (float)p4) * env * 0.7f;
                }
                return b;
            });
        }

        // a joint cracking, somewhere close: a few dry clicks
        public static AudioClip Crack(int variant)
        {
            return Cached("crack" + variant, () =>
            {
                var b = Buf(0.6f); var f = new Biquad(F.Highpass, 1500, 0);
                var r = new System.Random(100 + variant);
                int clicks = 3 + r.Next(5);
                for (int c = 0; c < clicks; c++)
                {
                    int at = (int)((0.02 + r.NextDouble() * 0.45) * SR), len = (int)(0.004f * SR + r.Next(80));
                    float g = 0.4f + (float)r.NextDouble() * 0.6f;
                    for (int i = 0; i < len && at + i < b.Length; i++) b[at + i] += Noise[(at + i * 7) % Noise.Length] * g * (1 - i / (float)len);
                }
                for (int i = 0; i < b.Length; i++) b[i] = f.Run(b[i]);
                return b;
            });
        }

        public static AudioClip Piano(float freq, float gain)
        {
            return Cached($"piano{freq:F1}_{gain:F2}", () =>
            {
                var b = Buf(3.6f);
                foreach (var (mult, amp) in new[] { (1f, 1f), (2f, 0.35f), (3f, 0.12f) })
                {
                    double ph = 0; float fr = freq * mult * (1 + (R() - 0.5f) * 0.002f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        ph += fr / SR; float t = i / (float)SR;
                        b[i] += Osc(mult == 1 ? "triangle" : "sine", (float)ph) * Env(t, gain * amp, 3.49f, 0.01f);
                    }
                }
                return b;
            });
        }

        // ---- loops ----
        public static AudioClip FilteredNoiseLoop(string key, float seconds, params (F type, float freq, float q, float gain)[] bands)
        {
            return Cached("loop" + key, () =>
            {
                int n = (int)(seconds * SR), warm = SR / 2;
                var b = new float[n];
                foreach (var band in bands)
                {
                    var f = new Biquad(band.type, band.freq, band.q);
                    for (int i = 0; i < warm; i++) f.Run(Noise[(i + 1000) % Noise.Length]);
                    for (int i = 0; i < n; i++) b[i] += f.Run(Noise[(i + warm + 1000) % Noise.Length]) * band.gain;
                }
                // crossfade the ends so the seam doesn't click
                int xf = SR / 20;
                for (int i = 0; i < xf; i++) { float k = i / (float)xf; b[i] *= k; b[n - 1 - i] *= k; }
                return b;
            });
        }

        public static AudioClip ToneLoop(string key, float seconds, params (string type, float freq, float gain)[] oscs)
        {
            return Cached("tloop" + key, () =>
            {
                int n = (int)(seconds * SR); var b = new float[n];
                foreach (var o in oscs) for (int i = 0; i < n; i++) b[i] += Osc(o.type, o.freq * i / SR) * o.gain;
                return b;
            });
        }

        public static AudioClip Drone()
        {
            return Cached("drone", () =>
            {
                float sec = 10; int n = (int)(sec * SR); var b = new float[n];
                foreach (var fr in new[] { 41f, 41.7f, 61.5f }) for (int i = 0; i < n; i++) b[i] += Mathf.Sin(2 * Mathf.PI * fr * i / SR);
                var air = new Biquad(F.Bandpass, 500, 1); var lp = new Biquad(F.Lowpass, 400, 0);
                for (int i = 0; i < n; i++) b[i] += lp.Run(air.Run(Noise[i % Noise.Length])) * 2.5f;
                return b;
            });
        }

        public static AudioClip Phone()
        {
            return Cached("phone", () =>
            {
                int n = SR; var b = new float[n]; var lp = new Biquad(F.Lowpass, 1800, 0);
                for (int k = 0; k < 2; k++)
                    for (int i = 0; i < n; i++) { float s = Osc("square", 440f * i / SR) + Osc("square", 480f * i / SR); float y = lp.Run(s); if (k == 1) b[i] = y; }
                return b;
            });
        }

        public static AudioClip Buzz()
        {
            return Cached("buzz", () =>
            {
                int n = SR; var b = new float[n]; var bp = new Biquad(F.Bandpass, 240, 2);
                for (int k = 0; k < 2; k++)
                    for (int i = 0; i < n; i++) { float y = bp.Run(Osc("sawtooth", 120f * i / SR)); if (k == 1) b[i] = y; }
                return b;
            });
        }
    }
}
