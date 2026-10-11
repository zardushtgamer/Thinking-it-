using UnityEngine;
using UnityEngine.EventSystems;

namespace ThinkingIt
{
    public class HoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData e) { Sfx.I?.Hover(); }
    }
}
