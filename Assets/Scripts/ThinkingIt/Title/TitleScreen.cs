using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ThinkingIt
{
    // The home screen. "Press any key", then the menu floats over a slow drift through the Waiting Room.
    // Something is sometimes standing in the corner.
    public class TitleScreen : MonoBehaviour
    {
        Camera cam; PostFX fx; World w; Chapter2 apt; ItModel it;
        float titleT, time, walkPhase, glitchT = 6.5f;
        bool inMenu;
        RectTransform splash, menu, mainList, settingsBox;
        CanvasGroup splashGroup, menuListGroup, footGroup;
        readonly List<(Text t, float delay)> letters = new List<(Text, float)>();
        readonly List<Text> glitchRed = new List<Text>(), glitchBlue = new List<Text>();
        Text tagline;
        string taglineFull = "don't think about it.";
        float typeT; int typed; float splashFade = -1;

        // camera keyframes: [x, z, lookX, lookZ] through the hallway, round the corner, into the living room
        static readonly float[][] KEYS = {
            new[] { 3f, 4f, 20f, 4f }, new[] { 12f, 4.2f, 12f, 1.6f }, new[] { 18.5f, 4.2f, 21f, 11f },
            new[] { 20.5f, 9f, 10f, 14f }, new[] { 16f, 13f, 3f, 16f }, new[] { 12f, 13.5f, 12f, 10f } };
        const float SEG = 11;

        void Start()
        {
            Sfx.Ensure();
            Mats.ClearCache(); MeshGen.ClearCache();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 60; cam.fieldOfView = 72;
            camGo.AddComponent<AudioListener>();
            fx = PostFX.Create(cam);

            w = new World("Title World");
            apt = new Chapter2 { w = w };
            apt.Build();
            it = new ItModel(w.root);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = apt.FogDensity; RenderSettings.fogColor = apt.FogColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = apt.Ambient * 1.4f;
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional && l.transform.root != w.root) l.enabled = false;

            Sfx.I.SilenceLoops(); Sfx.I.SetSpace(AudioReverbPreset.Livingroom);
            BuildUI();
        }

        // ------------------------------------------------------------------ UI
        void BuildUI()
        {
            UIKit.EnsureEventSystem();
            var canvas = UIKit.MakeCanvas("Title Canvas", 5).transform;

            // the left side darkens so the menu reads over the room
            var grad = new Painter(256, 4);
            for (int x = 0; x < 256; x++)
            {
                float u = x / 255f, a = u < 0.38f ? Mathf.Lerp(0.92f, 0.65f, u / 0.38f) : u < 0.7f ? Mathf.Lerp(0.65f, 0.1f, (u - 0.38f) / 0.32f) : Mathf.Lerp(0.1f, 0.35f, (u - 0.7f) / 0.3f);
                for (int y = 0; y < 4; y++) grad.Blend(x, y, new Color(0, 0, 0, a));
            }
            UIKit.Raw(canvas, "shade", grad.ToTexture2D(false, false), Color.white);

            // ---- menu ----
            menu = UIKit.Full(canvas, "menu");
            var titleRow = UIKit.Rect(menu, "title", new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.5f), Vector2.zero, Vector2.zero);
            titleRow.sizeDelta = new Vector2(1300, 200); titleRow.pivot = new Vector2(0, 0.5f); titleRow.anchoredPosition = new Vector2(0, 250);
            string title = "THINKING IT";
            var font = TexText.Serif; int size = 150; float x0 = 0;
            for (int i = 0; i < title.Length; i++)
            {
                string ch = title[i].ToString();
                float adv = ch == " " ? size * 0.35f : TexText.Measure(font, ch, size, FontStyle.Normal) + size * 0.18f;
                foreach (var layer in new[] { 0, 1, 2 })
                {
                    var t = UIKit.Label(titleRow, "letter", ch, size, layer == 0 ? MathX.Hex(0xece6da) : layer == 1 ? new Color(0.78f, 0.12f, 0.12f, 0.7f) : new Color(0.24f, 0.55f, 0.86f, 0.6f), TextAnchor.MiddleLeft);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(0, 1);
                    t.rectTransform.pivot = new Vector2(0, 0.5f); t.rectTransform.sizeDelta = new Vector2(adv + 20, 0);
                    t.rectTransform.anchoredPosition = new Vector2(x0 + (layer == 1 ? 5 : layer == 2 ? -5 : 0), 0);
                    if (layer == 0) letters.Add((t, 0.4f + i * 0.13f)); else { t.enabled = false; (layer == 1 ? glitchRed : glitchBlue).Add(t); }
                    if (layer > 0) t.transform.SetAsFirstSibling();
                }
                x0 += adv;
            }
            foreach (var l in letters) l.t.color = new Color(l.t.color.r, l.t.color.g, l.t.color.b, 0);

            tagline = UIKit.Label(menu, "tagline", "", 46, UIKit.Dim, TextAnchor.MiddleLeft, TexText.Face.Serif, FontStyle.Italic);
            tagline.rectTransform.anchorMin = tagline.rectTransform.anchorMax = new Vector2(0.08f, 0.5f);
            tagline.rectTransform.pivot = new Vector2(0, 0.5f); tagline.rectTransform.sizeDelta = new Vector2(1000, 60); tagline.rectTransform.anchoredPosition = new Vector2(8, 140);

            mainList = UIKit.Rect(menu, "list", new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.5f), Vector2.zero, Vector2.zero);
            mainList.pivot = new Vector2(0, 1); mainList.sizeDelta = new Vector2(900, 500); mainList.anchoredPosition = new Vector2(0, 60);
            menuListGroup = mainList.gameObject.AddComponent<CanvasGroup>(); menuListGroup.alpha = 0;
            AddItem(mainList, 0, "I", "Begin", "Arrival", () => SceneFlow.Chapter(1));
            AddItem(mainList, 1, "II", "Chapter Two", "The Waiting Room", () => SceneFlow.Chapter(2));
            AddItem(mainList, 2, "III", "Chapter Three", "The Archive", () => SceneFlow.Chapter(3));
            AddItem(mainList, 3, "IV", "Chapter Four", "The Lake House", () => SceneFlow.Chapter(4));
            AddItem(mainList, 4, "V", "Chapter Five", "The White Bear Room", () => SceneFlow.Chapter(5));
            AddItem(mainList, 5, "·", "Settings", null, () => { mainList.gameObject.SetActive(false); settingsBox.gameObject.SetActive(true); });
            AddItem(mainList, 6, "·", "Quit", null, () => { Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            });

            settingsBox = UIKit.Rect(menu, "settings", new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.5f), Vector2.zero, Vector2.zero);
            settingsBox.pivot = new Vector2(0, 1); settingsBox.sizeDelta = new Vector2(900, 400); settingsBox.anchoredPosition = new Vector2(0, 60);
            var vl = UIKit.Label(settingsBox, "vol", "Volume", 40, MathX.Hex(0xb8b2a6), TextAnchor.MiddleLeft);
            vl.rectTransform.anchorMin = vl.rectTransform.anchorMax = new Vector2(0, 1); vl.rectTransform.pivot = new Vector2(0, 0.5f); vl.rectTransform.sizeDelta = new Vector2(400, 60); vl.rectTransform.anchoredPosition = new Vector2(0, -40);
            UIKit.MakeSlider(settingsBox, new Vector2(0, 1), new Vector2(560, -40), 360, 0, 1, Sfx.Volume, v => Sfx.I?.SetVolume(v));
            var sl = UIKit.Label(settingsBox, "sens", "Mouse sensitivity", 40, MathX.Hex(0xb8b2a6), TextAnchor.MiddleLeft);
            sl.rectTransform.anchorMin = sl.rectTransform.anchorMax = new Vector2(0, 1); sl.rectTransform.pivot = new Vector2(0, 0.5f); sl.rectTransform.sizeDelta = new Vector2(400, 60); sl.rectTransform.anchoredPosition = new Vector2(0, -120);
            UIKit.MakeSlider(settingsBox, new Vector2(0, 1), new Vector2(560, -120), 360, 0.5f, 2, Settings.Sens, v => Settings.Sens = v);
            AddItem(settingsBox, 3, "←", "Back", null, () => { settingsBox.gameObject.SetActive(false); mainList.gameObject.SetActive(true); });
            settingsBox.gameObject.SetActive(false);

            var foot = UIKit.Label(menu, "foot", "WASD move · mouse look · E interact · hold SPACE to close your eyes · ESC pause · F9 skip chapter", 21, UIKit.Dark, TextAnchor.MiddleLeft, TexText.Face.Mono);
            foot.rectTransform.anchorMin = foot.rectTransform.anchorMax = new Vector2(0.08f, 0.5f); foot.rectTransform.pivot = new Vector2(0, 0.5f);
            foot.rectTransform.sizeDelta = new Vector2(1600, 40); foot.rectTransform.anchoredPosition = new Vector2(4, -470);
            footGroup = foot.gameObject.AddComponent<CanvasGroup>(); footGroup.alpha = 0;
            menu.gameObject.SetActive(false);

            // ---- splash ----
            splash = UIKit.Full(canvas, "splash");
            splash.gameObject.AddComponent<Image>().color = Color.black;
            splashGroup = splash.gameObject.AddComponent<CanvasGroup>();
            var press = UIKit.Label(splash, "press", "p r e s s   a n y   k e y", 46, MathX.Hex(0x9a958c), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);
            press.rectTransform.anchorMin = press.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); press.rectTransform.sizeDelta = new Vector2(1400, 80); press.rectTransform.anchoredPosition = new Vector2(0, 40);
            press.gameObject.AddComponent<Breathe>();
            var phones = UIKit.Label(splash, "phones", "( ◠ ‿ ◠ )\nbest with headphones, in the dark", 22, MathX.Hex(0x5a564f), TextAnchor.MiddleCenter, TexText.Face.Mono);
            phones.text = "best with headphones, in the dark";
            phones.rectTransform.anchorMin = phones.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); phones.rectTransform.sizeDelta = new Vector2(1400, 80); phones.rectTransform.anchoredPosition = new Vector2(0, -60);
        }

        void AddItem(RectTransform parent, int row, string num, string label, string sub, System.Action click)
        {
            var r = UIKit.Rect(parent, label, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(760, 64); r.anchoredPosition = new Vector2(0, -32 - row * 70);
            var hit = r.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
            var n = UIKit.Label(r, "num", num, 28, UIKit.Dark, TextAnchor.MiddleLeft);
            n.rectTransform.anchorMax = new Vector2(0, 1); n.rectTransform.sizeDelta = new Vector2(80, 0); n.rectTransform.pivot = new Vector2(0, 0.5f);
            var t = UIKit.Label(r, "label", label + (sub != null ? $"   <i><size=30><color=#6a665f>{sub}</color></size></i>" : ""), 54, MathX.Hex(0xb8b2a6), TextAnchor.MiddleLeft);
            t.rectTransform.offsetMin = new Vector2(84, 0);
            var ul = UIKit.Img(r, "underline", MathX.Hex(0xd8d2c4), UIKit.Solid);
            ul.rectTransform.anchorMin = new Vector2(0, 0); ul.rectTransform.anchorMax = new Vector2(0, 0); ul.rectTransform.pivot = new Vector2(0, 0);
            ul.rectTransform.sizeDelta = new Vector2(Mathf.Min(660, t.preferredWidth), 2); ul.rectTransform.anchoredPosition = new Vector2(84, 6);
            ul.rectTransform.localScale = new Vector3(0, 1, 1);
            var mi = r.gameObject.AddComponent<TitleMenuItem>(); mi.label = t; mi.underline = ul; mi.onClick = click;
        }

        void EnterMenu()
        {
            if (inMenu) return;
            inMenu = true;
            Sfx.I.StartMusic();
            splashFade = 0;
            menu.gameObject.SetActive(true);
            titleT = 0; typed = 0; typeT = 2.2f;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            time += dt;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (!inMenu && ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame))) EnterMenu();

            CameraDrift(dt);
            fx.Title(time);

            if (splashFade >= 0)
            {
                splashFade += dt / 1.4f;
                splashGroup.alpha = 1 - splashFade;
                if (splashFade >= 1) { splash.gameObject.SetActive(false); splashFade = -1; }
            }
            if (!inMenu) return;

            // letters fade in one by one, then the menu
            titleT += dt;
            foreach (var (t, delay) in letters)
            {
                float k = MathX.Clamp01((titleT - delay) / 2.4f);
                t.color = new Color(t.color.r, t.color.g, t.color.b, k);
                t.rectTransform.localScale = Vector3.one * (1 + (1 - k) * 0.06f);
            }
            menuListGroup.alpha = MathX.Clamp01((titleT - 2.2f) / 1.6f);
            footGroup.alpha = MathX.Clamp01((titleT - 3f) / 2f);
            // the tagline types itself out
            if (typed < taglineFull.Length && titleT > typeT) { typed++; typeT = titleT + 0.07f + Random.value * 0.06f; tagline.text = taglineFull.Substring(0, typed); }
            // now and then the title glitches, and the tagline loses a word
            glitchT -= dt;
            if (glitchT <= 0 && titleT > 4)
            {
                glitchT = 6.5f;
                float dur = 0.09f + Random.value * 0.12f;
                SetGlitch(true);
                Invoke(nameof(EndGlitch), dur);
                if (Random.value < 0.35f && typed >= taglineFull.Length) { tagline.text = "think about it."; Invoke(nameof(RestoreTagline), 0.26f); }
            }
        }

        void SetGlitch(bool on) { foreach (var t in glitchRed) t.enabled = on; foreach (var t in glitchBlue) t.enabled = on; }
        void EndGlitch() => SetGlitch(false);
        void RestoreTagline() => tagline.text = taglineFull;

        void CameraDrift(float dt)
        {
            float tt = time;
            float u = (tt / SEG) % 10; int i = Mathf.FloorToInt(u); float k = MathX.Smooth(u - i);
            var a = i < 5 ? KEYS[i] : KEYS[10 - i]; var b = i < 5 ? KEYS[i + 1] : KEYS[9 - i];
            float px = a[0] + (b[0] - a[0]) * k + Mathf.Sin(tt * 0.4f) * 0.1f;
            float pz = a[1] + (b[1] - a[1]) * k + Mathf.Cos(tt * 0.33f) * 0.1f;
            float lx = a[2] + (b[2] - a[2]) * k, lz = a[3] + (b[3] - a[3]) * k;
            float yaw = Mathf.Atan2(-(lx - px), -(lz - pz));
            float pitch = 0.02f + Mathf.Sin(tt * 0.25f) * 0.04f;
            walkPhase += dt * 1.4f;
            cam.transform.position = new Vector3(px, World.EYE + Mathf.Sin(walkPhase) * 0.035f, pz);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch)));
            // the glimpse: standing in the far corner, only while the camera is turned toward it, only some of the time
            bool glimpse = Mathf.FloorToInt(tt / (SEG * 10)) % 2 == 0 && ((i == 3 && k > 0.7f) || (i == 4 && k < 0.22f) || (i == 5 && k > 0.75f) || (i == 6 && k < 0.2f));
            it.SetVisible(glimpse);
            if (glimpse) { it.Place(World.cx(1) + 0.3f, World.cx(8) - 0.2f, px, pz, 0.12f); it.Flicker(Random.value > 0.04f); }
            apt.Ambience(tt, dt, Sfx.I, inMenu);
        }
    }

}
