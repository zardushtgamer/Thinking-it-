using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ThinkingIt
{
    public enum GState { Intro, Play, Paused, Caught, End, Cutscene }

    public class ItState
    {
        public string state = "gone";
        public float x, z, seenT, cool = 4, stepT, wanderX, wanderZ, fade;
        public bool wasSeen, hasWander;
    }

    public struct Gaze
    {
        public string tag, data; public Tag obj; public float dist; public Vector3 point;
    }

    // The shared rules of THINKING IT: you move, you look, looking makes things real,
    // dread rises, and "it" comes to the places you keep looking at.
    public class Game : MonoBehaviour
    {
        public static Game I;
        [System.NonSerialized] public Chapter ch;
        [System.NonSerialized] public World w;
        [System.NonSerialized] public Hud hud;
        [System.NonSerialized] public Sfx au;
        [System.NonSerialized] public ItModel itModel;
        public Camera cam;
        public Light flash;
        [System.NonSerialized] public PostFX fx;
        public int chapterNum;

        public GState state = GState.Intro;
        public float time, dread;

        // the player
        public float px, pz, yaw, pitch;
        public bool eyes = true;
        public string reading; public bool readingForbidden;
        public Vector3 checkpoint;   // x, z, yaw

        // shared story state
        public bool hall, firstSight;
        public string zone = "";
        public int timesSeen, timesCaught, forbiddenRead;
        public readonly HashSet<string> read = new HashSet<string>();
        public readonly ItState IT = new ItState();
        public readonly Timers timers = new Timers();

        // what's on screen
        readonly List<Line> subtitles = new List<Line>(); float subT;
        readonly List<(string text, float t)> hints = new List<(string, float)>();
        public string thought; float thoughtLeft; Vector2 thoughtOff;
        public bool HasSubtitles => subtitles.Count > 0;

        [System.NonSerialized] public Gaze? gaze;
        [System.NonSerialized] public Focusable focusing;
        [System.NonSerialized] public Interactable near;
        float stepAcc, heartT, thoughtT = 20, bumpT, walkPhase, ambientT = 5, caughtT;
        public float flashOverride = -1;   // chapters can force the flashlight
        public float exposure;
        public float shake;   // camera shake, decays by itself
        public float camY;    // extra camera height (stairs)
        public const float JUMP = 0.9f;   // length of the jumpscare when it reaches you

        public float Sens => 0.0021f * Settings.Sens;

        // ------------------------------------------------------------------ setup
        public void Begin(int n)
        {
            I = this; chapterNum = n;
            au = Sfx.Ensure();
            Mats.ClearCache(); MeshGen.ClearCache();

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 90; cam.fieldOfView = 72;
            camGo.AddComponent<AudioListener>();
            var fl = new GameObject("Flashlight"); fl.transform.SetParent(camGo.transform, false);
            fl.transform.localPosition = new Vector3(0.15f, -0.12f, 0);
            fl.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.15f, 0.02f, 4f));
            flash = fl.AddComponent<Light>(); flash.type = LightType.Spot; flash.color = MathX.Hex(0xfff0d8);
            flash.range = 18; flash.spotAngle = 0.42f * 2 * Mathf.Rad2Deg; flash.innerSpotAngle = flash.spotAngle * 0.4f;
            flash.shadows = LightShadows.Soft; flash.shadowBias = 0.02f; flash.shadowNormalBias = 0.4f;
            fx = PostFX.Create(cam);

            w = new World("World");
            ch = Chapter.Create(n); ch.g = this; ch.w = w;
            ch.Build();
            w.AutoObstacles();
            itModel = new ItModel(w.root);

            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = ch.FogDensity; RenderSettings.fogColor = ch.FogColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = ch.Ambient;
            RenderSettings.skybox = null; RenderSettings.reflectionIntensity = 0.3f;
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional && l.transform.root != w.root) l.enabled = false;

            hud = Hud.Create();
            au.StopMusic(); au.SetSpace(ch.Space); au.SilenceLoops();
            state = GState.Intro;
            PlaceCamera();
            hud.HideGameplay();
            hud.PlayCards(GameData.Cards[n], StartPlay);
        }

        void StartPlay()
        {
            if (state != GState.Intro) return;
            dread = 0; eyes = true; reading = null;
            IT.state = "gone"; IT.cool = 6; itModel.SetVisible(false);
            ch.Begin();
            checkpoint = new Vector3(px, pz, yaw);
            state = GState.Play;
            au.SetDrone(0.18f);
            LockCursor(true);
        }

        void LockCursor(bool on) { Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !on; }

        public void Hint(string text, float t = 5) { hints.Add((text, t)); }
        public void Say(IList<Line> lines) { subtitles.Clear(); subtitles.AddRange(lines); subT = 0; }
        public void Think(string text, float t) { thought = text; thoughtLeft = t; thoughtOff = Vector2.zero; }

        // ------------------------------------------------------------------ geometry queries
        public Vector2 Fwd => new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw));

        public bool Los(float ax, float az, float bx, float bz)
        {
            int n = Mathf.CeilToInt(MathX.Hypot(bx - ax, bz - az) / 0.25f);
            for (int i = 1; i < n; i++) { float t = (float)i / n; if (w.Opaque(ax + (bx - ax) * t, az + (bz - az) * t)) return false; }
            return true;
        }
        public bool InView(float x, float z, float cosLimit = 0.8f)
        {
            if (!eyes) return false;
            var f = Fwd; float dx = x - px, dz = z - pz, d = MathX.Hypot(dx, dz);
            if (d < 0.001f) return true;
            return d < 22 && (dx * f.x + dz * f.y) / d > cosLimit && Los(px, pz, x, z);
        }

        void PlaceCamera()
        {
            float bob = eyes ? Mathf.Sin(walkPhase) * 0.035f : 0;
            cam.transform.position = new Vector3(px, World.EYE + camY + bob, pz);
            var f = new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch));
            cam.transform.rotation = Quaternion.LookRotation(f) * Quaternion.AngleAxis(Mathf.Sin(time * 0.7f) * dread * 0.035f * Mathf.Rad2Deg, Vector3.forward)
                * Quaternion.Euler(MathX.Jit(shake * 3), MathX.Jit(shake * 3), MathX.Jit(shake * 2));
        }

        Gaze? Look()
        {
            PlaceCamera();
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 26f, ~0, QueryTriggerInteraction.Ignore)) return null;
            var t = hit.collider.GetComponentInParent<Tag>();
            return new Gaze { tag = t ? t.tagName : null, data = t ? t.data : null, obj = t, dist = hit.distance, point = hit.point };
        }

        // Where you look, the attention map grows. "It" will come to the places you keep looking.
        void RecordAttention(float dt)
        {
            if (gaze == null || !eyes) return;
            var g = gaze.Value; var f = Fwd;
            float x0 = g.point.x - f.x * 0.4f, z0 = g.point.z - f.y * 0.4f;
            int x = Mathf.FloorToInt(x0 / World.C), y = Mathf.FloorToInt(z0 / World.C);
            if (x < 0 || y < 0 || x >= w.W || y >= w.H || !w.Walkable(x, y)) return;
            w.heat[y * w.W + x] += dt * (g.dist > 6 ? 1.5f : 0.6f);
        }

        // ------------------------------------------------------------------ It
        public void ShowIt(float x, float z, string st)
        {
            IT.x = x; IT.z = z; IT.state = st; IT.seenT = 0; IT.wasSeen = false; IT.fade = 0;
            itModel.SetVisible(true); itModel.SetScale(1); itModel.SetHeight(0);
        }
        public void HideIt(float cool)
        {
            IT.state = "gone"; IT.cool = cool; itModel.SetVisible(false);
            au.Set(au.breath, 0);
        }

        public Vector2? Hotspot(float minD, float maxD, bool allowInView)
        {
            Vector2? best = null; float bh = -1;
            for (int y = 0; y < w.H; y++) for (int x = 0; x < w.W; x++)
                {
                    if (!w.Walkable(x, y) || w.InObstacle(World.cx(x), World.cx(y))) continue;
                    float wx = World.cx(x), wz = World.cx(y), d = MathX.Hypot(wx - px, wz - pz);
                    if (d < minD || d > maxD || (!allowInView && InView(wx, wz, 0.5f))) continue;
                    if (ch.HallZone != null && ch.Zone(wx, wz) == ch.HallZone && !hall) continue;
                    float hv = w.heat[y * w.W + x] + Random.value * 0.3f;
                    if (hv > bh) { bh = hv; best = new Vector2(wx, wz); }
                }
            return best;
        }

        void UpdateIt(float dt)
        {
            if (ch.CustomIt(dt)) return;
            IT.cool -= dt;
            float d = MathX.Hypot(IT.x - px, IT.z - pz);
            bool seen = IT.state != "gone" && InView(IT.x, IT.z, 0.86f);

            if (IT.state == "gone")
            {
                float want = hall ? 0.15f : 0.32f;
                if (dread > want && IT.cool <= 0)
                {
                    var spot = Hotspot(4, 16, false);
                    if (spot.HasValue) ShowIt(spot.Value.x, spot.Value.y, dread > 0.7f ? "stalk" : "lurk");
                    else IT.cool = 2;
                }
            }
            else if (!eyes && hall)
            {
                // eyes closed in the dark: it wanders, and you can only hear it
                IT.state = "wander";
                if (!IT.hasWander || MathX.Hypot(IT.wanderX - IT.x, IT.wanderZ - IT.z) < 0.5f)
                {
                    var s = Hotspot(0, 30, true); if (s.HasValue) { IT.wanderX = s.Value.x + MathX.Jit(1); IT.wanderZ = s.Value.y; IT.hasWander = true; }
                }
                bool towardP = Random.value < 0.35f;
                float tx = towardP ? px : IT.wanderX, tz = towardP ? pz : IT.wanderZ, vx = tx - IT.x, vz = tz - IT.z, vd = Mathf.Max(0.001f, MathX.Hypot(vx, vz));
                float nx = IT.x + vx / vd * 0.75f * dt, nz = IT.z + vz / vd * 0.75f * dt;
                if (!w.Solid(nx, IT.z)) IT.x = nx;
                if (!w.Solid(IT.x, nz)) IT.z = nz;
            }
            else
            {
                if (seen)
                {
                    IT.seenT += dt; dread = MathX.Clamp01(dread + 0.22f * dt);
                    if (!IT.wasSeen)
                    {
                        IT.wasSeen = true; timesSeen++; au.Sting();
                        // the closer it is when you turn and find it, the worse it is
                        if (d < 4.5f) { shake = Mathf.Max(shake, 1.1f); au.Scream(0.35f); dread = MathX.Clamp01(dread + 0.12f); }
                        else shake = Mathf.Max(shake, 0.35f);
                        if (!firstSight) { firstSight = true; timers.After(0.9f, () => Think("don't think about it", 1.6f)); }
                    }
                }
                if (IT.state == "stalk" || (hall && dread > 0.55f))
                {
                    IT.state = "stalk";
                    // it only moves when you aren't looking at it
                    IT.stepT -= dt;
                    if (!seen && IT.stepT <= 0)
                    {
                        IT.stepT = 0.9f;
                        float vx = px - IT.x, vz = pz - IT.z, vd = Mathf.Max(0.001f, MathX.Hypot(vx, vz)), jump = Mathf.Min(vd - 0.6f, 1.6f);
                        float nx = IT.x + vx / vd * jump, nz = IT.z + vz / vd * jump;
                        if (w.Solid(nx, nz))
                        {
                            var s = Hotspot(1.2f, Mathf.Max(2, vd - 1), true);
                            if (s.HasValue) { nx = s.Value.x; nz = s.Value.y; } else { nx = IT.x; nz = IT.z; }
                        }
                        IT.x = nx; IT.z = nz;
                        au.Thud(new Vector3(IT.x, 1, IT.z));
                    }
                    if (dread < 0.3f && !hall) HideIt(8);
                }
                else
                {
                    // lurking: there when you look back... gone once you've looked away again
                    if (IT.wasSeen && !seen && IT.seenT > 0.5f) HideIt(6 + Random.value * 8);
                    else if (dread > 0.72f) IT.state = "stalk";
                    else if (dread < 0.2f) HideIt(5);
                }
            }
            // close, unseen: you can hear its joints
            if (IT.state != "gone" && !seen && d < 8 && Random.value < dt * (0.25f + dread * 0.6f)) au.Crack(new Vector3(IT.x, 1.6f, IT.z), 0.35f + (8 - d) * 0.05f);
            if (IT.state != "gone" && d < 1.25f) { Caught(); return; }
            PlaceItModel(hall ? 1.6f : 0.8f);
        }

        public void PlaceItModel(float breathK)
        {
            if (IT.state == "gone") return;
            itModel.Place(IT.x, IT.z, px, pz, 0.12f * (1 + dread));
            itModel.Flicker(Random.value > 0.03f * (1 - dread));
            au.SetPos(au.breath, new Vector3(IT.x, 2.4f, IT.z));
            au.Set(au.breath, (0.25f + Mathf.Sin(time * 1.7f) * 0.15f) * breathK * (eyes ? 1 : 1.8f));
        }

        public void Caught()
        {
            if (state != GState.Play) return;
            state = GState.Caught; caughtT = 0; timesCaught++;
            CloseDoc();
            // the jumpscare: you're spun round to face it, it's right in your face, screaming, then black
            if (ch.CustomJumpscare) ch.Jumpscare(0, 0);
            else
            {
                float ddx = IT.x - px, ddz = IT.z - pz; if (MathX.Hypot(ddx, ddz) > 0.05f) yaw = Mathf.Atan2(-ddx, -ddz);
                IT.state = "gone"; IT.cool = 5; au.Set(au.breath, 0);
                itModel.SetVisible(true); itModel.SetScale(0.95f); itModel.lunge = 1;
                itModel.coreMat.SetColor("_BaseColor", Color.black);
            }
            eyes = true; shake = 1.6f;
            au.Scream(1); timers.After(0.6f, au.CaughtSound);
            ch.OnCaught();
        }

        void Respawn()
        {
            px = checkpoint.x; pz = checkpoint.y; yaw = checkpoint.z; pitch = 0; eyes = true;
            dread = hall ? 0.15f : 0.12f; System.Array.Clear(w.heat, 0, w.heat.Length); thought = null;
            HideIt(6);
            state = GState.Play;
            hud.SetCaught(false, 0, 0);
            ch.OnRespawn();
        }

        // ------------------------------------------------------------------ documents
        public void OpenDoc(string id)
        {
            var d = GameData.Docs[id];
            reading = id; readingForbidden = d.forbidden;
            hud.ShowDoc(d);
            au.Noise(0.4f, 1800, 0.2f);
            if (read.Add(id) && d.forbidden) { forbiddenRead++; dread = MathX.Clamp01(dread + 0.14f); }
            if (d.forbidden) Settings.MarkForbidden(id);
        }
        public void CloseDoc() { reading = null; hud.HideDoc(); }

        // forbidden documents start to corrupt as dread rises
        void CorruptDoc()
        {
            if (reading == null || !readingForbidden || Random.value > 0.15f) return;
            float k = MathX.Clamp((dread - 0.35f) * 0.25f, 0, 0.18f);
            if (k <= 0) return;
            var raw = GameData.Docs[reading].body; var sb = new StringBuilder(raw.Length + 16);
            string[] junk = { "█", "▓", "it", "░" };
            bool inTag = false;
            foreach (char c in raw)
            {
                if (c == '<') inTag = true;
                if (!inTag && char.IsLetter(c) && Random.value < k) sb.Append(MathX.Pick(junk)); else sb.Append(c);
                if (c == '>') inTag = false;
            }
            hud.SetDocBody(sb.ToString());
        }

        // ------------------------------------------------------------------ thoughts
        void IntrusiveThought()
        {
            string tier = dread < 0.3f ? "low" : dread < 0.65f ? "mid" : "high";
            string text = MathX.Pick(GameData.Thoughts[tier]);
            thought = text; thoughtLeft = 1.3f; thoughtOff = new Vector2(MathX.Jit(0.4f) * 1920, MathX.Jit(0.3f) * 1080);
            au.Thought(new Vector3(px + MathX.Jit(4), World.EYE, pz + MathX.Jit(4)));
            // sometimes it's telling the truth
            if (text.ToLower().Contains("look behind you") && dread > 0.45f && IT.state != "stalk" && IT.state != "final" && Random.value < 0.55f && eyes && !ch.CustomItActive)
            {
                float bx = px + Mathf.Sin(yaw) * 2.6f, bz = pz + Mathf.Cos(yaw) * 2.6f;
                if (!w.Solid(bx, bz)) ShowIt(bx, bz, "lurk");
            }
        }

        // ------------------------------------------------------------------ interaction
        void FindInteractable()
        {
            near = null;
            if (gaze == null || !eyes) return;
            var g = gaze.Value;
            foreach (var it in ch.interactables)
            {
                if (it.used || g.dist > it.maxGaze) continue;
                if (MathX.Hypot(it.pos.x - px, it.pos.z - pz) > it.range) continue;
                if (g.tag == it.tagName && (it.id == null || g.data == it.id)) { near = it; return; }
            }
        }

        void Interact()
        {
            if (reading != null) { CloseDoc(); return; }
            if (near == null) return;
            if (near.kind == "doc") OpenDoc(near.id);
            else ch.OnInteract(near);
        }

        public void Materialize(Focusable f)
        {
            f.done = true;
            foreach (var c in f.cells) w[c.x, c.y] = f.cellAfter;
            for (int i = 0; i < f.parts.Count; i++)
            {
                f.parts[i].sharedMaterial = f.real;
                f.parts[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var e in f.edges) e.SetActive(false);
            f.group.transform.localPosition = new Vector3(f.group.transform.localPosition.x, f.baseY, f.group.transform.localPosition.z);
            au.Materialize();
            dread = MathX.Clamp01(dread + 0.06f);
        }

        // ------------------------------------------------------------------ flow
        void Pause()
        {
            state = GState.Paused;
            LockCursor(false);
            hud.ShowPause(ch.Objective(), Resume, ToMenu);
            au.Set(au.focus, 0);
        }
        void Resume() { hud.HidePause(); state = GState.Play; LockCursor(true); }
        void ToMenu() { au.SilenceLoops(); SceneFlow.Title(); }

        public void EndChapter()
        {
            if (state == GState.End) return;
            state = GState.End;
            LockCursor(false);
            au.SilenceLoops();
            HideIt(999);
            Settings.Progress = chapterNum + 1;
            var e = ch.End();
            hud.HideGameplay(); hud.HideDoc();
            hud.ShowEnd(e.title, e.sub, e.stats, e.next, e.note,
                e.hasNext ? (System.Action)(() => SceneFlow.Chapter(chapterNum + 1)) : null,
                () => SceneFlow.Chapter(chapterNum), ToMenu, e.nextLabel);
            // the story carries straight on into the next chapter
            if (e.hasNext) { autoNext = AUTO_NEXT; autoNextText = e.next; }
        }
        const float AUTO_NEXT = 10f;
        float autoNext = -1; string autoNextText;

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0, 0.05f);
            time += dt;
            var kb = Keyboard.current;
            timers.Tick(dt);

            if (state == GState.Intro)
            {
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) hud.SkipCards();
                return;
            }

            if (kb != null && kb.f9Key.wasPressedThisFrame && (state == GState.Play || state == GState.Paused) && chapterNum < 5) { SceneFlow.Chapter(chapterNum + 1); return; }

            if (state == GState.Paused)
            {
                if (kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) Resume();
            }
            else if (state == GState.Play || state == GState.Cutscene)
            {
                if (state == GState.Play)
                {
                    if (kb != null && kb.eKey.wasPressedThisFrame) Interact();
                    if (kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { if (reading != null) CloseDoc(); else Pause(); }
                    if (state == GState.Play && Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && reading == null) LockCursor(true);
                    if (reading != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) CloseDoc();
                }
                if (state == GState.Play || state == GState.Cutscene) Step(dt);
            }
            else if (state == GState.Caught)
            {
                caughtT += dt;
                if (ch.CustomJumpscare)
                {
                    ch.Jumpscare(caughtT, dt);
                    if (caughtT < JUMP) { shake = Mathf.Max(shake, 1.2f); pitch = Mathf.Lerp(pitch, 0, dt * 10); hud.SetCaught(false, 0, 0); }
                    else hud.SetCaught(true, caughtT - JUMP, time);
                }
                else if (caughtT < JUMP)
                {
                    // lunging from just out of reach to filling the screen
                    var f = Fwd; float k = MathX.Smooth(Mathf.Clamp01(caughtT / 0.28f));
                    float dd = Mathf.Lerp(1.7f, 0.42f, k);
                    pitch = Mathf.Lerp(pitch, 0, dt * 10);
                    itModel.Place(px + f.x * dd, pz + f.y * dd, px, pz, 0.04f);
                    dd += Mathf.Max(0, itModel.HeadForward);   // put its face, not its feet, at that distance
                    itModel.Place(px + f.x * dd, pz + f.y * dd, px, pz, 0.04f);
                    itModel.SetHeight(World.EYE - itModel.HeadOffset + 0.05f);
                    itModel.Flicker(caughtT < 0.3f || Random.value > 0.15f);
                    shake = Mathf.Max(shake, 1.2f);
                    hud.SetCaught(false, 0, 0);
                }
                else
                {
                    if (itModel.Visible) itModel.SetVisible(false);
                    hud.SetCaught(true, caughtT - JUMP, time);
                }
                if (caughtT > 3.4f + JUMP) Respawn();
            }

            if (state == GState.End && autoNext > 0)
            {
                autoNext -= dt;
                hud.SetEndNext($"{autoNextText}\n<size=24>continuing in {Mathf.CeilToInt(Mathf.Max(0, autoNext))}…</size>");
                if (autoNext <= 0) { autoNext = -1; SceneFlow.Chapter(chapterNum + 1); return; }
            }

            shake = Mathf.Max(0, shake - dt * 2.2f);
            ItModel.Intensity = dread;
            if (state != GState.End)
            {
                PlaceCamera();
                cam.fieldOfView = 72 - dread * 7 + Mathf.Sin(time * 1.3f) * dread * 2.5f;
                fx.Apply(dread, time, exposure);
                DrawHud();
            }
        }

        void Step(float dt)
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool cutscene = state == GState.Cutscene;

            // look
            if (!cutscene && reading == null && Cursor.lockState == CursorLockMode.Locked && mouse != null)
            {
                var md = mouse.delta.ReadValue();
                yaw += md.x * Sens;
                pitch = Mathf.Clamp(pitch + md.y * Sens, -1.2f, 1.2f);
            }

            // eyes: hold SPACE to close them
            bool wantClosed = !cutscene && kb != null && (kb.spaceKey.isPressed || kb.qKey.isPressed) && reading == null && ch.AllowEyes;
            if (wantClosed == eyes) { eyes = !wantClosed; au.Click(); }

            // movement
            if (!cutscene && kb != null)
            {
                int f = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1 : 0) - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1 : 0);
                int s = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1 : 0) - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1 : 0);
                if ((f != 0 || s != 0) && reading == null)
                {
                    float sp = (eyes ? 2.5f : 1.2f) * dt;
                    float fx0 = -Mathf.Sin(yaw), fz = -Mathf.Cos(yaw);
                    float rx = fz, rz = -fx0;   // Unity's screen-right for this forward
                    float vx = fx0 * f + rx * s, vz = fz * f + rz * s, len = MathX.Hypot(vx, vz);
                    vx = vx / len * sp; vz = vz / len * sp;
                    float ox = px, oz = pz, r = 0.32f;
                    if (!w.Solid(px + vx + Mathf.Sign(vx) * r, pz - r) && !w.Solid(px + vx + Mathf.Sign(vx) * r, pz + r)) px += vx;
                    if (!w.Solid(px - r, pz + vz + Mathf.Sign(vz) * r) && !w.Solid(px + r, pz + vz + Mathf.Sign(vz) * r)) pz += vz;
                    float moved = MathX.Hypot(px - ox, pz - oz);
                    if (moved < sp * 0.3f) { bumpT -= dt; if (bumpT <= 0) { au.Bump(); bumpT = 0.45f; } }
                    walkPhase += moved * 3.2f; stepAcc += moved;
                    if (stepAcc > 1.5f) { stepAcc = 0; if (w.Cell(px, pz) == '=' || ch.Wood(px, pz)) au.Step(true); else au.Step(false); }
                }
            }

            // zones, checkpoints
            string z = ch.Zone(px, pz);
            if (z != zone)
            {
                zone = z;
                checkpoint = new Vector3(px, pz, yaw);
                if (ch.HallZone != null && z == ch.HallZone && !hall)
                {
                    hall = true;
                    dread = MathX.Clamp01(dread + 0.12f);
                    Hint("Your flashlight flickers... and dies.", 4);
                    timers.After(3.5f, () => Hint(ch.HallHint, 6));
                    IT.cool = 3;
                }
                ch.OnZone(z);
            }

            // what are you looking at?
            gaze = eyes && reading == null ? Look() : null;
            RecordAttention(dt);
            FindInteractable();

            // focus: stare at something unfinished and it becomes real
            focusing = null;
            if (gaze != null && gaze.Value.tag == "focus" && gaze.Value.dist < 12 && !cutscene)
            {
                var obj = gaze.Value.obj ? gaze.Value.obj.gameObject : null;
                var fo = ch.focusables.Find(q => q.group == obj);
                if (fo != null && !fo.done)
                {
                    focusing = fo;
                    fo.progress += dt / fo.time;
                    dread = MathX.Clamp01(dread + 0.025f * dt);
                    if (fo.progress >= 1) ch.OnFocusDone(fo);
                }
            }
            foreach (var fo in ch.focusables)
            {
                if (fo != focusing && !fo.done) fo.progress = Mathf.Max(0, fo.progress - dt * 0.25f);
                if (!fo.done && !fo.suspect)
                {
                    float flick = 0.08f + fo.progress * 0.55f + Mathf.Sin(time * 9) * 0.03f;
                    foreach (var m in fo.ghostMats) Mats.SetAlpha(m, flick);
                    foreach (var m in fo.edgeMats) Mats.SetAlpha(m, 0.25f + fo.progress * 0.6f);
                    var lp = fo.group.transform.localPosition;
                    fo.group.transform.localPosition = new Vector3(lp.x, fo.baseY + MathX.Jit(0.02f) * (1 - fo.progress), lp.z);
                }
            }
            au.Set(au.focus, focusing != null ? 0.06f + focusing.progress * 0.08f : 0);
            au.focus.src.pitch = (180 + (focusing != null ? focusing.progress * 260 : 0)) / 220f;

            // dread: looking into darkness, at the scratches, at it, reading about it... closing your eyes lets it drain away
            float dd = 0;
            if (!eyes) dd -= 0.12f;
            else
            {
                if (gaze != null && gaze.Value.tag == "trace") dd += 0.06f;
                if (gaze != null && gaze.Value.dist > 9 && !ch.Lit) dd += 0.012f;
                if (hall && zone == ch.HallZone) dd += 0.055f;
                if (reading != null && readingForbidden) dd += 0.03f;
                if (dread > 0.12f && !hall) dd -= 0.008f;
            }
            dd += ch.DreadDelta(dt);
            dread = MathX.Clamp01(dread + dd * dt);
            for (int i = 0; i < w.heat.Length; i++) w.heat[i] *= 1 - dt * 0.02f;
            CorruptDoc();

            ch.Tick(dt);
            if (state != GState.Play && state != GState.Cutscene) return;

            float fl = hall && zone == ch.HallZone ? 0 : (dread > 0.6f && Random.value < dread * 0.08f ? 0.2f : ch.FlashBase);
            if (flashOverride >= 0) fl = flashOverride;
            flash.intensity = fl * World.LK * 2.6f;

            UpdateIt(dt);
            if (state != GState.Play && state != GState.Cutscene) return;

            // heartbeat and whispers rise with dread
            heartT -= dt;
            if (dread > 0.35f && heartT <= 0) { au.Heart(dread); heartT = 1.3f - dread * 0.75f; }
            au.Set(au.whispers, Mathf.Max(0, dread - 0.3f) * 0.25f * (eyes ? 1 : 1.5f));
            if (au.drone.lp) au.drone.lp.cutoffFrequency = 110 + dread * 220;
            // the building is never quite silent
            ambientT -= dt;
            if (ambientT <= 0) { ambientT = 6 + Random.value * 12 - dread * 4; au.Ambient(px, pz); }
            thoughtT -= dt;
            if (thoughtT <= 0) { thoughtT = 26 - dread * 18 + Random.value * 8; if ((dread > 0.1f || ch.Lit) && !cutscene) IntrusiveThought(); }

            // subtitles and hints
            if (subtitles.Count > 0) { subT += dt; if (subT > subtitles[0].dur) { subtitles.RemoveAt(0); subT = 0; } }
            for (int i = 0; i < hints.Count; i++) hints[i] = (hints[i].text, hints[i].t - dt);
            hints.RemoveAll(h => h.t <= 0);
            if (thought != null && (thoughtLeft -= dt) <= 0) thought = null;
        }

        void DrawHud()
        {
            bool play = state == GState.Play;
            hud.SetEyes(!eyes && (play || state == GState.Cutscene), Mathf.Max(0, Mathf.Sin(time * (2 + dread * 3))) * 0.05f);
            string label = null;
            if (near != null) label = near.label ?? (near.kind == "doc" ? "read" : "open the door");
            hud.SetReticle(play && eyes && reading == null, focusing != null ? focusing.progress : -1, label);
            if (subtitles.Count > 0) hud.SetSubtitle(subtitles[0].who, subtitles[0].text); else hud.SetSubtitle(null, null);
            var hs = new List<(string, float)>();
            for (int i = Mathf.Max(0, hints.Count - 2); i < hints.Count; i++) hs.Add((hints[i].text, Mathf.Min(1, hints[i].t)));
            hud.SetHints(hs);
            if (thought != null)
            {
                float a = Mathf.Min(1, thoughtLeft * 2) * (Random.value < 0.1f ? 0.3f : 1);
                hud.SetThought(thought, a, thoughtOff + new Vector2(MathX.Jit(7 * dread), MathX.Jit(7 * dread)), (34 + dread * 20) * 1.8f);
            }
            else hud.SetThought(null, 0, Vector2.zero, 10);
        }
    }
}
