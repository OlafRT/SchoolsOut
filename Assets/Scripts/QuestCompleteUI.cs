using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuestCompleteUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI xpText;           // used as subtitle in Level Up mode
    [SerializeField] private RectTransform xpRect;

    [Tooltip("Material used by the video quad/plane (sampling the RenderTexture). We'll tint RGB down to fade out.")]
    [SerializeField] private Material videoMaterial;

    [Tooltip("Transform of the object that shows the video (Quad, UI RawImage, etc). We'll scale this from 0.5x → 1x.")]
    [SerializeField] private Transform videoTransform;

    [Header("Video Player (optional)")]
    [SerializeField] private VideoPlayer videoPlayer;          // Assign if you want this script to control playback.
    [SerializeField] private bool restartVideoOnPlay = true;

    [Header("Timings")]
    [SerializeField] private float titleInTime = 0.55f;
    [SerializeField] private float xpInTime = 0.45f;
    [SerializeField] private float holdTime = 3.0f;
    [SerializeField] private float fadeOutTime = 0.6f;

    [Header("Title Motion")]
    [SerializeField] private float titleStartScale = 0.5f;     // title starts smaller
    [SerializeField] private float titleMaxFontSize = 100f;

    [Header("XP/Subitle Motion")]
    [SerializeField] private float xpSlideDistance = 120f;     // px it travels up from
    [SerializeField] private float xpStartAlpha = 0f;

    [Header("Level Up Styling")]
    [SerializeField] private float levelUpTitleSize = 100f;
    [SerializeField] private float levelUpSubtitleSize = 60f;

    [Header("Video Appearance")]
    [Tooltip("Color/tint property on your video shader. URP Unlit = _BaseColor; Legacy = _Color")]
    [SerializeField] private string videoColorProperty = "_BaseColor";
    [SerializeField] private float videoStartIntensity = 1f;   // we lerp black → target color by this factor
    [SerializeField] private float videoStartScale = 0.5f;
    [SerializeField] private float videoEndScale = 1.0f;
    [SerializeField] private Color videoTargetColor = new Color(1f, 1f, 0.8f); // pale yellow-white

    [Header("Debug / Testing")]
    [SerializeField] private bool simulateQuestOnStart = false;
    [SerializeField] private bool simulateLevelUpOnStart = false;
    [SerializeField] private KeyCode testQuestKey = KeyCode.F9;
    [SerializeField] private KeyCode testLevelKey = KeyCode.F8;
    [SerializeField] private int testXp = 1000;
    [SerializeField] private int testLevel = 2;

    // cache
    private Vector2 xpStartAnchored;
    private Coroutine running;

    // dynamic
    private bool playWithVideo = true;    // set by PlayQuestComplete/PlayLevelUp
    private Renderer videoRenderer;
    private Graphic videoGraphic;

    void Reset()
    {
        titleText = GetComponentInChildren<TextMeshProUGUI>();
        var tmps = GetComponentsInChildren<TextMeshProUGUI>();
        if (tmps.Length > 1) xpText = tmps[tmps.Length - 1];
        xpRect = xpText ? xpText.rectTransform : null;
    }

    void Awake()
    {
        if (xpText) xpRect = xpText.rectTransform;
        if (xpRect != null) xpStartAnchored = xpRect.anchoredPosition;

        if (videoTransform)
        {
            videoRenderer = videoTransform.GetComponent<Renderer>();
            videoGraphic  = videoTransform.GetComponent<Graphic>();
        }

        HideImmediate();
    }

    void Start()
    {
        if (simulateQuestOnStart)   PlayQuestComplete(testXp);
        if (simulateLevelUpOnStart) PlayLevelUp(testLevel);
    }

    void Update()
    {
        if (Input.GetKeyDown(testQuestKey)) PlayQuestComplete(testXp);
        if (Input.GetKeyDown(testLevelKey)) PlayLevelUp(testLevel);
    }

    [ContextMenu("Test ▶ Quest Complete")]
    private void ContextTestQuest() => PlayQuestComplete(testXp);

    [ContextMenu("Test ▶ Level Up")]
    private void ContextTestLevel() => PlayLevelUp(testLevel);

    // ------------------ Public API ------------------

    /// <summary>QUEST COMPLETE mode: shows video.</summary>
    public void PlayQuestComplete(int xpAmount)
    {
        playWithVideo = true;
        // Title + second line content
        titleMaxFontSize = 100f;
        xpText.fontSize = 60f;

        // Kick off with desired strings
        xpText.text = $"{xpAmount:N0} XP";
        PlayInternal("QUEST COMPLETE");
    }

    /// <summary>LEVEL UP mode: hides video.</summary>
    public void PlayLevelUp(int newLevel)
    {
        playWithVideo = false;
        titleMaxFontSize = levelUpTitleSize;
        xpText.fontSize = levelUpSubtitleSize;

        xpText.text = $"YOU REACHED LEVEL\n{newLevel}";
        PlayInternal("LEVEL UP!");
    }

    // Keep the old testing style if you want to call it directly, defaults to video ON
    public void Play(int xpAmount, string title = "QUEST COMPLETE")
    {
        playWithVideo = true;
        xpText.text = $"{xpAmount:N0} XP";
        PlayInternal(title);
    }

    // ------------------ Core routine ------------------

    private void PlayInternal(string title)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine(title));
    }

    private IEnumerator PlayRoutine(string title)
    {
        // --- Prepare UI text ---
        titleText.text = title;
        titleText.fontSize = titleMaxFontSize;

        // Title start state
        titleText.alpha = 0f;
        titleText.rectTransform.localScale = Vector3.one * titleStartScale;

        // Subtitle/XP start state (below + transparent)
        xpText.alpha = xpStartAlpha;
        Vector2 xpBelow = xpStartAnchored + new Vector2(0f, -xpSlideDistance);
        xpRect.anchoredPosition = xpBelow;

        // --- Video visibility & setup per-mode ---
        if (playWithVideo)
        {
            SetVideoVisible(true);
            SetVideoIntensity(videoStartIntensity); // black → target color by factor
            if (videoTransform) videoTransform.localScale = Vector3.one * videoStartScale;

            // Ensure the VideoPlayer outputs fresh frames
            if (videoPlayer && restartVideoOnPlay)
            {
                videoPlayer.Stop();
                try { videoPlayer.frame = 0; } catch {}
                videoPlayer.time = 0.0;
                videoPlayer.Prepare();
                while (!videoPlayer.isPrepared) yield return null;
                videoPlayer.Play();
            }
        }
        else
        {
            // Level Up mode: completely hide video for the whole sequence
            SetVideoVisible(false);
            SetVideoIntensity(0f);
        }

        // --- Animate IN (texts) ---
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

            // Subtitle/XP: slide up + fade in
            float ntXp = Mathf.Clamp01(t / xpInTime);
            xpText.alpha = Mathf.SmoothStep(0f, 1f, ntXp);
            xpRect.anchoredPosition = Vector2.Lerp(xpBelow, xpStartAnchored, EaseOutCubic(ntXp));

            // Video growth (quest mode only)
            if (playWithVideo && videoTransform)
            {
                float totalVisible = titleInTime + holdTime;
                float pg = Mathf.Clamp01(Mathf.Min(t, totalVisible) / totalVisible);
                float vs = Mathf.Lerp(videoStartScale, videoEndScale, EaseOutCubic(pg));
                videoTransform.localScale = Vector3.one * vs;
            }

            yield return null;
        }

        // --- HOLD fully formed ---
        float held = 0f;
        while (held < holdTime)
        {
            held += Time.unscaledDeltaTime;

            if (playWithVideo && videoTransform)
            {
                float totalVisible = titleInTime + holdTime;
                float pg = Mathf.Clamp01((titleInTime + held) / totalVisible);
                float vs = Mathf.Lerp(videoStartScale, videoEndScale, EaseOutCubic(pg));
                videoTransform.localScale = Vector3.one * vs;
            }

            yield return null;
        }

        // --- Fade OUT texts + (maybe) video ---
        float f = 0f;
        while (f < fadeOutTime)
        {
            f += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(f / fadeOutTime);

            titleText.alpha = k;
            xpText.alpha = k;

            if (playWithVideo)
                SetVideoIntensity(k);  // black → target color

            yield return null;
        }

        HideImmediate();
        running = null;
    }

    // ------------------ Helpers ------------------

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

        // Reset video to hidden/black after each sequence
        SetVideoIntensity(0f);
        SetVideoVisible(false);

        if (videoTransform)
            videoTransform.localScale = Vector3.one * videoEndScale; // clean state
    }

    private void SetVideoIntensity(float k)
    {
        if (!videoMaterial) return;

        // Lerp black → target color by k (works for additive shaders; alpha kept at 1)
        Color target = videoTargetColor;
        Color fade = Color.Lerp(Color.black, target, Mathf.Clamp01(k));
        fade.a = 1f;

        if (videoMaterial.HasProperty(videoColorProperty))
            videoMaterial.SetColor(videoColorProperty, fade);
        else if (videoMaterial.HasProperty("_Color"))
            videoMaterial.SetColor("_Color", fade);
    }

    private void SetVideoVisible(bool visible)
    {
        if (videoRenderer) videoRenderer.enabled = visible;
        if (videoGraphic)  videoGraphic.enabled  = visible;
        if (videoTransform && !videoRenderer && !videoGraphic)
            videoTransform.gameObject.SetActive(visible); // last resort
    }

    // Easing
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1 + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
