using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThinkingIt
{
    // Everything drawn over the game: eyelids, reticle, subtitles, hints, intrusive thoughts,
    // documents, the pause and end screens, intro cards, and the moment it reaches you.
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        RawImage eyelids, caughtShape;
        Image black, transitionBlack, reticle, focusRing, caughtBlack;
        Text prompt, subWho, subText, thoughtText, thoughtRed, transitionLabel, caughtText;
        Image subBack;
        readonly Text[] hintTexts = new Text[2];
        RectTransform docPanel, pausePanel, endPanel, introPanel, settingsBox;
        Text docTitle, docBody, objectiveText, endTitle, endSub, endStats, endNext, endNote, cardText, skipText;
        Button nextBtn, againBtn;
        CanvasGroup cardGroup;
        System.Action onResume, onMenu, onNext, onAgain;

        public bool IntroRunning { get; private set; }
        bool skipRequested;

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var h = go.AddComponent<Hud>();
            h.Build();
            return h;
        }

        void Build()
        {
            UIKit.EnsureEventSystem();
            canvas = UIKit.MakeCanvas("HUD Canvas", 10);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // eyes closed: the inside of your eyelids
            var lid = new Painter(256, 256);
            lid.RadialFill(128, 128, 0, 180, (0, new Color(1, 1, 1, 1)), (1, new Color(0, 0, 0, 1)));
            eyelids = UIKit.Raw(root, "eyelids", lid.ToTexture2D(false, false), Color.black);
            eyelids.gameObject.SetActive(false);

            // reticle and focus ring
            reticle = new GameObject("reticle", typeof(RectTransform)).AddComponent<Image>();
            reticle.transform.SetParent(root, false); reticle.sprite = UIKit.Dot; reticle.color = new Color(0.86f, 0.84f, 0.8f, 0.5f);
            ((RectTransform)reticle.transform).sizeDelta = new Vector2(7, 7); reticle.raycastTarget = false;
            focusRing = new GameObject("focus", typeof(RectTransform)).AddComponent<Image>();
            focusRing.transform.SetParent(root, false); focusRing.sprite = UIKit.Ring; focusRing.color = new Color(0.9f, 0.92f, 0.96f, 0.75f);
            focusRing.type = Image.Type.Filled; focusRing.fillMethod = Image.FillMethod.Radial360; focusRing.fillOrigin = (int)Image.Origin360.Top; focusRing.fillClockwise = true;
            ((RectTransform)focusRing.transform).sizeDelta = new Vector2(54, 54); focusRing.raycastTarget = false;
            prompt = UIKit.Label(root, "prompt", "", 36, new Color(0.86f, 0.84f, 0.8f, 0.8f), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            prompt.rectTransform.sizeDelta = new Vector2(900, 60); prompt.rectTransform.anchoredPosition = new Vector2(0, -84);

            // subtitles
            subBack = UIKit.Img(root, "subBack", new Color(0, 0, 0, 0.55f), UIKit.Solid);
            subBack.rectTransform.anchorMin = subBack.rectTransform.anchorMax = new Vector2(0.5f, 0);
            subBack.rectTransform.sizeDelta = new Vector2(800, 62); subBack.rectTransform.anchoredPosition = new Vector2(0, 148);
            subText = UIKit.Label(root, "sub", "", 40, UIKit.Ink);
            subText.rectTransform.anchorMin = subText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            subText.rectTransform.sizeDelta = new Vector2(1500, 62); subText.rectTransform.anchoredPosition = new Vector2(0, 148);
            subWho = UIKit.Label(root, "who", "", 23, UIKit.Dim, TextAnchor.MiddleCenter, TexText.Face.Mono);
            subWho.rectTransform.anchorMin = subWho.rectTransform.anchorMax = new Vector2(0.5f, 0);
            subWho.rectTransform.sizeDelta = new Vector2(800, 30); subWho.rectTransform.anchoredPosition = new Vector2(0, 198);

            // hints
            for (int i = 0; i < 2; i++)
            {
                var t = UIKit.Label(root, "hint" + i, "", 34, new Color(0.78f, 0.77f, 0.74f, 0.85f), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1);
                t.rectTransform.sizeDelta = new Vector2(1700, 50); t.rectTransform.anchoredPosition = new Vector2(0, -108 - i * 50);
                hintTexts[i] = t;
            }

            // intrusive thoughts: big, slightly doubled in red
            thoughtRed = UIKit.Label(root, "thoughtRed", "", 64, new Color(0.78f, 0.16f, 0.16f, 0.4f));
            thoughtText = UIKit.Label(root, "thought", "", 64, new Color(0.92f, 0.9f, 0.87f, 1));
            foreach (var t in new[] { thoughtText, thoughtRed }) { t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); t.rectTransform.sizeDelta = new Vector2(1600, 120); }

            // the door in the waiting room: darkness, then the same hallway
            transitionBlack = UIKit.Img(root, "transition", new Color(0, 0, 0, 0), UIKit.Solid);
            transitionLabel = UIKit.Label(transitionBlack.transform, "n", "", 30, new Color(0.63f, 0.6f, 0.58f, 0), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);

            // caught
            caughtBlack = UIKit.Img(root, "caught", new Color(0, 0, 0, 0), UIKit.Solid);
            var shape = new Painter(256, 512);
            shape.Ellipse(128 + 14 * 0.6f, 120, 42, 54, -0.4f, Color.black);
            shape.StrokeEllipse(128 + 14 * 0.6f, 120, 42, 54, -0.4f, 3, new Color(0.78f, 0.8f, 0.84f, 1));
            shape.Ellipse(128, 330, 72, 150, 0, Color.black);
            shape.StrokeEllipse(128, 330, 72, 150, 0, 3, new Color(0.78f, 0.8f, 0.84f, 1));
            caughtShape = UIKit.Raw(caughtBlack.transform, "shape", shape.ToTexture2D(false, true), Color.white);
            caughtShape.rectTransform.anchorMin = caughtShape.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caughtShape.rectTransform.sizeDelta = new Vector2(256, 512);
            caughtText = UIKit.Label(caughtBlack.transform, "text", "you thought about it", 60, new Color(0.86f, 0.84f, 0.8f, 0), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);
            caughtBlack.gameObject.SetActive(false);

            // documents
            docPanel = UIKit.Full(root, "doc");
            var dim = docPanel.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.75f);
            var paper = UIKit.At(docPanel, "paper", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1100, 760));
            paper.localRotation = Quaternion.Euler(0, 0, 0.6f);
            var paperImg = paper.gameObject.AddComponent<Image>(); paperImg.color = MathX.Hex(0xe9e3d3);
            var shadow = paper.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, 0.8f); shadow.effectDistance = new Vector2(0, -20);
            docTitle = UIKit.Label(paper, "title", "", 46, MathX.Hex(0x26221c), TextAnchor.UpperLeft, TexText.Face.Serif, FontStyle.Bold);
            docTitle.rectTransform.offsetMin = new Vector2(70, 0); docTitle.rectTransform.offsetMax = new Vector2(-70, -54);
            docBody = UIKit.Label(paper, "body", "", 27, MathX.Hex(0x26221c), TextAnchor.UpperLeft, TexText.Face.Mono);
            docBody.rectTransform.offsetMin = new Vector2(70, 50); docBody.rectTransform.offsetMax = new Vector2(-70, -130);
            docBody.lineSpacing = 1.25f;
            var docHint = UIKit.Label(docPanel, "hint", "E to put it down", 22, MathX.Hex(0x888888), TextAnchor.MiddleCenter, TexText.Face.Mono);
            docHint.rectTransform.anchorMin = docHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            docHint.rectTransform.sizeDelta = new Vector2(600, 40); docHint.rectTransform.anchoredPosition = new Vector2(0, -420);
            docPanel.gameObject.SetActive(false);

            // pause
            pausePanel = UIKit.Full(root, "pause");
            pausePanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.88f);
            var ph = UIKit.Label(pausePanel, "h", "P A U S E D", 72, UIKit.Ink);
            ph.rectTransform.anchorMin = ph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ph.rectTransform.sizeDelta = new Vector2(1000, 100); ph.rectTransform.anchoredPosition = new Vector2(0, 230);
            objectiveText = UIKit.Label(pausePanel, "objective", "", 42, MathX.Hex(0xcfc8ba), TextAnchor.MiddleCenter, TexText.Face.Serif, FontStyle.Italic);
            objectiveText.rectTransform.anchorMin = objectiveText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); objectiveText.rectTransform.sizeDelta = new Vector2(1500, 70); objectiveText.rectTransform.anchoredPosition = new Vector2(0, 140);
            UIKit.Btn(pausePanel, "RESUME", new Vector2(0.5f, 0.5f), new Vector2(-150, 50), new Vector2(260, 58), () => onResume?.Invoke());
            UIKit.Btn(pausePanel, "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(150, 50), new Vector2(260, 58), () => onMenu?.Invoke());
            settingsBox = UIKit.At(pausePanel, "settings", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(700, 110));
            var vl = UIKit.Label(settingsBox, "vol", "Volume", 28, MathX.Hex(0xb8b2a6), TextAnchor.MiddleLeft);
            vl.rectTransform.anchorMin = new Vector2(0, 0.5f); vl.rectTransform.anchorMax = new Vector2(0.4f, 1);
            UIKit.MakeSlider(settingsBox, new Vector2(0.7f, 0.75f), Vector2.zero, 360, 0, 1, Sfx.Volume, v => Sfx.I?.SetVolume(v));
            var sl = UIKit.Label(settingsBox, "sens", "Mouse sensitivity", 28, MathX.Hex(0xb8b2a6), TextAnchor.MiddleLeft);
            sl.rectTransform.anchorMin = new Vector2(0, 0); sl.rectTransform.anchorMax = new Vector2(0.4f, 0.5f);
            UIKit.MakeSlider(settingsBox, new Vector2(0.7f, 0.25f), Vector2.zero, 360, 0.5f, 2, Settings.Sens, v => Settings.Sens = v);
            var ps = UIKit.Label(pausePanel, "small", "Look at something long enough and it becomes real.\nSome things should not be looked at.\nHold SPACE to close your eyes.", 24, MathX.Hex(0x6a665f), TextAnchor.MiddleCenter, TexText.Face.Mono);
            ps.rectTransform.anchorMin = ps.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ps.rectTransform.sizeDelta = new Vector2(1400, 140); ps.rectTransform.anchoredPosition = new Vector2(0, -230);
            pausePanel.gameObject.SetActive(false);

            // end of chapter
            endPanel = UIKit.Full(root, "end");
            endPanel.gameObject.AddComponent<Image>().color = Color.black;
            endTitle = Centered(endPanel, "title", 84, UIKit.Ink, 260, FontStyle.Normal);
            endSub = Centered(endPanel, "sub", 44, UIKit.Dim, 175, FontStyle.Italic);
            endStats = Centered(endPanel, "stats", 27, UIKit.Dim, 30, FontStyle.Normal, TexText.Face.Mono, 240);
            endNext = Centered(endPanel, "next", 36, UIKit.Dim, -120, FontStyle.Italic);
            endNote = Centered(endPanel, "note", 24, UIKit.Faint, -165, FontStyle.Normal, TexText.Face.Mono);
            nextBtn = UIKit.Btn(endPanel, "CONTINUE", new Vector2(0.5f, 0.5f), new Vector2(-380, -260), new Vector2(300, 58), () => onNext?.Invoke());
            againBtn = UIKit.Btn(endPanel, "PLAY THIS CHAPTER AGAIN", new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(420, 58), () => onAgain?.Invoke());
            UIKit.Btn(endPanel, "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(380, -260), new Vector2(300, 58), () => onMenu?.Invoke());
            endPanel.gameObject.SetActive(false);

            // full-screen black (fades)
            black = UIKit.Img(root, "black", new Color(0, 0, 0, 0), UIKit.Solid);

            // intro cards
            introPanel = UIKit.Full(root, "intro");
            introPanel.gameObject.AddComponent<Image>().color = Color.black;
            cardText = UIKit.Label(introPanel, "card", "", 58, UIKit.Ink);
            cardText.rectTransform.anchorMin = cardText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); cardText.rectTransform.sizeDelta = new Vector2(1650, 700);
            cardGroup = cardText.gameObject.AddComponent<CanvasGroup>();
            skipText = UIKit.Label(introPanel, "skip", "SPACE to skip", 22, MathX.Hex(0x444444), TextAnchor.LowerRight, TexText.Face.Mono);
            skipText.rectTransform.offsetMin = new Vector2(0, 30); skipText.rectTransform.offsetMax = new Vector2(-40, 0);
            introPanel.gameObject.SetActive(false);
        }

        Text Centered(Transform p, string name, int size, Color c, float y, FontStyle st, TexText.Face face = TexText.Face.Serif, float h = 100)
        {
            var t = UIKit.Label(p, name, "", size, c, TextAnchor.MiddleCenter, face, st);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(1700, h); t.rectTransform.anchoredPosition = new Vector2(0, y);
            return t;
        }

        // ------------------------------------------------------------------ per-frame state
        public void SetEyes(bool closed, float beat)
        {
            eyelids.gameObject.SetActive(closed);
            if (closed) eyelids.color = new Color(Mathf.Clamp01((24 + beat * 200) / 255f), 4 / 255f, 4 / 255f, 1);
        }

        public void SetReticle(bool show, float focus, string promptText)
        {
            reticle.enabled = show;
            focusRing.enabled = show && focus >= 0;
            if (focus >= 0) focusRing.fillAmount = focus;
            prompt.text = show && promptText != null ? "E  —  " + promptText : "";
        }

        public void SetSubtitle(string who, string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            subBack.enabled = on; subText.text = on ? text : ""; subWho.text = on ? who : "";
            if (on) subBack.rectTransform.sizeDelta = new Vector2(Mathf.Min(1800, subText.preferredWidth + 44), 62);
        }

        public void SetHints(List<(string text, float alpha)> hs)
        {
            for (int i = 0; i < 2; i++)
            {
                if (i < hs.Count) { hintTexts[i].text = hs[i].text; var c = hintTexts[i].color; c.a = hs[i].alpha * 0.85f; hintTexts[i].color = c; }
                else hintTexts[i].text = "";
            }
        }

        public void SetThought(string text, float alpha, Vector2 offset, float size)
        {
            if (string.IsNullOrEmpty(text)) { thoughtText.text = thoughtRed.text = ""; return; }
            thoughtText.text = thoughtRed.text = text;
            thoughtText.fontSize = thoughtRed.fontSize = Mathf.RoundToInt(size);
            thoughtText.color = new Color(0.92f, 0.9f, 0.87f, alpha);
            thoughtRed.color = new Color(0.78f, 0.16f, 0.16f, alpha * 0.4f);
            thoughtText.rectTransform.anchoredPosition = offset;
            thoughtRed.rectTransform.anchoredPosition = offset + new Vector2(4, 0);
        }

        public void SetTransition(float a, string label, float labelAlpha)
        {
            transitionBlack.color = new Color(0, 0, 0, Mathf.Clamp01(a));
            transitionLabel.text = label ?? "";
            transitionLabel.color = new Color(0.63f, 0.6f, 0.58f, labelAlpha);
        }

        public void SetBlack(float a) { black.color = new Color(0, 0, 0, Mathf.Clamp01(a)); }

        public void SetCaught(bool on, float k, float time)
        {
            caughtBlack.gameObject.SetActive(on);
            if (!on) return;
            caughtBlack.color = new Color(0, 0, 0, Mathf.Min(1, k * 1.5f));
            bool showShape = k < 1.4f;
            caughtShape.enabled = showShape;
            if (showShape)
            {
                float sc = (1 + k) * 1.6f;
                caughtShape.rectTransform.localScale = new Vector3(sc, sc, 1);
                caughtShape.rectTransform.anchoredPosition = new Vector2(MathX.Jit(54), MathX.Jit(54));
                caughtShape.color = new Color(1, 1, 1, 1);
            }
            caughtText.color = new Color(0.86f, 0.84f, 0.8f, k > 1.6f ? Mathf.Min(1, (k - 1.6f) * 1.2f) : 0);
        }

        public void HideGameplay()
        {
            SetReticle(false, -1, null); SetSubtitle(null, null); SetHints(new List<(string, float)>()); SetThought(null, 0, Vector2.zero, 10);
            SetEyes(false, 0); SetTransition(0, null, 0);
        }

        // ------------------------------------------------------------------ panels
        public void ShowDoc(Doc d)
        {
            docTitle.text = d.title; docBody.text = d.body;
            docPanel.gameObject.SetActive(true);
        }
        public void SetDocBody(string body) { docBody.text = body; }
        public void HideDoc() { docPanel.gameObject.SetActive(false); }
        public bool DocOpen => docPanel.gameObject.activeSelf;

        public void ShowPause(string objective, System.Action resume, System.Action menu)
        {
            objectiveText.text = objective; onResume = resume; onMenu = menu;
            pausePanel.gameObject.SetActive(true);
        }
        public void HidePause() { pausePanel.gameObject.SetActive(false); }

        public void ShowEnd(string title, string sub, string stats, string next, string note, System.Action next1, System.Action again, System.Action menu, string nextLabel = "CONTINUE")
        {
            endTitle.text = title; endSub.text = sub; endStats.text = stats; endNext.text = next ?? ""; endNote.text = note ?? "";
            onNext = next1; onAgain = again; onMenu = menu;
            nextBtn.gameObject.SetActive(next1 != null);
            nextBtn.GetComponentInChildren<Text>().text = nextLabel;
            endPanel.gameObject.SetActive(true);
        }
        public void HideEnd() { endPanel.gameObject.SetActive(false); }
        public void SetEndNext(string t) { endNext.text = t; }

        // ------------------------------------------------------------------ intro cards
        public void PlayCards(Card[] cards, System.Action done)
        {
            StopAllCoroutines();
            StartCoroutine(Cards(cards, done));
        }
        public void SkipCards() { if (IntroRunning) skipRequested = true; }

        IEnumerator Cards(Card[] cards, System.Action done)
        {
            IntroRunning = true; skipRequested = false;
            introPanel.gameObject.SetActive(true);
            cardGroup.alpha = 0;
            foreach (var c in cards)
            {
                // fade out, swap, fade in, hold
                for (float t = 0; t < 0.9f && !skipRequested; t += Time.unscaledDeltaTime) { cardGroup.alpha = Mathf.Min(cardGroup.alpha, 1 - t / 0.9f); yield return null; }
                if (skipRequested) break;
                cardText.text = c.text;
                for (float t = 0; t < 1.6f && !skipRequested; t += Time.unscaledDeltaTime) { cardGroup.alpha = t / 1.6f; yield return null; }
                cardGroup.alpha = 1;
                for (float t = 0; t < c.dur && !skipRequested; t += Time.unscaledDeltaTime) yield return null;
                if (skipRequested) break;
            }
            introPanel.gameObject.SetActive(false);
            IntroRunning = false;
            done?.Invoke();
        }
    }

    // player settings, remembered between sessions
    public static class Settings
    {
        public static float Sens
        {
            get => PlayerPrefs.GetFloat("ti_sens", 1f);
            set => PlayerPrefs.SetFloat("ti_sens", value);
        }
        // which forbidden documents have ever been read, across every chapter (the hidden ending needs all of them)
        public static System.Collections.Generic.HashSet<string> ForbiddenEverRead =>
            new System.Collections.Generic.HashSet<string>(PlayerPrefs.GetString("ti_forbidden", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        public static void MarkForbidden(string id)
        {
            var set = ForbiddenEverRead;
            if (set.Add(id)) { PlayerPrefs.SetString("ti_forbidden", string.Join(",", set)); PlayerPrefs.Save(); }
        }
        public static int ForbiddenTotal { get { int n = 0; foreach (var d in GameData.Docs.Values) if (d.forbidden) n++; return n; } }
        public static bool AllForbiddenRead { get { var s = ForbiddenEverRead; foreach (var kv in GameData.Docs) if (kv.Value.forbidden && !s.Contains(kv.Key)) return false; return true; } }

        public static int Progress
        {
            get => PlayerPrefs.GetInt("ti_progress", 1);
            set { if (value > Progress) PlayerPrefs.SetInt("ti_progress", value); }
        }
    }
}
