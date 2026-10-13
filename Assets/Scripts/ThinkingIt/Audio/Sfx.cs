using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // Every sound in the game. One instance lives for the whole session.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }
        public static Sfx Ensure()
        {
            if (I) return I;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sfx>();
            I.Init();
            return I;
        }

        readonly List<AudioSource> pool2D = new List<AudioSource>(), pool3D = new List<AudioSource>();
        readonly Timers timers = new Timers();
        public class Loop { public AudioSource src; public AudioLowPassFilter lp; public float gain; }
        [System.NonSerialized] public Loop drone, whispers, focus, breath, phone, radio, buzz, lake;
        AudioSource music;
        float musicTarget, droneTarget, musicT;
        public bool musicOn;
        AudioReverbZone reverb;
        public static float Volume = 0.8f;

        void Init()
        {
            Volume = PlayerPrefs.GetFloat("ti_vol", 0.8f);
            AudioListener.volume = Volume;
            for (int i = 0; i < 16; i++) pool2D.Add(NewSource(false));
            for (int i = 0; i < 28; i++) pool3D.Add(NewSource(true));
            drone = MakeLoop(Synth.Drone(), false, true);
            whispers = MakeLoop(Synth.FilteredNoiseLoop("wh", 3, (Synth.F.Bandpass, 2200, 6, 1f), (Synth.F.Bandpass, 800, 8, 1f)), false);
            focus = MakeLoop(Synth.ToneLoop("focus", 1, ("sine", 220, 1f)), false);
            breath = MakeLoop(Synth.FilteredNoiseLoop("breath", 3, (Synth.F.Bandpass, 350, 3, 1f)), true);
            phone = MakeLoop(Synth.Phone(), true);
            radio = MakeLoop(Synth.FilteredNoiseLoop("radio", 3, (Synth.F.Bandpass, 1400, 0.6f, 1f)), true);
            buzz = MakeLoop(Synth.Buzz(), true);
            lake = MakeLoop(Synth.FilteredNoiseLoop("lake", 4, (Synth.F.Lowpass, 380, 0, 1.6f), (Synth.F.Bandpass, 900, 2, 0.25f)), false);
            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0; music.volume = 0;
        }

        AudioSource NewSource(bool spatial)
        {
            var go = new GameObject(spatial ? "src3d" : "src2d");
            go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial ? 1 : 0;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1.5f; s.maxDistance = 60f;
            s.dopplerLevel = 0;
            return s;
        }

        Loop MakeLoop(AudioClip clip, bool spatial, bool lowpass = false)
        {
            var s = NewSource(spatial); s.clip = clip; s.loop = true; s.volume = 0; s.Play();
            var l = new Loop { src = s };
            if (lowpass) { l.lp = s.gameObject.AddComponent<AudioLowPassFilter>(); l.lp.cutoffFrequency = 120; }
            return l;
        }

        public void SetVolume(float v) { Volume = v; AudioListener.volume = v; PlayerPrefs.SetFloat("ti_vol", v); }

        // the reverb that makes the buildings sound big and hollow
        public void SetSpace(AudioReverbPreset preset)
        {
            var listener = FindAnyObjectByType<AudioListener>();
            if (!listener) return;
            if (!reverb || reverb.transform != listener.transform)
            {
                reverb = listener.GetComponent<AudioReverbZone>();
                if (!reverb) reverb = listener.gameObject.AddComponent<AudioReverbZone>();
            }
            reverb.minDistance = 500; reverb.maxDistance = 1000;
            reverb.reverbPreset = preset;
        }

        public void SilenceLoops()
        {
            foreach (var l in new[] { whispers, focus, breath, phone, radio, buzz, lake }) Set(l, 0);
        }

        public void Set(Loop l, float gain) { if (l != null) l.src.volume = Mathf.Clamp01(gain); }
        public void SetPos(Loop l, Vector3 p) { if (l != null) l.src.transform.position = p; }

        // ---- playback ----
        int i2, i3;
        public AudioSource Play(AudioClip clip, Vector3? at = null, float vol = 1, float pitch = 1)
        {
            if (!clip) return null;
            AudioSource s;
            if (at.HasValue) { s = pool3D[i3++ % pool3D.Count]; s.transform.position = at.Value; }
            else s = pool2D[i2++ % pool2D.Count];
            s.Stop(); s.clip = clip; s.volume = Mathf.Clamp01(vol); s.pitch = pitch; s.Play();
            return s;
        }
        public void After(float t, System.Action a) => timers.After(t, a);

        public void Noise(float dur, float freq, float gain, Synth.F type = Synth.F.Bandpass, Vector3? at = null, float q = 1, float attack = 0.005f)
            => Play(Synth.NoiseBurst(dur, freq, gain, type, q, attack, Random.Range(0, 3)), at);
        public void Tone(float f0, float f1, float dur, float gain, string type = "sine", Vector3? at = null, float attack = 0.005f)
            => Play(Synth.Tone(f0, f1, dur, gain, type, attack), at);

        // footsteps: wood in the apartment and the lake house, concrete everywhere else
        public void Step(bool wood)
        {
            if (wood) { Tone(95 + Random.Range(0, 4) * 5, 60, 0.09f, 0.35f); Noise(0.06f, 900 + Random.Range(0, 3) * 100, 0.12f, Synth.F.Bandpass, null, 1.5f); }
            else { Noise(0.05f, 1800 + Random.Range(0, 3) * 200, 0.14f, Synth.F.Bandpass, null, 1.2f); Tone(70, 45, 0.06f, 0.2f); }
        }
        public void StepWater() { Noise(0.18f, 700 + Random.Range(0, 3) * 150, 0.18f, Synth.F.Bandpass, null, 0.8f, 0.02f); Noise(0.1f, 2600, 0.05f, Synth.F.Highpass); }
        public void Bump() { Noise(0.22f, 140, 0.7f, Synth.F.Lowpass); Tone(80, 45, 0.18f, 0.35f); }
        public void Heart(float v)
        {
            System.Action beat = null;
            float k = 1;
            beat = () => { Tone(58, 34, 0.16f, 0.6f * v * k); Noise(0.08f, 90, 0.3f * v * k, Synth.F.Lowpass); };
            beat();
            timers.After(0.17f, () => { Tone(58, 34, 0.16f, 0.45f * v); Noise(0.08f, 90, 0.22f * v, Synth.F.Lowpass); });
        }
        public void Whisper(Vector3? at, float gain = 0.4f) => Play(Synth.Whisper(Random.Range(0, 12)), at, gain);
        public void Thought(Vector3 at) { Whisper(at, 0.55f); Noise(0.6f, 3000, 0.08f, Synth.F.Highpass, at, 1, 0.1f); }
        public void Materialize()
        {
            Play(Synth.Chord("mat", (523, 523, 2.2f, 0.1f, "sine", 0.005f, 0), (784, 784, 2.2f, 0.1f / 1.4f, "sine", 0.005f, 0.06f),
                (1046, 1046, 2.2f, 0.1f / 1.8f, "sine", 0.005f, 0.12f), (1568, 1568, 2.2f, 0.1f / 2.2f, "sine", 0.005f, 0.18f), (2093, 2093, 2.2f, 0.1f / 2.6f, "sine", 0.005f, 0.24f)));
            Noise(1.2f, 2500, 0.12f, Synth.F.Bandpass, null, 0.7f, 0.6f);
        }
        public void Sting()
        {
            Play(Synth.Chord("sting", (311, 305, 1.8f, 0.07f, "sawtooth", 0.03f, 0), (330, 323, 1.8f, 0.07f, "sawtooth", 0.03f, 0),
                (466, 457, 1.8f, 0.07f, "sawtooth", 0.03f, 0), (494, 484, 1.8f, 0.07f, "sawtooth", 0.03f, 0), (55, 40, 1.2f, 0.4f, "sine", 0.005f, 0)));
            Noise(0.4f, 600, 0.35f, Synth.F.Lowpass);
        }
        public void CaughtSound() => Play(Synth.Caught());
        public void Scream(float gain = 1) => Play(Synth.Scream(), null, gain);
        public void Crack(Vector3 at, float gain = 0.5f) => Play(Synth.Crack(Random.Range(0, 6)), at, gain, 0.8f + Random.value * 0.4f);
        public void Lever() { Noise(0.3f, 900, 0.7f); Tone(130, 60, 0.4f, 0.4f, "square"); }
        public void PowerUp()
        {
            Tone(40, 120, 2.5f, 0.45f, "sawtooth", null, 0.3f);
            timers.After(0.6f, () => Noise(0.2f, 2000, 0.3f));
            timers.After(1.1f, () => Noise(0.2f, 2000, 0.3f));
        }
        public void Click() => Noise(0.03f, 3000, 0.22f);
        // typewriter: a key strike, a softer backspace, and the bell + carriage return at a line break
        public void TypeKey() { Noise(0.022f, 2100 + Random.Range(0, 900), 0.17f, Synth.F.Bandpass, null, 1.2f, 0.001f); Tone(160 + Random.Range(0, 40), 85, 0.05f, 0.12f, "square", null, 0.001f); }
        public void TypeBack() { Noise(0.018f, 1700 + Random.Range(0, 400), 0.09f, Synth.F.Bandpass, null, 1f, 0.001f); Tone(120, 80, 0.03f, 0.06f, "square", null, 0.001f); }
        public void TypeReturn() { Noise(0.09f, 800, 0.22f, Synth.F.Bandpass, null, 0.8f, 0.002f); Tone(2300, 2290, 0.35f, 0.045f, "sine", null, 0.002f); }
        public void Flicker() => Noise(0.06f, 5000, 0.12f, Synth.F.Highpass);
        public void Creak(Vector3? at, float gain = 0.25f) => Play(Synth.Creak(Random.Range(0, 10)), at, gain);
        public void DoorCreak() { Creak(null, 0.5f); timers.After(0.3f, () => Creak(null, 0.35f)); Noise(0.6f, 300, 0.4f, Synth.F.Lowpass, null, 1, 0.05f); }
        public void Tick(Vector3 at, bool tock)
        {
            Noise(0.018f, tock ? 1700 : 2600, 0.5f, Synth.F.Bandpass, at, 6);
            Tone(tock ? 900 : 1300, tock ? 880 : 1280, 0.03f, 0.05f, "sine", at);
        }
        public void Drip(Vector3 at) => Tone(1400 + Random.Range(0, 9) * 100, 500, 0.12f, 0.15f, "sine", at, 0.002f);
        public void Splash(Vector3 at, float gain = 0.4f) { Noise(0.5f, 900, gain, Synth.F.Bandpass, at, 0.7f, 0.02f); Noise(0.3f, 300, gain * 0.8f, Synth.F.Lowpass, at, 1, 0.01f); }
        public void Thud(Vector3 at) => Noise(0.25f, 220, 0.5f, Synth.F.Lowpass, at);
        public void Ambient(float px, float pz)
        {
            var at = new Vector3(px + MathX.Jit(16), 1 + Random.value * 2, pz + MathX.Jit(16));
            float r = Random.value;
            if (r < 0.45f) Creak(at, 0.18f);
            else if (r < 0.65f) Noise(0.5f, 120, 0.3f, Synth.F.Lowpass, at);
            else if (r < 0.8f) { for (int i = 0; i < 3; i++) timers.After(i * 0.16f, () => Noise(0.05f, 700, 0.18f, Synth.F.Bandpass, at, 4)); }
            else Whisper(at, 0.12f);
        }
        public void Hover() => Tone(1200, 1150, 0.06f, 0.04f);
        public void Select() { Tone(330, 320, 0.9f, 0.12f, "triangle"); Tone(495, 490, 0.9f, 0.06f); }
        public void Tape() { Click(); timers.After(0.25f, Click); }

        // ---- title music: slow, sparse piano notes over the low drone ----
        public void StartMusic() { musicOn = true; musicTarget = 0.5f; droneTarget = 0.1f; }
        public void StopMusic() { musicOn = false; musicTarget = 0; droneTarget = 0.18f; }
        public void SetDrone(float target) { droneTarget = target; }
        static readonly float[] scale = { 220, 246.9f, 261.6f, 293.7f, 329.6f, 349.2f, 392, 440, 523.3f };

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            timers.Tick(dt);
            if (music) music.volume = Mathf.MoveTowards(music.volume, musicTarget, dt * 0.4f);
            if (drone != null) drone.src.volume = Mathf.MoveTowards(drone.src.volume, droneTarget, dt * 0.08f);
            if (musicOn)
            {
                musicT -= dt;
                if (musicT <= 0)
                {
                    float n = MathX.Pick(scale); bool wrong = Random.value < 0.08f;
                    music.PlayOneShot(Synth.Piano(wrong ? n * 1.06f : n, 0.22f));
                    if (Random.value < 0.4f) timers.After(0.14f, () => music.PlayOneShot(Synth.Piano(n / 2, 0.14f)));
                    musicT = 1.4f + Random.value * 2.2f;
                }
            }
        }
    }
}
