using UnityEngine;

namespace ThinkingIt
{
    // Sits on the "World" object of a chapter layout scene (made by Tools > Thinking It > Layout).
    // It only tells the layout tools which chapter this scene belongs to.
    public class LayoutMarker : MonoBehaviour
    {
        public int chapter = 1;
    }
}
