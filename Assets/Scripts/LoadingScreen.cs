using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The loading screen between every scene. Builds its own UI, shows a thin bar, then loads
// whatever ThinkingIt.SceneFlow asked for (the title screen when the game first starts).
public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "Title";
    [SerializeField] private float minimumLoadTime = 1.5f;
    [SerializeField] private float barWidth = 520f;
    [SerializeField] private float barHeight = 2f;

    private Image fill;
    private Text label;

    private void Awake()
    {
        if (Camera.main == null)
        {
            var camGO = new GameObject("Loading Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
        }
        BuildUI();
    }

    private void Start() => StartCoroutine(LoadRoutine());

    private void BuildUI()
    {
        var canvasGO = new GameObject("Loading Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var bg = CreateImage("Background", canvasGO.transform, Color.black);
        Stretch(bg.rectTransform);

        label = CreateText("Label", canvasGO.transform, "don't think about it", 34);
        label.fontStyle = FontStyle.Italic;
        SetCentered(label.rectTransform, new Vector2(0, 10), new Vector2(barWidth * 2, 60));

        var barBG = CreateImage("Bar Background", canvasGO.transform, new Color(1, 1, 1, 0.08f));
        SetCentered(barBG.rectTransform, new Vector2(0, -40), new Vector2(barWidth, barHeight));

        fill = CreateImage("Bar Fill", barBG.transform, new Color(0.85f, 0.83f, 0.8f, 0.9f));
        fill.type = Image.Type.Filled;
        fill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        Stretch(fill.rectTransform);
    }

    private IEnumerator LoadRoutine()
    {
        string target = string.IsNullOrEmpty(ThinkingIt.SceneFlow.Next) ? sceneToLoad : ThinkingIt.SceneFlow.Next;
        ThinkingIt.SceneFlow.Next = null;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            label.text = "Scene \"" + target + "\" is not in the build list.\nRun Tools > Thinking It > Setup Project.";
            yield break;
        }
        yield return null;
        AsyncOperation op = SceneManager.LoadSceneAsync(target);
        op.allowSceneActivation = false;

        float timer = 0f;
        while (true)
        {
            timer += Time.unscaledDeltaTime;
            float real = Mathf.Clamp01(op.progress / 0.9f);          // async progress stops at 0.9
            float fake = Mathf.Clamp01(timer / minimumLoadTime);
            float shown = Mathf.Min(real, fake);
            fill.fillAmount = shown;
            label.color = new Color(0.6f, 0.58f, 0.55f, 0.5f + Mathf.Sin(timer * 2f) * 0.3f);
            if (shown >= 1f) break;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.25f);
        op.allowSceneActivation = true;
    }

    // ---------- helpers ----------
    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text CreateText(string name, Transform parent, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.6f, 0.58f, 0.55f);
        t.font = ThinkingIt.TexText.Serif;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetCentered(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
