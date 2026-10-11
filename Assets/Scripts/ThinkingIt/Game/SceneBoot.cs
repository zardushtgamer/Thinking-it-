using UnityEngine;

namespace ThinkingIt
{
    // Which scene is this? One of these sits in every scene; it builds the rest in code.
    public class SceneBoot : MonoBehaviour
    {
        public enum Mode { Title, Chapter }
        public Mode mode = Mode.Chapter;
        [Range(1, 5)] public int chapter = 1;

        void Start()
        {
            Sfx.Ensure();
            if (mode == Mode.Title) gameObject.AddComponent<TitleScreen>();
            else gameObject.AddComponent<Game>().Begin(chapter);
        }
    }

}
