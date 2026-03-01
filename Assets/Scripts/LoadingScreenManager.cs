using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Unity splash sonrasi yukleme ekrani.
/// Bos bir sahneye bos bir GameObject ekle, bu scripti at.
/// UI tamamen koddan olusturulur, Inspector'da bir sey atamana gerek yok.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    [SerializeField] private string targetScene = "SampleScene";
    [SerializeField] private float minimumLoadTime = 2.5f;
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.1f, 1f);
    [SerializeField] private Color barColor = new Color(0.3f, 0.75f, 0.95f, 1f);
    [SerializeField] private Color barBgColor = new Color(0.15f, 0.15f, 0.2f, 1f);
    [Tooltip("Opsiyonel. Atarsan arka plan olarak bu sprite kullanilir.")]
    public Sprite backgroundSprite;

    private static readonly string[] Tips =
    {
        "Oteli olan mülkler satın alınamaz!",
        "Start'tan geçmeden 4. evi dikemezsin.",
        "Rakibinin mülkünü 2x kira ödeyerek satın alabilirsin.",
        "Şans kartları seni zengin de fakir de yapabilir!",
        "Ev sayısı arttıkça kira katlanarak yükselir.",
        "Strateji önemli: önce ucuz mülkleri topla!",
    };

    private Image _progressBar;
    private Text _statusText;
    private Text _tipText;
    private CanvasGroup _fadeGroup;

    private static Texture2D _whitePixel;

    private void Start()
    {
        BuildUI();
        StartCoroutine(LoadSequence());
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("LoadingCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _fadeGroup = canvasGo.AddComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = backgroundColor;
        if (backgroundSprite != null)
        {
            bgImg.sprite = backgroundSprite;
            bgImg.color = Color.white;
            bgImg.preserveAspect = false;
            bgImg.type = Image.Type.Simple;
        }

        var titleGo = CreateTextObj(canvasGo.transform, "Title", "Brom City Travellers",
            new Vector2(0.5f, 0.6f), new Vector2(600, 60), 42, FontStyle.Bold, Color.white);

        var tipGo = CreateTextObj(canvasGo.transform, "Tip", "",
            new Vector2(0.5f, 0.48f), new Vector2(700, 40), 20, FontStyle.Italic, new Color(0.7f, 0.7f, 0.8f));
        _tipText = tipGo.GetComponent<Text>();

        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(canvasGo.transform, false);
        var barBgRt = barBgGo.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.5f, 0.32f);
        barBgRt.anchorMax = new Vector2(0.5f, 0.32f);
        barBgRt.pivot = new Vector2(0.5f, 0.5f);
        barBgRt.sizeDelta = new Vector2(500, 16);
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.color = barBgColor;

        var barGo = new GameObject("ProgressBar");
        barGo.transform.SetParent(barBgGo.transform, false);
        var barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin = Vector2.zero;
        barRt.anchorMax = Vector2.one;
        barRt.offsetMin = Vector2.zero;
        barRt.offsetMax = Vector2.zero;
        _progressBar = barGo.AddComponent<Image>();
        _progressBar.color = barColor;
        _progressBar.type = Image.Type.Filled;
        _progressBar.fillMethod = Image.FillMethod.Horizontal;
        _progressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        _progressBar.fillAmount = 0f;
        // Sprite yoksa Filled bazi surumlerde gorunmuyor; built-in 1x1 beyaz kullan
        if (_progressBar.sprite == null)
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            _progressBar.sprite = Sprite.Create(_whitePixel, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f));
        }

        var statusGo = CreateTextObj(canvasGo.transform, "Status", "Yükleniyor...",
            new Vector2(0.5f, 0.26f), new Vector2(400, 30), 18, FontStyle.Normal, new Color(0.8f, 0.8f, 0.85f));
        _statusText = statusGo.GetComponent<Text>();
    }

    private GameObject CreateTextObj(Transform parent, string name, string content,
        Vector2 anchorPos, Vector2 size, int fontSize, FontStyle style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var txt = go.AddComponent<Text>();
        txt.text = content;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    private IEnumerator LoadSequence()
    {
        if (_tipText != null)
            _tipText.text = Tips[Random.Range(0, Tips.Length)];

        float fadeIn = 0f;
        while (fadeIn < 0.5f)
        {
            fadeIn += Time.unscaledDeltaTime;
            _fadeGroup.alpha = Mathf.Clamp01(fadeIn / 0.5f);
            yield return null;
        }
        _fadeGroup.alpha = 1f;

        var op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        const float stallAt40Duration = 0.8f;
        const float stallAt70Duration = 0.6f;
        float elapsed = 0f;
        bool passedStall40 = false, passedStall70 = false;
        float stall40Timer = 0f, stall70Timer = 0f;

        while (op.progress < 0.9f || elapsed < minimumLoadTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float realProgress = Mathf.Clamp01(op.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsed / minimumLoadTime);
            float rawTarget = Mathf.Min(realProgress, timeProgress);

            // %40 ve %70'te yalandan takilma
            float displayProgress;
            if (rawTarget >= 0.4f && !passedStall40)
            {
                displayProgress = 0.4f;
                stall40Timer += Time.unscaledDeltaTime;
                if (stall40Timer >= stallAt40Duration)
                    passedStall40 = true;
            }
            else if (rawTarget >= 0.7f && passedStall40 && !passedStall70)
            {
                displayProgress = 0.7f;
                stall70Timer += Time.unscaledDeltaTime;
                if (stall70Timer >= stallAt70Duration)
                    passedStall70 = true;
            }
            else
            {
                displayProgress = rawTarget;
            }

            if (_progressBar != null)
                _progressBar.fillAmount = displayProgress;
            if (_statusText != null)
                _statusText.text = $"Yükleniyor... {Mathf.RoundToInt(displayProgress * 100)}%";

            yield return null;
        }

        if (_progressBar != null) _progressBar.fillAmount = 1f;
        if (_statusText != null) _statusText.text = "Yükleniyor... 100%";

        // Canvas'i sahne gecisinde hayatta tut; bu script'i de canvas'a tasi ki coroutine kesilmesin
        var canvasRoot = _fadeGroup.gameObject;
        DontDestroyOnLoad(canvasRoot);
        transform.SetParent(canvasRoot.transform, true);

        op.allowSceneActivation = true;

        // Yeni sahnenin tam yüklenmesini bekle
        while (!op.isDone)
            yield return null;

        // Birkaç frame bekle ki yeni sahne renderlansin
        yield return null;
        yield return null;

        // Artik yeni sahne ekranda, güvenle fade-out yap
        float fadeOut = 0f;
        while (fadeOut < 0.4f)
        {
            fadeOut += Time.unscaledDeltaTime;
            _fadeGroup.alpha = 1f - Mathf.Clamp01(fadeOut / 0.4f);
            yield return null;
        }

        Destroy(_fadeGroup.gameObject);
    }
}
