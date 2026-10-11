using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThinkingIt
{
    // Builds uGUI by code. Everything in the game's UI is made from these few pieces.
    public static class UIKit
    {
        public static readonly Color Ink = MathX.Hex(0xd8d4cc), Dim = MathX.Hex(0x8a857c), Faint = MathX.Hex(0x6a665f), Dark = MathX.Hex(0x4a463f);

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.6f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var m = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            try { m.AssignDefaultActions(); } catch { }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
            return r;
        }
        public static RectTransform Full(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // a box of given size anchored at a point (anchor in 0..1, pos in reference pixels)
        public static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var r = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            r.sizeDelta = size; r.anchoredPosition = pos;
            return r;
        }

        public static Image Img(Transform parent, string name, Color c, Sprite s = null)
        {
            var r = Full(parent, name);
            var i = r.gameObject.AddComponent<Image>(); i.color = c; i.sprite = s; i.raycastTarget = false;
            return i;
        }

        public static RawImage Raw(Transform parent, string name, Texture t, Color c)
        {
            var r = Full(parent, name);
            var i = r.gameObject.AddComponent<RawImage>(); i.texture = t; i.color = c; i.raycastTarget = false;
            return i;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter,
                                 TexText.Face face = TexText.Face.Serif, FontStyle style = FontStyle.Normal)
        {
            var r = Full(parent, name);
            var t = r.gameObject.AddComponent<Text>();
            t.font = TexText.Get(face); t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = style;
            t.text = text; t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            return t;
        }

        static Sprite frame, ring, dot, solid;
        public static Sprite Frame
        {
            get
            {
                if (frame) return frame;
                var p = new Painter(16, 16); p.StrokeRect(0.5f, 0.5f, 15, 15, 1, Color.white);
                var t = p.ToTexture2D(false, false); t.filterMode = FilterMode.Point;
                return frame = Sprite.Create(t, new UnityEngine.Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
            }
        }
        public static Sprite Ring
        {
            get
            {
                if (ring) return ring;
                var p = new Painter(128, 128); p.StrokeEllipse(64, 64, 58, 58, 0, 5, Color.white);
                return ring = Sprite.Create(p.ToTexture2D(false, true), new UnityEngine.Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            }
        }
        public static Sprite Dot
        {
            get
            {
                if (dot) return dot;
                var p = new Painter(32, 32); p.Circle(16, 16, 14, Color.white);
                return dot = Sprite.Create(p.ToTexture2D(false, true), new UnityEngine.Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            }
        }
        public static Sprite Solid
        {
            get
            {
                if (solid) return solid;
                var t = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; t.SetPixels(px); t.Apply();
                return solid = Sprite.Create(t, new UnityEngine.Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
        }

        // a bordered text button, like the web version's
        public static Button Btn(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action onClick)
        {
            var r = At(parent, "btn " + label, anchor, pos, size);
            var img = r.gameObject.AddComponent<Image>(); img.sprite = Frame; img.type = Image.Type.Sliced; img.color = MathX.Hex(0x555555);
            var b = r.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(2f, 2f, 2f, 1); colors.pressedColor = Color.gray; colors.selectedColor = Color.white; b.colors = colors;
            b.targetGraphic = img;
            var t = Label(r, "label", label, 26, MathX.Hex(0xdddddd));
            b.onClick.AddListener(() => { Sfx.I?.Select(); onClick?.Invoke(); });
            var hov = r.gameObject.AddComponent<HoverSound>();
            return b;
        }

        public static Slider MakeSlider(Transform parent, Vector2 anchor, Vector2 pos, float width, float min, float max, float value, System.Action<float> onChange)
        {
            var r = At(parent, "slider", anchor, pos, new Vector2(width, 24));
            var bg = Img(r, "bg", new Color(1, 1, 1, 0.15f), Solid);
            bg.rectTransform.anchorMin = new Vector2(0, 0.4f); bg.rectTransform.anchorMax = new Vector2(1, 0.6f);
            var fillArea = Rect(r, "fill area", new Vector2(0, 0.4f), new Vector2(1, 0.6f), Vector2.zero, Vector2.zero);
            var fill = Img(fillArea, "fill", MathX.Hex(0xb8b2a6), Solid);
            var handleArea = Rect(r, "handle area", Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            var handle = At(handleArea, "handle", new Vector2(0, 0.5f), Vector2.zero, new Vector2(16, 16));
            var hImg = handle.gameObject.AddComponent<Image>(); hImg.sprite = Dot; hImg.color = MathX.Hex(0xe8e2d6);
            var s = r.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle; s.targetGraphic = hImg;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static string Html(string s) => s;   // documents are already written with \n and <i>
    }

}
