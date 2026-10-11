using UnityEngine;
using UnityEngine.UI;

namespace ThinkingIt
{
    // slow breathing fade for "press any key"
    public class Breathe : MonoBehaviour
    {
        Text t; float a0;
        void Start() { t = GetComponent<Text>(); a0 = t.color.a; }
        void Update() { var c = t.color; c.a = a0 * (0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2 / 3.5f))); t.color = c; }
    }
}
