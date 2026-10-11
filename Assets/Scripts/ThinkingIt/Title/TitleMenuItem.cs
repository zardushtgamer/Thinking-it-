using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThinkingIt
{
    // A title-menu entry: brightens, drifts right and grows an underline when hovered.
    public class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Text label;
        public Image underline;
        public System.Action onClick;
        bool hover;
        float k;
        Vector2 basePos;
        static readonly Color idle = MathX.Hex(0xb8b2a6), lit = MathX.Hex(0xf0eadc);

        void Start() { basePos = ((RectTransform)transform).anchoredPosition; }

        public void OnPointerEnter(PointerEventData e) { hover = true; Sfx.I?.Hover(); }
        public void OnPointerExit(PointerEventData e) { hover = false; }
        public void OnPointerClick(PointerEventData e) { Sfx.I?.Select(); onClick?.Invoke(); }

        void Update()
        {
            k = Mathf.MoveTowards(k, hover ? 1 : 0, Time.unscaledDeltaTime * 2.5f);
            float s = MathX.Smooth(k);
            if (label) label.color = Color.Lerp(idle, lit, s);
            ((RectTransform)transform).anchoredPosition = basePos + new Vector2(8 * s, 0);
            if (underline) underline.rectTransform.localScale = new Vector3(s, 1, 1);
        }
    }
}
