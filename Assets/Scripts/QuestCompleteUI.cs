using UnityEngine;
using UnityEngine.Video;
using TMPro;
using System.Collections;

public class QuestCompleteUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private RectTransform xpRect;

    [Tooltip("Material used by the video quad/plane (sampling the RenderTexture). We'll tint RGB down to fade out.")]
    [SerializeField] private Material videoMaterial;

    [Tooltip("Transform of the object that shows the video (Quad, UI RawImage, etc). We'll scale this from 0.5x → 1x.")]
    [SerializeField] private Transform videoTransform;

    [Header("Video Player (optional)")]
    [SerializeField] private VideoPlayer videoPlayer;          // Assign if you want this script to control playback.
    [SerializeField] private bool restartVideoOnPlay = true;

    [Header("Timings")]
    [SerializeField] private float titleInTime = 0.55f;        // title pop-in duration
    [SerializeField] private float xpInTime = 0.45f;           // XP slide+fade-in duration
    [SerializeField] private float holdTime = 3.0f;            // time fully readable
    [SerializeField] private float fadeOutTime = 0.6f;         // fade-out duration (texts + video)

    [Header("Title Motion")]
    [SerializeField] private float titleStartScale = 0.5f;     // title starts smaller
    [SerializeField] private float titleMaxFontSize = 100f;

    [Header("XP Motion")]
    [SerializeField] private float xpSlideDistance = 120f;     // px it travels up from
    [SerializeField] private float xpStartAlpha = 0f;

    [Header("Video Appearance")]
    [Tooltip("Color/tint property on your video shader. URP Unlit = _BaseColor; Legacy = _Color")]
    [SerializeField] private string videoColorProperty = "_BaseColor";
    [SerializeField] private float videoStartIntensity = 1f;   // 1 = full RGB; we fade this to 0 on exit
    [SerializeField] private float videoStartScale = 0.5f;     // starts half-size
    [SerializeField] private float videoEndScale = 1.0f;       // grows to full size right before fade

    [Header("Debug / Testing")]
    [SerializeField] private bool simulateOnStart = false;
    [SerializeField] private KeyCode testKey = KeyCode.F9;
    [SerializeField] private int testXp = 1000;
    [SerializeField] private string testTitle = "QUEST COMPLETE";

    // cache
    private Vector2 xpStartAnchored;
    private Coroutine running;

    void Reset()
    {
        titleText = GetComponentInChildren<TextMeshProUGUI>();
        var tmps = GetComponentsInChildren<TextMeshProUGUI>();
        if (tmps.Length > 1)
            xpText = tmps[tmps.Length - 1];
        xpRect = xpText ? xpText.rectTransform : null;
    }

    void Awake()
    {
        if (xpText) xpRect = xpText.rectTransform;
        if (xpRect != null) xpStartAnchored = xpRect.anchoredPosition;
        HideImmediate();
    }

    void Start()
    {
        if (simulateOnStart)
            Play(testXp, testTitle);
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey))
            Play(testXp, testTitle);
    }

    [ContextMenu("Test ▶ Play")]
    private void ContextTestPlay() => Play(testXp, testTitle);

    /// <summary>External trigger: call this when a quest completes.</summary>
    public void Play(int xpAmount, string title = "QUEST COMPLETE")
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine(xpAmount, title));
    }

    private IEnumerator PlayRoutine(int xpAmount, string title)
    {
        // --- Prepare UI text ---
        titleText.text = title;
        titleText.fontSize = titleMaxFontSize;
        xpText.text = $"{xpAmount:N0} XP";

        // Title start state
        titleText.alpha = 0f;
        titleText.rectTransform.localScale = Vector3.one * titleStartScale;

        // XP start state (below + transparent)
        xpText.alpha = xpStartAlpha;
        Vector2 xpBelow = xpStartAnchored + new Vector2(0f, -xpSlideDistance);
        xpRect.anchoredPosition = xpBelow;

        // Video: set starting intensity and scale
        SetVideoIntensity(videoStartIntensity);
        if (videoTransform) videoTransform.localScale = Vector3.one * videoStartScale;

        // --- Ensure the VideoPlayer is actually playing frames ---
        if (videoPlayer && restartVideoOnPlay)
        {
            videoPlayer.Stop();
            // Rewind safely for all backends
            try { videoPlayer.frame = 0; } catch {}
            videoPlayer.time = 0.0;
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;
            videoPlayer.Play();
        }

        // --- Animate IN (texts) while video is already visible ---
        float t = 0f;
        float maxIn = Mathf.Max(titleInTime, xpInTime);
        while (t < maxIn)
        {
            t += Time.unscaledDeltaTime;

            // Title: scale pop + fade in
            float ntTitle = Mathf.Clamp01(t / titleInTime);
            float s = EaseOutBack(ntTitle);
            titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(titleStartScale, 1f, s);
            titleText.alpha = Mathf.SmoothStep(0f, 1f, ntTitle);

            // XP: slide up + fade in
            float ntXp = Mathf.Clamp01(t / xpInTime);
            xpText.alpha = Mathf.SmoothStep(0f, 1f, ntXp);
            xpRect.anchoredPosition = Vector2.Lerp(xpBelow, xpStartAnchored, EaseOutCubic(ntXp));

            // Video growth starts now and continues through the hold
            if (videoTransform)
            {
                // progress across IN+HOLD (we'll also advance during hold below)
                float totalVisible = titleInTime + holdTime;
                float pg = Mathf.Clamp01(Mathf.Min(t, totalVisible) / totalVisible);
                float vs = Mathf.Lerp(videoStartScale, videoEndScale, EaseOutCubic(pg));
                videoTransform.localScale = Vector3.one * vs;
            }

            yield return null;
        }

        // --- HOLD fully formed (keep growing video toward final size) ---
        float held = 0f;
        while (held < holdTime)
        {
            held += Time.unscaledDeltaTime;

            if (videoTransform)
            {
                float totalVisible = titleInTime + holdTime;
                float pg = Mathf.Clamp01((titleInTime + held) / totalVisible);
                float vs = Mathf.Lerp(videoStartScale, videoEndScale, EaseOutCubic(pg));
                videoTransform.localScale = Vector3.one * vs;
            }

            yield return null;
        }

        // --- Fade OUT texts + video together ---
        float f = 0f;
        while (f < fadeOutTime)
        {
            f += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(f / fadeOutTime);

            titleText.alpha = k;
            xpText.alpha = k;

            // Fade video by tint intensity (RGB), additive-friendly
            SetVideoIntensity(k);

            yield return null;
        }

        HideImmediate();
        running = null;
    }

    private void HideImmediate()
    {
        if (titleText)
        {
            titleText.alpha = 0f;
            titleText.rectTransform.localScale = Vector3.one;
        }
        if (xpText)
        {
            xpText.alpha = 0f;
            if (xpRect != null) xpRect.anchoredPosition = xpStartAnchored;
        }
        SetVideoIntensity(0f);

        if (videoTransform)
            videoTransform.localScale = Vector3.one * videoEndScale; // reset to clean state
    }

    private void SetVideoIntensity(float k)
    {
        if (!videoMaterial) return;

        if (videoMaterial.HasProperty(videoColorProperty))
        {
            Color c = videoMaterial.GetColor(videoColorProperty);
            // For additive shaders keep alpha = 1, scale RGB
            c.a = 1f;
            c.r = k; c.g = k; c.b = k;
            videoMaterial.SetColor(videoColorProperty, c);
        }
        else if (videoMaterial.HasProperty("_Color"))
        {
            Color c = videoMaterial.GetColor("_Color");
            c.a = 1f;
            c.r = k; c.g = k; c.b = k;
            videoMaterial.SetColor("_Color", c);
        }
    }

    // --- Easing helpers ---
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1 + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
